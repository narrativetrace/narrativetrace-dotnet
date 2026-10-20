---
name: add-narrative-tracing
description: "Adds NarrativeTrace to a .NET project end to end: installs the real toolchain with a frozen restore, wraps a service and renders its first trace, sends the trace to a logger with one call, finishes by running the doctor CLI to confirm the install, and ends by previewing the agent skills install so the next session finds them. Use when asked to \"add narrative tracing\", \"set up NarrativeTrace\", \"wire up tracing for this service\", \"instrument this .NET app with NarrativeTrace\", or \"get a trace out of this code\"."
when_to_use: "A .NET project has no NarrativeTrace install yet and needs one wired up from scratch."
allowed-tools: Bash(dotnet *), Bash(git *)
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

## 2. Wire the frameworks this project already uses

```bash
dotnet tool run dotnet-narrativetrace doctor --json
```

**verify:** every config.<framework>-* finding in the report, and config.registration-pair, passes: apply each failing one's fix in order, as printed, then run the doctor again; a framework the doctor reports as having no integration shipped is left alone

**failure:** the same framework finding fails again after its fix was applied — the snippet was pasted as printed instead of adapted to this project — its service and namespace names are the fixture's, not this project's — fix: keep the printed calls, swap in this project's own services and namespaces, rerun the doctor; if it still fails, report it with the narrativetrace-feedback skill

**failure:** a service under an included namespace is not traced by AddNarrativeTracing — generated code is never traced, and the shipped default exclude list leaves framework, mocking and test-runner namespaces alone; an exclude always wins over an include — fix: read the doctor's config.exclude finding for the effective list; ExcludeNamespaces(...) or NARRATIVETRACE_EXCLUDE replaces that default as a whole, so repeat any prefix you still want

## 3. First trace: wrap the service, call it, render the tree, run it

**when:** if the project already has tests — a test project in the solution, a test SDK among its package references — and the doctor reports a test integration for that runner, do not add a demo program: run one existing test through that integration with output on, which writes that test's narrative files, `.md` and `.nt`, under the integration's output directory — the files narrativetrace-doctor, narrativetrace-verify, narrativetrace-debug and narrativetrace-explain read; print the rendered file so the trace is also on the console; the step is done when that test's files exist; a project whose runner has no integration keeps that runner and takes the console path below; otherwise, if the project already has an application entry point — a Main method or top-level program it already runs, a web or application framework the doctor reports — do not add a demo program: run the application the way it already runs, exercise one real boundary, and read that request's trace; the verify below is for the console app, and in an existing application the step is done when that request's trace is in the output; otherwise create the smallest console app as follows; nothing is written to disk by this program — the application's log is the record once the logging provider in the later step is wired; this page never adds a test runner or a test to a project that has none

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
    new NarrativeTraceConfig(
        serviceIdentity: new ServiceIdentity("hello-service"), // names the service; unset, spans read unknown_service:dotnet
        initialTraceparent: Traceparent.Parse(DemoTraceparent)));
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

**failure:** a span is named after the interface, not the class that implements it — the proxy wraps an interface and names its spans after it; there is no typeless target to name — fix: keep the interface name, or pass `new ProxyOptions(ClassName: "OrderService")` to the proxy to name the span yourself

**failure:** a parameter renders as arg0, arg1 — reflection reads parameter names from the interface method, and this one carries none (an interface emitted at run time without parameter names) — fix: put `[Traced("customerId", "productId")]` on the interface method, one name per parameter

## 4. Name the service: set NARRATIVETRACE_SERVICE_NAME, or `new ServiceIdentity("order-service")` in the config the context is built from

```bash
dotnet tool run dotnet-narrativetrace doctor --json
```

**verify:** the doctor's config.service-identity finding reports "passed": true

**failure:** spans show the service as unknown_service:dotnet, on the platform or in a trace backend — no service name was set, and NarrativeTrace never guesses one from the assembly — fix: set NARRATIVETRACE_SERVICE_NAME (and NARRATIVETRACE_SERVICE_VERSION, NARRATIVETRACE_ENVIRONMENT) in the environment, or pass `new NarrativeTraceConfig(serviceIdentity: new ServiceIdentity("order-service"))` to the context, `options.ServiceIdentity` to AddNarrativeTracing or AddNarrativeTrace, or `NarrativeTrace:ServiceName` in appsettings.json

## 5. Send it to your logger — one call, same captured tree

<!-- snippet: examples/NarrativeTrace.Examples.SixtySeconds.WithLogger/Program.cs -->
```csharp
using Microsoft.Extensions.Logging; // new: send the trace to an ILogger, not only the console
using Microsoft.Extensions.Logging.Console; // new: for LoggerColorBehavior below
using NarrativeTrace.Core;
using NarrativeTrace.Logging; // new: TraceLogExporter replays a captured trace onto an ILogger
using NarrativeTrace.Proxy;
using NarrativeTrace.Runtime;

// A fixed W3C traceparent, seeded through NarrativeTraceConfig so this page's embedded output
// always names the same trace. A real run adopts nothing here (or a real inbound request header,
// via NarrativeTraceMiddleware) and gets a fresh, randomly generated trace id every time.
const string DemoTraceparent = "00-a1b2c3d4a1b2c3d4a1b2c3d4a1b2c3d4-a1b2c3d4a1b2c3d4-01";

var context = new SyncNarrativeContext(
    new NarrativeTraceConfig(
        serviceIdentity: new ServiceIdentity("hello-service"), // names the service; unset, spans read unknown_service:dotnet
        initialTraceparent: Traceparent.Parse(DemoTraceparent)));
var orders = NarrativeTraceProxy.Create<IOrderService>(new OrderService(), context);

orders.PlaceOrder("cust-1", "book-123", 2);

var tree = context.CaptureTrace(); // new: capture once, render it to both destinations below
Console.WriteLine(IndentedTextRenderer.Render(tree));

using var loggerFactory = LoggerFactory.Create(builder => // new: a second destination for the same trace
    builder.AddSimpleConsole(options => options.ColorBehavior = LoggerColorBehavior.Disabled)); // new: disable ANSI color codes so captured/piped output stays plain text
TraceLogExporter.ExportToLogger(tree, loggerFactory.CreateLogger("NarrativeTrace")); // new: replay the same tree onto the logger

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

**verify:** the console logger prints one Information-level record per trace node

## 6. Run the doctor and resolve every finding it reports

```bash
dotnet tool run dotnet-narrativetrace doctor --json
```

**verify:** the doctor CLI's own process exit code is 0 — no failing findings remain

**failure:** the doctor CLI exits 1 — at least one finding failed — fix: read its "fix" field and doc URL, resolve it, then rerun doctor

## 7. Install the skills for next time

```bash
dotnet tool run dotnet-narrativetrace init --dry-run
```

**verify:** the diff names .agents/skills/ and AGENTS.md, and neither exists yet — nothing is written until the same command runs again without --dry-run; the next session verifies with narrativetrace-verify — once a change's tests are green, it reads the trace before it reports

**failure:** command not found: dotnet-narrativetrace — NarrativeTrace.Cli is not installed as a local tool in this project yet — fix: run this skill's own first step, or install it directly with `dotnet tool install NarrativeTrace.Cli`

## Always
- Install with the project's own frozen restore, never a bare `dotnet add package` left unlocked (an unlocked restore can silently resolve a different version tomorrow than it did today)
- Finish by running the doctor CLI (it is the same tested check the narrativetrace-doctor skill uses — resolving its findings here means starting from a clean baseline)

## Never
- Never seed a fixed traceparent outside an example or test (the constant exists so this page's output is reproducible — a real run adopts a real inbound header or generates its own trace id)
- Never call the wrapped target directly instead of the proxy (only calls made through the proxy returned by NarrativeTraceProxy.Create<T> are captured)
- Never apply the installer without showing its diff first (it writes into AGENTS.md and the project's skill directories, and the approval for that is a person reading the diff — run it with --dry-run, show the output, and let them run it again without the flag)

