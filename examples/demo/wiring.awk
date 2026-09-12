# Per-scenario wiring notes for the demo launcher: how THAT scenario's trace is
# configured. Loaded by demo.sh ahead of colorize.awk, which prints the matching entry
# under each scenario header.
#
# Keys are the scenario titles exactly as the examples print them, between "=== " and
# " ===", plus two sections that have no such header: clarity's report banner, and the
# "--- Trace tree ---" marker, which carries the renderer model shown once per run. They live
# here rather than in the example sources so the examples stay reference-grade code with
# no demo scaffolding in them. The cost of that split is drift — a renamed or added
# scenario silently losing its note — which the DemoWiringCheck build target (backed by
# build/DemoWiringSupport.cs, tested in tests/BuildScript.Tests) fails the build on, in
# both directions.
#
# Keep entries short: the note explains the mechanism, the trace does the rest. State
# only what the code actually does — every claim here is checkable in the sources.

BEGIN {
  # ---- how renderings are chosen — printed once, at the first rendering section ----

  wiring["--- Trace tree ---"] = \
    "Renderers are not configured: there is no default, no registry, no setting. Capture\n" \
    "produces a TraceTree and you call the renderer you want — here that is one line,\n" \
    "IndentedTextRenderer.Render(trace); ProseRenderer, MarkdownRenderer and the diagram\n" \
    "renderers below are the same deal — static Render(TraceTree) methods, so your own\n" \
    "renderer is any function from a TraceTree to a string.\n" \
    "The live → ← !! lines are not a renderer at all: that is a listener on the\n" \
    "DualPathPipeline (NarrationStreamListener in Examples.Common, logging through an\n" \
    "ordinary ILogger), formatting each event as it happens — the only view you get\n" \
    "without writing any rendering code, and what your log tool ingests.\n" \
    "The SAME live stream also feeds the shipped NarrativeTrace.Logging bridge\n" \
    "(LoggingTraceEventListener, wired once in DemoRun.Create — see\n" \
    "documentation/guides/installation.md) into a real ILogger sink at\n" \
    "narrativetrace-realistic.log next to this build, independent of the console view\n" \
    "above — the file a production app's own logging provider would receive instead.\n" \
    "Configuration picks a renderer in exactly one place, trace files written from tests:\n" \
    "NARRATIVETRACE_OUTPUT=true with NARRATIVETRACE_FORMAT=markdown|text|mermaid|plantuml\n" \
    "(markdown is the default; the NarrativeTrace.MSBuild package's NarrativeTraceOutput /\n" \
    "NarrativeTraceFormat properties set the same switches)."

  # ---- ecommerce (Microsoft DI auto-wrapping, attributes, async completion) ----

  wiring["Scenario 1: Successful Order + Async Notification"] = \
    "Wiring: services.AddNarrativeTracing(o => o.Namespaces(\"NarrativeTrace.Examples.ECommerce\"))\n" \
    "in ECommerceExample.BuildContainer — the container wraps every interface service in\n" \
    "that namespace in a DispatchProxy at registration, so OrderService holds no tracing\n" \
    "code at all. The // line in the tree is [Narrated] on IOrderService.PlaceOrder;\n" \
    "cardToken prints as [REDACTED] because that parameter carries [NotTraced]. The\n" \
    "notification returns a Task: the proxy defers its exit until the task completes on a\n" \
    "thread-pool thread, and the AsyncLocal context lands that completion in this same trace."

  wiring["Scenario 2: Payment Failure — Inventory Leak Bug"] = \
    "Wiring: unchanged from scenario 1 — nothing was added to catch or log this failure.\n" \
    "The proxy records the thrown PaymentDeclinedException and unwinds the tree itself;\n" \
    "the bracketed text after !! comes from [OnError] on IPaymentService.Charge."

  wiring["Scenario 3: Flaky External Service"] = \
    "Wiring: no container in this one — NarrativeTraceProxy.Create<INotificationService>(\n" \
    "impl, context) wraps a plain object at runtime through a DispatchProxy. Same\n" \
    "SyncNarrativeContext as the container's services, so both calls land in the same\n" \
    "trace: attributes and a container are conveniences, not requirements."

  wiring["Scenario 4: Unknown Customer"] = \
    "Wiring: unchanged — the same container proxies produced these lines, and only the\n" \
    "rendering differs. [OnError] on ICustomerService.FindCustomer supplies the bracketed\n" \
    "message; the traceId in the prefix is a log scope opened by the example per\n" \
    "scenario, as the ASP.NET Core middleware would open one per request."

  wiring["Scenario 5: Out of Stock"] = \
    "Wiring: same container proxies; [OnError] on IInventoryService.Reserve supplies the\n" \
    "bracketed message. The markup below is that same captured tree handed to\n" \
    "PlantUmlSequenceRenderer — not a re-run."

  wiring["Scenario 6: Explicit Async Trace Capture"] = \
    "Wiring: SyncNarrativeContext keeps its call stack in an AsyncLocal, so Task.Run\n" \
    "inherits the context and the worker's call joins the main thread's story with no\n" \
    "decorator or executor wrapper. ForkJoinGroup.Create(context) and\n" \
    "FireAndForgetGroup.Create(context, ...) hand each concurrent task an isolated child\n" \
    "context (CreateConcurrentChild) and graft the results back under the parent on join."

  # ---- clarity (plain proxies; the variable under test is naming, not configuration) ----

  wiring["Scenario 1: Guest Books a Room (Excellent Naming)"] = \
    "Wiring: no container, no attributes — NarrativeTraceProxy.Create<IService>(impl,\n" \
    "context) around each service, with a DualPathPipeline listener turning events into\n" \
    "the live lines. All four scenarios are wired identically; only the naming quality\n" \
    "changes."

  wiring["Scenario 2: Booking via Manager (Adequate Naming)"] = \
    "Wiring: the same proxy setup after context.Reset() — one traced interface this time.\n" \
    "Nothing in the configuration changed between scenarios; the names did."

  wiring["Scenario 3: Legacy Data Processing (Poor Naming)"] = \
    "Wiring: the same proxy setup again. A tracer can only report what the code calls\n" \
    "itself, so generic names in, generic trace out — no configuration rescues this one."

  wiring["Scenario 4: Guest Repository (Cohesion Mismatch)"] = \
    "Wiring: the same proxy setup, one repository interface. Cohesion is judged afterwards\n" \
    "from the captured tree, so the unrelated calls below are all the analyzer has to go on."

  wiring["CLARITY ANALYSIS REPORT"] = \
    "Wiring: the four trees captured above are passed to ClarityAnalyzer.Analyze, and\n" \
    "ClarityReportRenderer.Render prints the suite report. The analysis reads captured\n" \
    "traces — no extra instrumentation, no second run of the code."

  # ---- minecraft (identical wiring on both halves; only the vocabulary differs) ----

  wiring["Refactored: Player Joins World"] = \
    "Wiring: no container, no attributes — every interface is wrapped with\n" \
    "NarrativeTraceProxy.Create<IService>(impl, context), and a DualPathPipeline listener\n" \
    "turns the events into ordinary ILogger log lines."

  wiring["Unrefactored: Player Joins World"] = \
    "Wiring: byte for byte the setup above — same proxies, same pipeline, same context\n" \
    "after a Reset(). Only the class and method names differ, and that is the whole point.\n" \
    "The closing line is ClarityScanner over the compiled types — reflection, no run."

  # ---- library (F#, same proxy API, attributes on interfaces) ----

  wiring["Scenario 1: Successful Book Borrow"] = \
    "Wiring: F#, no container — NarrativeTraceProxy.Create<IService>(impl, context) around\n" \
    "ICatalogService, IMemberService and ILendingService, F# interfaces with the same\n" \
    "attributes C# uses. [<Narrated>] on ILendingService.BorrowBook supplies the // line;\n" \
    "cardNumber prints as [REDACTED] from [<NotTraced>] on the parameter; the records\n" \
    "render through their [<NarrativeSummary>] members, so a Book is a title and an author."

  wiring["Scenario 2: Book Unavailable"] = \
    "Wiring: the same three proxies after context.Reset(). BookUnavailableException is\n" \
    "raised by DefaultLendingService itself and the proxy records it on the way out — there\n" \
    "is no error-handling code in this path, and nothing to keep in sync when it changes."
}
