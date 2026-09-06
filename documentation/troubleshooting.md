# Troubleshooting

**English** | [Español](es/solucion-de-problemas.md) | [Português](pt-BR/solucao-de-problemas.md) | [简体中文](zh-CN/故障排查.md)

Symptom → cause → fix, harvested from this port's own code and tests. Where
something is a known, uncovered rough edge rather than a demonstrated
guarantee, it's called out as one — this page states what's actually true
today, not what would be nice to promise.

## A parameter renders as `arg0`, `arg1` instead of its real name

**Cause:** the capture code reads `ParameterInfo.Name`, which is `null` only
when the compiled metadata never carried parameter names — dynamically
built delegates (`System.Reflection.Emit`) or some interop-generated
interfaces. **Ordinary C#/F# code compiled by `dotnet build` always keeps
parameter names**, including in Release builds and under trimming, so this
should not happen in normal use.

**Fix:** if you hit it, check whether the interface came from a code
generator or dynamic-assembly path that dropped parameter metadata. This is
the one difference from the JVM: .NET needs no `-parameters` compiler flag
at all for ordinary code.

## `NarrativeTraceProxy.Create<T>` throws at startup

**Cause:** `T` isn't an interface, or the target doesn't implement it. This
port adds no guard clause of its own here — the exception you see is the
underlying .NET reflection exception (`DispatchProxy.Create`), not a
NarrativeTrace-specific one.

**Fix:** confirm `T` is an interface type and that the instance you're
wrapping actually implements it.

## A DI-registered service isn't being traced

**Cause**, any of:

- It isn't registered by **interface** — `AddNarrativeTracing` only wraps
  interface-typed registrations.
- Its implementation namespace doesn't match a configured prefix.
  Matching is dot-boundary: `MyApp.Services` never matches
  `MyApp.ServicesExtra`.
- It's a **keyed service** or an **open generic** — both are silently left
  unwrapped.
- `AddNarrativeTracing` was called **before** the service was registered —
  wrapping only touches the descriptors present at the moment of the call.
- It's factory-registered, and matching fell back to the **interface's**
  namespace (the implementation type is opaque at registration time) —
  which can differ from the real implementation's namespace.

**Fix:** call `AddNarrativeTracing` last, double-check the namespace
prefix, and avoid keyed/open-generic registrations for services you need
traced.

## ASP.NET Core requests show no trace, and no error

**Cause:** `HttpContext.GetNarrativeContext()` returns a silent no-op
context whenever nothing was stashed by the middleware — an excluded path,
or code that runs before `UseMiddleware<NarrativeTraceMiddleware>()` in the
pipeline. The request completes normally; it's just untraced.

**Fix:** place the middleware near the outer edge of the pipeline, and
check `ExcludedPaths` if a specific route is the one going quiet.

## A background `Task.Run`'s work is missing, or shows up as a separate root

**Cause:** `AsyncLocal` context flows correctly for work started *and*
observed inside a `RunAsync` scope. A bare `Task.Run` that's still running
when its scope closes attaches at **execution time, not submission time** —
a documented divergence, not a bug — so it can land as a second root instead
of nested under the call that launched it.

**Fix:** use `AsyncNarrativeContext.RunAsync`, `ForkJoinGroup`, or
`FireAndForgetGroup` for the lineage guarantee you actually need, rather
than a bare `Task.Run`.

## `CaptureTrace()` returns an empty trace when you expected content

**Cause:** reading capture from outside any `AsyncNarrativeContext` scope —
or after two scopes ran concurrently — deliberately returns an **empty**
tree rather than a foreign one. An absent narrative is honest; someone
else's request's narrative would not be.

**Fix:** capture from inside the scope that produced the work, or make sure
you've awaited the work before capturing.

## A fire-and-forget failure never shows up anywhere

**Cause:** `FireAndForgetGroup` swallows an exception in launched work by
design — that's what makes it safe to fire and forget — and excludes the
failed branch from `ChildRoots`. This is intentional isolation, not a
missing feature.

**Fix:** if you need to observe the failure, add your own handling inside
the launched work; don't rely on the trace to surface it.

## `ForkJoinGroup` branches ran but their spans are missing

**Cause:** skipping `await fork.JoinAsync()` means the branches' spans never
reach the parent trace — the work still runs, but the narrative loses it.

**Fix:** always `await` the join.

## Two xUnit tests' traces are mixed together

**Cause:** a `NarrativeFixture` holds a single context, so it serves one
test at a time. `IClassFixture<NarrativeFixture>` is safe because xUnit
never parallelizes within a class — but sharing one fixture instance across
classes (or via a collection) that *do* run in parallel merges their spans
into one trace.

**Fix:** let each test class own its own fixture instance; don't share one
across parallel test collections.

## `clarity-scan --assembly` crashes instead of printing a clean error

**Cause:** a missing file is handled cleanly (`error: assembly not found`,
exit code `1`). An **existing but invalid** file — not a real .NET assembly
— is not: it's loaded via `MetadataLoadContext` with no guard, so a
malformed file surfaces as a raw, unhandled .NET exception rather than one
of the documented exit codes. This is a known, uncovered rough edge, not a
documented behavior.

**Fix:** double-check the path really points at a compiled .NET assembly.
If you hit the crash, that's worth reporting rather than working around.

## A `NARRATIVETRACE_*` typo does nothing, silently

**Cause:** by design, every `NARRATIVETRACE_*` environment variable
degrades to its default rather than throwing on an unrecognized value —
"bad configuration never crashes capture." `NARRATIVETRACE_LEVEL=Detial`
(typo) quietly resolves to `Detail`, not an error.

**Fix:** don't rely on a typo being caught — double-check spelling, or log
the resolved configuration at startup if you need to be sure.

## `NARRATIVETRACE_OUTPUT=true` but no files appear

**Cause**, either of:

- `NARRATIVETRACE_LEVEL` is `Off` — nothing was ever captured.
- The trace really is empty. **An empty trace writes nothing at all, by
  design** — a missing artifact means "nothing was captured," not "the
  write failed." This usually means the test called the raw, unwrapped
  service instead of the proxy-wrapped one.

**Fix:** confirm the level isn't `Off`, and confirm you're calling through
`NarrativeTraceProxy.Create<T>` (or an auto-wrapped DI service), not the
bare implementation.

## `clarity-report.md` doesn't match what I expect from `NARRATIVETRACE_OUTPUT`

**Cause:** the suite-level `clarity-results.json`/`clarity-report.md` are
written whenever the suite fixture runs and accumulates at least one entry —
**independent of `NARRATIVETRACE_OUTPUT`**. Only the per-test `.md`/`.json`/
`.mmd`/`.nt` files need that flag.

**Fix:** don't treat "no per-test trace files" as "no clarity report" —
they're gated by two different conditions.

## Glossary harvesting never writes anything

**Cause:** harvesting is opt-in by **file presence** — `GlossarySuiteReporter`
silently does nothing unless `glossary.json` already exists at the resolved
location (an upward directory search, `NARRATIVETRACE_GLOSSARY`, or the
literal value `off`).

**Fix:** commit a starting `glossary.json` if you want harvesting to run —
see [What to Commit](what-to-commit.md#glossary-harvesting-is-opt-in-by-file-presence).

## Referencing `NarrativeTrace.MSBuild` pulls it into my package's dependencies

**Cause:** it's meant to be a build-time-only dependency.

**Fix:** reference it with `PrivateAssets="all"` so it doesn't flow
transitively to consumers of your own NuGet package.
