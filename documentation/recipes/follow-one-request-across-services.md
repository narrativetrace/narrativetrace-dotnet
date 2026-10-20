# Follow one request across services (distributed tracing)

**English** | [Español](../es/recetas/sigue-una-peticion-entre-servicios.md) | [Português](../pt-BR/receitas/acompanhe-uma-requisicao-entre-servicos.md) | [简体中文](../zh-CN/实用方案/跨服务追踪一次请求.md)

## The problem

> The checkout calls the payment service over HTTP. When an order goes wrong,
> I want one request across both — not two logs I have to line up by hand.

Two services, two log files, two sets of ids. What makes them one story is a
single value travelling with the request: the W3C
[`traceparent`](https://www.w3.org/TR/trace-context/#traceparent-header)
header. The caller sends the trace it is in and the span it is calling from;
the callee adopts both instead of starting a trace of its own. Below is one
run of exactly that, with both services' lines in one file.

## Minimal setup

Beyond the [install](../guides/installation.md), the caller needs one handler
on the `HttpClient` it already uses: `TraceparentPropagationHandler`, from
`NarrativeTrace.Runtime`, attaches the header to every outgoing request. With
`IHttpClientFactory` in an ASP.NET Core caller, `AddNarrativeTraceTraceparentPropagation()`
on the client builder does the same. The callee needs the inbound half — and
which code that is depends on how it is built:

| The callee is | The inbound half is |
|---|---|
| An ASP.NET Core application | `NarrativeTrace.AspNetCore`'s middleware (`UseNarrativeTrace()`), which adopts the header for you — [ASP.NET Core Integration Guide](../guides/aspnetcore.md) |
| Anything else — a plain `HttpListener`, a queue consumer, another framework | Three lines in whatever runs first per request: a fresh context, `AdoptTraceparent(Traceparent.Parse(header))`, serve |

This page's payment service is the first row, and the test hosts it: a
loopback Kestrel server with the middleware, started by the test on a port the
operating system picks. The request really crosses a socket and the header
really arrives as a header.

The log lines come from the logging bridge, as in
[Add logging to this service](add-logging-to-this-service.md). This page uses
a logger that prints the raw trace id in brackets after the category, because
the trace id is the thing being followed. It reads the id from the `nt.traceId`
field NarrativeTrace puts in each log scope, and writes the `NarrativeTrace`
categories only, so the host's own request logging stays out of the page:

<!-- snippet: recipes/checkout/Checkout.Tests/CorrelationLog.cs -->
```csharp
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Checkout.Tests;

/// <summary>
/// The logger configuration the correlation recipes run under: one line per message —
/// <c>LEVEL [category] [trace id] - message</c> — with the trace id read from the <c>nt.traceId</c>
/// field NarrativeTrace puts in each log scope. The trace id is the thing those pages follow; the
/// built-in console formatter would print it only as part of a scope dump. It writes the
/// <c>NarrativeTrace</c> categories only, so a hosted service's request logging stays out of the page. Deliberately no
/// timestamp: the embedded block is a real run's bytes, and a clock differs on every run.
/// </summary>
internal sealed class CorrelationLoggerProvider(TextWriter output) : ILoggerProvider, ISupportExternalScope
{
    private const string OwnCategories = "NarrativeTrace";

    private readonly object _writing = new();
    private IExternalScopeProvider _scopes = new LoggerExternalScopeProvider();

    public void SetScopeProvider(IExternalScopeProvider scopeProvider) => _scopes = scopeProvider;

    // Only the narrative's own categories: the framework's request logging is not what these pages follow.
    public ILogger CreateLogger(string categoryName) =>
        categoryName.StartsWith(OwnCategories, StringComparison.Ordinal)
            ? new CorrelationLogger(categoryName, output, _writing, () => _scopes)
            : NullLogger.Instance;

    public void Dispose()
    {
    }

    private sealed class CorrelationLogger(string category, TextWriter output, object writing, Func<IExternalScopeProvider> scopes) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => scopes().Push(state);

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var line = $"{logLevel.ToString().ToUpperInvariant(),-5} [{category}] [{TraceIdOfScope()}] - {formatter(state, exception)}";
            lock (writing)
            {
                output.WriteLine(line);
            }
        }

        private string TraceIdOfScope()
        {
            var traceId = "";
            scopes().ForEachScope(
                (scope, _) =>
                {
                    if (scope is IEnumerable<KeyValuePair<string, object>> fields)
                    {
                        foreach (var (key, value) in fields)
                        {
                            traceId = key == "nt.traceId" ? value.ToString() ?? "" : traceId;
                        }
                    }
                },
                0);
            return traceId;
        }
    }
}
```
<!-- /snippet -->

## The scenario

**The caller's side: an ordinary client.** The checkout's `IPaymentService` is
now an HTTP client. It knows nothing about tracing — the `HttpClient` it is
given carries the header:

<!-- snippet: recipes/checkout/Checkout/HttpPaymentService.cs -->
```csharp
namespace Checkout;

/// <summary>
/// The checkout's payment collaborator when the payment service is a different process: an ordinary
/// HTTP client call. It knows nothing about tracing — the client it is given carries the
/// <c>traceparent</c> header through a delegating handler, so this class stays the code a project
/// already has.
/// </summary>
public sealed class HttpPaymentService(HttpClient client, Uri endpoint) : IPaymentService
{
    private const int DeclinedStatus = 402;

    public string Charge(string customerId, int amountCents, string card)
    {
        using var body = new StringContent($"{customerId},{amountCents},{card}");
        // SendAsync, not the synchronous Send: a handler's SendAsync override only runs on this path.
        using var response = client.PostAsync(endpoint, body).GetAwaiter().GetResult();
        var text = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        return (int)response.StatusCode == DeclinedStatus ? throw new PaymentDeclinedException(text) : text;
    }
}
```
<!-- /snippet -->

**The callee's side: its own context.** The payment service is a second
service, which means a second narrative context — a request-scoped one that
`AddNarrativeTrace` registers — and its own logger category, so one log file
can tell the two apart (`recipes/checkout/Checkout.Tests/PaymentServiceHost.cs`):

<!-- snippet: recipes/checkout/Checkout.Tests/PaymentServiceHost.cs region=own-context -->
```csharp
var builder = WebApplication.CreateSlimBuilder();
builder.WebHost.UseUrls("http://127.0.0.1:0");
builder.Logging.ClearProviders().SetMinimumLevel(LogLevel.Trace).AddProvider(new CorrelationLoggerProvider(log));
builder.Services.AddSingleton<ITraceExporter>(host); // the seam that hands over this service's own trace
builder.Services.AddNarrativeTrace(); // a request-scoped context, the middleware, the exporter
```
<!-- /snippet -->

**The callee's side: adopt, serve, export.** This is the whole inbound half,
in the same file. The middleware adopts the request's `traceparent`, lets the
endpoint run, and hands the request's own trace to the exporter registered
above:

<!-- snippet: recipes/checkout/Checkout.Tests/PaymentServiceHost.cs region=inbound -->
```csharp
host._app.UseNarrativeTrace(); // adopts the inbound traceparent and exports the request's trace
host._app.MapPost("/charge", (Delegate)host.Charge);
```
<!-- /snippet -->

**The run.** The checkout, wrapped as always, with the HTTP client where the
local payment service used to be:

<!-- snippet: recipes/checkout/Checkout.Tests/AcrossServicesRecipeTests.cs -->
```csharp
using Checkout;
using Microsoft.Extensions.Logging;
using NarrativeTrace.Core;
using NarrativeTrace.Logging;
using NarrativeTrace.Proxy;
using NarrativeTrace.Runtime;
using Xunit;

namespace Checkout.Tests;

/// <summary>
/// Proves recipes/follow-one-request-across-services.md: the checkout charges a card through a
/// second service over HTTP, the W3C <c>traceparent</c> goes with the request, and the callee's own
/// trace carries the caller's trace id and names the caller's span as its parent (rule 8, docs as tests).
/// The callee's narrative is captured here, not by the caller's context: the second service has a
/// context — and a narrative — of its own, which is the point of the page.
/// </summary>
public sealed class AcrossServicesRecipeTests
{
    private const string Slug = "follow-one-request-across-services";

    // A fixed W3C traceparent, so the page's embedded artifacts name the same trace every run.
    private const string DemoTraceparent = "00-c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7-c4e1d0a7c4e1d0a7-01";

    [Fact]
    public async Task A_customer_checks_out_against_the_payment_service()
    {
        var log = new StringWriter { NewLine = "\n" };
        await using var paymentService = await PaymentServiceHost.StartAsync(log);
        var stock = new InMemoryInventory("SKU-KB", 5);
        var repository = new InMemoryOrderRepository();
        var context = CallerContext(log);
        using var client = new HttpClient(new TraceparentPropagationHandler(context) { InnerHandler = new HttpClientHandler() });

        var orderId = CheckoutCalling(client, paymentService, context, stock, repository)
            .PlaceOrder("C-1234", "SKU-KB", 2, "4242424242424242");

        Assert.Equal("ORD-C-1234-SKU-KB-2", orderId);
        Assert.Single(repository.Saved);
        AssertTheTraceCrossedTheWire(paymentService, context.CaptureTrace());
        CapturedOutput.Write(Slug, "checkout.log", log.ToString());
        CapturedOutput.Write(Slug, "caller.nt", StructuralTraceRenderer.RenderDocument(context.CaptureTrace(), "A customer checks out against the payment service"));
        CapturedOutput.Write(Slug, "payment-service.md", MarkdownRenderer.Render(paymentService.LastTrace));
    }

    private static LoggingNarrativeContext CallerContext(TextWriter log)
    {
        var loggerFactory = LoggerFactory.Create(builder => builder
            .SetMinimumLevel(LogLevel.Trace)
            .AddProvider(new CorrelationLoggerProvider(log)));
        var traceparent = Traceparent.Parse(DemoTraceparent);
        return new LoggingNarrativeContext(
            new SyncNarrativeContext(new NarrativeTraceConfig(initialTraceparent: traceparent)),
            loggerFactory.CreateLogger("NarrativeTrace"));
    }

    /// <summary>The checkout, with the HTTP payment client where the local one used to be.</summary>
    private static IOrderService CheckoutCalling(
        HttpClient client, PaymentServiceHost paymentService, INarrativeContext context,
        InMemoryInventory stock, InMemoryOrderRepository repository)
    {
        var inventory = NarrativeTraceProxy.Create<IInventoryService>(stock, context);
        var payments = NarrativeTraceProxy.Create<IPaymentService>(
            new HttpPaymentService(client, paymentService.Endpoint), context);
        var orders = NarrativeTraceProxy.Create<IOrderRepository>(repository, context);
        return NarrativeTraceProxy.Create<IOrderService>(
            new DefaultOrderService(inventory, payments, orders), context);
    }

    /// <summary>
    /// One trace, two services: the header named the caller's own span, and the callee's root adopted
    /// it as its parent instead of starting a trace of its own.
    /// </summary>
    private static void AssertTheTraceCrossedTheWire(PaymentServiceHost paymentService, TraceTree callerTrace)
    {
        var sent = Traceparent.Parse(paymentService.InboundTraceparent);
        Assert.NotNull(sent);
        Assert.Equal(Traceparent.Parse(DemoTraceparent)!.TraceId, sent.TraceId);

        var calleeRoot = Assert.Single(paymentService.LastTrace.Roots);
        Assert.Equal("Charge", calleeRoot.Signature.MethodName);
        Assert.Equal(sent.TraceId, calleeRoot.SpanContext!.TraceId);
        Assert.Equal(sent.ParentSpanId, calleeRoot.SpanContext.ParentSpanId);
        var exportedEnter = System.Text.Json.JsonDocument.Parse(
                JsonExporter.Export(paymentService.LastTrace, new TraceMetadata("callee", ScenarioResult.Success)))
            .RootElement.GetProperty("events")[0];
        Assert.Equal(sent.ParentSpanId.Value, exportedEnter.GetProperty("parentSpanId").GetString());
        var callersCharge = callerTrace.Roots[0].Children.Single(n => n.Signature.MethodName == "Charge");
        Assert.Equal(callersCharge.SpanContext!.SpanId, sent.ParentSpanId);
    }
}
```
<!-- /snippet -->

## The actual output

**Both services' lines, one file** — the caller's under `NarrativeTrace`, the
payment service's under `NarrativeTrace.Payments`:

<!-- snippet: artifacts/recipes/follow-one-request-across-services/checkout.log -->
```text
TRACE [NarrativeTrace] [c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7] - → IOrderService.PlaceOrder(customerId: "C-1234", sku: "SKU-KB", quantity: 2, cardNumber: [REDACTED])
TRACE [NarrativeTrace] [c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7] - → IInventoryService.Reserve(sku: "SKU-KB", quantity: 2)
TRACE [NarrativeTrace] [c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7] - ← completed
TRACE [NarrativeTrace] [c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7] - → IPaymentService.Charge(customerId: "C-1234", amountCents: 7998, card: [REDACTED])
TRACE [NarrativeTrace.Payments] [c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7] - → IPaymentService.Charge(customerId: "C-1234", amountCents: 7998, card: [REDACTED])
TRACE [NarrativeTrace.Payments] [c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7] - ← returned: "PAY-C-1234-7998"
TRACE [NarrativeTrace] [c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7] - ← returned: "PAY-C-1234-7998"
TRACE [NarrativeTrace] [c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7] - → IOrderRepository.Save(order: Order(OrderId: "ORD-C-1234-SKU-KB-2", CustomerId: "C-1234", Sku: "SKU-KB", Quantity: 2, AmountCents: 7998))
TRACE [NarrativeTrace] [c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7] - ← completed
TRACE [NarrativeTrace] [c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7] - ← returned: "ORD-C-1234-SKU-KB-2"
```
<!-- /snippet -->

**The caller's own trace,** the value-free structural artifact of that run —
the span ids below are the ones this page reads by:

<!-- snippet: artifacts/recipes/follow-one-request-across-services/caller.nt -->
```text
scenario: A customer checks out against the payment service

#1 - IOrderService.PlaceOrder(customerId, sku, quantity, cardNumber) → value
  #1.1 - IInventoryService.Reserve(sku, quantity)
  #1.2 - IPaymentService.Charge(customerId, amountCents, card) → value
  #1.3 - IOrderRepository.Save(order)
```
<!-- /snippet -->

**The payment service's own narrative,** captured by its own context at the
end of the request and rendered beside the caller's:

<!-- snippet: artifacts/recipes/follow-one-request-across-services/payment-service.md mask=duration,traceName -->
```text
---
type: trace
entry_point: IPaymentService.Charge
trace_id: c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7
trace_name: solid wand romps
method_count: 1
error_count: 0
result: success
duration_ms: 0
---

- **IPaymentService.Charge**(customerId: `"C-1234"`, amountCents: `7998`, card: `[REDACTED]`) → `"PAY-C-1234-7998"` — 0ms #1
```
<!-- /snippet -->

## How to read it

- **One trace id, in both services' lines.**
  `c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7` is the second bracket of every line
  above, the caller's and the callee's alike. The caller's is the trace this
  run is in; the callee's is that same id because it *adopted* it. Nothing
  correlates them after the fact — they were never apart.
- **The two services are told apart by the logger category, not by the id.**
  `[NarrativeTrace]` is the checkout, `[NarrativeTrace.Payments]` is the
  payment service, because its context was wrapped with a logger of that
  name.
- **The caller's `#1.2` and the callee's `#1` are the same work, seen from
  both ends.** In the caller's trace, `#1.2 IPaymentService.Charge` is the
  *client* span: it starts when the checkout calls the client and ends when
  the response comes back, so it includes the whole round trip. In the payment
  service's narrative, `#1 IPaymentService.Charge` is the *server* span: the
  work it actually did. Span ids are per-trace-tree and each service numbers
  its own from `#1` — the join is the trace id, not the number.
- **The parent span id on the callee's root is the caller's span.** The header
  the request carried was
  `00-c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7-<the caller's #1.2 span id>-01`, and
  the callee's root span records that id as its parent. That is not visible in
  a log line — a span id is generated per span, so it differs on every run and
  nothing byte-stable could be printed here — so it is asserted instead, in
  `AssertTheTraceCrossedTheWire` above: the callee's root carries the sent
  trace id *and* the sent parent span id, and the sent parent span id is the
  span id of the caller's `Charge`. Parentage across the wire is this page's
  claim, and that assertion is its proof.
- **How to join them in a log search.** The trace id is in the log scope under
  `nt.traceId` on every line NarrativeTrace writes, in either service. A
  provider that renders scopes (as the one above does for this field) or a
  JSON formatter puts it on the line, and one request across both services is
  `grep c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7` — or, in whatever aggregator you run,
  `nt.traceId:"c4e1…"`. The trace *name* that
  [Add logging to this service](add-logging-to-this-service.md) greps by is
  derived from the same id, so it works as a search key too, and reads better
  for a human.
- **The two narratives stay two.** The payment service's spans are in the
  payment service's trace, not in the caller's: `CaptureTrace()` on either
  side returns that side's work. One trace id, two trees — which is exactly
  what you have in production, where the other service is a different process
  on a different host.
- **Why the caller's artifact is the `.nt`.** The caller's Markdown rendering
  marks any call over 200 ms as slow, and a call that makes a real HTTP round
  trip can cross that line on a cold start, which would change the output from
  one run to the next. The structural trace carries no timings and is
  byte-stable.

## What is not captured

- **A hop through a service that does not forward the header breaks the
  chain.** Correlation is cooperative: the chain holds for as long as every
  service in it *passes `traceparent` on*. A proxy, a queue consumer or a
  third-party service that drops the header leaves the next NarrativeTrace
  service with nothing to adopt, and it starts a fresh trace — correctly, since
  it has no honest parent to name. A service with no NarrativeTrace at all is
  fine *if* it forwards the header it received.
- **No header is sent when there is nothing to name.** With no span open and
  nothing adopted, there is no trace to propagate and the handler sends
  nothing rather than a `traceparent` pointing at a span that never existed.
  An untraced call out of the checkout therefore cannot be joined, and that is
  by design. A `traceparent` the caller already set is left untouched.
- **`traceparent` only.** The client above sends no `tracestate` and no
  baggage: nothing in this path reads or writes vendor state, and no
  application value crosses the wire because of tracing.
- **Nothing comes back.** The response carries no trace header, so the
  caller's narrative says what it sent and what it got, never what the callee
  did inside. The callee's own narrative is the record of that, and the trace
  id is what finds it.
- **Two services, one process, one socket.** What this run does not exercise
  is a second process and a network that can fail. The header mechanism is
  unchanged by either; the failure modes of the call itself are your HTTP
  client's.

**The skill for this:** `narrativetrace-verify` — a change that crosses
services is exactly the case it exists for: write the intent down, run the
smallest real path, and read the trace against it before calling it done.
