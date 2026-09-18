# NarrativeTrace .NET — FAQ

**English** | [Español](es/preguntas-frecuentes.md) | [Português](pt-BR/perguntas-frequentes.md) | [简体中文](zh-CN/常见问题.md)

## Which wins: the tracing level or my logger's level?

**I set the tracing level to `Detail` but nothing shows up in my logs. Or: I set my logger to
`Warning` and the trace still appears complete in `CaptureTrace()`. Which setting wins?**

Both, because they answer different questions. NarrativeTrace has two independent dials, and a
captured event can reach a logger by two different paths.

**Dial 1, `TracingLevel`, decides what is captured.** `Off`, `Errors`, `Summary`, `Narrative`,
`Detail` — NarrativeTrace's own setting (`NarrativeTraceConfig.Level`), low to high. It sits at
the front of capture: a call this level filters out never becomes part of the trace tree. It
doesn't exist for `CaptureTrace()`, any renderer, or any logger, and no other setting can bring it
back. This dial is also the only one that changes the cost of tracing: `Off` skips capture
entirely — `EnterMethod` returns immediately and no event is created. Every other level
intercepts every call — `Errors` and `Summary` still capture a full enter/exit event, then
`TraceTreeBuilder` prunes the assembled tree afterward (an error node keeps its whole subtree; a
`Summary` tree collapses to leaves and error frames) — while `Narrative` and `Detail` keep the
tree unfiltered, and `Detail` additionally captures parameter values.

**Dial 2, your logger's level, decides what is printed.** `TraceLoggingOptions` maps event kinds
to `Microsoft.Extensions.Logging` levels: entry and fork/join/fire-and-forget lifecycle events at
`Trace` (`EnterLevel`), a successful return at `Trace` (`ReturnLevel`), a thrown exit at `Warning`
(`ExceptionLevel`) — the defaults for both `LoggingNarrativeContext` and the
`AddNarrativeLogging()` event-stream bridge. (An ASP.NET Core host additionally gets one
`Information`-level line per finished *request* trace from `LoggerTraceExporter`, logger category
`NarrativeTrace.Export` — a separate, request-scoped summary, not part of `TraceLoggingOptions`.)
Your logger's own minimum level then does what it always does: raising it silences lines. It never
captures more, and it never captures less.

**Now the two paths a captured event can reach a logger by — this is where the confusion comes
from.** `DualPathPipeline` is the type name: a fan-out with one synchronous slot and one buffered
slot, both optional.

- The **synchronous path** runs inline, on the calling thread, before the traced call returns —
  either `DualPathPipeline`'s own synchronous-listener slot (wire
  `LoggingTraceEventListener.OnEvent` into it directly), or `LoggingNarrativeContext`, a context
  decorator with the same before-the-call-returns property that doesn't go through
  `DualPathPipeline` at all. Either way, the log line is written before anything downstream sees
  the result.
- The **buffered path** is `DualPathPipeline`'s other slot: a `BufferedEventConsumer` — a
  bounded, lock-free ring buffer drained on a background thread, load-shedding under pressure so
  it never blocks the caller. `services.AddNarrativeLogging()` subscribes
  `LoggingTraceEventListener` to it automatically in a DI-hosted app; the live OpenTelemetry
  listener (`OtelTraceEventListener`, `NarrativeTrace.Observability`) can subscribe the same way.

Both slots — the whole pipeline, in fact — are opt-in: the shipped `AddNarrativeTracing`/
`AddNarrativeTrace` registrations build a plain `SyncNarrativeContext` with no sink at all, so
nothing streams live until a host wires one.

**Neither path is where `CaptureTrace()` reads from.** The trace file, `CaptureTrace()`'s
returned `TraceTree`, an approval baseline, and `TraceActivityExporter`'s OpenTelemetry export all
come from a context's own always-on, synchronous, in-memory capture list — present whether or not
any pipeline sink is ever wired, and never consulted by a logger at all. So: a logger at `Warning`
and a tracing level at `Detail` gives you a quiet log and a complete `CaptureTrace()` result. A
tracing level at `Summary` and a logger at `Trace` gives you a loud log of a thin trace. A tracing
level at `Off` gives you nothing anywhere, because nothing was captured.

**Rules of thumb.**

| Goal | Adjust |
|---|---|
| Reduce log volume | Raise your logger's minimum level for the `NarrativeTrace` category (or narrow `TraceLoggingOptions`); the trace file and `CaptureTrace()` are untouched. |
| Reduce trace size | Lower `TracingLevel` (`Detail` → `Narrative` → `Summary` → `Errors`). |
| Reduce CPU/memory | Lower `TracingLevel` — `Off` skips capture entirely; the logger threshold changes nothing about capture cost. |
| Keep tracing on in production but out of the logs | Leave `TracingLevel` at `Summary` or `Narrative`; either skip wiring `AddNarrativeLogging()`/`LoggingNarrativeContext` entirely, or raise the `NarrativeTrace` category above `Warning` — `CaptureTrace()` and any exporter still see the full picture. |

See [Configuration Guide §8, "Two dials, two paths"](guides/configuration.md#8-two-dials-two-paths)
for where each dial lives, file by file.
