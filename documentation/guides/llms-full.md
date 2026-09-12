# NarrativeTrace .NET — Complete Reference

A single-file API reference for LLM and human consumers. Everything below
reflects the current source; where behavior differs from the JVM edition
it is called out explicitly.

## Overview and philosophy

**Code is the log.** Well-named methods, parameters, and return values
already describe what a program does at runtime. NarrativeTrace captures
that description automatically — by intercepting interface calls through
`System.Reflection.DispatchProxy` — and renders it as a readable,
nested narrative. You write clean, expressive code instead of manual
logging, and reach for attributes only in exceptional cases (targeted
narration, error context, redaction, custom value rendering).

A second module scores **naming clarity**, turning "is this code
readable?" into a number you can enforce in CI.

## Quick start

```csharp
using NarrativeTrace.Core;             // TracingLevel, renderers
using NarrativeTrace.Core.Annotation;  // [Narrated], [OnError], [NotTraced], [NarrativeSummary]
using NarrativeTrace.Proxy;            // NarrativeTraceProxy, [Traced]
using NarrativeTrace.Runtime;          // SyncNarrativeContext

var context = new SyncNarrativeContext(
    new NarrativeTraceConfig(TracingLevel.Detail));

var orders = NarrativeTraceProxy.Create<IOrderService>(
    new DefaultOrderService(), context);

orders.PlaceOrder("C-1234", "SKU-KB", 2);

Console.WriteLine(IndentedTextRenderer.Render(context.CaptureTrace()));
context.Reset();
```

Share **one** context across collaborating services so their calls nest
into a single tree.

## Target frameworks and packaging

- Libraries multi-target `net10.0` and `netstandard2.0`; the `Legacy`
  package additionally targets `net48`.
- **No compiler flag required** — unlike the JVM's `-parameters`, .NET
  retains method parameter names in metadata by default.
- NuGet package id = project directory name. Keep all `NarrativeTrace.*`
  packages on the same version.

### Package map (dependency direction)

`Core` is the zero-dependency foundation; nothing in `Core` depends on
`Runtime` or any integration package (enforced by architecture tests).

| Package | Depends on | Purpose |
|---|---|---|
| `NarrativeTrace.Core` | — | Trace model, `INarrativeContext`, config, redaction, value + text/Markdown/prose renderers, and the `NarrativeTrace.Core.Annotation` attributes `[Narrated]`/`[OnError]`/`[NotTraced]`/`[NarrativeSummary]`. |
| `NarrativeTrace.Runtime` | Core | Capture engine (`SyncNarrativeContext`, `AsyncNarrativeContext`), concurrency groups, event pipeline, JSON/chapter exporters. |
| `NarrativeTrace.Proxy` | Core | `NarrativeTraceProxy`, `NarrativeInterceptor`, `[Traced]`; reads the Core annotations. |
| `NarrativeTrace.DependencyInjection` | Core, Proxy, Runtime | `AddNarrativeTracing` namespace auto-wrap. |
| `NarrativeTrace.AspNetCore` | Core, Runtime, Proxy | Per-request middleware, `ITraceExporter`, request/user context. |
| `NarrativeTrace.Logging` | Core | `Microsoft.Extensions.Logging` bridge. |
| `NarrativeTrace.Observability` | Core | OpenTelemetry (batch export + live listener). |
| `NarrativeTrace.Diagrams` | Core | Mermaid / PlantUML sequence renderers. |
| `NarrativeTrace.Clarity` | Core | Naming-clarity analysis and reporting. |
| `NarrativeTrace.Cli` | Core, Clarity | `dotnet-narrativetrace` global tool. |
| `NarrativeTrace.MSBuild` | — (build-only) | Wires the CLI into `dotnet build`/`test`. |
| `NarrativeTrace.Testing.Xunit` / `.NUnit` | Core, Runtime, Proxy | Per-test context + failure narrative. |

## Data model (`NarrativeTrace.Core`)

```csharp
sealed record TraceTree(IReadOnlyList<TraceNode> Roots) { bool IsEmpty; }

sealed record TraceNode(
    MethodSignature Signature,
    TraceOutcome Outcome,
    IReadOnlyList<TraceNode> Children,
    long DurationTicks,
    long StartTimestamp = 0,
    ConcurrencyInfo? Concurrency = null,
    SpanContext? SpanContext = null);

sealed record MethodSignature(
    string ClassName,
    string MethodName,
    IReadOnlyList<ParameterCapture> Parameters,
    string? Narration = null,      // resolved [Narrated] text
    string? ErrorContext = null);  // resolved [OnError] text

sealed record ParameterCapture(string Name, string RenderedValue, bool Redacted);

abstract record TraceOutcome;
sealed record Returned(string? RenderedValue) : TraceOutcome;
sealed record Threw(Exception? Error)          : TraceOutcome;
sealed record Incomplete()                      : TraceOutcome;  // in-flight / deferred
```

`SpanContext` carries correlation identity (trace id + human-readable
trace name, span/parent ids, service identity, request/user tiers, schema
version). `ConcurrencyInfo` marks fork-join and fire-and-forget groups.

## Context API

```csharp
interface INarrativeContext
{
    SpanId EnterMethod(string className, string methodName,
        IReadOnlyList<ParameterCapture> parameters, MethodOptions? options = null);
    void ExitMethodWithReturn(string? renderedValue, SpanId? handle = null);
    void ExitMethodWithException(Exception? exception, SpanId? handle = null);
    void DetachFrame(SpanId handle);
    TraceTree CaptureTrace();
    void Reset();

    bool IsActive { get; }                 // false only at TracingLevel.Off
    bool CapturesParameterValues { get; }  // true only at Detail
    TraceId? CurrentTraceId { get; }
    string? StoryId { get; }
    string? ChapterId { get; }

    SpanId? ParentOf(SpanId handle);
    T RunScoped<T>(SpanId handle, Func<T> fn);
    void GraftChild(TraceNode node);

    void SetRequestContext(string? httpMethod, HttpRoute? httpRoute, ClientIp? clientIp);
    void SetUserContext(EnduserId? enduserId, SessionId? sessionId, TenantId? tenantId);
}
```

Implementations:

- **`SyncNarrativeContext(NarrativeTraceConfig)`** — the workhorse; a
  single-threaded logical stack. Use for synchronous flows and per-test
  capture.
- **`AsyncNarrativeContext(NarrativeTraceConfig)`** — carries context
  across `await` via `AsyncLocal`. Wrap work in `Run`/`RunAsync`
  (`await ctx.RunAsync(async () => …)`); calling the interface members
  outside a `Run` scope throws. `CaptureTrace()` returns the active
  scope's trace, else the one **this flow** last finished, else the last
  scope that was alone for its whole lifetime — after concurrent scopes it
  returns an empty tree rather than a concurrent scope's trace.
- **`NoopContext.Instance`** — a shared do-nothing context; `IsActive`
  is `false`. Returned by `HttpContext.GetNarrativeContext()` on untraced
  requests, so proxy wrapping is always safe.

You rarely call `EnterMethod`/`ExitMethod*` directly — the proxy does.
`CaptureTrace()` and `Reset()` are the everyday methods.

### Cross-thread propagation is bidirectional (ADR-013, NET-007)

`context.Snapshot()` carries the trace id, the span that is open right now
and the request/user metadata into a worker — **and the work traced there
comes back**:

```csharp
var snapshot = context.Snapshot();          // on the flow that owns the trace
await Task.Run(() =>
{
    using var scope = snapshot.Activate();  // fresh capture, flow-scoped
    notifier.Send(orderId);                 // appears in the caller's trace
});
```

The rule is **published, therefore reportable**, not *joined, therefore
reportable*: the caller's `CaptureTrace()` shows the worker's calls from
the moment they are published, not from the moment the scope closes — a
framework routinely resumes the caller in between. It holds
**transitively**, so a worker that itself hands off to a grandchild
reaches the origin whole.

- **Placement follows submit time.** Submitted while a span is open → a
  child of that span, even if the worker runs after it returned;
  submitted with no span open → the next root of the same trace. A bare
  `Task.Run` with no snapshot carries no submit-time lineage, so it is
  placed where it *runs*; take a snapshot at the submission point when
  placement matters.
- **`snapshot.ActivateWithoutAdoption()`** registers nothing and hands
  over nothing (grandchildren included) — for helpers that publish their
  own children, which is why fork-join and fire-and-forget work is never
  counted twice.
- **Bounded and honest.** A capture adopts at most 10,000 spans; a batch
  that does not fit is refused *whole* and counted in `TraceLoss`
  (`DroppedEvents`, `RefusedScopes`, `RefusedSpans`), read through
  `ITraceLossSource` and printed as one suite-footer line that a clean run
  omits entirely.
- The origin capture is held **weakly**: a pending snapshot cannot keep a
  finished request alive, and one whose origin was `Reset()` simply hands
  its spans nowhere.

## Concurrency (`NarrativeTrace.Runtime`)

Parallel work is a first-class citizen in the trace tree. Because .NET context
flows through `AsyncLocal`, each parallel branch runs against an **isolated
child context** whose trace is merged back into the parent with thread metadata
and a shared `groupId`.

`ConcurrencyInfo` (on every merged/launched node):

```csharp
sealed record ConcurrencyInfo(
    string GroupId, string TaskLabel, int ThreadId,
    string? ThreadName, bool IsThreadPoolThread, ConcurrencyKind Kind);
enum ConcurrencyKind { ForkJoin, FireAndForget, Async }
```

**Fork-join** — `ForkJoinGroup` runs branches in parallel, joins, and grafts each
branch's roots under the parent tagged `ConcurrencyKind.ForkJoin` with the group's
`groupId`:

```csharp
var fork = ForkJoinGroup.Create(context);
_ = fork.Fork(iso => NarrativeTraceProxy.Create<IPricing>(pricing, iso).Quote(order));
_ = fork.Fork(iso => { NarrativeTraceProxy.Create<IInventory>(inv, iso).Check(order); return true; });
await fork.JoinAsync();                    // grafts branch traces, emits join wall-time
// or the two-branch helper:
var (a, b) = await ForkJoinGroup.WhenAll(context, c => f1(c), c => f2(c));
```

**Fire-and-forget** — `FireAndForgetGroup.Create` grafts a synthetic launcher node
(method `fire-and-forget`, `ConcurrencyKind.FireAndForget`) into the parent
immediately; `Launch` runs background work on an isolated context and links its
child roots by `groupId`. The parent does not wait:

```csharp
var group = FireAndForgetGroup.Create(context, "OrderService");
group.Launch(iso => NarrativeTraceProxy.Create<INotification>(notifier, iso).Send(orderId));
IReadOnlyList<TraceTree> children = group.ChildRoots();   // completed background traces
```

**Rendering / export** — `MarkdownRenderer` emits `⑂ fork [n tasks]` / `⑃ join`
markers, a `⤳ fire-and-forget` launcher, thread names, and join wall-time;
sequential-await anti-patterns render an optimization hint. `JsonExporter` writes
a `concurrency` object (`groupId`, `kind` = `fork-join`/`fire-and-forget`/`async`,
thread fields) on each concurrent node. For ambient propagation across an
`await`, use `AsyncNarrativeContext.RunAsync`. Worked example:
`examples/NarrativeTrace.Examples.ECommerce/ConcurrencyScenario.cs`.

**Async** — work adopted across a snapshot boundary is tagged
`ConcurrencyKind.Async` on the **first** span the worker's flow opens (everything
deeper is ordinary sequential work), grouped by the launching span. The
structural `.nt` artifact renders such a group under `~ async [n]` with members
sorted by signature, and partitions roots the same way as children — work that
outlived its caller is a root, and its capture order is the scheduler's choice
rather than the code's, so a committed baseline must not pin it.

## Proxy (`NarrativeTrace.Proxy`) and attributes (`NarrativeTrace.Core.Annotation`)

```csharp
T Create<T>(T target, INarrativeContext context, ProxyOptions? options = null) where T : class;
object Create(Type interfaceType, object target, INarrativeContext context, ProxyOptions? options = null);

sealed record ProxyOptions(string? ClassName = null, bool IncludeReturnValues = true);
```

`Create<T>` requires `T` to be an interface (`DispatchProxy` limitation).
The interceptor caches per-method metadata (names, redaction set,
narration/error templates), captures parameters (respecting the tracing
level and redaction), records return values or exceptions, and unwraps
`TargetInvocationException` so the original exception propagates.

Attributes. All but `[Traced]` live in `NarrativeTrace.Core.Annotation` — pure
metadata in Core, read here by the interceptor; `[Traced]` is
`DispatchProxy`-specific and stays in `NarrativeTrace.Proxy`:

| Attribute | Target | Effect | Surfaces in |
|---|---|---|---|
| `[Narrated("… {param} …")]` | method | Resolved narration text on the node. | Markdown output; `Signature.Narration`. |
| `[OnError("…", ExceptionType = typeof(X))]` | method (repeatable) | Error text resolved **on entry**; most-specific declared type wins. | Markdown + Prose (on a throwing node); `Signature.ErrorContext`. |
| `[NotTraced]` | parameter | Value redacted (`[REDACTED]`). | All renderers/exporters. |
| `[Traced("n0","n1")]` | method | Positional parameter-name override. | All renderers/exporters. |
| `[NarrativeSummary]` | method/property | Preferred value rendering for the declaring type (public, parameterless, inherited). | Everywhere values render. |

Template placeholders: `{param}` and one level of `{param.Property}`
(PascalCase). Unknown placeholders survive literally as a typo signal. A
path reaching a redacted property resolves to `[REDACTED]`, through the
same `RedactionPolicy` decision reflective rendering uses — naming a path
never weakens the rules that apply to the value directly. See the
[Annotations Guide](annotations.md) for the full behavior, including the
`[OnError]` entry-time-resolution note.

## Rendering and export

| API | Package | Output |
|---|---|---|
| `IndentedTextRenderer.Render(tree)` | Core | Plain nested text. |
| `MarkdownRenderer.Render(tree, MarkdownOptions?)` | Core | Markdown call flow (renders `[Narrated]` narration, slow-call marks). |
| `MarkdownRenderer.RenderDocument(tree, TraceMetadata, MarkdownOptions?)` | Core | The standalone document: YAML frontmatter + `## Trace:` header + call flow. What per-test `.md` artifacts contain. |
| `ProseRenderer.Render(tree)` | Core | Flowing prose. |
| `MermaidSequenceRenderer.Render(tree)` | Diagrams | Mermaid sequence diagram. |
| `PlantUmlSequenceRenderer.Render(tree)` | Diagrams | PlantUML sequence diagram. |
| `JsonExporter.Export(tree, TraceMetadata)` | Runtime | JSON (schema `version` `1.0`). |
| `ChapterExporter.ExportChapter(tree, TraceMetadata)` | Runtime | Canonical "chapter" JSON with `nt.*` fields. |
| `TraceLogExporter.ExportToLogger(tree, ILogger)` | Logging | Structured log events. |
| `TraceActivityExporter.Export(tree)` | Observability | OpenTelemetry spans (batch). |

`MarkdownOptions(string? ScenarioName = null, int SlowThresholdMs = 200, bool IncludeFrontmatter = true)`.
There is no document-header switch: the header needs a scenario outcome, so it
lives on `RenderDocument`, which takes a `TraceMetadata`.

`TraceMetadata(string Scenario, ScenarioResult Result, string? TestClass = null, string? TestMethod = null, string? Framework = null, string? Timestamp = null)`.
`Result` is **required** and is written verbatim — exporters never re-derive it
from the trace, because "did the traced code throw?" is a different question
from "did the scenario pass?". Where no framework verdict exists (an HTTP
request trace), derive one at the call site with
`ScenarioResultExtensions.Of(TraceNode.HasAnyError(tree.Roots))`.

`ScenarioResult` is the outcome enum with two spellings: `WireName()` gives the
schema-legal `success`/`error` for artifacts, `DisplayName()` gives
`PASSED`/`FAILED` for prose. Never write the member name into an artifact.
`From(string)` parses either spelling and **throws** on anything else — unlike
the lenient `FromName` config parsers, an out-of-contract result must not reach
an artifact.

Value rendering (`ValueRenderer`) is redact-by-default: reflective
property rendering hides values whose field name matches
`RedactionPolicy` (see below). `ValueRenderer.RenderStructured` produces a
typed `RenderedValue` tree (`StringVal`, `LongVal`, `DoubleVal`,
`BooleanVal`, `InstantVal`, `ObjectVal`, `ListVal`, `NullVal`).

## Configuration

`NarrativeTraceConfig(TracingLevel level = Detail, ServiceIdentity? identity = null)`;
`Level` is mutable at runtime.

`TracingLevel`: `Off` < `Errors` < `Summary` < `Narrative` < `Detail`.
`IsActive()` is true at `Errors` and above (false only at `Off`); parameter
values are captured only at `Detail`.

`ConfigResolver.Resolve()` reads env vars into
`ResolvedConfig(Level, Output, OutputDir, Format, CanonicalJson, StructuralJson)`:

- `NARRATIVETRACE_LEVEL` (default `Detail`)
- `NARRATIVETRACE_OUTPUT` (`true`/`false`/`1`/`0`, default `true` — on by
  default since 2026-09-11; only an explicit `false`/`0` opts out)
- `NARRATIVETRACE_OUTPUT_DIR` (default `TestResults/narrativetrace`, the
  ephemeral, already-gitignored `.NET` test-output convention)
- `NARRATIVETRACE_FORMAT` (`Markdown`/`Text`/`Prose`/`Json`, default `Markdown`)
- `NARRATIVETRACE_CANONICAL_JSON` (default `false`) — also write
  `<test>.canonical.json`, the trace flattened into `entry.schema.json` entries
  (`TraceTreeCanonicalMapper`: one `method_enter` + one `method_exit` per call,
  depth-first, span-linked). Deterministic: identity is inherited across the
  tree, or synthesized from fixed constants when the tree has no `SpanContext`.
- `NARRATIVETRACE_STRUCTURAL_JSON` (default `false`) — also write
  `<test>.structural.json`, the same array through `StructuralProjection`
  (ADR-002 Level 1): parameter values become `[ELIDED]`, return values and
  exception messages are removed, names and call shape survive.
- `NARRATIVETRACE_NARRATION` — `off` (case-insensitive) vetoes narration;
  every other value, unset included, leaves it on. Read by
  `AddNarrativeLogging()` in `NarrativeTrace.Logging`, which otherwise
  registers `LoggingTraceEventListener` from the host's `ILoggerFactory` and
  attaches it to a registered `IEventSubscribable` stream. No `ILoggerFactory`
  registered means no registration — that is the .NET activation signal, since
  a type probe cannot manufacture an `ILogger` the way SLF4J's static factory
  can.

Invalid values degrade to defaults; level/format parsing is lenient.

`RenderOptions(int MaxStringLength = 200, int MaxArrayItems = 5, int MaxObjectKeys = 5, int MaxDepth = 4, RedactionPolicy? Redaction = null)`.
`RedactionPolicy.Default` (built-in sensitive-name deny-list, marker
`[REDACTED]`), `.OfPatterns(...)`, `.Disabled`. Name matching is substring
except `pan`/`iban`, which match on identifier-token boundaries so they
don't also redact `companyName`/`planId`. A second, name-independent axis
masks string values that structurally look like a JWT, a Luhn-valid card
number, or a `Set-Cookie` header value, regardless of field name; only
`.Disabled` turns it off.

See the [Configuration Guide](configuration.md).

## Dependency injection

```csharp
services.AddNarrativeTracing(options =>
{
    options.Level = TracingLevel.Detail;
    options.Namespaces("MyApp.Orders", "MyApp.Payments");
});
```

Wraps interface-registered services whose implementation namespace matches
a configured prefix (dot-boundary matching). Preserves the original
lifetime; registers a scoped `INarrativeContext`. Open generics are
skipped. This is independent of the ASP.NET Core middleware context.

## ASP.NET Core

```csharp
services.AddNarrativeTrace(configuration, options => { options.ExcludedPaths.Add("/health"); });
app.UseMiddleware<NarrativeTraceMiddleware>();
```

`NarrativeTraceMiddleware` (an `IMiddleware`) creates a per-request
`SyncNarrativeContext` in `HttpContext.Items`, stamps request (method,
route, client IP) and user tiers, runs the pipeline, then captures and
exports (even on exception, with the response status). Trace services via
`HttpContext.GetNarrativeContext()`. Default exporter: `LoggerTraceExporter`
(category `NarrativeTrace.Export`); override with a registered
`ITraceExporter`. `ITraceExporter.Export(TraceTree, RequestContext(int StatusCode, long DurationMs))`.
Implement `IRequestContextProvider.ResolveUserContext(HttpContext)` for user
identity; it returns `UserContext(string? EnduserId, string? SessionId,
string? TenantId)` or null for anonymous traffic, and the middleware stamps
the returned values onto the span and the request log scope. See the [ASP.NET Core Guide](aspnetcore.md).

## OpenTelemetry (`NarrativeTrace.Observability`)

The bridge uses idiomatic `System.Diagnostics.ActivitySource`/`Activity`.
Register the source name `"NarrativeTrace"` with your `TracerProvider`
(`.AddSource("NarrativeTrace")`).

- **Batch** (post-hoc): `TraceActivityExporter.Export(context.CaptureTrace())`
  emits one activity per node with `narrative.*`/`nt.*` tags.
- **Live** (real-time): `new OtelTraceEventListener(activitySource)` — an
  `Action<TraceEvent>` you subscribe to the event pipeline
  (`consumer.Subscribe(listener.OnEvent)`). It creates spans from
  enter/exit event pairs with explicit timestamps and parent linking; it
  snapshots/restores `Activity.Current` per event to keep the drain thread
  from leaking ambient parents. Optional capacity/TTL:
  `new OtelTraceEventListener(source, maxActiveSpans, ttl)`.

## Clarity (`NarrativeTrace.Clarity`)

- `ClarityScanner.Scan(IEnumerable<Type>, DomainVocabulary?) → IReadOnlyDictionary<string, ClarityResult>`
  (reflection-only, keyed by `Type.Name`).
- `ClarityAnalyzer.Analyze(TraceTree)` and
  `.Analyze(TraceTree, ISet<string> propertyNames, DomainVocabulary?) → ClarityResult`.
- `ClarityResult(Overall, Method, Class, Parameter, Structural, Cohesion, IReadOnlyList<ClarityIssue> Issues)`.
  Component weights: Method 30%, Parameter 25%, Class 20%, Structural 15%,
  Cohesion 10%.
- `ClarityIssue(string Severity, string Description, string Suggestion)`.
  The analyzer currently flags generic-verb method names as `high`.
- Reporting: `ClarityReportRenderer.Render(IReadOnlyList<ScenarioResult>)`,
  `ClarityJsonExporter.Export`/`.ExportReport`.

- `DomainVocabulary` — the project's own words, read from the committed
  `glossary.json` (ADR-012) by `GlossaryVocabulary.Of(Glossary)` /
  `.FromFile(path)` / `.From(directory)`. A `verb-phrase` term contributes its
  leading verb as a domain verb and the rest as domain nouns; `word` and
  `noun-phrase` terms contribute every token as a domain noun. Accepted
  shorthand is a separate declaration — the root-level `abbreviations` map of
  schema 2 (`{"fx": "foreign exchange"}`), reached through
  `IsAcceptedAbbreviation(token)` / `ExpansionOf(token)`: for those tokens
  `AbbreviationDictionary.Classify` returns null and the teaching note spells
  the token out (`noun 'fx' (foreign exchange)`). A token that merely appears
  inside a committed term is a domain noun and nothing more. The section is
  human-owned (a harvest never writes it, a merge carries it through), the `2`
  stamp is emitted only when it has entries, and readers accept it at any
  version from 1 up. Built-in tiers keep authority — generic verbs, boolean prefixes
  and meaningless placeholders are never promoted, and deprecated synonyms,
  `template` entries and `stale` terms are never vocabulary. Only the
  *committed* file counts. The xUnit fixture, the NUnit suite report and
  `clarity-scan` all find it via `GlossarySettings.ResolveFile`; reading is
  unconditional wherever a glossary exists, and an unreadable one degrades to
  the built-in dictionaries with a console note.

See the [Clarity Guide](clarity.md) for the JSON contract and CI gate.

## CLI and MSBuild

`dotnet-narrativetrace` (from `NarrativeTrace.Cli`):

- `clarity-scan --assembly <path> [--output-dir <dir>] [--format both|md|json]` →
  `clarity-scan-results.json` / `clarity-scan-report.md`. The `clarity-scan-`
  prefix keeps a static scan from overwriting the test-run artifacts
  (`clarity-results.json` / `clarity-report.md`) that `clarity-check` gates on.
- `clarity-check --results <path> [--min-score <x>] [--max-high-issues <n>] [--warn-only]`.
  Exit `0` pass/warn, `1` gate failure, `2` usage/malformed. Missing
  results file is skipped, not failed. HIGH issues are counted
  case-insensitively.

`NarrativeTrace.MSBuild` adds `ClarityScan`/`ClarityCheck` targets and the
`ClarityMinScore`/`ClarityMaxHighIssues`/`ClarityWarnOnly` and
`NarrativeTrace*` properties, forwarding `NARRATIVETRACE_*` to the test
host.

## Testing

- **xUnit** (`NarrativeTrace.TestingXunit`): `using var narrative = new NarrativeFixture();`
  then `narrative.Run("Scenario", ctx => { … });` — prints the narrative on
  failure, then rethrows. `TraceOutputWriter.Write(tree, testName, outputDir, formats…)`
  with `TraceFormat.{Markdown,Mermaid,PlantUml,Json,ClarityJson}`.
- **NUnit** (`NarrativeTrace.TestingNUnit`): derive from `NarrativeTestBase`;
  `Context` is fresh per test; the narrative prints on failure. Override
  `OnTraceComplete(TraceTree)` to write files.

Note the namespaces omit the second dot: `NarrativeTrace.TestingXunit`,
`NarrativeTrace.TestingNUnit`.

## Namespace responsibilities

Each public namespace owns a slice of the pipeline (the .NET stand-in for Java's
`package-info.java` briefings):

- **`NarrativeTrace.Core`** — the trace *model* and contracts: `TraceNode`,
  `TraceOutcome` (`Returned`/`Threw`/`Incomplete`), `MethodSignature`,
  `ParameterCapture`, `ConcurrencyInfo`; the `INarrativeContext` abstraction;
  config (`NarrativeTraceConfig`, `TracingLevel`); and the string renderers
  (`IndentedTextRenderer`, `MarkdownRenderer`, `ProseRenderer`). Zero
  dependencies. Owns *what a trace is*, not *how it is captured*.
- **`NarrativeTrace.Core.Annotation`** — the four narrative attributes that
  shape what is captured and how it reads: `[Narrated]` and `[OnError]` attach
  human-written templates to methods, `[NotTraced]` marks a parameter, property
  or field as redacted, and `[NarrativeSummary]` lets a type contribute its own
  one-line summary. Mirrors Java's `ai.narrativetrace.core.annotation`. Owns
  *declaration only* — nothing here reads an attribute; the proxy and
  `ValueRenderer` do. Depends on nothing outside Core.
- **`NarrativeTrace.Runtime`** — the capture *engine*: `SyncNarrativeContext` /
  `AsyncNarrativeContext`, the event pipeline, `JsonExporter`, and the
  concurrency groups. Owns *how a trace is built and stitched*.
- **`NarrativeTrace.Proxy`** — the `DispatchProxy` interceptor and the
  proxy-specific `[Traced]` name override. Owns *value capture and template
  resolution* at the interface boundary; does not own rendering, and no longer
  owns the narrative attributes it reads.
- **`NarrativeTrace.Clarity`** — naming analysis: `ClarityAnalyzer`,
  `ClarityScanner` (reflection-only), scorers, dictionaries, and
  `ClarityReportRenderer`. Owns *code-quality scoring*, independent of capture.
- **`NarrativeTrace.DependencyInjection` / `.AspNetCore`** — wiring: namespace
  auto-wrap and the request-lifecycle middleware + `ITraceExporter` SPI. Own
  *how tracing attaches to an app*, not the trace model.
- **`NarrativeTrace.Observability` / `.Logging` / `.Diagrams`** — output bridges
  to OpenTelemetry `Activity` spans, `ILogger`, and Mermaid/PlantUML. Each owns
  one *export target*.

### Concurrency coordination narrative

The design narrative Java keeps in its `context` package-info: a fork or
fire-and-forget spawns an **isolated child context** (a fresh logical stack) so
concurrent branches never interleave on one stack. Each branch's trace is
captured independently, then **grafted** back onto the parent — fork-join on
`JoinAsync` (synchronously, with join wall-time), fire-and-forget via a launcher
node grafted at `Create` (the child roots collected later via `ChildRoots`). The
`groupId` on every branch's `ConcurrencyInfo` is what links the merged children
back to the fork/launch site, so a renderer or JSON consumer can reconstruct
which nodes ran together even though they were captured on different threads.

## Divergences from the JVM edition

- **No Gradle plugin / Java agent / Spring / Micronaut / SLF4J.** The .NET
  equivalents are the MSBuild package, DI auto-wrap, ASP.NET Core
  middleware, and the `Microsoft.Extensions.Logging` bridge. Compile-time
  weaving (C# interceptors) is a future spike.
- **`[OnError]` resolves on entry** and selects the most-specific declared
  exception type — it does not inspect the thrown exception. The error text
  renders in Markdown/Prose (the structure-only `IndentedTextRenderer` omits
  it, as it does all outcomes).
- **`[NotTraced]` targets parameters only** (not fields/record members);
  nested-object redaction is handled by name-based `RedactionPolicy`.
- **Clarity JSON keys** are `scenario`/`overall`/`method`/… (not the JVM's
  `name`/`overallScore`/…).
- **net48** validation is pending (Windows-blocked).
