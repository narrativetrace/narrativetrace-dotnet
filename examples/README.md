# NarrativeTrace Examples

**English** | [Español](LEAME.md) | [简体中文](自述文件.md)

Runnable tutorials that show NarrativeTrace in practice. These projects are **not
published** — they exist so a developer (or an AI agent) can run a realistic scenario,
read the resulting trace, and connect it back to the code that produced it.

Treat the projects as tutorials with a suggested order, not as a reusable API. Each
example's entry class carries a guided reading order in its XML docs; start there when
drilling into one.

## Project map

| Project | Language | Entry point | What it teaches |
|---|---|---|---|
| `NarrativeTrace.Examples.ECommerce` | C# | `ECommerceDemo` | The flagship: tracing a realistic service graph — container wiring with `AddNarrativeTracing`, `[Narrated]` / `[OnError]` / `[NotTraced]`, async completion joining the trace, fork-join and fire-and-forget groups, success and failure scenarios. |
| `NarrativeTrace.Examples.Clarity` | C# | `ClarityExample` | How the clarity subsystem scores naming quality, using a hotel-reservation domain with deliberately excellent, adequate, and poor names. |
| `NarrativeTrace.Examples.Minecraft` | C# | `MinecraftExample` | How much naming alone changes trace quality: the same behavior traced twice, once with domain-rich names, once with generic ones. |
| `NarrativeTrace.Examples.Library` | F# | `LibraryExample` | Using NarrativeTrace from F#: tracing F# services through `DispatchProxy` from F# interfaces in a small book-lending domain. |
| `NarrativeTrace.Examples.Common` | C# | *(library, no entry point)* | Shared example tooling: the `DemoRun` driver, the `ConsoleLoggerFactory` the runs log through, the `NarrationStreamListener` that turns trace events into the live `→ ← !!` lines, and the section markers. |

Every example project has **no NuGet dependencies** — only project references to the
libraries under `src/`. The examples have their own test projects under `tests/`
(`NarrativeTrace.Examples.*.Tests`), which pin the shape of each run.

## Running the examples

Each example is an ordinary console project:

```bash
dotnet run --project examples/NarrativeTrace.Examples.ECommerce
dotnet run --project examples/NarrativeTrace.Examples.Clarity
dotnet run --project examples/NarrativeTrace.Examples.Minecraft
dotnet run --project examples/NarrativeTrace.Examples.Library

./build.sh RunExamples   # all four in sequence
```

The only switch an example takes is `--classic` (after `--`): every line then carries
the traditional timestamp / level / `[thread]` / `[traceId]` / `[logger]` prefix instead
of the bare message. Pacing and colour are the launcher's business, not the example's.

## Demo launcher

The repository root ships `./demo.sh` — the fastest way to watch the examples: one
command, no build noise, the live narration colorized and indented by call depth, and
every rendering announced as its own section. `./demo.sh` opens an interactive picker;
`--example <name>` runs non-interactively; `--list` enumerates the examples;
`./build.sh Demo --example <name> --no-pause` runs the same thing from the build.

**It walks, it does not scroll.** On a terminal the demo stops after every scenario —
`[Enter]` moves on, `q` quits — and each scenario opens with a note on *how that
scenario's trace is configured*: `AddNarrativeTracing` on the container here, a plain
`NarrativeTraceProxy.Create` there, which of `[Narrated]` / `[OnError]` / `[NotTraced]`
produced what you are about to read. The notes live in `examples/demo/wiring.awk`, keyed
by scenario title, and the `DemoWiringCheck` build target (also a test in
`tests/BuildScript.Tests`, so it runs with the suite) fails if a scenario ever loses its
note or a note outlives its scenario. Paced runs are recorded first and then walked, so a
stop point can never inflate the durations the trace tree reports; `--no-pause` plays
the run straight through, live, and is what pipes and CI get.

**Where the renderings come from** is answered once per run, at the first rendering
section, because it is the question every demo viewer asks next. There is no default
renderer and nothing to configure: capture produces a `TraceTree` and you call the
renderer you want (`IndentedTextRenderer.Render(trace)`); the renderers are static
`Render(TraceTree)` methods, so your own is any function from a tree to a string. The
live `→ ← !!` lines are not a renderer at all — that is a listener on the
`DualPathPipeline` (`NarrationStreamListener` in `Examples.Common`, the `ILogger` twin of
the Java `Slf4jTraceEventListener`), the only view that costs no rendering code.
Configuration selects a renderer in exactly one place, trace files written from tests
(on by default; `NARRATIVETRACE_OUTPUT=false` opts out): `NARRATIVETRACE_FORMAT=
markdown|text|mermaid|plantuml`, where `markdown` is the default and the
`NarrativeTrace.MSBuild` package's `NarrativeTraceOutput` / `NarrativeTraceFormat`
properties set the same switches. Each section marker names the renderer that
produced it.

**Classic log output is a first-class mode.** The narration is ordinary `ILogger`
traffic through an ordinary logger provider, so it renders in the traditional format
every log tool ingests — full `yyyy-MM-dd HH:mm:ss.fff` timestamps, level, thread, a
per-scenario `traceId` scope, and logger name. Three places to see it:

- `./demo.sh --example <name> --classic` — the whole run, verbatim.
- The ecommerce demo shows one scenario ("Unknown Customer") in classic form inline,
  mid-run — same events as the styled scenarios, presentation apart.
- `dotnet run --project examples/<name> -- --classic` — the example alone.

**Translated traces are available via the language picker.** `--lang es|zh-CN`
re-renders the same run through the example's committed glossary (`glossary.json`):
identifiers get glossed, values stay byte-identical, and untranslated phrases land in a
"glossary gaps" footer. On an interactive terminal without `--classic`, `demo.sh` prompts
you to pick a language whenever the example's glossary carries more than one; the picker
lists only the locales the glossary actually carries, from the candidate set `es` and
`zh-CN`.

The launcher is bash 3.2-clean (a stock macOS shell runs it) and needs `awk`; Git Bash
and WSL run it unchanged on Windows. `demo.ps1` is a thin PowerShell wrapper — build
quietly, run, print — without the colorized walk; it was written without a Windows box
and is deliberately minimal.

## The examples in detail

### ecommerce — production-style service graph

The most complete example. `ECommerceDemo` runs six scenarios:

1. **Successful order + async notification** — the happy path, with a `Task`-returning
   notification whose completion on a thread-pool thread lands in the same trace;
   rendered as indented text, prose, and a Mermaid sequence diagram.
2. **Payment failure — inventory leak bug** — the trace exposes that
   `IInventoryService.Reserve` was called but `Release` never was: a demonstration of
   traces surfacing real bugs.
3. **Flaky external service** — a decorator (`FlakyNotificationService`) wrapping a
   simulated gateway succeeds once, then fails; wired by hand with
   `NarrativeTraceProxy.Create`, no container.
4. **Unknown customer** — input-validation failure branch.
5. **Out of stock** — business-rule failure branch, additionally rendered as PlantUML
   sequence-diagram markup.
6. **Explicit async capture** — the `AsyncLocal` context flowing into `Task.Run`, then
   `ForkJoinGroup` and `FireAndForgetGroup` giving concurrent work isolated child
   contexts merged back into the parent (`ConcurrencyScenario`).

Key supporting classes: `ECommerceExample` (`BuildContainer` — the `ServiceCollection`
plus `AddNarrativeTracing`; `BuildTracedOrderService` — the same graph wired by hand),
`OrderService` (the orchestration that produces the interesting traces), and simple
in-memory adapters for catalog, inventory, payment, customer, and notification so the
trace stays easy to follow.

### clarity — what the clarity analyzer rewards and penalizes

Four scenarios over a hotel-reservation domain, each at a different naming-quality tier:

1. **Guest books a room** — excellent, domain-specific naming (`DefaultReservationService`).
2. **Booking via manager** — adequate but less expressive naming (`DefaultBookingManager`).
3. **Legacy data processing** — intentionally weak naming (`DefaultDataProcessor`) that
   the analyzer should penalize.
4. **Guest repository operations** — a cohesion-focused scenario.

After running all scenarios it feeds the captured traces to `ClarityAnalyzer` and prints
`ClarityReportRenderer` output, so you can connect each score back to the naming choices
that caused it.

### minecraft — naming quality, contrasted directly

Two halves perform comparable "player joins world" work:

- `Refactored.cs` — domain-rich names: `IWorldGenerator`, `IPlayerInventory`,
  `ICraftingTable`, `ICreatureSpawner`, `IWorldServer`.
- `Unrefactored.cs` — the same intent hidden behind generic labels: `IDataProcessor`,
  `IStateManager`, `IThingFactory`, `IEntityHandler`, `IGameManager`.

`MinecraftExample` runs both back to back so the traces can be compared side by side, and
closes with `ClarityScanner`'s score for each half. It is a teaching aid about naming and
observability, not a gameplay sample.

### library — F# consumer

A small book-lending domain (`ICatalogService`, `IMemberService`, `ILendingService`)
traced through `NarrativeTraceProxy` from F#, with a successful borrow and a
`BookUnavailableException` failure scenario rendered as text, prose, and Mermaid. The
F# interfaces carry the same attributes C# uses (`[<Narrated>]`, `[<OnError>]`,
`[<NotTraced>]`), and the records render through `[<NarrativeSummary>]` members. No
compiler flag is needed: .NET keeps parameter names in metadata, so the trace reads the
F# names as written.

### common — shared demo tooling

`DemoRun` owns the shared `SyncNarrativeContext` (with the live stream on its
`DualPathPipeline`), opens a `traceId` log scope per scenario, and prints the section
markers; `ConsoleLoggerFactory` is the dependency-free `ILoggerFactory` the runs log
through, in bare or classic format; `DemoOptions` parses `--classic`. It is example
scaffolding, not product code — a real application plugs its own logger provider into
the same `ILogger` seam.

## Where the logger is configured

Every example sends its trace to a real logger, not just the styled console above.
`DemoRun.Create` (`examples/NarrativeTrace.Examples.Common/DemoRun.cs`) is the one
composition root all four examples share, and it attaches the shipped
`NarrativeTrace.Logging` bridge (`LoggingTraceEventListener` — see the
[Installation Guide](../documentation/guides/installation.md)) as a second, independent
listener on the same live event stream the console narration comes from. Because the
demo's console view is deliberately styled for reading (`./demo.sh`'s colorized walk),
the bridge writes to a real log file instead of interleaving with it:

| Example | Realistic logger output |
|---|---|
| `NarrativeTrace.Examples.ECommerce` | `examples/NarrativeTrace.Examples.ECommerce/bin/<Debug\|Release>/net10.0/narrativetrace-realistic.log` |
| `NarrativeTrace.Examples.Clarity` | `examples/NarrativeTrace.Examples.Clarity/bin/<Debug\|Release>/net10.0/narrativetrace-realistic.log` |
| `NarrativeTrace.Examples.Minecraft` | `examples/NarrativeTrace.Examples.Minecraft/bin/<Debug\|Release>/net10.0/narrativetrace-realistic.log` |
| `NarrativeTrace.Examples.Library` | `examples/NarrativeTrace.Examples.Library/bin/<Debug\|Release>/net10.0/narrativetrace-realistic.log` |

Run any example (`dotnet run --project examples/<name>`, or `./demo.sh`) and open its
file — traditional log-tool format (timestamp, level, thread, logger name), redacted
parameters as `[REDACTED]`, exceptions and fork/join lifecycle events included, produced
by the exact package a real project adds (`dotnet add package NarrativeTrace.Logging`).
The demo launcher's own wiring note at the first `--- Trace tree ---` section names this
file too.

## Quality gates

The examples are excluded from packaging, but they are **not** exempt from code
quality: the Roslyn and Sonar analyzers, the 20-NCSS method-length gate, `dotnet format`,
and the test suite all apply (`./build.sh Verify`). The F# project is skipped by
`dotnet format` and the C# analyzers, which do not cover F#.
