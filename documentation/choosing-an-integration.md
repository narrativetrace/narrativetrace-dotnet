# Choosing an Integration

**English** | [Español](es/elegir-una-integracion.md) | [Português](pt-BR/escolhendo-uma-integracao.md) | [简体中文](zh-CN/选择集成方式.md)

NarrativeTrace ships several ways to get a call traced. This page is a
decision aid, not a tutorial — for the code behind each path, see the
[Installation Guide](guides/installation.md).

## You want... / Start with...

| You want | Start with |
|---|---|
| Traces in a test, least ceremony | xUnit `NarrativeFixture` or NUnit `NarrativeTestBase` |
| Explicit control over exactly what's wrapped, in plain .NET | `NarrativeTraceProxy.Create<T>` (a `DispatchProxy`) |
| Every MS.DI-registered interface service under a namespace traced automatically | `AddNarrativeTracing` (the .NET analogue of Spring/Micronaut bean tracing) |
| Production HTTP request lifecycle in ASP.NET Core | `NarrativeTrace.AspNetCore` middleware |
| A CI naming-quality gate over a compiled assembly, no test run required | `dotnet-narrativetrace clarity-scan` + `clarity-check`, or the `NarrativeTrace.MSBuild` package |
| Traces routed into your existing `ILogger` pipeline | `NarrativeTrace.Logging` (`AddNarrativeLogging()`) |
| OpenTelemetry spans, batch or live | `NarrativeTrace.Observability` |
| Sequence diagrams (Mermaid/PlantUML) alongside a trace | `NarrativeTrace.Diagrams` |
| Zero code changes at all — an app you don't control the wiring of | **Not shipped yet.** Planned (Free) — no CLR analogue of a Java `-javaagent` has been decided; a source-generator vs. IL-weaving spike is the open question. See the [Feature Guide](feature-guide.md). |

## The decision

Every path in this runtime attaches at an **interface boundary** — there is no
bytecode-weaving or zero-code option today (the row above). The real
question is *how* you reach that interface call:

```
Are you writing a test?
  yes -> xUnit?  -> NarrativeFixture
         NUnit?  -> NarrativeTestBase (derive from it)

  no  -> Do you construct/own the instance being wrapped yourself?
           yes -> Is it registered in MS.DI by interface?
                    yes -> AddNarrativeTracing (namespace-matched auto-wrap)
                    no  -> NarrativeTraceProxy.Create<T> directly
           no  -> Is this an ASP.NET Core HTTP request boundary?
                    yes -> NarrativeTrace.AspNetCore middleware
                    no  -> zero-code instrumentation is Planned, not
                           shipped — see the Feature Guide before assuming
                           it exists
```

Layer on top, independent of the answer above:

- Want the trace in your existing log stream too? Add
  `NarrativeTrace.Logging` beside whichever capture path you chose.
- Want OpenTelemetry spans? Add `NarrativeTrace.Observability` — it exports
  from a completed `TraceTree` (batch) or a live event stream, so it
  layers on top of any of the paths above rather than replacing one.
- Want a naming-quality gate as part of the build, independent of whether
  tests run? `NarrativeTrace.MSBuild`/the CLI work from a **compiled
  assembly** via reflection, not from captured traces — a different data
  source than everything else on this page.

## One thing every path shares

Whatever you wrap, only **interface** calls are visible — `DispatchProxy` is
interface-scoped by construction, so private and internal calls inside an
implementation are never individually traced. If you want finer-grained
narration, split the behavior you care about behind its own interface.
Redaction, bounds, and exception isolation are identical across every path
too — see [Privacy and Redaction](privacy-and-redaction.md); there is no
"more trusted" integration that relaxes them.

## Caveats per path

- **`NarrativeTraceProxy.Create<T>`** — `T` must be an interface and the
  target must implement it; a mismatch surfaces as the underlying .NET
  reflection exception (`DispatchProxy.Create`/`MethodInfo.Invoke`), not a
  NarrativeTrace-specific one. See [Troubleshooting](troubleshooting.md).
- **`AddNarrativeTracing`** — call it **last**, after every service it
  should see is already registered; wrapping only touches the descriptors
  present at the moment of the call. Namespace matching is dot-boundary
  (`MyApp.Services` never matches `MyApp.ServicesExtra`) and is checked
  against the **interface's** namespace for factory-registered services,
  since the implementation type is opaque at registration time. Keyed
  services and open generics are silently left unwrapped.
- **ASP.NET Core middleware** — place `UseMiddleware<NarrativeTraceMiddleware>()`
  near the outer edge of the pipeline. Code that calls
  `HttpContext.GetNarrativeContext()` before the middleware ran (or on an
  excluded path) gets a silent no-op context, not an exception — the
  request completes normally, just untraced.
- **xUnit `NarrativeFixture`** — one fixture holds one context, so it
  serves one test at a time; `IClassFixture<NarrativeFixture>` is safe
  because xUnit doesn't parallelize within a class, but sharing an instance
  across classes that do run in parallel merges their spans into one trace.
- **NUnit `NarrativeTestBase`** — derive from it; setup/teardown wiring is
  automatic, and `OnTraceComplete` is your override point for writing files
  or running clarity analysis.
- **`clarity-scan`** — needs a real, compiled .NET assembly path; it never
  executes the assembly, only reflects over its metadata.

## Platform ceilings

- `net10.0` — full surface, including `NarrativeTrace.AspNetCore` (net10.0
  only today).
- `netstandard2.0` — everything except the ASP.NET Core middleware.
- `net48` — via `NarrativeTrace.Legacy`; compiles on every platform, but
  runtime validation on Windows is still pending (see the
  [Feature Guide](feature-guide.md)).

## Recipes

Full, runnable code for every path above lives in the
[Installation Guide](guides/installation.md#choose-an-integration-path).
For the least-ceremony path end-to-end — one service, one test, real
output — see [See a trace in 60 seconds](sixty-seconds.md).
