# NarrativeTrace .NET — User Guides

**English** | [Español](es/guias-de-usuario.md) | [Português](pt-BR/guia-de-usuario.md) | [简体中文](zh-CN/用户指南.md)

Task-oriented guides for using NarrativeTrace in a .NET project. For the
value proposition and a 60-second quick start, see the
[repository README](../../README.md).

| Guide | Read it to… |
|---|---|
| [Installation](installation.md) | Add packages and pick an integration path (proxy, DI, ASP.NET Core, test frameworks, CLI, MSBuild). |
| [Configuration](configuration.md) | Set tracing levels, env vars, DI/ASP.NET Core options, MSBuild properties, and redaction. |
| [Annotations](annotations.md) | Use `[Narrated]`, `[OnError]`, `[NotTraced]`, `[Traced]`, `[NarrativeSummary]`, and `[NarrativeElements]`. |
| [Dependency Injection](dependency-injection.md) | Auto-wrap namespace-matched interface services in the MS.DI container (bean-style tracing). |
| [ASP.NET Core Integration](aspnetcore.md) | Wire the per-request middleware, exporters, path exclusion, and user context. |
| [Clarity](clarity.md) | Score naming quality and enforce it as a CI gate. |
| [MSBuild & CLI](msbuild-cli.md) | Run the `dotnet-narrativetrace` verbs and wire the clarity gate into `dotnet build` / `dotnet test`. |

## Recipes

One page per problem, in the words a user would type, each with a real run's code and output.

- [Add logging to this service](../recipes/add-logging-to-this-service.md) — Get every call, with parameter names and results, into the project's own `ILogger` without writing a log statement.
- [Help me debug and fix this bug](../recipes/help-me-debug-and-fix-this-bug.md) — Find the call a flow is missing, fix it, and pin the fixed flow as an approved baseline.
- [Why did this test fail](../recipes/why-did-this-test-fail.md) — Read a red test's narrative against its assertion and tell a code bug from a stale expectation.
- [Review a complex flow before changing it](../recipes/review-a-complex-flow-before-changing-it.md) — See what a flow does at `Detail` and `Summary`, and the value-free `.nt` an agent reads.
- [Review a refactoring: did behaviour change](../recipes/review-a-refactoring-did-behaviour-change.md) — Catch a dropped call the green tests missed, with the approval diff.
- [Follow one request across services (distributed tracing)](../recipes/follow-one-request-across-services.md) — Carry one trace id and the caller's span across an HTTP hop, so two services' lines read as one request.
- [Keep secrets out of the logs](../recipes/keep-secrets-out-of-the-logs.md) — See a password and a card number redacted by name and by value, and prove it with a test that reads the log back.
- [Follow a flow across threads](../recipes/follow-a-flow-across-threads.md) — Keep background work inside the trace with a fork-join group, and see what it looks like without one.
- [Send traces to the observability stack you already run](../recipes/send-traces-to-the-observability-stack-you-already-run.md) — Feed the same run to your logger and to OpenTelemetry spans, side by side.
- [Trace in production without the cost](../recipes/trace-in-production-without-the-cost.md) — Keep only the runs that failed with the `Errors` level, and know what that drops.

## For AI agents

- [`llms.txt`](llms.txt) — concise index of the library for LLM consumers.
- [`llms-full.md`](llms-full.md) — complete single-file API reference.

## Philosophy

**Code is the log.** Method names, parameter names, and return values
already tell the runtime story. NarrativeTrace captures that story
automatically and renders it as a readable narrative — so you write clean,
expressive code instead of manual log statements, and reach for
annotations only in exceptional cases.
