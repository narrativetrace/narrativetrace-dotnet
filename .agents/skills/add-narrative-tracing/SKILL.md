---
name: add-narrative-tracing
description: "Adds NarrativeTrace to a .NET project end to end: installs the real toolchain with a frozen restore, wraps a service and renders its first trace, sends the trace to a logger with one call, and finishes by running the doctor CLI to confirm the install. Use when asked to \"add narrative tracing\", \"set up NarrativeTrace\", \"wire up tracing for this service\", \"instrument this .NET app with NarrativeTrace\", or \"get a trace out of this code\"."
when_to_use: "A .NET project has no NarrativeTrace install yet and needs one wired up from scratch."
allowed-tools: dotnet, git
---

# add-narrative-tracing

## 1. Install with the real toolchain — a frozen restore, never an ad hoc add

```bash
dotnet tool install NarrativeTrace.Cli
dotnet add package NarrativeTrace.Proxy
dotnet restore --locked-mode
```

**verify:** every finding whose id starts with "toolchain." reports "passed": true

**failure:** `dotnet restore --locked-mode` fails with no lock file found — the project has no committed packages.lock.json yet — fix: run `dotnet restore` once without --locked-mode to generate the lock file, commit it, then use --locked-mode from then on

## 2. First trace: wrap the service, call it, render the tree, run it

<!-- snippet: examples/NarrativeTrace.Examples.SixtySeconds/Program.cs -->
```csharp
using NarrativeTrace.Core;
using NarrativeTrace.Proxy;
using NarrativeTrace.Runtime;

// snippet:begin fixedTraceparent
// A fixed W3C traceparent, seeded through NarrativeTraceConfig so this page's embedded output
// always names the same trace. A real run adopts nothing here (or a real inbound request header,
// via NarrativeTraceMiddleware) and gets a fresh, randomly generated trace id every time.
const string DemoTraceparent = "00-a1b2c3d4a1b2c3d4a1b2c3d4a1b2c3d4-a1b2c3d4a1b2c3d4-01";
// snippet:end fixedTraceparent

var context = new SyncNarrativeContext(
    new NarrativeTraceConfig(initialTraceparent: Traceparent.Parse(DemoTraceparent)));
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

**verify:** `dotnet run` prints a "trace: <name> (<id>)" line followed by the call tree

**failure:** no trace line appears — the wrapped target was called directly instead of the proxy returned by NarrativeTraceProxy.Create<T> — fix: call the proxy instance everywhere the service is used, never the wrapped target

## 3. Send it to your logger — one call, same captured tree

<!-- snippet: examples/NarrativeTrace.Examples.SixtySeconds/WithLogger.cs -->
```csharp
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using NarrativeTrace.Core;
using NarrativeTrace.Logging;
using NarrativeTrace.Proxy;
using NarrativeTrace.Runtime;

namespace NarrativeTrace.Examples.SixtySeconds;

/// <summary>
/// The "Send it to your logger" postscript
/// (<c>documentation/sixty-seconds.md</c>) — the same tutorial call as
/// <c>Program.cs</c>, plus the two packages and four lines the page's diff
/// block adds. A second file rather than a second run mode of
/// <c>Program.cs</c>, so that file — and the code block it backs — stays
/// byte-identical forever; this one is never itself the entry point
/// <c>dotnet run</c> executes.
/// </summary>
public static class WithLogger
{
    /// <summary>
    /// Runs the postscript's call and returns exactly what a reader would see
    /// printed to their terminal after applying the diff and running
    /// <c>dotnet run</c> again: the unchanged console renderer line, then the
    /// <c>ILogger</c> record <see cref="TraceLogExporter"/> replays onto it.
    /// </summary>
    /// <remarks>
    /// Disables the console formatter's color codes — not part of the page's
    /// diff, which configures nothing — purely so the captured text is the
    /// plain characters a terminal shows, never the ANSI escapes underneath
    /// them: a real terminal emulator strips those before a reader could ever
    /// copy them into a page, but a redirected <see cref="Console.Out"/>
    /// wouldn't know to.
    /// </remarks>
    public static string Run()
    {
        var originalOut = Console.Out;
        var captured = new StringWriter { NewLine = "\n" };
        Console.SetOut(captured);
        try
        {
            RunTutorial();
            return captured.ToString();
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    // Mirrors Program.cs's own fixed demo traceparent constant (see that file's
    // "fixedTraceparent" snippet region) — kept a separate literal for the same
    // reason the rest of this file duplicates Program.cs's call rather than
    // sharing it: Program.cs's own embedded snippet must never depend on this
    // test-only helper.
    private const string DemoTraceparent = "00-a1b2c3d4a1b2c3d4a1b2c3d4a1b2c3d4-a1b2c3d4a1b2c3d4-01";

    // Console.Out is already redirected by Run() above by the time this
    // executes — split out only to keep Run() under the line-count gate.
    private static void RunTutorial()
    {
        var context = new SyncNarrativeContext(
            new NarrativeTraceConfig(initialTraceparent: Traceparent.Parse(DemoTraceparent)));
        var orders = NarrativeTraceProxy.Create<IOrderService>(new OrderService(), context);

        orders.PlaceOrder("cust-1", "book-123", 2);

        var tree = context.CaptureTrace();
        Console.WriteLine(IndentedTextRenderer.Render(tree));

        using var loggerFactory = LoggerFactory.Create(builder =>
            builder.AddSimpleConsole(options => options.ColorBehavior = LoggerColorBehavior.Disabled));
        // Disposal (end of scope) blocks until the console provider's
        // background writer thread has flushed everything queued above —
        // without it, Run()'s captured text could be read before the
        // logger's line ever lands in it.
        TraceLogExporter.ExportToLogger(tree, loggerFactory.CreateLogger("NarrativeTrace"));
    }
}
```
<!-- /snippet -->

**verify:** the console logger prints one Information-level record per trace node

## 4. Run the doctor and resolve every finding it reports

```bash
dotnet tool run dotnet-narrativetrace doctor --json
```

**verify:** the doctor CLI's own process exit code is 0 — no failing findings remain

**failure:** the doctor CLI exits 1 — at least one finding failed — fix: read its "fix" field and doc URL, resolve it, then rerun doctor

## Always
- Install with the project's own frozen restore, never a bare `dotnet add package` left unlocked (an unlocked restore can silently resolve a different version tomorrow than it did today)
- Finish by running the doctor CLI (it is the same tested check the narrativetrace-doctor skill uses — resolving its findings here means starting from a clean baseline)

## Never
- Never seed a fixed traceparent outside an example or test (the constant exists so this page's output is reproducible — a real run adopts a real inbound header or generates its own trace id)
- Never call the wrapped target directly instead of the proxy (only calls made through the proxy returned by NarrativeTraceProxy.Create<T> are captured)

