# NarrativeTrace .NET — Configuration Guide

**English** | [Español](es/guia-de-configuracion.md) | [Português](pt-BR/guia-de-configuracao.md) | [简体中文](zh-CN/配置指南.md)

This guide documents runtime and build configuration for NarrativeTrace.

## Configuration surface

| Path | Mechanism | Best for |
|---|---|---|
| Programmatic | `NarrativeTraceConfig` constructor | Any app — direct control |
| Environment | `NARRATIVETRACE_*` variables (`ConfigResolver`) | CI, containers, test hosts |
| Dependency injection | `AddNarrativeTracing(options => …)` | MS.DI apps |
| ASP.NET Core | `AddNarrativeTrace(configuration)` + `"NarrativeTrace"` section | Web apps |
| MSBuild | `NarrativeTrace*` / `Clarity*` MSBuild properties | Build-time clarity gate |

## 1. Tracing levels (`NarrativeTraceConfig`)

A context is created from a `NarrativeTraceConfig`, which defaults to
`Detail`.

```csharp
using NarrativeTrace.Core;
using NarrativeTrace.Runtime;

var config = new NarrativeTraceConfig(TracingLevel.Narrative);
var context = new SyncNarrativeContext(config);
```

Available levels (enum `TracingLevel`, low → high):

| Level | Behavior |
|---|---|
| `Off` | No tracing captured. |
| `Errors` | Only exception paths captured. |
| `Summary` | Root entry, deepest leaf, and full exception chains. |
| `Narrative` | Full call flow, parameter values suppressed. |
| `Detail` | Full call flow with parameter and return values. |

The level is mutable at runtime (`config.Level = TracingLevel.Errors;`).
`IsActive()` is `true` at `Errors` and above (only `Off` deactivates
capture); `CapturesParameterValues` is `true` only at `Detail`.

### Service identity

Stamp stable process identity onto every span for correlation across
services:

```csharp
var identity = new ServiceIdentity(
    ServiceName: "order-service",
    ServiceVersion: "2.0.0",
    Environment: "production");

var config = new NarrativeTraceConfig(TracingLevel.Detail, identity);
```

These surface as `service.name` / `service.version` /
`service.environment` in JSON export, logging scopes, and OpenTelemetry
spans.

### Traceparent seeding *(since 0.1.4, unreleased)*

Seed a starting [W3C `traceparent`](https://www.w3.org/TR/trace-context/#traceparent-header)
so every context built from a configuration continues a caller's trace
instead of starting its own — the no-HTTP-header equivalent of what
`NarrativeTraceMiddleware` adopts from an inbound request (§4 below):

```csharp
var config = new NarrativeTraceConfig(
    initialTraceparent: Traceparent.Parse("00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01"));
var context = new SyncNarrativeContext(config);
```

`Traceparent.Parse` never throws — a malformed or absent value returns
`null`, which `NarrativeTraceConfig` treats as "no seed", so a context
built from it falls back to a fresh, randomly generated trace id. Fixed at
construction like `ServiceIdentity`: there is no setter, and re-seeding a
running context needs `context.AdoptTraceparent(...)` directly instead.

Leave it unset for production traffic — every context built from one
shared `NarrativeTraceConfig` adopts the same fixed value, which is
correct for a single top-level context (a demo, a one-shot script) but
defeats `AsyncNarrativeContext`'s whole purpose of giving each scope its
own distinct trace. Do not pair the two.

## 2. Environment variables (`ConfigResolver`)

`ConfigResolver` reads six variables — the `.NET`-native override
channel that keeps `Core` dependency-free. Invalid values **degrade to
the default rather than throwing**, so bad configuration never crashes
capture.

| Variable | Values | Default |
|---|---|---|
| `NARRATIVETRACE_LEVEL` | `Off`, `Errors`, `Summary`, `Narrative`, `Detail` | `Detail` |
| `NARRATIVETRACE_OUTPUT` | `true` / `false` (or `1` / `0`) | `true` |
| `NARRATIVETRACE_OUTPUT_DIR` | any writable path | `TestResults/narrativetrace` |
| `NARRATIVETRACE_FORMAT` | `Markdown`, `Text`, `Prose`, `Json` | `Markdown` |
| `NARRATIVETRACE_CANONICAL_JSON` | `true` / `false` (or `1`) | `false` |
| `NARRATIVETRACE_STRUCTURAL_JSON` | `true` / `false` (or `1`) | `false` |
| `NARRATIVETRACE_APPROVAL` | `true` / `false` (or `1`) | `false` |
| `NARRATIVETRACE_APPROVED_DIR` | any writable path | `narratives` |

Level and format parsing is lenient (case- and punctuation-insensitive:
`detail`, `DETAIL`, and `Detail` all resolve).

`NARRATIVETRACE_OUTPUT` is **on by default** (owner ruling, 2026-09-11)
*(since 0.1.4, unreleased)*: the per-test artifacts the xUnit fixture and
NUnit base write are the payoff of
adopting this library, so writing happens without any flag. Only an explicit
`NARRATIVETRACE_OUTPUT=false` (or `0`) opts out; `true`/`1` are accepted as a
no-op for scripts that still set it. With no `NARRATIVETRACE_OUTPUT_DIR`
override, files land under `TestResults/narrativetrace/` — the `.NET`
convention `dotnet test --results-directory` and Visual Studio/Rider already
treat as disposable, and which this repository's own `.gitignore` already
excludes.

```csharp
var resolved = ConfigResolver.Resolve();          // reads the process env
var config = new NarrativeTraceConfig(resolved.Level);
```

`Resolve()` returns a
`ResolvedConfig(Level, Output, OutputDir, Format, CanonicalJson, StructuralJson)`.

The last two switch on the machine-readable per-test artifacts written beside
the trace file, whatever the primary format is:

- `<test>.canonical.json` — the trace flattened into canonical schema entries
  (one `method_enter` and one `method_exit` per call), for schema consumers and
  cross-runtime conformance fixtures.
- `<test>.structural.json` — the same array with every runtime value elided
  (ADR-002 Level 1), for handing to an AI consumer: parameter names survive,
  parameter values, return values and exception messages do not.

Both are off by default; they are machine artifacts, not something you read
next to the trace.

`NARRATIVETRACE_APPROVAL` turns on approval mode *(since 0.1.4,
unreleased)*: after a **passing** test, the scenario's value-free structure
(the same render as the `.nt` artifact) is verified against the committed
approved trace `<approvedDir>/<TestClassSimpleName>/<artifact_name>.approved.nt`
— the same artifact identity as every other per-test file, so a method that
runs more than once has one approved trace per invocation. A missing
approved trace or a structural difference fails the test with a readable
diff and writes the current structure beside it as `*.received.nt`; review
it and accept it via the `Approve` build target (`./build.sh Approve`) or
rename it manually. A failing test's structure is never verified — it is
mid-flight and must not churn the received traces.
`NARRATIVETRACE_APPROVED_DIR` names the directory those baselines live in
(default `narratives`). See [Structural Trace Format](../structural-trace-format.md)
for the full behaviour, and [What to Commit](../what-to-commit.md) for
which of these files to commit.

## 3. Dependency injection

`AddNarrativeTracing` wraps interface-registered services whose
implementation namespace matches a configured prefix (the `.NET`
equivalent of Spring/Micronaut bean auto-wrapping).

```csharp
using NarrativeTrace.DependencyInjection;

services.AddNarrativeTracing(options =>
{
    options.Level = TracingLevel.Detail;           // default: Detail
    options.Namespaces("MyApp.Orders", "MyApp.Payments");
});
```

Behavior:

- Only **interface** service types are considered; open generics are
  skipped (a `DispatchProxy` limitation).
- Namespace matching uses **dot-boundary** semantics: `MyApp.Orders`
  matches `MyApp.Orders` and `MyApp.Orders.Sub`, but not
  `MyApp.OrdersLegacy`.
- The original registration's **lifetime is preserved** (a singleton stays
  a singleton, etc.).
- A **scoped** `INarrativeContext` is registered automatically; all wrapped
  services in the same scope share it, so their calls nest into one tree.
- For a factory- or instance-registered service, the concrete
  implementation namespace is used; if unavailable, the service-type
  (interface) namespace is used as a fallback.

## 4. ASP.NET Core

`AddNarrativeTrace` binds an optional `IConfiguration` section named
`NarrativeTrace`, then lets a delegate override it.

```csharp
builder.Services.AddNarrativeTrace(builder.Configuration, options =>
{
    options.Level = TracingLevel.Detail;
    options.ExcludedPaths.Add("/health");
    options.ExcludedPaths.Add("/metrics");
});
```

`appsettings.json`:

```json
{
  "NarrativeTrace": {
    "Level": "Detail",
    "ExcludedPaths": [ "/health", "/metrics" ]
  }
}
```

| Key | Type | Purpose |
|---|---|---|
| `Level` | `TracingLevel` | Capture level for the request context. |
| `ExcludedPaths` | `string[]` | Path prefixes skipped entirely (matched by segment). |

`NarrativeTraceMiddleware` adopts an inbound `traceparent` request header
automatically *(since 0.1.4, unreleased)* — no option to flip; an absent,
malformed, or forbidden-version header is ignored and the request gets a
freshly generated trace id, exactly like the config-seeded path above but
driven by the caller's header instead of a fixed value.

See the [ASP.NET Core Integration Guide](aspnetcore.md) for the middleware
and exporter wiring.

## 5. MSBuild

The `NarrativeTrace.MSBuild` package is a thin shim over the
`dotnet-narrativetrace` CLI — all threshold logic lives in the CLI. It
declares these overridable properties:

| Property | Default | Purpose |
|---|---|---|
| `NarrativeTraceOutput` | `false` | Emit `NARRATIVETRACE_OUTPUT=true` to the test host. |
| `NarrativeTraceOutputDir` | `$(MSBuildProjectDirectory)/narrativetrace` | Where results/traces are written. |
| `NarrativeTraceFormat` | `markdown` | Trace output format. |
| `NarrativeTraceLevel` | `DETAIL` | Capture level for the test host. |
| `ClarityMinScore` | `0.0` | Fail the build below this overall score. |
| `ClarityMaxHighIssues` | `2147483647` | Fail the build above this HIGH-issue count. |
| `ClarityWarnOnly` | `false` | Downgrade a gate failure to a warning. |

It also registers two targets:

- **`ClarityScan`** (depends on `Build`) — reflection-only scan of the
  built assembly into `clarity-results.json`.
- **`ClarityCheck`** (depends on `ClarityScan`) — runs the gate;
  incremental via a `.stamp` file, so a re-check is skipped when
  `clarity-results.json` has not changed.

```bash
# Enforce a threshold as part of the build:
dotnet build /t:ClarityCheck /p:ClarityMinScore=0.80 /p:ClarityMaxHighIssues=0
```

The tool must be installed (`dotnet tool install --global NarrativeTrace.Cli`,
or a local tool manifest).

## 6. Redaction

Reflective value rendering is **redact-by-default, not leak-by-default**.
When a traced object has no curated summary, `ValueRenderer` reflects over
its properties — but a name-based deny-list (`RedactionPolicy.Default`)
replaces values whose field name matches a sensitive pattern (`password`,
`secret`, `token`, `apikey`, `cvv`, `ssn`, `authorization`, `credential`,
`privatekey`, `cardNumber`, `jwt`, `cookie`, `setCookie`, `sessionId`,
`accountNumber`, `routingNumber`, `pan`, `iban`, …) with `[REDACTED]`
before rendering.

```csharp
var options = new RenderOptions(
    MaxStringLength: 200,
    MaxArrayItems: 5,
    MaxObjectKeys: 5,
    MaxDepth: 4,
    Redaction: RedactionPolicy.OfPatterns(["ssn", "pan"]));  // custom deny-list
```

- `RedactionPolicy.Default` — the built-in deny-list.
- `RedactionPolicy.OfPatterns(...)` — your own case-insensitive substring
  patterns.
- `RedactionPolicy.Disabled` — opt out (values rendered verbatim, and turns
  off value-shape masking below too).

Matching is by substring for every pattern **except `pan` and `iban`**,
which are matched on identifier-token boundaries instead — as a substring,
`pan` would also redact `companyName`, `planId` and `spanCount`, and `iban`
would catch ordinary business fields the same way. `cardPan` and
`ibanNumber` still match (the pattern is a whole token within the name);
`companyName` does not.

**Value-shape masking is a second, name-independent axis.** Regardless of
what a field is called (or whether it has a name at all — a bare return
value, a list item), a string value is redacted when it structurally looks
like a JWT (three base64url segments, the first starting `eyJ`), a
Luhn-valid payment card number (13–19 digits, `[ -]` separators tolerated),
or an HTTP `Set-Cookie` header value (a `name=value` pair followed by a
named attribute such as `Path=` or `Secure`). It is deliberately narrow —
no entropy heuristics — so an ordinary numeric identifier that happens to
fail the Luhn check stays visible. `RedactionPolicy.OfPatterns(...)` keeps
value-shape masking on (a custom name list is not an opinion about whether
these byte shapes are a credential); only `RedactionPolicy.Disabled` turns
it off.

For sensitive **parameters**, prefer the
[`[NotTraced]`](annotations.md#nottraced) attribute — it redacts by
position regardless of name.

**Wiring a custom policy into the proxy path.** The `RenderOptions` above is
what `ValueRenderer.Render` takes when you call it yourself; reaching the
*shipped* `NarrativeTraceProxy` capture path is a separate step, through
`ProxyOptions.Redaction` *(since 0.1.4, unreleased)*:

```csharp
var proxy = NarrativeTraceProxy.Create<IOrderService>(
    new OrderService(), context,
    new ProxyOptions(Redaction: RedactionPolicy.OfPatterns(["ssn", "holderName"])));
```

Given explicitly, the policy *replaces* the default name-based decision —
for both a proxy parameter's own name and a nested object's reflected
property names — rather than adding to it, so `RedactionPolicy.Disabled`
here really does turn name-based redaction off end to end (`[NotTraced]`
still redacts regardless). Leave `Redaction` unset (the default) and a
proxy behaves exactly as before.

DI auto-wrap and the ASP.NET Core integration expose the same hook
*(since 0.1.4, unreleased)*: `NarrativeTracingDiOptions.Redaction` on
[`AddNarrativeTracing`](dependency-injection.md) reaches every service that
call wraps, and `NarrativeTraceOptions.Redaction` on
[`AddNarrativeTrace`](aspnetcore.md) reaches auto-wrapped proxies too when
both are registered in the same app — a policy configured on either side is
visible to the other, since the two packages share one service collection.
`AddNarrativeTracing`'s own `Redaction`, when set, takes precedence for the
services it wraps. See [Privacy and Redaction](../privacy-and-redaction.md)
for the full, surface-by-surface picture.

**Widening every surface at once, without a code change.**
`NARRATIVETRACE_REDACTION_ADDITIONALPATTERNS` is a comma-separated list of
extra field-name patterns unioned into `RedactionPolicy.Default` itself:

```bash
NARRATIVETRACE_REDACTION_ADDITIONALPATTERNS=holderName,betalingskort
```

Unlike `ProxyOptions.Redaction`, this can only add patterns, never remove
built-in ones, and it reaches every surface in the table above — including
DI auto-wrap and the ASP.NET Core middleware, which have no per-call hook.
It is read once, into a `static readonly` field, so it must be set before
anything in the process first touches `RedactionPolicy` (an app's own
startup code, or a test setting it at runtime, is too late).

## 7. Logging bridge (`Microsoft.Extensions.Logging`)

Route trace events through your existing logging with the
`NarrativeTrace.Logging` package. `LoggingNarrativeContext` decorates any
context and logs each entry/return/exception via an `ILogger`:

```csharp
using NarrativeTrace.Logging;

var inner = new SyncNarrativeContext(new NarrativeTraceConfig());
INarrativeContext context = new LoggingNarrativeContext(inner, logger);
```

To export a completed tree once, use `TraceLogExporter`:

```csharp
TraceLogExporter.ExportToLogger(context.CaptureTrace(), logger);
```

**A note on ordering with the stock console logger.** If `logger` above is
backed by `Microsoft.Extensions.Logging.Console`, its lines write through a
background queue by default, so they can land after, or interleaved oddly
with, output your process writes synchronously (`Console.Write*`, a
different provider, a test runner capturing stdout) — a platform behavior of
the console provider, not a NarrativeTrace defect. The fix that actually
works: dispose the `ILoggerFactory` (or the console provider) — most simply
with `using var loggerFactory = LoggerFactory.Create(...)` — before anything
depends on the order; `Dispose()` blocks until the provider's background
writer thread has drained everything queued. See the [sixty-seconds
tutorial's "Send it to your logger"](../sixty-seconds.md#send-it-to-your-logger)
step for a worked, verified example.

In a hosted application, wire the **event-stream** bridge through DI instead of
constructing it yourself:

```csharp
services.AddNarrativeLogging();   // call it last, like AddNarrativeTracing
```

It registers `LoggingTraceEventListener` as a singleton built from the
registered `ILoggerFactory`, and attaches it to any registered
`IEventSubscribable` event stream so the stream arrives already narrating. Two
conditions silence it, both deliberately without an exception: no
`ILoggerFactory` is registered (the bridge would have nowhere to write), or
`NARRATIVETRACE_NARRATION=off` vetoes narration — the .NET spelling of Java's
`narrativetrace.narration=off`. `off` is the only value that vetoes, so a typo
leaves you narrating rather than silently silenced.

Well-structured code — small methods with clear names, computed values
returned rather than logged — needs few manual log calls at all;
NarrativeTrace captures the story from signatures and return values. Mix
in `ILogger` calls only for decisions that don't surface at method
boundaries, then remove them as you refactor.

**A test-suite run has a name too** *(since 0.1.4, unreleased)*: while
`NarrativeTrace.Testing.Xunit` or `NarrativeTrace.Testing.NUnit` has an
active suite run, both bridges above additionally add `nt.runName` to the
scope — the run's own three-word phrase, alongside `nt.traceId`/
`nt.traceName`, so one grep finds one run's log lines. Absent entirely
outside a tracked run. The same identity also names the console suite
footer (`run: bold elk soars`) and `manifest.json`'s top-level `run`
object, and prefixes the Markdown trace document's frontmatter (`run:`)
and the text/prose renderers' opening line — never the value-free `.nt`
structural artifact (see [Structural Trace Format](../structural-trace-format.md)).

## 8. TracingLevel vs. logging level

These are two independent filters. **TracingLevel** controls what is
*recorded* into the trace tree (and therefore the overhead of capture).
**Logging level** controls what a logging bridge *emits*. A call filtered
out by TracingLevel never reaches the tree, renderers, or any logger.

| Goal | Adjust |
|---|---|
| Reduce log noise | Raise the logger's level (tree still captured for files/renderers). |
| Reduce trace size | Lower `TracingLevel` (`Narrative` → `Summary`). |
| Reduce CPU/memory | Lower `TracingLevel` — logging level has no effect on capture cost. |

## 9. Recommended defaults by environment

| Environment | Level | Output |
|---|---|---|
| Local feature work | `Detail` | on by default, `FORMAT=Markdown` |
| CI test runs | `Narrative` or `Summary` | on by default, `FORMAT=Markdown` |
| Performance-sensitive prod | `Errors` (or `Off`) | no test fixtures run here — no file output |

## 10. Environment variable worked examples

Every `NARRATIVETRACE_*` variable the code reads, in one place — what it
sets, its default, and a runnable demonstration of the effect. The
`ConfigResolver` variables are introduced in §2 above; the rest are
introduced where they live (redaction in §6, the logging bridge in §7);
this section is the worked-example companion to all of them.

| Variable | Sets | Default |
|---|---|---|
| `NARRATIVETRACE_LEVEL` | The `TracingLevel` a context captures at. | `Detail` |
| `NARRATIVETRACE_OUTPUT` | Whether per-test trace artifacts are written to disk. | `true` |
| `NARRATIVETRACE_OUTPUT_DIR` | The directory trace artifacts are written under. | `TestResults/narrativetrace` |
| `NARRATIVETRACE_FORMAT` | The primary artifact format (`Markdown`/`Text`/`Prose`/`Json`). | `Markdown` |
| `NARRATIVETRACE_CANONICAL_JSON` | Whether to also write the per-test canonical entry array. | `false` |
| `NARRATIVETRACE_STRUCTURAL_JSON` | Whether to also write the per-test value-free entry array. | `false` |
| `NARRATIVETRACE_APPROVAL` | Whether a passing test's structure is checked against its committed `*.approved.nt` baseline. | `false` |
| `NARRATIVETRACE_APPROVED_DIR` | The directory approval baselines live in. | `narratives` |
| `NARRATIVETRACE_REDACTION_ADDITIONALPATTERNS` | Comma-separated field-name patterns unioned into `RedactionPolicy.Default`. | *(none)* |
| `NARRATIVETRACE_NARRATION` | Vetoes the logging bridge when set to `off`; anything else leaves it on. | *(unset — narrating)* |
| `NARRATIVETRACE_GLOSSARY` | An explicit glossary file path for suite harvesting, or `off` to disable harvesting outright. | Upward search from the test run for `glossary.json` |
| `NARRATIVETRACE_GLOSSARY_PATH` | A glossary file to load for live translation, overriding the file beside the deployed app. | The `glossary.json` beside the app, if any |

Each worked example below runs through the same injected-reader overload
`ConfigResolver`/`GlossarySettings`/`GlossaryLoader`/`AddNarrativeLogging`
already expose for tests (`Resolve(Func<string, string?> read, …)` and
friends) — the seam that lets the demonstration set exactly one variable
without touching the real process environment. In your own shell, set the
variable for real; the observable effect is identical either way.

### `NARRATIVETRACE_LEVEL`

```bash
export NARRATIVETRACE_LEVEL=Narrative
```

<!-- snippet: tests/NarrativeTrace.Core.Tests/DocConfigResolverExamples.cs region=level -->
```csharp
var resolved = ConfigResolver.Resolve(key => key == "NARRATIVETRACE_LEVEL" ? "Narrative" : null);
Console.WriteLine($"resolved.Level == TracingLevel.{resolved.Level}");
```
<!-- /snippet -->

Observable effect — the resolved level reflects the variable, leniently parsed:

<!-- snippet: artifacts/config-envvars/level.txt -->
```text
resolved.Level == TracingLevel.Narrative
```
<!-- /snippet -->

### `NARRATIVETRACE_OUTPUT`

```bash
export NARRATIVETRACE_OUTPUT=false
```

<!-- snippet: tests/NarrativeTrace.Core.Tests/DocConfigResolverExamples.cs region=output -->
```csharp
var resolved = ConfigResolver.Resolve(key => key == "NARRATIVETRACE_OUTPUT" ? "false" : null);
Console.WriteLine($"resolved.Output == {resolved.Output}");
```
<!-- /snippet -->

Observable effect — only an explicit `false`/`0` turns writing off (§2 above spells out the full lenience):

<!-- snippet: artifacts/config-envvars/output.txt -->
```text
resolved.Output == False
```
<!-- /snippet -->

### `NARRATIVETRACE_OUTPUT_DIR`

```bash
export NARRATIVETRACE_OUTPUT_DIR=artifacts/traces
```

<!-- snippet: tests/NarrativeTrace.Core.Tests/DocConfigResolverExamples.cs region=output-dir -->
```csharp
var resolved = ConfigResolver.Resolve(key => key == "NARRATIVETRACE_OUTPUT_DIR" ? "artifacts/traces" : null);
Console.WriteLine($"resolved.OutputDir == \"{resolved.OutputDir}\"");
```
<!-- /snippet -->

Observable effect — the configured path passes through verbatim (trimmed; blank is treated as unset):

<!-- snippet: artifacts/config-envvars/output-dir.txt -->
```text
resolved.OutputDir == "artifacts/traces"
```
<!-- /snippet -->

### `NARRATIVETRACE_FORMAT`

```bash
export NARRATIVETRACE_FORMAT=Json
```

<!-- snippet: tests/NarrativeTrace.Core.Tests/DocConfigResolverExamples.cs region=format -->
```csharp
var resolved = ConfigResolver.Resolve(key => key == "NARRATIVETRACE_FORMAT" ? "Json" : null);
Console.WriteLine($"resolved.Format == OutputFormat.{resolved.Format}");
```
<!-- /snippet -->

Observable effect — the primary artifact format switches from the `Markdown` default:

<!-- snippet: artifacts/config-envvars/format.txt -->
```text
resolved.Format == OutputFormat.Json
```
<!-- /snippet -->

### `NARRATIVETRACE_CANONICAL_JSON`

```bash
export NARRATIVETRACE_CANONICAL_JSON=true
```

<!-- snippet: tests/NarrativeTrace.Core.Tests/DocConfigResolverExamples.cs region=canonical-json -->
```csharp
var resolved = ConfigResolver.Resolve(key => key == "NARRATIVETRACE_CANONICAL_JSON" ? "true" : null);
Console.WriteLine($"resolved.CanonicalJson == {resolved.CanonicalJson}");
```
<!-- /snippet -->

Observable effect — the per-test `<test>.canonical.json` machine artifact starts being written alongside the primary format:

<!-- snippet: artifacts/config-envvars/canonical-json.txt -->
```text
resolved.CanonicalJson == True
```
<!-- /snippet -->

### `NARRATIVETRACE_STRUCTURAL_JSON`

```bash
export NARRATIVETRACE_STRUCTURAL_JSON=true
```

<!-- snippet: tests/NarrativeTrace.Core.Tests/DocConfigResolverExamples.cs region=structural-json -->
```csharp
var resolved = ConfigResolver.Resolve(key => key == "NARRATIVETRACE_STRUCTURAL_JSON" ? "true" : null);
Console.WriteLine($"resolved.StructuralJson == {resolved.StructuralJson}");
```
<!-- /snippet -->

Observable effect — the per-test `<test>.structural.json` value-free array starts being written:

<!-- snippet: artifacts/config-envvars/structural-json.txt -->
```text
resolved.StructuralJson == True
```
<!-- /snippet -->

### `NARRATIVETRACE_APPROVAL`

```bash
export NARRATIVETRACE_APPROVAL=true
```

<!-- snippet: tests/NarrativeTrace.Core.Tests/DocConfigResolverExamples.cs region=approval -->
```csharp
var resolved = ConfigResolver.Resolve(key => key == "NARRATIVETRACE_APPROVAL" ? "true" : null);
Console.WriteLine($"resolved.Approval == {resolved.Approval}");
```
<!-- /snippet -->

Observable effect — a passing test's structure now gets checked against its committed baseline (see [Structural Trace Format](../structural-trace-format.md)):

<!-- snippet: artifacts/config-envvars/approval.txt -->
```text
resolved.Approval == True
```
<!-- /snippet -->

### `NARRATIVETRACE_APPROVED_DIR`

```bash
export NARRATIVETRACE_APPROVED_DIR=baselines
```

<!-- snippet: tests/NarrativeTrace.Core.Tests/DocConfigResolverExamples.cs region=approved-dir -->
```csharp
var resolved = ConfigResolver.Resolve(key => key == "NARRATIVETRACE_APPROVED_DIR" ? "baselines" : null);
Console.WriteLine($"resolved.ApprovedDir == \"{resolved.ApprovedDir}\"");
```
<!-- /snippet -->

Observable effect — approval baselines are now read from and written to `baselines/` instead of the `narratives` default:

<!-- snippet: artifacts/config-envvars/approved-dir.txt -->
```text
resolved.ApprovedDir == "baselines"
```
<!-- /snippet -->

### `NARRATIVETRACE_REDACTION_ADDITIONALPATTERNS`

Unlike the variables above, there is no code to change at the call site —
`RedactionPolicy.Default` reads this variable itself, once, into a `static
readonly` field (§6 above). That means the demonstration has to run in its
**own process**, started with the variable already set, rather than through
the injected-reader seam the other examples use — so this one is verified
by hand rather than snippet-checked against a committed source file, per
the same rule 8 that backs every other example on this page (a real,
compiled run, not a typed guess):

```bash
export NARRATIVETRACE_REDACTION_ADDITIONALPATTERNS=holderName,betalingskort
```

```csharp
using NarrativeTrace.Core;

Console.WriteLine(RedactionPolicy.Default.ShouldRedact("holderName"));
Console.WriteLine(RedactionPolicy.Default.ShouldRedact("password"));
```

Observable effect — run once with the variable unset and once with it set
(`dotnet run` twice, in two separate process invocations):

```text
# unset
False
True

# NARRATIVETRACE_REDACTION_ADDITIONALPATTERNS=holderName,betalingskort
True
True
```

`password` redacts either way (it is already in the built-in deny-list);
`holderName` only redacts once the variable widens it. Setting the
variable *after* anything in the process has already touched
`RedactionPolicy` has no effect — see §6 above.

### `NARRATIVETRACE_NARRATION`

```bash
export NARRATIVETRACE_NARRATION=off
```

<!-- snippet: tests/NarrativeTrace.Logging.Tests/DocNarrationExample.cs region=narration -->
```csharp
var services = new ServiceCollection();
services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);

services.AddNarrativeLogging(null, key => key == "NARRATIVETRACE_NARRATION" ? "off" : null);

var listener = services.BuildServiceProvider().GetService<LoggingTraceEventListener>();
Console.WriteLine($"listener is null == {listener is null}");
```
<!-- /snippet -->

Observable effect — `off` (case-insensitive) vetoes registration entirely, even with an `ILoggerFactory` present; anything else, including a typo, leaves narration on:

<!-- snippet: artifacts/config-envvars/narration.txt -->
```text
listener is null == True
```
<!-- /snippet -->

### `NARRATIVETRACE_GLOSSARY`

```bash
export NARRATIVETRACE_GLOSSARY=off
```

<!-- snippet: tests/NarrativeTrace.Glossary.Tests/DocGlossaryExamples.cs region=glossary -->
```csharp
var resolved = GlossarySettings.ResolveFile(
    key => key == "NARRATIVETRACE_GLOSSARY" ? "off" : null, root);
Console.WriteLine($"resolved == {(resolved is null ? "null (harvesting disabled)" : resolved)}");
```
<!-- /snippet -->

(`root` above is whatever directory the upward search would otherwise start
from — a test run's working directory in practice.) Observable effect —
the literal value `off` disables harvesting outright, overriding the
upward `glossary.json` search even when a file would otherwise be found:

<!-- snippet: artifacts/config-envvars/glossary.txt -->
```text
resolved == null (harvesting disabled)
```
<!-- /snippet -->

### `NARRATIVETRACE_GLOSSARY_PATH`

```bash
export NARRATIVETRACE_GLOSSARY_PATH=/srv/app/committed-glossary.json
```

<!-- snippet: tests/NarrativeTrace.Glossary.Tests/DocGlossaryExamples.cs region=glossary-path -->
```csharp
var loaded = GlossaryLoader.Load(
    key => key == "NARRATIVETRACE_GLOSSARY_PATH" ? overridePath : null, root);
Console.WriteLine($"loaded from override == {loaded is not null}");
```
<!-- /snippet -->

(`overridePath` above is the file the variable names; `root` is the
deployed application's base directory.) Observable effect — the override
wins over any `glossary.json` sitting beside the deployed application:

<!-- snippet: artifacts/config-envvars/glossary-path.txt -->
```text
loaded from override == True
```
<!-- /snippet -->

## See also

- [Installation Guide](installation.md) — packages and integration paths
- [Annotations Guide](annotations.md) — redaction and narration attributes
- [ASP.NET Core Integration Guide](aspnetcore.md) — middleware and exporters
- [Clarity Guide](clarity.md) — the `Clarity*` MSBuild/CLI gate
