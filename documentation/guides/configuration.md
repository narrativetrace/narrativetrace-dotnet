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

Level and format parsing is lenient (case- and punctuation-insensitive:
`detail`, `DETAIL`, and `Detail` all resolve).

`NARRATIVETRACE_OUTPUT` is **on by default** (owner ruling, 2026-09-11): the
per-test artifacts the xUnit fixture and NUnit base write are the payoff of
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

## See also

- [Installation Guide](installation.md) — packages and integration paths
- [Annotations Guide](annotations.md) — redaction and narration attributes
- [ASP.NET Core Integration Guide](aspnetcore.md) — middleware and exporters
- [Clarity Guide](clarity.md) — the `Clarity*` MSBuild/CLI gate
