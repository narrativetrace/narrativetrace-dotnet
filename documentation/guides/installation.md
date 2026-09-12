# NarrativeTrace .NET — Installation Guide

**English** | [Español](es/guia-de-instalacion.md) | [Português](pt-BR/guia-de-instalacao.md) | [简体中文](zh-CN/安装指南.md)

This guide covers installing and wiring NarrativeTrace in a .NET project.

## Prerequisites

- .NET 10 SDK for building; libraries multi-target `net10.0` and
  `netstandard2.0`, so they run on any .NET runtime that supports
  netstandard2.0 (.NET Core 2.0+, .NET 5+, and — via the `Legacy`
  package — .NET Framework 4.8).
- **No compiler flag is required.** Unlike the JVM (which needs
  `-parameters`), .NET retains method parameter names in metadata by
  default, so traces show real names out of the box. Use the
  [`[Traced]`](annotations.md#traced) attribute only when you want to
  override a name.

## Packages

Every project under `src/` publishes as a NuGet package of the same id.
Start with the minimum and add only what you need.

```xml
<!-- Minimum: capture + render -->
<PackageReference Include="NarrativeTrace.Proxy" Version="0.1.4" />
```

`NarrativeTrace.Proxy` depends on `.Runtime`, which depends on `.Core` — one
`dotnet add package NarrativeTrace.Proxy` restores all three, and their
types (`SyncNarrativeContext`, `IndentedTextRenderer`, …) are available for
you to use directly *(since 0.1.4, unreleased)*. On `0.1.3`, `Proxy` depends
on `Core` alone; add all three explicitly until `0.1.4` is out:

```xml
<PackageReference Include="NarrativeTrace.Core" Version="0.1.3" />
<PackageReference Include="NarrativeTrace.Runtime" Version="0.1.3" />
<PackageReference Include="NarrativeTrace.Proxy" Version="0.1.3" />
```

| Package | When to add it |
|---|---|
| `NarrativeTrace.Core` | Always — trace model, `INarrativeContext`, config, redaction, Markdown/Prose/text renderers, and the `[Narrated]`/`[OnError]`/`[NotTraced]`/`[NarrativeSummary]` attributes (namespace `NarrativeTrace.Core.Annotation`). Pulled in transitively by `.Runtime` *(since 0.1.4, unreleased)*. |
| `NarrativeTrace.Runtime` | Always — the capture engine (`SyncNarrativeContext`, `AsyncNarrativeContext`, JSON/chapter exporters). Pulled in transitively by `.Proxy` *(since 0.1.4, unreleased)*. |
| `NarrativeTrace.Proxy` | Interface tracing via `DispatchProxy`, plus the proxy-specific `[Traced]` parameter-name override and `ProxyOptions.Redaction` (a custom `RedactionPolicy` for that proxy's captures — see [Configuration §6](configuration.md#6-redaction)). |
| `NarrativeTrace.DependencyInjection` | `AddNarrativeTracing` — auto-wrap namespace-matched interface services in the MS.DI container. |
| `NarrativeTrace.AspNetCore` | Per-request trace lifecycle middleware for ASP.NET Core. |
| `NarrativeTrace.Testing.Xunit` | `NarrativeFixture` — per-test context and failure-narrative printing (namespace `NarrativeTrace.TestingXunit`). |
| `NarrativeTrace.Testing.NUnit` | `NarrativeTestBase` — same for NUnit (namespace `NarrativeTrace.TestingNUnit`). |
| `NarrativeTrace.Diagrams` | Mermaid / PlantUML sequence-diagram renderers. |
| `NarrativeTrace.Clarity` | Naming-clarity analysis and reporting (`ClarityScanner`, `ClarityAnalyzer`). |
| `NarrativeTrace.Observability` | OpenTelemetry bridge — batch export and live span creation via `System.Diagnostics.ActivitySource`. |
| `NarrativeTrace.Logging` | `Microsoft.Extensions.Logging` bridge (`LoggingNarrativeContext`, `TraceLogExporter`, `AddNarrativeLogging()`). |
| `NarrativeTrace.Cli` | `dotnet-narrativetrace` global tool — reflection-only clarity scan + CI gate. |
| `NarrativeTrace.MSBuild` | Build-only package that wires the CLI into `dotnet build` / `dotnet test`. |

> Versions are pre-1.0 (`0.1.3` on nuget.org as of this writing, `0.1.4`
> next). Match the version you actually installed; keep every
> `NarrativeTrace.*` package on the same version.

## Choose an integration path

### Option A — DispatchProxy (works in any .NET app)

Wrap an interface-typed service; every call through the proxy is recorded
into the shared context.

```csharp
using NarrativeTrace.Core;
using NarrativeTrace.Proxy;
using NarrativeTrace.Runtime;

var context = new SyncNarrativeContext(
    new NarrativeTraceConfig(TracingLevel.Detail));

var orders = NarrativeTraceProxy.Create<IOrderService>(
    new DefaultOrderService(), context);

orders.PlaceOrder("C-1234", "SKU-KB", 2);

Console.WriteLine(IndentedTextRenderer.Render(context.CaptureTrace()));
context.Reset();
```

Use this when your services are interface-based. Share **one** context
across collaborating services so their calls nest into a single tree.

### Option B — Dependency-injection auto-wrapping

The `.NET` equivalent of Spring/Micronaut bean auto-wrapping. Register
your services as usual, then wrap the ones whose implementation namespace
matches a prefix:

```csharp
using NarrativeTrace.DependencyInjection;

services.AddScoped<IOrderService, DefaultOrderService>();

services.AddNarrativeTracing(options => options
    .Namespaces("MyApp.Services", "MyApp.Payments"));
```

Only **interface**-registered services whose implementation namespace
matches a configured prefix are wrapped. A scoped `INarrativeContext` is
registered automatically. See the
[Configuration Guide](configuration.md#3-dependency-injection) for options.

### Option C — ASP.NET Core request tracing

Add the services and place the middleware early in the pipeline. Each
request gets a fresh trace (in `HttpContext`), which the middleware
captures and exports when the request completes. Trace your services by
resolving that per-request context:

```csharp
using NarrativeTrace.AspNetCore;
using NarrativeTrace.Proxy;

builder.Services.AddNarrativeTrace(builder.Configuration);

var app = builder.Build();
app.UseMiddleware<NarrativeTraceMiddleware>();

app.MapGet("/orders/{id}", (string id, HttpContext http) =>
{
    var orders = NarrativeTraceProxy.Create<IOrderService>(
        new DefaultOrderService(), http.GetNarrativeContext());
    return orders.FindOrder(id);
});
```

See the [ASP.NET Core Integration Guide](aspnetcore.md) for exporters,
excluded paths, and request/user context.

### Option D — xUnit auto context + failure narrative

```csharp
using NarrativeTrace.Proxy;
using NarrativeTrace.TestingXunit;   // note: no dot before Xunit
using Xunit;

public sealed class OrderServiceTests
{
    [Fact]
    public void Customer_places_order()
    {
        using var narrative = new NarrativeFixture();

        narrative.Run("Customer places order", context =>
        {
            var orders = NarrativeTraceProxy.Create<IOrderService>(
                new DefaultOrderService(), context);
            orders.PlaceOrder("C-1234", "SKU-KB", 2);
        });
    }
}
```

Create one `NarrativeFixture` per test with `using var` so each test gets
a clean context (disposal resets it). `NarrativeFixture.Run` prints the
captured narrative to the console when the body throws, then rethrows —
so a failing test explains itself.

### Option E — NUnit auto context + failure narrative

```csharp
using NarrativeTrace.Proxy;
using NarrativeTrace.TestingNUnit;   // note: no dot before NUnit
using NUnit.Framework;

public sealed class OrderServiceTests : NarrativeTestBase
{
    [Test]
    public void Customer_places_order()
    {
        var orders = NarrativeTraceProxy.Create<IOrderService>(
            new DefaultOrderService(), Context);
        orders.PlaceOrder("C-1234", "SKU-KB", 2);
    }
}
```

`NarrativeTestBase` opens a fresh context per test (`[SetUp]`) and prints
the narrative on failure (`[TearDown]`). Override `OnTraceComplete` to
write files or run clarity analysis.

### Option F — `dotnet-narrativetrace` CLI

Install the global tool and analyze naming clarity from a compiled
assembly — no test run required:

```bash
dotnet tool install --global NarrativeTrace.Cli

dotnet-narrativetrace clarity-scan --assembly bin/Release/net10.0/MyApp.dll
dotnet-narrativetrace clarity-check --results clarity-results.json --min-score 0.80 --max-high-issues 0
```

See the [Clarity Guide](clarity.md) for the scoring model and CI gate.

### Option G — MSBuild integration

Add the build-only package to run the clarity gate as part of your build:

```xml
<PackageReference Include="NarrativeTrace.MSBuild" Version="0.1.3"
                  PrivateAssets="all" />
```

This registers `ClarityScan` and `ClarityCheck` targets and forwards the
`NARRATIVETRACE_*` settings to the test host. Configure thresholds with
MSBuild properties (see the [Configuration Guide](configuration.md#5-msbuild)).

## Configure trace output

The library reads four `NARRATIVETRACE_*` environment variables through
`ConfigResolver` — the `.NET`-native override channel:

| Variable | Values | Default |
|---|---|---|
| `NARRATIVETRACE_LEVEL` | `Off`, `Errors`, `Summary`, `Narrative`, `Detail` | `Detail` |
| `NARRATIVETRACE_OUTPUT` | `true` / `false` (also `1` / `0`) | `true` |
| `NARRATIVETRACE_OUTPUT_DIR` | any writable path | `TestResults/narrativetrace` |
| `NARRATIVETRACE_FORMAT` | `Markdown`, `Text`, `Prose`, `Json` | `Markdown` |

Invalid values degrade to the default rather than throwing, so bad
configuration never crashes capture. Level parsing is case- and
punctuation-lenient (`detail`, `DETAIL`, `Detail` all resolve).
`NARRATIVETRACE_OUTPUT` is on by default *(since 0.1.4, unreleased)* — the
xUnit fixture and NUnit base write per-test artifacts to
`TestResults/narrativetrace/` (ephemeral, already gitignored by the `.NET`
`TestResults/` convention) without any flag; set it to `false` to opt out.

## Validate installation

Render a trace to the console:

```csharp
var context = new SyncNarrativeContext(
    new NarrativeTraceConfig(TracingLevel.Detail));
var svc = NarrativeTraceProxy.Create<IGreeter>(new Greeter(), context);
svc.Greet("world");
Console.WriteLine(IndentedTextRenderer.Render(context.CaptureTrace()));
```

You should see a nested narrative with the method name, parameter values,
and return value.

## See also

- [Configuration Guide](configuration.md) — tracing levels, env vars, DI, ASP.NET Core, MSBuild
- [Annotations Guide](annotations.md) — `[Narrated]`, `[OnError]`, `[NotTraced]`, `[Traced]`, `[NarrativeSummary]`
- [ASP.NET Core Integration Guide](aspnetcore.md) — middleware, exporters, request/user context
- [Clarity Guide](clarity.md) — scoring model, scanner, CI gate
