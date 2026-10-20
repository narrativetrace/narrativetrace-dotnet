# Send traces to the observability stack you already run

**English** | [Español](../es/recetas/envia-las-trazas-al-stack-de-observabilidad-que-ya-tienes.md) | [Português](../pt-BR/receitas/envie-os-traces-para-o-stack-de-observabilidade-que-voce-ja-tem.md) | [简体中文](../zh-CN/实用方案/把追踪发到你已有的可观测性栈.md)

## The problem

> We already have tracing and dashboards. I am not adopting a second place to
> look — can this go into the stack we run?

Yes: one run, two consumers. The narrative lines keep going to your logger,
and the same events become OpenTelemetry spans on the way out. Below is one
checkout with both outputs side by side.

## Minimal setup

`NarrativeTrace.Observability`, which brings the OpenTelemetry SDK package
with it:

```bash
dotnet add package NarrativeTrace.Observability
```

There are two ways in, and the difference is *when* a span exists:

| Mode | Type | Use it when |
|---|---|---|
| Live, as the methods run | `OtelTraceEventListener` | You want spans in your collector even if the process dies mid-request — this page |
| Batch, after the fact | `TraceActivityExporter` | You want the spans of a finished trace tree, exported once, at the lowest execution cost: `TraceActivityExporter.Export(context.CaptureTrace())` |

The batch exporter emits through an `ActivitySource` named `NarrativeTrace`;
the live listener through whichever source you give it. This page names its
source the same, so one `AddSource("NarrativeTrace")` and your exporter on the
OpenTelemetry builder cover both. In a deployment the
live listener is subscribed to the pipeline your host builds — a
`BufferedEventConsumer.Subscribe(listener.OnEvent)` — or placed in the
synchronous slot, as below. An in-memory exporter in a test needs neither a
host nor a global SDK, so the scenario below composes the pipeline explicitly:
that is also the clearest picture of what a host would build.

## The scenario

The pipeline is the only new idea: the narration listener you already have,
with the OpenTelemetry listener composed onto the same synchronous path. The
checkout itself is wrapped exactly as on every other page:

<!-- snippet: recipes/checkout/Checkout.Tests/ObservabilityRecipeTests.cs -->
```csharp
using System.Diagnostics;
using System.Globalization;
using System.Text;
using Checkout;
using Microsoft.Extensions.Logging;
using NarrativeTrace.Core;
using NarrativeTrace.Logging;
using NarrativeTrace.Observability;
using NarrativeTrace.Proxy;
using NarrativeTrace.Runtime;
using OpenTelemetry;
using OpenTelemetry.Trace;
using Xunit;

namespace Checkout.Tests;

/// <summary>
/// Proves recipes/send-traces-to-the-observability-stack-you-already-run.md: ONE run of the checkout
/// writes the log lines it always wrote AND an OpenTelemetry span per traced call, with the parentage
/// and the attributes the page shows — and without the redacted values (rule 8, docs as tests).
/// The pipeline is composed explicitly — narration on the synchronous path, the OpenTelemetry listener
/// beside it — because an in-memory exporter cannot be discovered and a global SDK leaks between tests.
/// The exported spans are written out by <see cref="ExportedSpans"/>, naming and sorting everything it
/// prints, so one run's dump is byte-identical to the next: a raw span dump carries timestamps and
/// OpenTelemetry's own generated ids and could never be embedded.
/// </summary>
public sealed class ObservabilityRecipeTests
{
    private const string Slug = "send-traces-to-the-observability-stack-you-already-run";
    private const string SourceName = "NarrativeTrace";

    // A fixed W3C traceparent, so the page's embedded artifacts name the same trace every run.
    private const string DemoTraceparent = "00-c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7-c4e1d0a7c4e1d0a7-01";

    [Fact]
    public void One_run_narrates_and_exports()
    {
        var log = new StringWriter { NewLine = "\n" };
        var spans = new List<Activity>();
        var context = ExportingContext(log, spans, TracingLevel.Detail, out var cleanup);
        using var _ = cleanup;

        var orderId = CheckOut(context);

        Assert.Equal("ORD-C-1234-SKU-KB-2", orderId);
        AssertOneSpanPerTracedCall(spans);
        AssertNoRedactedValueWasExported(spans);
        CapturedOutput.Write(Slug, "checkout.log", log.ToString());
        CapturedOutput.Write(Slug, "exported-spans.txt", ExportedSpans(spans));
    }

    [Fact]
    public void At_summary_the_same_four_spans_leave_without_parameter_attributes()
    {
        var spans = new List<Activity>();
        var context = ExportingContext(new StringWriter(), spans, TracingLevel.Summary, out var cleanup);
        using var _ = cleanup;

        CheckOut(context);

        Assert.Equal(4, spans.Count);
        Assert.DoesNotContain(spans.SelectMany(span => span.TagObjects), tag => tag.Key.StartsWith("narrative.param.", StringComparison.Ordinal));
    }

    private static SyncNarrativeContext ExportingContext(
        TextWriter log, List<Activity> spans, TracingLevel level, out IDisposable cleanup)
    {
        var loggerFactory = LoggerFactory.Create(builder => builder
            .SetMinimumLevel(LogLevel.Trace)
            .AddProvider(new CorrelationLoggerProvider(log)));
        var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(SourceName)
            .AddProcessor(new SimpleActivityExportProcessor(new InMemoryExporter(spans)))
            .Build();

        // The pipeline this page is about: the narration you already have, and the OpenTelemetry
        // listener beside it on the same synchronous path. What CaptureTrace() reads is untouched.
        var narration = new LoggingTraceEventListener(loggerFactory.CreateLogger("NarrativeTrace"));
        var source = new ActivitySource(SourceName);
        var exporter = new OtelTraceEventListener(source);
        var pipeline = new DualPathPipeline(traceEvent =>
        {
            narration.OnEvent(traceEvent);
            exporter.OnEvent(traceEvent);
        });
        cleanup = new Disposables(tracerProvider, source, loggerFactory);
        return new SyncNarrativeContext(
            new NarrativeTraceConfig(level, initialTraceparent: Traceparent.Parse(DemoTraceparent)), pipeline);
    }

    private static string CheckOut(INarrativeContext context)
    {
        var inventory = NarrativeTraceProxy.Create<IInventoryService>(new InMemoryInventory("SKU-KB", 5), context);
        var payments = NarrativeTraceProxy.Create<IPaymentService>(new CardPaymentService(), context);
        var orders = NarrativeTraceProxy.Create<IOrderRepository>(new InMemoryOrderRepository(), context);
        var checkout = NarrativeTraceProxy.Create<IOrderService>(
            new DefaultOrderService(inventory, payments, orders), context);
        return checkout.PlaceOrder("C-1234", "SKU-KB", 2, "4242424242424242");
    }

    /// <summary>Four traced calls, four spans, ended innermost first — and the tree is reconstructed.</summary>
    private static void AssertOneSpanPerTracedCall(List<Activity> exported)
    {
        Assert.Equal(
            ["IInventoryService.Reserve", "IPaymentService.Charge", "IOrderRepository.Save", "IOrderService.PlaceOrder"],
            exported.Select(span => span.DisplayName));
        var root = Named(exported, "IOrderService.PlaceOrder");
        Assert.Equal(root.SpanId, Named(exported, "IPaymentService.Charge").ParentSpanId);
        Assert.DoesNotContain(exported, span => span.SpanId == root.ParentSpanId);
    }

    /// <summary>What the deny-list caught never reaches the exporter either — there is no attribute for it.</summary>
    private static void AssertNoRedactedValueWasExported(List<Activity> exported)
    {
        var tags = exported.SelectMany(span => span.TagObjects).ToList();
        Assert.DoesNotContain(tags, tag => tag.Key is "narrative.param.cardNumber" or "narrative.param.card");
        Assert.DoesNotContain(
            tags, tag => Convert.ToString(tag.Value, CultureInfo.InvariantCulture)!.Contains("4242424242424242", StringComparison.Ordinal));
    }

    private static Activity Named(List<Activity> exported, string name) =>
        exported.Single(span => span.DisplayName == name);

    /// <summary>The dump the page embeds: span name, parent, and every attribute, sorted by key.</summary>
    private static string ExportedSpans(List<Activity> exported)
    {
        var text = new StringBuilder();
        foreach (var span in exported)
        {
            var parent = exported.FirstOrDefault(other => other.SpanId == span.ParentSpanId)?.DisplayName;
            text.Append("span: ").Append(span.DisplayName).Append('\n');
            text.Append("  parent: ").Append(parent ?? "(none — this is the root)").Append('\n');
            foreach (var tag in span.TagObjects.OrderBy(tag => tag.Key, StringComparer.Ordinal))
            {
                text.Append("    ").Append(tag.Key).Append(": ")
                    .Append(Convert.ToString(tag.Value, CultureInfo.InvariantCulture)).Append('\n');
            }
        }

        return text.ToString();
    }

    private sealed class Disposables(params IDisposable[] items) : IDisposable
    {
        public void Dispose()
        {
            foreach (var item in items)
            {
                item.Dispose();
            }
        }
    }

    private sealed class InMemoryExporter(List<Activity> spans) : BaseExporter<Activity>
    {
        public override ExportResult Export(in Batch<Activity> batch)
        {
            foreach (var activity in batch)
            {
                spans.Add(activity);
            }

            return ExportResult.Success;
        }
    }
}
```
<!-- /snippet -->

## The actual output

**The log lines**, unchanged by any of this — the same narration
[Add logging to this service](add-logging-to-this-service.md) produces:

<!-- snippet: artifacts/recipes/send-traces-to-the-observability-stack-you-already-run/checkout.log -->
```text
TRACE [NarrativeTrace] [c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7] - → IOrderService.PlaceOrder(customerId: "C-1234", sku: "SKU-KB", quantity: 2, cardNumber: [REDACTED])
TRACE [NarrativeTrace] [c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7] - → IInventoryService.Reserve(sku: "SKU-KB", quantity: 2)
TRACE [NarrativeTrace] [c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7] - ← completed
TRACE [NarrativeTrace] [c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7] - → IPaymentService.Charge(customerId: "C-1234", amountCents: 7998, card: [REDACTED])
TRACE [NarrativeTrace] [c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7] - ← returned: "PAY-C-1234-7998"
TRACE [NarrativeTrace] [c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7] - → IOrderRepository.Save(order: Order(OrderId: "ORD-C-1234-SKU-KB-2", CustomerId: "C-1234", Sku: "SKU-KB", Quantity: 2, AmountCents: 7998))
TRACE [NarrativeTrace] [c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7] - ← completed
TRACE [NarrativeTrace] [c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7] - ← returned: "ORD-C-1234-SKU-KB-2"
```
<!-- /snippet -->

**The spans that left the same run**, as the in-memory exporter received
them — name, parent and every attribute, sorted:

<!-- snippet: artifacts/recipes/send-traces-to-the-observability-stack-you-already-run/exported-spans.txt -->
```text
span: IInventoryService.Reserve
  parent: IOrderService.PlaceOrder
    narrative.class: IInventoryService
    narrative.method: Reserve
    narrative.param.quantity: 2
    narrative.param.sku: SKU-KB
    narrative.trace_id: c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7
    narrative.trace_name: solid wand romps
    nt.entryType: entry
    nt.schemaVersion: 1.2
span: IPaymentService.Charge
  parent: IOrderService.PlaceOrder
    narrative.class: IPaymentService
    narrative.method: Charge
    narrative.outcome: "PAY-C-1234-7998"
    narrative.param.amountCents: 7998
    narrative.param.customerId: C-1234
    narrative.trace_id: c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7
    narrative.trace_name: solid wand romps
    nt.entryType: entry
    nt.schemaVersion: 1.2
span: IOrderRepository.Save
  parent: IOrderService.PlaceOrder
    narrative.class: IOrderRepository
    narrative.method: Save
    narrative.param.order: Order(OrderId: "ORD-C-1234-SKU-KB-2", CustomerId: "C-1234", Sku: "SKU-KB", Quantity: 2, AmountCents: 7998)
    narrative.trace_id: c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7
    narrative.trace_name: solid wand romps
    nt.entryType: entry
    nt.schemaVersion: 1.2
span: IOrderService.PlaceOrder
  parent: (none — this is the root)
    narrative.class: IOrderService
    narrative.method: PlaceOrder
    narrative.outcome: "ORD-C-1234-SKU-KB-2"
    narrative.param.customerId: C-1234
    narrative.param.quantity: 2
    narrative.param.sku: SKU-KB
    narrative.trace_id: c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7
    narrative.trace_name: solid wand romps
    nt.entryType: entry
    nt.schemaVersion: 1.2
```
<!-- /snippet -->

## How to read it

- **Four traced calls, four spans, one tree.** The spans arrive in the order
  they *ended* — innermost first, the root last, which is how any OpenTelemetry
  pipeline sees a finished tree. Parentage is not inferred from that order:
  the listener reads each event's explicit parent span id, which is why
  `IInventoryService.Reserve`, `IPaymentService.Charge` and
  `IOrderRepository.Save` all name `IOrderService.PlaceOrder` as their parent
  even though they ended before it did.
- **What each span carries.** `narrative.class` and `narrative.method` are the
  call; `narrative.param.<name>` is one attribute per parameter, typed
  (`narrative.param.quantity: 2` is a long, not the string `"2"`);
  `narrative.outcome` is the returned value, and it is absent on the two
  `void` calls because there was nothing to return. `narrative.trace_id` and
  `narrative.trace_name` are NarrativeTrace's own identity for the trace,
  carried as attributes so a span in your backend can be matched to the log
  line and the trace file that describe the same call —
  [Follow one request across services](follow-one-request-across-services.md)
  is the same id doing the same job across a process boundary. `nt.entryType`
  and `nt.schemaVersion` mark the schema these attributes follow.
- **A record parameter arrives as one attribute.** `IOrderRepository.Save(order)`
  is one parameter, and it exported as one `narrative.param.order` attribute
  holding the record rendered the way the log line renders it, by reading its
  state and never by calling a `ToString()` of your own.
- **Redaction reaches this path too.** There is no `narrative.param.cardNumber`
  and no `narrative.param.card` attribute on any span above — not an attribute
  holding `[REDACTED]`, no attribute at all. A denied value is dropped at
  capture, before the event exists, so no consumer on either path can receive
  it; the scenario asserts that as well as the span tree
  ([Keep secrets out of the logs](keep-secrets-out-of-the-logs.md)).
- **Which setting moved an event to which consumer.** Three decisions, in
  this order:
  1. `TracingLevel` decides whether an event is created at all, and what it
     carries. The second test above runs the same checkout at `Summary`: the
     same four spans leave, with no `narrative.param.*` attribute among
     them, because parameter values are not captured at that level; at `Off`
     there is no event, and therefore no span
     ([Trace in production without the cost](trace-in-production-without-the-cost.md)).
  2. The pipeline's shape decides who sees the event. Here one synchronous
     listener — narration, then export — runs inline, and the context's own
     capture, which is what `CaptureTrace()` and the trace files read, is
     untouched. Nothing was taken away from the logger to feed the exporter.
  3. Your logger's level decides what is *printed*. Raising the
     `NarrativeTrace` category to `Information` empties the log block above
     and changes nothing about the spans.
- **Why this is not a second place to look.** The spans are yours, in your
  collector, under your service name, beside the spans your framework already
  emits. What NarrativeTrace adds is a span per *traced method call* with the
  parameters and the outcome on it — one level below the HTTP span you
  already have.

## What is not captured

- **What your stack does with the spans is your stack's.** Sampling,
  retention, indexing and whether an attribute is searchable are the
  collector's and the backend's decisions. This page shows what leaves the
  process; no claim is made about any vendor's UI.
- **The ids are not shared.** OpenTelemetry generates its own trace and span
  ids for the spans it creates here, and NarrativeTrace's identity rides along
  as `narrative.trace_id` / `narrative.trace_name` attributes. A span's
  position in your backend's waterfall is OpenTelemetry's; matching it to a
  log line is the attribute's job.
- **Live and batch are not the same spans.** This page's listener creates a
  span as a method runs, so an orphan (an enter with no exit — a process
  killed mid-call) ends up ended by the listener's own time limit and bound on
  open spans. The batch exporter only ever sees finished trees and adds
  `narrative.duration_ms`, which the live path does not.
- **The cost is a listener on the caller's thread.** A listener in the
  synchronous slot runs inline, so span creation is on the path of the traced
  call. That is the trade for durability; a `BufferedEventConsumer` moves it
  off the caller's thread and sheds load under pressure instead of blocking.
- **Only what is traced is exported.** No span exists for a call no
  integration intercepted — the export path inherits the boundary of the
  capture path exactly.

**The skill for this:** `narrativetrace-doctor` — it checks the wiring of each
integration, and reports whether any sink actually receives what is traced.
