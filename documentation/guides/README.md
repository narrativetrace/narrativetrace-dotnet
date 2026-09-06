# NarrativeTrace .NET — User Guides

**English** | [Español](es/guias-de-usuario.md) | [Português](pt-BR/guia-de-usuario.md) | [简体中文](zh-CN/用户指南.md)

Task-oriented guides for using NarrativeTrace in a .NET project. For the
value proposition and a 60-second quick start, see the
[repository README](../../README.md).

| Guide | Read it to… |
|---|---|
| [Installation](installation.md) | Add packages and pick an integration path (proxy, DI, ASP.NET Core, test frameworks, CLI, MSBuild). |
| [Configuration](configuration.md) | Set tracing levels, env vars, DI/ASP.NET Core options, MSBuild properties, and redaction. |
| [Annotations](annotations.md) | Use `[Narrated]`, `[OnError]`, `[NotTraced]`, `[Traced]`, and `[NarrativeSummary]`. |
| [Dependency Injection](dependency-injection.md) | Auto-wrap namespace-matched interface services in the MS.DI container (Spring/Micronaut-style bean tracing). |
| [ASP.NET Core Integration](aspnetcore.md) | Wire the per-request middleware, exporters, path exclusion, and user context. |
| [Clarity](clarity.md) | Score naming quality and enforce it as a CI gate. |
| [MSBuild & CLI](msbuild-cli.md) | Run the `dotnet-narrativetrace` verbs and wire the clarity gate into `dotnet build` / `dotnet test`. |

## For AI agents

- [`llms.txt`](llms.txt) — concise index of the library for LLM consumers.
- [`llms-full.md`](llms-full.md) — complete single-file API reference.

## Philosophy

**Code is the log.** Method names, parameter names, and return values
already tell the runtime story. NarrativeTrace captures that story
automatically and renders it as a readable narrative — so you write clean,
expressive code instead of manual log statements, and reach for
annotations only in exceptional cases.
