# NarrativeTrace™ .NET

**English** | [Español](LEAME.md) | [Português](LEIAME.md) | [简体中文](自述文件.md)

## Start here

[See a trace in 60 seconds](documentation/first-10-minutes.md) — a console
app, one run, and the trace is in your terminal.

## Demo

Clone the repository and run `./demo.sh`.

## Examples

See [the examples](examples/) — NarrativeTrace in realistic applications.

## Code is the log

**Turn what your code *did* into a story you can read.** NarrativeTrace records
method execution as a narrative trace — a nested, human-readable account of the
calls, arguments, outcomes, and timing behind a unit of work — and scores the
*clarity* of your naming so unreadable code gets flagged before it ships.

A migration-first architecture: the same library runs on modern .NET,
`netstandard2.0`, and legacy `net48`.

## The problem

Half of this method is logging noise:

```csharp
public OrderResult PlaceOrder(string customerId, string productId, int quantity)
{
    _logger.LogInformation("Placing order for {Customer} product {Product} qty {Qty}",
        customerId, productId, quantity);

    var customer = _customers.FindCustomer(customerId);
    var unitPrice = _catalog.LookupPrice(productId);
    _logger.LogDebug("Priced {Product} at {Price}", productId, unitPrice);

    _inventory.Reserve(productId, quantity);
    var payment = _payments.Charge(customerId, unitPrice * quantity, $"tok_{customer.Id}");
    _logger.LogInformation("Payment processed: {Txn}", payment.TransactionId);
    return new OrderResult(NextOrderId(), payment.TransactionId, unitPrice * quantity, quantity);
}
```

The business logic is a handful of lines; the logging is another handful. Every
developer writes those logs differently — different messages, levels, and
included values. The result is inconsistent, verbose, and tangled with the code
it describes.

NarrativeTrace eliminates it. Write pure business logic:

```csharp
public OrderResult PlaceOrder(string customerId, string productId, int quantity)
{
    var customer = _customers.FindCustomer(customerId);
    var unitPrice = _catalog.LookupPrice(productId);
    _inventory.Reserve(productId, quantity);
    var payment = _payments.Charge(customerId, unitPrice * quantity, $"tok_{customer.Id}");
    return new OrderResult(NextOrderId(), payment.TransactionId, unitPrice * quantity, quantity);
}
```

The trace is generated automatically from the method names, parameter names, and
return values — the information that was already there:

```
OrderService.PlaceOrder(customerId: "cust-1", productId: "book-123", quantity: 2)
  CustomerService.FindCustomer(customerId: "cust-1") → Customer { Id = cust-1, Name = Ada Lovelace, Tier = Premium }
  ProductCatalogService.LookupPrice(productId: "book-123") → 9.99
  InventoryService.Reserve(productId: "book-123", quantity: 2)
  PaymentService.Charge(customerId: "cust-1", amount: 19.98) → PaymentConfirmation { TransactionId = txn-00001, Amount = 19.98 }
→ OrderResult { OrderId = ORD-00001, TransactionId = txn-00001, Total = 19.98, Quantity = 2 }
```

This flow is the real
[`NarrativeTrace.Examples.ECommerce`](examples/NarrativeTrace.Examples.ECommerce)
example, regression-guarded by its characterization tests.

## When something goes wrong

The trace makes bugs visible. Charge a customer over their limit and the story
stops exactly where it broke:

```
OrderService.PlaceOrder(customerId: "cust-1", productId: "book-123", quantity: 2)
  CustomerService.FindCustomer(customerId: "cust-1") → Customer { Id = cust-1, … }
  ProductCatalogService.LookupPrice(productId: "book-123") → 9.99
  InventoryService.Reserve(productId: "book-123", quantity: 2)
  PaymentService.Charge(customerId: "cust-1", amount: 19.98) !! PaymentDeclinedException: Amount 19.98 exceeds approval limit 10
!! PaymentDeclinedException: Amount 19.98 exceeds approval limit 10
```

`Reserve` was called but no compensating `Release` appears — the leaked
reservation is visible in the trace, not buried in a log file.

## The trace is only as good as your names

The same "player joins world" flow, traced twice — once with domain names, once
with generic ones (the
[Minecraft naming demo](examples/NarrativeTrace.Examples.Minecraft)):

**Refactored (clean names):**
```
WorldServer.PlayerJoined(playerName: "Steve")
  WorldGenerator.GenerateChunk(x: 0, z: 0) → Chunk { X = 0, Z = 0, Biome = plains }
  PlayerInventory.AddItem(item: "wooden_pickaxe", count: 1) → true
  CraftingTable.Craft(recipe: "wooden_pickaxe") → "wooden_pickaxe"
  CreatureSpawner.SpawnHostile(creatureType: "zombie", x: 10, y: 64, z: 20) → "zombie"
```

**Unrefactored (generic names):**
```
GameManager.Handle(input: "Steve")
  DataProcessor.Process(a: 0, b: 0) → "0,0"
  StateManager.Update(key: "wooden_pickaxe", value: 1) → true
  ThingFactory.Create(spec: "wooden_pickaxe") → "wooden_pickaxe"
  EntityHandler.Execute(type: "zombie", a: 10, b: 64, c: 20) → "zombie"
```

Identical call graph, identical return values — only the names differ. The
clarity analyzer scores the clean version strictly higher (a characterization
test pins both the identical structure and the score gap). If your code can't
tell its own story, it needs refactoring.

## Why this matters for AI-assisted development

Every `_logger.LogInformation(...)` line is a line AI coding tools — Claude Code,
Copilot, Cursor — have to parse, spend tokens on, and reason around. In a typical
service class, logging is 30–50% of the lines. Remove them and you get:

- **More business logic per context window** — the same token budget covers more
  of your actual code.
- **Cleaner reasoning** — the model sees what the code *does*, not how it logs
  what it does.
- **Signal-only diffs** — pull requests show logic changes, not mixed
  logic-and-logging changes.

## How it compares

- **Structured logging** (`Microsoft.Extensions.Logging`) requires you to *write*
  log statements. NarrativeTrace generates the narrative from your code
  structure — no `LogInformation` calls, no message templates, no scope wiring.
  When request tracing is active it also populates a `traceId` and a deterministic
  human-readable trace name.
- **Distributed tracing** (OpenTelemetry, Jaeger) tracks request flow across
  services with spans. NarrativeTrace captures *method-level* call trees with full
  parameter and return values — detail span-based tracers don't record. The
  `NarrativeTrace.Observability` package bridges both: export trees as `Activity`
  spans (batch) or live via `OtelTraceEventListener`.
- **Interceptor/AOP logging** (`DispatchProxy`, Castle) auto-logs entry/exit but
  produces flat, mechanical output. NarrativeTrace produces nested call trees and
  scores your naming quality on top.

It doesn't replace production alerting or service-topology maps; it gives you
what neither provides — a human-readable execution narrative that doubles as a
code-quality diagnostic.

## It doesn't replace your logging framework

NarrativeTrace has no sink, no provider, and no shipping pipeline of its
own. Your `Microsoft.Extensions.Logging` configuration — Serilog, NLog,
Application Insights, whatever sinks and enrichers you already run —
keeps working untouched.

What it replaces is the hand-written narration *statements* — lines like
`_logger.LogInformation("Placing order for {Customer}...", customerId)`.
A traced method produces that narrative automatically, emitted through
the same `ILogger` abstraction those lines would have used: wire
`NarrativeTrace.Logging`'s bridge (`AddNarrativeLogging()`, or the
`LoggingNarrativeContext` decorator) and every enter/exit becomes an
ordinary structured log record — same `LoggerMessage` machinery, same
`BeginScope`.

Manual logging and NarrativeTrace intermix freely — point both at the
same `ILogger` and they share every sink, filter, and enricher your host
already has configured. Add a `_logger.LogWarning(...)` anywhere you
still want one.

## What you get

- **Execution narratives** — wrap a service in a tracing proxy and every call is
  captured as a tree of `enter → outcome` events with parameters, return values,
  exceptions, and durations.
- **Multiple renderings** — the same trace as indented text, Markdown, prose,
  Mermaid / PlantUML sequence diagrams, canonical JSON, or OpenTelemetry spans.
- **Clarity scoring** — an NLP-driven analyzer grades class/method/parameter
  names (verbs, abbreviations, cohesion, generic tokens) and surfaces issues.
- **Framework integrations** — ASP.NET Core request middleware, DI auto-wrapping,
  `ILogger` scopes, OpenTelemetry (batch + live), and xUnit / NUnit test helpers
  that print the narrative when a test fails.
- **Tooling** — a `dotnet-narrativetrace` CLI (scan assemblies, gate on clarity)
  and a `NarrativeTrace.MSBuild` package that wires it into your build.

## Add it to one test

The least-ceremony path from "interesting library" to "I saw a trace of my
own code": wrap the test body in a fixture that prints the story when the
test fails.

**xUnit** — wrap the body (xUnit does not hand fixtures the test outcome):

```csharp
public class OrderTests : IClassFixture<NarrativeFixture>
{
    private readonly NarrativeFixture _fixture;
    public OrderTests(NarrativeFixture fixture) => _fixture = fixture;

    [Fact]
    public void Places_an_order() => _fixture.Run(nameof(Places_an_order), ctx =>
    {
        var orders = NarrativeTraceProxy.Create<IOrderService>(new OrderService(), ctx);
        Assert.Equal("confirmed", orders.PlaceOrder("book-123", 2));
    });
}
```

**NUnit** — derive from `NarrativeTestBase`; failures are detected and
narrated automatically in teardown via `TestContext`.

Both write real files to disk by default, no flag to set:
`TestResults/narrativetrace/traces/<Class>/<slug>.md`, a sibling `.json`, a
`.mmd` diagram, and a value-free `.nt` — under the ephemeral, gitignored
directory `dotnet test` already treats as disposable output. Set
`NARRATIVETRACE_OUTPUT=false` to opt out. See
[See a trace in 60 seconds](documentation/first-10-minutes.md) for the
fastest path to a trace, and the
[Clarity guide](documentation/guides/clarity.md) for renaming a method and
watching the score drop.

## Choose your integration

| You want | Start with |
|---|---|
| Explicit control over what's wrapped, in plain .NET | `NarrativeTraceProxy.Create<T>` below |
| Every namespace-matched interface service traced automatically | Dependency injection, below |
| Production HTTP request lifecycle in ASP.NET Core | ASP.NET Core middleware, below |
| Traces in tests, least wiring | Add it to one test, above |
| A CI naming-quality gate, no test run required | [CLI](#cli-dotnet-narrativetrace) / MSBuild |

Full matrix and a decision diagram:
[Choosing an Integration](documentation/choosing-an-integration.md).

**Proxy — explicit control, works in any .NET app:**

```csharp
using NarrativeTrace.Core;
using NarrativeTrace.Proxy;
using NarrativeTrace.Runtime;

var context = new SyncNarrativeContext(new NarrativeTraceConfig(TracingLevel.Detail));
IOrderService orders = NarrativeTraceProxy.Create<IOrderService>(new OrderService(), context);
orders.PlaceOrder("book-123", quantity: 2);

Console.WriteLine(IndentedTextRenderer.Render(context.CaptureTrace()));
```

```
└── IOrderService.PlaceOrder(sku: "book-123", quantity: 2) → "confirmed" — 6ms
```

**Dependency injection — auto-wrap every interface whose implementation
lives under a namespace prefix**, the .NET equivalent of Spring/Micronaut
bean tracing (call it last, after every service it should see is already
registered):

```csharp
services.AddNarrativeTracing(o =>
{
    o.Level = TracingLevel.Detail;
    o.Namespaces("MyApp.Services", "MyApp.Domain");
});
```

**ASP.NET Core — per-request trace, exported when the request completes:**

```csharp
builder.Services.AddNarrativeTrace(builder.Configuration);

var app = builder.Build();
app.UseMiddleware<NarrativeTraceMiddleware>(); // near the outer edge of the pipeline

app.MapGet("/orders/{id}", (string id, HttpContext http) =>
    NarrativeTraceProxy.Create<IOrderService>(new OrderService(), http.GetNarrativeContext())
        .FindOrder(id));
```

See the [ASP.NET Core Integration Guide](documentation/guides/aspnetcore.md)
for exporters and user context, and the
[Installation Guide](documentation/guides/installation.md) for the CLI,
MSBuild, and logging/OpenTelemetry bridges.

**Rendering formats** — pick independently of how you captured the trace:

```csharp
MarkdownRenderer.Render(trace);          // Markdown report
IndentedTextRenderer.Render(trace);      // console / log-friendly
ProseRenderer.Render(trace);             // narrative prose
MermaidSequenceRenderer.Render(trace);   // Mermaid sequence diagram
PlantUmlSequenceRenderer.Render(trace);  // PlantUML sequence diagram
JsonExporter.Export(trace, metadata);    // canonical JSON (schema-validated)
```

## Privacy and safety, in one screen

This library runs inside your process and writes files your team will
share. Verified against the code, not assumed:

| Guarantee | How it holds |
|---|---|
| **Redaction is unconditional in every shipped integration** | `[NotTraced]` and an always-on, multilingual name deny-list (plus JWT/payment-card/`Set-Cookie`/national-ID-checksum value-shape detection, independent of field name) apply to proxy capture, DI auto-wrap, ASP.NET Core middleware, test output, and template placeholders alike. No flag turns them off. |
| **`[NotTraced]` wins outright** | On a property or field its getter is never even invoked; on a parameter, the value is never rendered. It outranks a curated `ToString()` and beats a `RedactionPolicy.Disabled` escape hatch that exists but that no shipped integration wires up. |
| **The AI-safe structural artifact holds no values at all** | The `.nt` file (and the opt-in `.structural.json`) contain names, hierarchy, and outcome kind only — a property test seeds hostile content into every value field and fails if any of it survives. |
| **Tracing failures cannot fail your application** | Capture and rendering are exception-isolated at every hazard — a throwing `ToString()`, `[NarrativeSummary]` member, or template-path getter degrades to a placeholder without touching your business call's outcome. |
| **Resource use is bounded** | String length, collection size, object width, and nesting depth are all capped, with cycle detection independent of depth. |

Two honest limits: detection is name/shape-based, not statistical (no
"looks random" heuristics — a secret in an innocuously named field isn't
caught), and template placeholder resolution
(`[Narrated]`/`[OnError]`) always uses the default deny-list even if you've
threaded a custom `RedactionPolicy` into `ValueRenderer` elsewhere.

→ [Privacy and Redaction](documentation/privacy-and-redaction.md) for the
full, row-by-row contract.

## Performance

Tracing does work and costs something — no "zero overhead" claim. The
regression gate (BenchmarkDotNet) fails the build on **more than 15% slower**
or **any** allocation increase against a committed baseline. Recorded
baseline numbers, `Detail` level (the default), from
[`benchmarks/benchmark-baseline.json`](benchmarks/benchmark-baseline.json):

| Benchmark | Mean | Allocated |
|---|---|---|
| `CoreBenchmarks.EnterExitCycle` | ~2.5 µs | 2368 B |
| `CoreBenchmarks.RenderObject` | ~819 ns | 1611 B |
| `RendererBenchmarks.MarkdownSmall` | ~595 ns | 2704 B |
| `ConcurrencyBenchmarks.ForkJoin_TwoTasks` | ~3.9 µs | 5602 B |

Host-native only: a shared CI runner cannot hold these thresholds (proven on
the first public run — every other gate green, then dozens of spurious
"regressions" from noisy neighbors), so both GitHub Actions
(`./build.sh Verify --skip Benchmark`) and GitLab's default-branch push skip
it. It still runs for real, unattended, as a plain NUKE target on matched
hardware: `./build.sh Benchmark` restores, builds, runs all 16 benchmarks
(BenchmarkDotNet's `medium` job) and gates them against
[`benchmarks/benchmark-baseline.json`](benchmarks/benchmark-baseline.json),
writing machine-readable JSON results under `artifacts/benchmarks/`. That
one command is the entry point a nightly/host job invokes; regenerate the
baseline after an intentional performance change with
`./build.sh BenchmarkBaseline`.

At `TracingLevel.Off` the context short-circuits and captures nothing — a
characterization test pins that an off-level context produces an empty
trace — but that path isn't separately benchmarked yet, so treat "Off is
architecturally zero-capture" as verified-by-test, not a measured ns/op
number.

## What's free and what's Pro

**Free** is everything in this repository — source-available under BSL 1.1,
free in production: the whole capture pipeline, every renderer and export
format, clarity scoring, every integration above, and the value-free
structural artifact. Today's Pro-gated features (not shipped in this repo,
relocated to `narrative-trace-dotnet-enterprise`): cross-run event-stream
aggregation (`EventAggregator`), and — planned, not yet built — flow
summaries, migration diffs, runtime dependency graphs, and MCP tool handlers
for AI agents. The [Feature Guide](documentation/feature-guide.md) is the
authoritative status table: every feature is labeled Free, Pro, In
development, or Planned, with the code behind each shipped row.

## Packages

| Package | What it provides |
|---|---|
| `NarrativeTrace.Core` | Trace model, contexts, renderers, clarity types, config, and the `NarrativeTrace.Core.Annotation` attributes |
| `NarrativeTrace.Runtime` | Context implementations, event pipeline, JSON/chapter exporters |
| `NarrativeTrace.Proxy` | `DispatchProxy`-based tracing proxy, plus `[Traced]` |
| `NarrativeTrace.DependencyInjection` | `AddNarrativeTracing` namespace auto-wrapping |
| `NarrativeTrace.AspNetCore` | Request-lifecycle middleware + `ITraceExporter` SPI |
| `NarrativeTrace.Observability` | OpenTelemetry `Activity` export — batch + live listener |
| `NarrativeTrace.Logging` | `ILogger` narrative export + correlation scopes |
| `NarrativeTrace.Diagrams` | Mermaid / PlantUML sequence renderers |
| `NarrativeTrace.Clarity` | Naming clarity analyzer, scorers, dictionaries, scanner |
| `NarrativeTrace.Testing.Xunit` / `.NUnit` | Test fixtures that narrate failures |
| `NarrativeTrace.Cli` | `dotnet-narrativetrace` global tool |
| `NarrativeTrace.MSBuild` | Build targets for clarity scan/gate |
| `NarrativeTrace.Legacy` | `net48` compatibility surface |

## OpenTelemetry

```csharp
// Batch: export a completed trace tree as Activity spans.
TraceActivityExporter.Export(context.CaptureTrace());

// Live: turn a pipeline's enter/exit events into spans as they happen.
var listener = new OtelTraceEventListener(new ActivitySource("MyApp"));
bufferedConsumer.Subscribe(listener.OnEvent);
```

## Concurrency support

NarrativeTrace tracks concurrent execution — fork-join parallelism and
fire-and-forget tasks — as first-class citizens in the trace tree. Because .NET
context flows through `AsyncLocal` (the analogue of Java's `ThreadLocal`), each
parallel branch runs against an isolated child context whose trace is merged
back into the parent with thread metadata.

**Fork-join** — `ForkJoinGroup` runs branches in parallel, joins them, and grafts
their traces under the parent with a shared `groupId`:

```csharp
var fork = ForkJoinGroup.Create(context);
_ = fork.Fork(iso => NarrativeTraceProxy.Create<IProductCatalogService>(catalog, iso)
    .LookupPrice("book-123"));
_ = fork.Fork(iso => { NarrativeTraceProxy.Create<IInventoryService>(inventory, iso)
    .Reserve("book-123", 2); return true; });
await fork.JoinAsync();
```

**Fire-and-forget** — `FireAndForgetGroup` grafts a launcher node into the parent
and links the background child trace by `groupId`; the parent continues without
waiting:

```csharp
var group = FireAndForgetGroup.Create(context, "OrderService");
group.Launch(iso => NarrativeTraceProxy.Create<INotificationService>(notifier, iso)
    .NotifyOrderPlaced("cust-1", "ORD-00001"));
```

Rendered Markdown includes `⑂ fork [n tasks]` / `⑃ join` markers, a
`⤳ fire-and-forget` launcher, thread names, and join wall-time. The full worked
scenario — parallel pricing/stock then an async notification — is
[`ConcurrencyScenario`](examples/NarrativeTrace.Examples.ECommerce/ConcurrencyScenario.cs),
with characterization tests asserting the shared `groupId`, the launcher node,
and the concurrency fields in the JSON export. For ambient propagation across an
`await`, `AsyncNarrativeContext.RunAsync` attaches continuation work to the same
trace.

## CLI: `dotnet-narrativetrace`

```bash
dotnet tool install --global NarrativeTrace.Cli

# Score naming clarity of a compiled assembly (reflection-only — never runs it):
dotnet-narrativetrace clarity-scan --assembly bin/MyApp.dll --output-dir clarity

# Fail CI when clarity drops below a threshold:
dotnet-narrativetrace clarity-check --results clarity/clarity-scan-results.json \
    --min-score 0.7 --max-high-issues 0
```

### MSBuild integration

Reference `NarrativeTrace.MSBuild` and drive the same gate from your build:

```xml
<PropertyGroup>
  <ClarityMinScore>0.7</ClarityMinScore>
  <ClarityMaxHighIssues>0</ClarityMaxHighIssues>
  <ClarityWarnOnly>false</ClarityWarnOnly>
</PropertyGroup>
```

```bash
dotnet build /t:ClarityCheck   # scans + gates; incremental via a stamp file
```

## Configuration

Every knob is resolvable from `NARRATIVETRACE_*` environment variables
(`NARRATIVETRACE_LEVEL`, `NARRATIVETRACE_OUTPUT`, `NARRATIVETRACE_FORMAT`, …).
Capture levels, from least to most detail: `Off`, `Errors`, `Summary`,
`Narrative`, `Detail`.

## Documentation

Start here:

- [See a trace in 60 seconds](documentation/first-10-minutes.md) — one console app, `dotnet run`, real output in your terminal
- [Choosing an Integration](documentation/choosing-an-integration.md) — which module you need, as a decision diagram
- [Troubleshooting](documentation/troubleshooting.md) — symptom → cause → fix for the failure modes people actually hit
- [What to Commit](documentation/what-to-commit.md) — which generated files are throwaway output and which are reviewed
- [Privacy and Redaction](documentation/privacy-and-redaction.md) — the row-by-row redaction contract, verified against the code

Task-oriented guides live in [`documentation/guides/`](documentation/guides/):

- [Installation](documentation/guides/installation.md) — packages and integration paths
- [Configuration](documentation/guides/configuration.md) — levels, env vars, DI, MSBuild, redaction
- [Annotations](documentation/guides/annotations.md) — `[Narrated]`, `[OnError]`, `[NotTraced]`, `[Traced]`, `[NarrativeSummary]`
- [Dependency Injection](documentation/guides/dependency-injection.md) — namespace auto-wrapping in the MS.DI container
- [ASP.NET Core Integration](documentation/guides/aspnetcore.md) — request middleware, exporters, user context
- [Clarity](documentation/guides/clarity.md) — scoring model and CI gate
- [MSBuild & CLI](documentation/guides/msbuild-cli.md) — `dotnet-narrativetrace` verbs and the build-time clarity gate

Going deeper:

- [Feature Guide](documentation/feature-guide.md) — canonical status table (Free/Pro/In development/Planned) for every feature, with the code behind each shipped row
- [Security Tooling](documentation/security-tooling.md) — the scanner lineup and what gates the build vs. runs on a schedule
- [Security Testing](documentation/security-testing.md) — the fuzz/property suite mirrored across every NarrativeTrace runtime

For AI consumers: [`llms.txt`](documentation/guides/llms.txt) and
[`llms-full.md`](documentation/guides/llms-full.md).

## Target frameworks

- Modern runtime: `net10.0`
- Compatibility: `netstandard2.0`
- Legacy: `net48` (in `NarrativeTrace.Legacy`)

## Build and test

```bash
dotnet build NarrativeTrace.sln
dotnet test NarrativeTrace.sln
```

Or via [NUKE](https://nuke.build) (`./build.sh`, `build.ps1`, `build.cmd`):

```bash
./build.sh Test        # run the full suite
./build.sh Verify      # clean + format + analyze + test + coverage + metrics + benchmark
./build.sh VerifyAll   # every verification this repo has, gate and heavy alike — see below
./build.sh Coverage    # Coverlet cobertura reports
./build.sh Mutation    # Stryker.NET mutation testing
./build.sh Pack        # produce NuGet packages
```

### Quality gates (enforced)

Formatting (`dotnet format`), static analysis (Roslyn + SonarAnalyzer, incl. a
20-line method cap via S138, and the full CA5xxx security rule category),
secrets scanning (gitleaks, staged-diff pre-commit hook plus a full-history
sweep), tests (xUnit / NUnit), coverage (Coverlet), and benchmark regression
(BenchmarkDotNet) all gate the build. Mutation testing (Stryker.NET,
`./build.sh Mutation`) is defined and runnable — including on the public
mirror via [`.github/workflows/mutation.yml`](.github/workflows/mutation.yml)
— but is **not** a `Verify` dependency: like Semgrep's OSS C# ruleset and
dependency-advisory scanning (OSV-Scanner, `dotnet list package
--vulnerable`), it runs on a scheduled/manual-dispatch tier, never per commit
— see [`documentation/security-tooling.md`](documentation/security-tooling.md).
`tests/BuildScript.Tests` validates build behavior itself. CI runs
`./build.sh Verify` on GitHub Actions (`.github/workflows/ci.yml`).

`./build.sh VerifyAll` is the one command that runs every verification this
repo has, gate and heavy alike — unit tests, coverage, mutation, property
tests, both fuzz tiers, architecture, conformance, benchmarks/allocation,
concurrency stress, and the secrets/SAST/SCA/lint/format/complexity/
translation checks — in one sitting, collecting every category's result
rather than stopping at the first failure. It writes
`reports/verification/<date>.json` and a rendered `.md` twin, in the
cross-runtime shape this family's Java repo defines
(`reports/verification/SCHEMA.md`): the same field names, the same four
statuses (`passed`/`failed`/`skipped`/`not-implemented`), the same 21
category ids across every NarrativeTrace runtime. Long-running by design —
mutation across every Stryker module and the raised-iteration stress sweep
are each historically minutes, not seconds — so it is a scheduled/manual run,
never a per-commit one.

## FAQ

**How much overhead does this add, and what happens under high concurrency?**
We do not claim "zero overhead" — see [Performance](#performance) above for
the dated numbers this answer summarizes (BenchmarkDotNet, `TracingLevel.Detail`,
the default, from [`benchmarks/benchmark-baseline.json`](benchmarks/benchmark-baseline.json),
refreshed 2026-08-12): a full enter/exit cycle costs ~2.5 µs and allocates
2368 B; rendering an object costs ~819 ns/1611 B; a small Markdown render
costs ~595 ns/2704 B. There is no separate benchmark yet for
`TracingLevel.Off` — worth saying plainly rather than implying a number that
doesn't exist: an off-level context is verified by characterization test to
produce an empty trace, architecturally zero-capture, but that path isn't a
measured ns/op figure yet.

What NarrativeTrace itself adds is that capture — intercepting the call,
reading arguments, building the trace tree. Everything downstream of capture
(the `ILogger` write, the collector, the disk or network) is the same cost
your logging stack already pays; NarrativeTrace does not add a second sink.
For a team replacing hand-written log statements, the sink side is close to a
wash: N log calls per method become one trace write, and those statements
stop being written, reviewed, and kept in sync with the code.

Under concurrency, the pipeline's two paths carry different guarantees. A
synchronous listener — `NarrativeTrace.Logging`'s `ILogger` export, if you
wire it — runs inline: the write completes before the method returns, so it
is exactly as durable, and costs exactly what, a log call already does. The
buffered, best-effort path is a bounded ring that sheds above 70% fill rather
than blocking the caller, and every dropped event is **counted** — ring
overwrites and adaptive-drain discards alike, via
`BufferedEventConsumer.DroppedCount` — and surfaced in the trace's own
`TraceLoss` footer ("N events dropped (buffer full)"), never silently.

**The honest gap:** there is no sampling in this runtime, or in any
NarrativeTrace runtime, today — every traced call is captured in full at its
configured `TracingLevel`. A percentage- or rate-based sampler is on the
roadmap, not shipped. If you need to cap capture volume now, use
`TracingLevel.Off` or narrow the traced scope to the boundary that matters.

**How do I know a parameter with PII or credentials won't leak into a
trace?** Four independent layers, not one blanket promise — the row-by-row
contract is [Privacy and Redaction](documentation/privacy-and-redaction.md):

1. `[NotTraced]` on a parameter, property, or field — explicit redaction you
   control. On a property or field the getter is never even invoked; it
   outranks a curated `ToString()`, and it is unconditional — no flag turns
   it off.
2. An always-on, multilingual name deny-list — matches field and parameter
   names against English, Spanish, Portuguese, French, German and Chinese
   patterns for passwords, tokens, national IDs and the like, widened
   (never replaced) via `NARRATIVETRACE_REDACTION_ADDITIONALPATTERNS`. On
   by default, not opt-in.
3. Value-shape matching, independent of the field name — a JWT-shaped
   string, a Luhn-valid card number, a `Set-Cookie`-shaped value, or a
   national-ID checksum or structural rule (Chilean RUT, Brazilian
   CPF/CNPJ, Spanish DNI/NIE, French NIR, Chinese resident ID, or a dashed
   US Social Security number) is redacted even under an innocuous name
   like `data` or `value`.
4. The value-free `.nt` structural artifact (plus the opt-in
   `.structural.json` sibling) — the categorical guarantee. Names,
   hierarchy, and outcome kind only, zero runtime values; a property test
   seeds hostile content into every value field and fails if any of it
   survives.

Two honest limits, already named in the [Privacy and safety
table](#privacy-and-safety-in-one-screen) above: detection is name/shape-based,
not statistical — a secret in an innocuously named field, in a shape none of
the checks recognize, is not caught — and template placeholder resolution
(`[Narrated]`/`[OnError]`) always uses the default deny-list even if you've
threaded a custom `RedactionPolicy` into `ValueRenderer` elsewhere. Layers
1–3 are heuristic and extensible; layer 4, the structural artifact, is the
only *categorical* one — reach for it if your threat model requires that no
value can possibly leave the process.

**Can trace IDs correlate with a standard correlation ID across services, or
is tracing local only?** Local only, today — and we'd rather say that
plainly than let the `NarrativeTrace.Observability` package imply otherwise.
This runtime does not parse an inbound W3C `traceparent` header, and does
not attach one on outbound HTTP calls; that is an explicit, tested
non-goal right now, not an oversight — a stub property test in the security
suite asserts that no `Traceparent` type exists in `NarrativeTrace.Core` yet,
precisely so a future parser lands with its fuzz cases already in place.
`TraceId` is generated locally and is W3C-*shaped* (32 lowercase hex
characters, comparable byte-for-byte against ids the Java runtime or a real
`traceparent` header would produce) — but it is never *derived* from an
inbound header, so a NarrativeTrace trace id will not equal an upstream
distributed trace's id.

What does exist: `NarrativeTrace.Observability`'s `TraceActivityExporter`
(batch) and `OtelTraceEventListener` (live) turn a completed or in-flight
NarrativeTrace tree into `System.Diagnostics.Activity` spans under an
`ActivitySource("NarrativeTrace")` — but this is a replay, not live
instrumentation: the spans carry the trace's own hierarchy and are not
children of whatever `Activity.Current` happens to be at export time. If
cross-service correlation is a hard requirement today, propagate your own
correlation id through `NarrativeTrace.Logging`'s `ILogger` scopes alongside
NarrativeTrace's output; treating W3C trace-context propagation as shipped
here would be inaccurate — it is a gap on the roadmap, not a shipped feature.

**Are parameter and return values serialized eagerly?** Yes — values are rendered
to strings at capture time, at the configured level. That is deliberate: the
trace records what the value *was* at the moment of the call, not a later,
possibly-mutated view. See [Privacy and safety](#privacy-and-safety-in-one-screen)
above and the [Privacy and Redaction](documentation/privacy-and-redaction.md)
page for the full, verified redaction contract.

**Can tracing trigger side effects in my code?** Rendering may invoke a small,
documented set of members: property getters during reflective introspection, a
custom `ToString()`, a `[NarrativeSummary]` member, and property paths named in
`[Narrated]`/`[OnError]` templates. Keep those pure (side-effect-free getters
are already a .NET Framework Design Guideline) — or mark the member
`[NotTraced]`, in which case its value is never read at all. Every invocation
is bounded (length/depth/item caps), exception-isolated (a throwing getter can
never fail your business call), and happens eagerly at the call site; at
`TracingLevel.Off` no rendering happens whatsoever. See the purity contract in
the [annotations guide](documentation/guides/annotations.md).

**Can it trace non-interface / private methods?** The `DispatchProxy` proxy traces
*interface* calls, so tracing happens at interface boundaries — private and
internal method calls inside an implementation are not individually traced. This
keeps the narrative at the collaboration level (service-to-service), which is
usually the level you want to read. For finer granularity, split behavior behind
an interface.

**How does NarrativeTrace interact with other libraries that wrap methods
(AOP, proxies, contract libraries)?** It narrates business-boundary
crossings, not implementation machinery — a generated decorator, a bridge
method, or another library's proxy stub is never turned into a trace
frame in its own right. When a contract or validation library rejects a
call on an interface `NarrativeTraceProxy` also wraps, the rejection
always reaches the trace as an exception, regardless of which wrapper is
registered first — only *which* frame it attaches to (inner or outer)
depends on that order. A dedicated ingress seam for violation events that
don't throw is planned but not built yet. The exclusion knobs today are
narrow: `AddNarrativeTracing`'s `ExcludeNamespaces` carves interface
namespaces out of DI auto-wrap with dot-boundary matching, and the raw
proxy only ever wraps what you explicitly pass to
`NarrativeTraceProxy.Create`. There is no default-ignore list for known
machinery shapes (generated decorators, compiler-generated types) yet,
and `DispatchProxy`'s one-interface-per-proxy design means a wrapped
instance does not expose any *other* marker interface it implements —
both remain open items on the roadmap.

## Status

0.1.0 is released — the packages are on NuGet. The capture pipeline, canonical
JSON schema, two-tier attribute model, ASP.NET Core lifecycle, DI wiring,
OpenTelemetry (batch + live), clarity tooling + CLI, and test-framework
integrations and the runnable examples with their demo launcher are in place.
Remaining work: `net48` runtime validation.

## License

NarrativeTrace's API and output format are open standards (Apache 2.0). Its
runtime is free and source-available (BSL 1.1, converting to Apache 2.0 four
years after each release). Pro is commercial.

Everything in this repository is the runtime, licensed under the
[Business Source License 1.1](LICENSE). You may use it in production for any
purpose, including internally and in products and services you provide to your
own customers — the one exclusion is offering NarrativeTrace itself, or a
product or service whose value derives substantially from it, to third
parties as a logging, tracing or code-narrative product or service. Four
years after a version is published, that version becomes Apache 2.0 — the
grant is automatic and per-version, so what you adopt today is on a clock you
can read.

Documentation prose is [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/);
the JSON schema files are Apache 2.0.

<!-- legal:trademark:begin -->
NarrativeTrace is a trademark of Empower Agile. The license grants no
trademark rights.
<!-- legal:trademark:end -->

### The licence, in plain words

Everything in this repository is Business Source License 1.1 today; the open
contract layer arrives with the api split.

<!-- legal:plain-words:begin -->
**Free to run.** The runtime is source-available under the Business Source
License 1.1: read it, audit it, patch it, and use it in production at no cost —
including inside the products and services you sell to your own customers.

**One exclusion.** You may not offer NarrativeTrace itself — or a product or
service whose value derives substantially from it — to third parties as a
logging, tracing or code-narrative product or service.

**It opens on a date.** Every release converts to Apache 2.0 four years after it
is published; the exact date is printed in that release's LICENSE.

*This summary is a courtesy, not a licence. The LICENSE file is the only binding
text; where the two differ, the LICENSE governs.*
<!-- legal:plain-words:end -->
