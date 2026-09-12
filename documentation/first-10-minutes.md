# See a trace in 60 seconds

**English** | [Español](es/primeros-10-minutos.md) | [Português](pt-BR/primeiros-10-minutos.md) | [简体中文](zh-CN/前10分钟.md)

No log statements, no test framework, no files to open: a console app, one
`dotnet run`, and a trace in your terminal. Everything below was run for
real against the published packages — the output is pasted, not imagined.

## 1. New console app, add the packages

```bash
dotnet new console -n Hello && cd Hello
dotnet add package NarrativeTrace.Core
dotnet add package NarrativeTrace.Runtime
dotnet add package NarrativeTrace.Proxy
```

Three packages, resolved from nuget.org — there's no single metapackage
that pulls in everything at once (yet).

## 2. Replace Program.cs

<!-- snippet: examples/NarrativeTrace.Examples.SixtySeconds/Program.cs -->
```csharp
using NarrativeTrace.Core;
using NarrativeTrace.Proxy;
using NarrativeTrace.Runtime;

var context = new SyncNarrativeContext(new NarrativeTraceConfig());
var orders = NarrativeTraceProxy.Create<IOrderService>(new OrderService(), context);

orders.PlaceOrder("cust-1", "book-123", 2);

Console.WriteLine(IndentedTextRenderer.Render(context.CaptureTrace()));

public interface IOrderService
{
    string PlaceOrder(string customerId, string productId, int quantity);
}

public sealed class OrderService : IOrderService
{
    public string PlaceOrder(string customerId, string productId, int quantity)
        => $"confirmed:{customerId}:{productId}:{quantity}";
}
```
<!-- /snippet -->

## 3. Run it

```bash
dotnet run
```

<!-- snippet: artifacts/sixty-seconds/see-a-trace.txt mask=duration -->
```text
└── IOrderService.PlaceOrder(customerId: "cust-1", productId: "book-123", quantity: 2) → "confirmed:cust-1:book-123:2" — 9ms
```
<!-- /snippet -->

(The timing will vary run to run — everything else is stable.)

You didn't write a single log statement. That narrative came entirely from
your method name, your parameter names, and the value you returned.

## What just happened

- `NarrativeTraceProxy.Create<T>` wraps `OrderService` behind its
  `IOrderService` interface and records every call made through the wrapper.
- `SyncNarrativeContext` holds the recording in memory; `NarrativeTraceConfig()`
  defaults to `TracingLevel.Detail` — full parameter and return-value capture,
  no flags to flip.
- `CaptureTrace()` returns the recorded tree; `IndentedTextRenderer` printed
  it above. `MarkdownRenderer` and `ProseRenderer` render the same tree in
  other formats. Wrap a service that calls other traced services and the
  tree nests — one call, one story.

## Send it to your logger

Two more packages, the same captured tree, no new capture logic —
`TraceLogExporter` replays it onto whatever `ILogger` your app already
wires up.

```bash
dotnet add package NarrativeTrace.Logging
dotnet add package Microsoft.Extensions.Logging.Console
```

```diff
+using Microsoft.Extensions.Logging;
 using NarrativeTrace.Core;
+using NarrativeTrace.Logging;
 using NarrativeTrace.Proxy;
 using NarrativeTrace.Runtime;

 var context = new SyncNarrativeContext(new NarrativeTraceConfig());
 var orders = NarrativeTraceProxy.Create<IOrderService>(new OrderService(), context);

 orders.PlaceOrder("cust-1", "book-123", 2);

-Console.WriteLine(IndentedTextRenderer.Render(context.CaptureTrace()));
+var tree = context.CaptureTrace();
+Console.WriteLine(IndentedTextRenderer.Render(tree));
+
+using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
+TraceLogExporter.ExportToLogger(tree, loggerFactory.CreateLogger("NarrativeTrace"));
```

```bash
dotnet run
```

<!-- snippet: artifacts/sixty-seconds/see-a-trace-with-logger.txt mask=duration -->
```text
└── IOrderService.PlaceOrder(customerId: "cust-1", productId: "book-123", quantity: 2) → "confirmed:cust-1:book-123:2" — 0ms

info: NarrativeTrace[1]
      IOrderService.PlaceOrder(customerId: "cust-1", productId: "book-123", quantity: 2) -> "confirmed:cust-1:book-123:2"
```
<!-- /snippet -->

(The timing will vary run to run — everything else is stable.)

Same trace, two destinations: the console renderer stays exactly as it was,
and the `ILogger` record below it proves the tree lands in the sink you
already have — swap `AddConsole()` for your real provider and nothing else
changes. See the [Installation Guide](guides/installation.md) for
`NarrativeTrace.Logging`'s other entry points (`LoggingNarrativeContext` for
live per-call logging, `AddNarrativeLogging()` for DI).

## Next

- **Use it in your tests, or wire it into DI / ASP.NET Core** —
  [Choosing an Integration](choosing-an-integration.md) is the decision
  diagram; [Installation Guide](guides/installation.md) has the full
  package and wiring reference.
- **Keep a value out of the trace** — `[NotTraced]`, the always-on name
  deny-list, and the value-free `.nt` structural artifact are three
  independent layers — see
  [Privacy and Redaction](privacy-and-redaction.md).
- **Score your naming** — rename `PlaceOrder` to `Process`, keep everything
  else the same, and the clarity score drops — see the
  [Clarity guide](guides/clarity.md).
- **Every knob** — [Configuration Guide](guides/configuration.md): tracing
  levels, output format, every `NARRATIVETRACE_*` variable.
- **Something not working?** — [Troubleshooting](troubleshooting.md):
  symptom → cause → fix for the failure modes people actually hit.
