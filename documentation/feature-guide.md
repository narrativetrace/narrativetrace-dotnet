# NarrativeTrace Feature Guide (.NET)

What NarrativeTrace for .NET ships, from a user's perspective. For the full
cross-platform feature catalog (all tiers, all platforms), see the canonical
feature guide:
<https://github.com/narrativetrace/narrativetrace-java/blob/main/documentation/feature-guide.md>.

**Status labels** (same vocabulary as the canonical guide):

- **Free** — shipped and available in this repository, under the Business
  Source License 1.1 (free for production use; each version converts to
  Apache 2.0 four years after it is published — see [`LICENSE`](../LICENSE)).
- **Pro** — shipped in NarrativeTrace Pro (commercial tier; for .NET,
  the `narrative-trace-dotnet-enterprise` repository).
- **In development** — actively being built; the design is settled.
- **Planned** — specified, not yet started; may change.

Per-topic documentation lives under [guides/](guides/).

---

## Capture the story of your code (core tracing)

| Feature | Status | Notes |
|---|---|---|
| Automatic narrative capture — method, class, parameter names, return values, timing, errors; zero log statements | Free | `NarrativeTraceProxy.Create<T>(target, context)`; .NET keeps parameter names in metadata, so no compiler flag is needed |
| Enrichment attributes — `[Narrated("… {param} / {param.Property} …")]`, repeatable `[OnError]` (most-specific exception type wins), `[Traced]` positional name overrides, `[NarrativeSummary]` | Free | [guides/annotations.md](guides/annotations.md); unresolved placeholders stay literal so typos are visible |
| Sensitive data redaction — `[NotTraced]` (value never read at all) + name-pattern `RedactionPolicy` (deny-by-default: `password`, `cvv`, `ssn`, `token`, `secret`, `authorization`) | Free | [guides/configuration.md](guides/configuration.md); dictionary keys render through the same guarded path (redaction + bounds + cycle detection), never a bare `ToString()` |
| Void-completion contract — a `void` method carries no rendered value (`null`, never the string `"null"`) | Free | Markdown/text render nothing, JSON omits `returnValue`, diagrams render ✔, the `ILogger` bridge logs `Exit completed`; a rendered `null` always means a real null return |
| Five capture levels (`Off` → `Errors` → `Summary` → `Narrative` → `Detail`), runtime-changeable, `NARRATIVETRACE_LEVEL` env channel | Free | Level names match the Python runtime's (`Summary`) rather than the Java runtime's (`FLOW`); parameter values exist only at `Detail` — suppression happens at capture, not at render |
| Two-gate levels — capture level and log level are independent | Free | [guides/configuration.md](guides/configuration.md) |
| Concurrency capture — `ForkJoinGroup` / `FireAndForgetGroup` over `Task`s, isolated child contexts grafted back with a shared `groupId`, thread metadata, join wall-time; `AsyncNarrativeContext.RunAsync` for ambient flow across `await` | Free | Context flows via `AsyncLocal`, not `ThreadLocal` — `async`/`await` continuations resume on arbitrary thread-pool threads, so only `AsyncLocal` tracks logical rather than physical execution |
| Trace identity — traceId, human-readable trace names, storyId/chapterId derivation | Free | Canonical-schema aligned |
| Purity contract — every member invoked during rendering is bounded (length/depth/item caps) and exception-isolated; a throwing getter never fails the business call | Free | Documented in [guides/annotations.md](guides/annotations.md) and the README FAQ |

## Attach it to your stack (integrations)

| Feature | Status | Notes |
|---|---|---|
| `DispatchProxy` interface wrapping — explicit, in-box, no Castle dependency | Free | Interface boundaries only; private/internal calls inside an implementation are not individually traced |
| DI auto-wrap — `AddNarrativeTracing(o => o.Namespaces(...))` decorates namespace-matched interface registrations in the MS.DI container (Spring/Micronaut-style bean tracing) | Free | [guides/dependency-injection.md](guides/dependency-injection.md); one proxy per interface (a `DispatchProxy` limit), keyed services skipped |
| ASP.NET Core middleware — per-request scoped context, request tier (HTTP method/route/client IP) + user tier (enduser/session/tenant via `IRequestContextProvider`), path exclusion, `ITraceExporter` at request end; exceptions still produce a trace with the response status | Free | [guides/aspnetcore.md](guides/aspnetcore.md); `AddNarrativeTrace()` + `UseNarrativeTrace()`; net10.0 only |
| xUnit — `NarrativeFixture` (wrap the test body), `NarrativeSuiteFixture`, per-test trace artifacts, failure narratives | Free | xUnit does not hand fixtures the test outcome, hence the wrap |
| NUnit — `NarrativeTestBase` with automatic failure detection in teardown via `TestContext`, suite setup/report | Free | |
| `dotnet-narrativetrace` CLI — `clarity-scan` (reflection-only, never runs the assembly), `clarity-check` (threshold gate), `clarity-aggregate` (runtime artifacts) | Free | [guides/msbuild-cli.md](guides/msbuild-cli.md) |
| MSBuild clarity gate — `NarrativeTrace.MSBuild` targets `ClarityScan` / `ClarityAggregate` / `ClarityCheck` (incremental via stamp file), config validation, `NARRATIVETRACE_*` pass-through to the test host | Free | Thin shim over the CLI; scan (static) or runtime source selectable |
| Zero-code-change instrumentation (javaagent analogue) — C# interceptors via source generator (preferred) or IL weaving (Fody/Cecil) | Planned (Free) | WS13 spike; no CLR analogue of `-javaagent`, decision deferred |
| Multi-targeting — `net10.0` + `netstandard2.0` across the libraries; `net48` via `NarrativeTrace.Legacy` | Free | `net48` compiles on all platforms; **runtime validation on Windows is still pending** (WS15) |

## Read the story (outputs)

| Feature | Status | Notes |
|---|---|---|
| Indented text, Markdown, and prose renderers | Free | Markdown renders parent returns inline on the entry line (no closing repeat); exceptions and in-flight calls still close after the child block |
| Trace value references — content-addressed dedup of repeated captured values with readable labels (`‹Hotel›=full` on first emission, `‹Hotel›` after) | Free | `ValueReferenceIndex` via `MarkdownRenderer`; labels come from the structured value's identity field (Name/Id/Description/…, matched case-insensitively), never a redacted one; byte equality certifies sameness — any difference renders in full; containment inside other captured values counts and is replaced |
| Intra-trace value deltas — a re-capture of the same entity, changed, renders as a diff against the reference (`‹Dinner›′{Amount: 100→92, Currency: "USD"→"EUR"}`) | Free | `ValueDelta` via `ValueReferenceIndex`/`MarkdownRenderer`. "Same entity" is the same structured type name plus an equal identity field — the same case-insensitive ladder that names the label; different rendered bytes mean it changed. Changed scalar fields only (string, integer, decimal, boolean, timestamp, null), formatted the way `ValueRenderer` prints them and never reconstructed from the structured tree. A changed nested object or list, a different field set, or a value with no identity field renders in full exactly as before, with no label stamped on a definition nothing refers back to. A changed variant that itself repeats is defined AS the diff (`‹Dinner·2›=‹Dinner›′{…}`). Fields are compared by a structural walk, not record equality — `ObjectVal`/`ListVal` compare their dictionary and list members by reference, so an unchanged nested value would otherwise read as changed and suppress every diff. `DoubleVal` carries a `double`, so a `decimal` captured as `100.00` prints `100` in the diff while the reference line keeps the original text. Presentation-only and Markdown-only |
| Per-test trace artifacts — Java-compatible `traces/<Class>/<slug>` layout; Markdown output gains a sibling `.json` and a `diagrams/…/*.mmd` companion | Free | Empty traces write nothing |
| Canonical JSON export (chapter-tree schema) | Free | `JsonExporter.Export(trace, metadata)`; schema-validated |
| Per-service chapter export (`chapter.schema.json`, `nt.chapterTree`) | Free | `ChapterExporter` |
| Per-test canonical entry array — `<test>.canonical.json`, the trace flattened into `entry.schema.json` entries (one `method_enter` + one `method_exit` per call) | Free | `TraceTreeCanonicalMapper` + `CanonicalEntryArrayExporter`; opt in with `NARRATIVETRACE_CANONICAL_JSON=true`. Identity is inherited across the tree and synthesized deterministically for a context-free one, so the file is byte-identical run to run — which is what makes it usable as a conformance fixture |
| Per-test AI-safe entry array — `<test>.structural.json`, the same array with every runtime value elided (product ADR-002 Level 1) | Free | `StructuralProjection`; opt in with `NARRATIVETRACE_STRUCTURAL_JSON=true`. Parameter names survive (a name is source code); parameter values, return values and exception messages do not exist in the output at all |
| Sequence diagrams — Mermaid + PlantUML | Free | `NarrativeTrace.Diagrams` |
| Console test summaries with clarity scores | Free | |
| Flow summaries — aggregated paths + frequencies per entry point | Planned (Pro, gated) | Enterprise plan Phase E3 |
| Migration diffs — behavioral before/after comparison | Planned (Pro, gated) | Enterprise plan Phase E3 |
| Runtime dependency graphs (always-called vs conditional) | Planned (Pro, gated) | Enterprise plan Phase E4 |

## Keep your logging stack (logging + observability)

| Feature | Status | Notes |
|---|---|---|
| `ILogger` bridge — `LoggingNarrativeContext` decorator (synchronous per-call emission) + `LoggingTraceEventListener` (event-stream adapter), cached `LoggerMessage` delegates, per-event-type levels, correlation ids + service identity via `BeginScope` | Free | The MEL twin of the Java SLF4J bridge — MEL is the one abstraction every .NET logging provider plugs into, so teams keep their own sinks |
| DI composition for the bridge — `AddNarrativeLogging()` registers the event-stream listener from the host's `ILoggerFactory` and attaches it to a registered `IEventSubscribable` stream | Free | The .NET answer to the Java runtime's classpath-probing `PipelineBootstrap`, which has no .NET equivalent: SLF4J's factory is a static global, an `ILogger` is not. Silent no-op when no `ILoggerFactory` is registered or `NARRATIVETRACE_NARRATION=off` |
| Three-tier attribute model — `AttributeTier` (resource / trace / span) decided at the export boundary; every span internally carries full context | Free | Implements product ADR-009 |
| Coexistence with hand-written logs | Free | Remove them at your own pace |
| OpenTelemetry span export — `TraceActivityExporter` (batch, completed tree → `Activity` spans) + `OtelTraceEventListener` (live, from the event stream) | Free | `NarrativeTrace.Observability` |
| Event pipeline — `IEventPipeline` contract, `DualPathPipeline` fan-out, lock-free MPSC `BoundedEventBuffer`, `BufferedEventConsumer` drain thread with adaptive shedding + watchdog, `EventStore` retention | Free | Buffering/retention is free by design (product ADR-010); the pipeline contract lives in `NarrativeTrace.Core` (netstandard2.0-safe), the threading machinery in `NarrativeTrace.Runtime` |
| Event-stream aggregation — aggregate tree, hotspots, error paths/rates, method/error frequencies (`EventAggregator`) | Pro | Relocated 2026-07-12 (Phase 31a) to `narrative-trace-dotnet-enterprise`; feed it `EventStore.Events()` |

Note: this runtime is not a replacement for `Microsoft.Extensions.Logging` —
it ships no sink, no provider, and no shipping pipeline of its own. What
it replaces is the hand-written narration statements a traced method
would otherwise need; the `ILogger` bridge above emits that narrative
through the same `LoggerMessage`-based `ILogger` calls a hand-written
log line would make, carried in the same `BeginScope`. Every sink,
filter, and enricher the host already has configured for
`Microsoft.Extensions.Logging` keeps working untouched, and hand-written
logging keeps intermixing freely with the traced narrative on the same
logger.

## Improve the code (clarity diagnostics)

| Feature | Status | Notes |
|---|---|---|
| Clarity scoring — class / method / parameter / **property** naming quality (verb, morphology, cohesion, generic-token, structural scorers; shared dictionaries) | Free | [guides/clarity.md](guides/clarity.md) |
| Property names scored as domain vocabulary — declared public instance properties are judged on the **noun** rubric and reported under `property-name` | Free | Scanner-only (properties are not traced calls, so runtime clarity is unchanged). Indexers and compiler-generated properties are excluded — nobody chose those names. Folded into the existing method dimension so the cross-language weight vector is untouched. **Breaking for pinned clarity gates:** more types are scored, so a build near `ClarityMinScore` / `ClarityMaxHighIssues` can go red on upgrade |
| Suite-level clarity report + `clarity-results.json` / report rendering | Free | |
| Standalone assembly scanner — score compiled assemblies without running them (`clarity-scan` CLI, `ClarityScan` MSBuild target) | Free | |
| Project vocabulary in scoring — the committed glossary extends the built-in dictionaries | Free | One file, one review workflow: verbs of the committed `glossary.json` score as domain verbs and its nouns as domain tokens (`DomainVocabulary`, `GlossaryVocabulary`); accepted shorthand is declared separately, in the root-level `abbreviations` map of schema 2, which also supplies the spell-out the teaching note uses. Found by the same upward search the harvest uses (`GlossarySettings.ResolveFile`, `NARRATIVETRACE_GLOSSARY=off` to disable); reading is unconditional wherever a glossary exists. Built-in tiers keep authority — generic verbs, boolean prefixes, meaningless placeholders, deprecated synonyms and `stale` terms are never promoted |
| Optional CI gate — `--min-score`, `--max-high-issues`, `ClarityWarnOnly` | Free | Advisory by default is recommended |
| Runtime-sourced gate input — aggregate per-test `*.clarity.json` artifacts so the gate scores real call depth, not the depth-1 static scan | Free | `clarity-aggregate` / `ClarityAggregate` |

## Let AI agents see runtime truth (AI integration)

| Feature | Status | Notes |
|---|---|---|
| LLM-oriented docs (`llms.txt`, `llms-full.md`) | Free | [guides/](guides/) |
| MCP analysis tool handlers | Planned (Pro, gated) | Enterprise plan Phase E5 |

Note: value separation happens at capture time (product ADR-002 — below
`Detail`, parameter values are never recorded), and the AI-safe artifacts now
exist on both sides: the value-free `.nt` structural trace file is written
beside every Markdown artifact, and `<test>.structural.json` — the canonical
entry array with every value elided — is written when
`NARRATIVETRACE_STRUCTURAL_JSON=true`.

## Pro tier (commercial)

The .NET Pro tier lives in the `narrative-trace-dotnet-enterprise`
repository (proprietary license, sibling checkout, never published to
NuGet). Event-stream aggregation was relocated there from this repo's
free core on 2026-07-12 (Phase 31a Stage 2, .NET leg):
**`EventAggregator` — Pro**. Flow summaries, migration diffs,
dependency-graph diagrams, and MCP tool handlers are **Planned (Pro,
gated)** — execution waits on a paying .NET-stack engagement (Phases
E3–E5). Audit & compliance is Java-first and **not planned** for .NET
(Phase E6).

---

## Keeping this guide honest

Adapted from the canonical guide's rules:

1. Every user-visible feature of NarrativeTrace for .NET appears here,
   exactly once, with a status.
2. A feature moves to **Free**/**Pro** only when it is merged, tested,
   and documented. "In development" means the design is settled and work
   is scheduled; "Planned" means specified only.
3. Changes that add or promote a feature must update this file in the
   same commit.
4. This guide covers only what NarrativeTrace for .NET ships.
   Product-wide features and their cross-platform statuses live in the
   canonical guide (linked at the top) — do not fork its rows here;
   record only the .NET-side reality (including honest gaps like the pending net48
   runtime validation, and the 1.2 schema fields that are declared but not
   yet produced — instance/thread/host/process identity and source location;
   see item 2 of the repository task list).
