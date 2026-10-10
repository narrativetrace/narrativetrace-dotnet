# Framework wiring snippets

The wiring lines of every framework-table row whose wiring is source-level, one section per row id.
Every fenced block is a compiled, tested fixture's text: `SnippetSync` writes it, `SnippetCheck`
fails the build when it drifts. Never edit a block by hand. The doctor reads this file from the
`NarrativeTrace.Tooling` assembly to print each framework fix.

## aspnetcore

<!-- snippet: tests/NarrativeTrace.AspNetCore.Tests/Wiring/AspNetCoreWiring.cs region=wiring -->
```csharp
builder.Services.AddNarrativeTrace(builder.Configuration);

var app = builder.Build();
app.UseNarrativeTrace(); // early: it wraps the whole request

// Wrap a service against the request's own context, so its calls nest into the request's trace.
app.MapGet("/orders/{customerId}", (string customerId, HttpContext http) =>
    NarrativeTraceProxy.Create<IOrderService>(new OrderService(), http.GetNarrativeContext())
        .PlaceOrder(customerId));
```
<!-- /snippet -->

## di

<!-- snippet: tests/NarrativeTrace.DependencyInjection.Tests/Wiring/DependencyInjectionWiring.cs region=wiring -->
```csharp
services.AddScoped<IOrderService, OrderService>();

// Last: it wraps the interface registrations made before it whose implementation lives in a
// listed namespace (dot-boundary match) — list this project's own service namespaces.
services.AddNarrativeTracing(options => options.Namespaces("Example.Orders"));
```
<!-- /snippet -->

## xunit

<!-- snippet: tests/NarrativeTrace.Examples.SixtySeconds.Tests/RedactionQuickstartTests.cs -->
```csharp
using NarrativeTrace.Core;
using NarrativeTrace.Proxy;
using NarrativeTrace.TestingXunit;
using Xunit;

namespace NarrativeTrace.Examples.SixtySeconds.Tests;

public sealed class RedactionQuickstartTests
{
    [Fact]
    public void Redacts_a_deny_listed_parameter()
    {
        using var narrative = new NarrativeFixture();

        narrative.Run("pay with a deny-listed parameter", context =>
        {
            var account = NarrativeTraceProxy.Create<IAccountService>(new AccountService(), context);
            account.Pay("POL-123", "tok-super-secret-xyz");
        });

        var rendered = IndentedTextRenderer.Render(narrative.CaptureTrace());
        Assert.Contains("paymentToken: [REDACTED]", rendered);
        Assert.DoesNotContain("tok-super-secret-xyz", rendered);
    }
}

public interface IAccountService
{
    string Pay(string policyId, string paymentToken);
}

public sealed class AccountService : IAccountService
{
    public string Pay(string policyId, string paymentToken) => $"paid:{policyId}";
}
```
<!-- /snippet -->

## nunit

<!-- snippet: tests/NarrativeTrace.Testing.NUnit.EngineTests/Wiring/NUnitWiring.cs region=wiring -->
```csharp
[TestFixture]
public sealed class OrderTraceTests : NarrativeTestBase // a fresh Context per test; the narrative prints on failure
{
    [Test]
    public void Places_an_order()
    {
        var orders = NarrativeTraceProxy.Create<IOrderService>(new OrderService(), Context);

        Assert.That(orders.PlaceOrder("cust-1"), Is.EqualTo("confirmed:cust-1"));
        Assert.That(Context.CaptureTrace().Roots, Has.Count.EqualTo(1));
    }
}
```
<!-- /snippet -->

## opentelemetry

<!-- snippet: tests/NarrativeTrace.Observability.Tests/Wiring/OpenTelemetryWiring.cs region=wiring -->
```csharp
using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .AddSource("NarrativeTrace") // the ActivitySource NarrativeTrace emits its spans under
    .AddProcessor(new SimpleActivityExportProcessor(exporter)) // your exporter
    .Build();

TraceActivityExporter.Export(context.CaptureTrace()); // one span per traced call
```
<!-- /snippet -->

## default-logger

<!-- snippet: tests/NarrativeTrace.Logging.Tests/Wiring/LoggerWiring.cs region=wiring -->
```csharp
TraceLogExporter.ExportToLogger(context.CaptureTrace(), loggerFactory.CreateLogger("NarrativeTrace"));
```
<!-- /snippet -->
