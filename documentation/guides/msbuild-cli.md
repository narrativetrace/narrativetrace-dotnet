# NarrativeTrace .NET — MSBuild & CLI Guide

**English** | [Español](es/guia-de-msbuild-y-cli.md) | [Português](pt-BR/guia-de-msbuild-e-cli.md) | [简体中文](zh-CN/MSBuild与CLI指南.md)

NarrativeTrace ships two build-integration surfaces: the
`dotnet-narrativetrace` command-line tool and the `NarrativeTrace.MSBuild`
package that wraps it. Together they turn naming clarity into a build gate
and forward trace configuration to the test host — the `.NET` analogue of
the Gradle plugin on the JVM side.

The division of labor is deliberate: **every decision lives in the CLI**;
the MSBuild package is a thin shim that only declares defaults and invokes
the tool. Learn the CLI first, then the MSBuild wiring falls out of it.

## Install the tool

```bash
# Global tool:
dotnet tool install --global NarrativeTrace.Cli

# …or add it to a local manifest (recommended for CI reproducibility):
dotnet new tool-manifest
dotnet tool install NarrativeTrace.Cli
```

The MSBuild targets invoke the tool by name (`dotnet-narrativetrace`), so
it must be on `PATH` (global) or restored from a local manifest before you
run `ClarityScan` / `ClarityCheck`.

## CLI verbs

`dotnet-narrativetrace <verb> [options]`. With no verb — or an unknown one
— the tool prints usage and exits `2`. Options are parsed as
`--name value` pairs (flags like `--warn-only` take no value).

### `clarity-scan`

Reflection-only scan of a compiled assembly into a clarity report. Loads
the assembly in a metadata-only context and never executes it, so it is
safe in CI.

```bash
dotnet-narrativetrace clarity-scan \
    --assembly bin/Release/net10.0/MyApp.dll \
    --output-dir narrativetrace \
    --format both
```

| Option | Required | Default | Meaning |
|---|---|---|---|
| `--assembly <path>` | yes | — | Assembly to scan. |
| `--output-dir <dir>` | no | `.` | Directory to write reports into (created if missing). |
| `--format <both\|md\|json>` | no | `both` | `json` writes `clarity-results.json`; `md` writes `clarity-report.md`; `both` writes both. |

Exit codes:

| Code | When |
|---|---|
| `0` | Scan completed and report(s) written. |
| `1` | Assembly file not found. |
| `2` | Missing `--assembly`, or unknown `--format` value. |

### `clarity-aggregate`

Merges the per-test `*.clarity.json` artifacts (written at runtime by the
test-framework integrations) under a directory into a single
`clarity-results.json` envelope. Use this when you want the gate to score
**real captured traces** — actual call depth and nesting — rather than the
depth-1 static reflection scan.

```bash
dotnet-narrativetrace clarity-aggregate \
    --input-dir narrativetrace \
    --output-dir narrativetrace
```

| Option | Required | Default | Meaning |
|---|---|---|---|
| `--input-dir <dir>` | yes | — | Directory scanned for `*.clarity.json` files. |
| `--output-dir <dir>` | no | value of `--input-dir` | Where `clarity-results.json` is written. |

Exit codes:

| Code | When |
|---|---|
| `0` | Aggregation completed (even if zero files matched). |
| `1` | Input directory not found. |
| `2` | Missing `--input-dir`. |

### `clarity-check`

The gate. Parses a `clarity-results.json` envelope and fails when any
scenario scores below `--min-score` or has more than `--max-high-issues`
HIGH-severity issues (counted case-insensitively).

```bash
dotnet-narrativetrace clarity-check \
    --results narrativetrace/clarity-results.json \
    --min-score 0.80 \
    --max-high-issues 0
```

| Option | Required | Default | Meaning |
|---|---|---|---|
| `--results <path>` | yes | — | Path to the `clarity-results.json` envelope. |
| `--min-score <x>` | no | `0.0` | Fail any scenario scoring below this overall value. |
| `--max-high-issues <n>` | no | `2147483647` (`int.MaxValue`) | Fail any scenario with more HIGH issues than this. |
| `--warn-only` | no | off | Downgrade a gate failure to a warning (exit `0`). |

Exit codes:

| Code | When |
|---|---|
| `0` | Gate passed, **or** `--warn-only` was set, **or** the results file was missing (a missing file is skipped, not failed — a project with no scan yet won't break CI). |
| `1` | Gate failed (a scenario is below `--min-score` or over `--max-high-issues`). |
| `2` | Missing `--results`, or the results file is malformed JSON. |

`--min-score` and `--max-high-issues` are parsed with invariant-culture
numerics; an unparseable value falls back to its default rather than
erroring.

## MSBuild integration

Add the build-only package. `PrivateAssets="all"` keeps it out of your
package's transitive dependencies:

```xml
<PackageReference Include="NarrativeTrace.MSBuild" Version="0.1.1"
                  PrivateAssets="all" />
```

### Properties

Override any of these in the consuming project or on the command line
(`/p:Name=Value`). The package only declares defaults — all threshold
logic lives in the CLI.

| Property | Default | Purpose |
|---|---|---|
| `NarrativeTraceOutput` | `false` | When `true`, forward `NARRATIVETRACE_*` to the test host during `VSTest`. |
| `NarrativeTraceOutputDir` | `$(MSBuildProjectDirectory)/narrativetrace` | Where scan/aggregate results and traces are written. |
| `NarrativeTraceFormat` | `markdown` | Trace output format. Valid: `markdown`, `text`, `mermaid`, `plantuml`. |
| `NarrativeTraceLevel` | `DETAIL` | Capture level for the test host. Valid: `OFF`, `ERRORS`, `SUMMARY`, `NARRATIVE`, `DETAIL`. |
| `NarrativeTraceClaritySource` | `scan` | Which producer the gate scores: `scan` (static reflection scan) or `runtime` (aggregate per-test captured traces). |
| `ClarityMinScore` | `0.0` | Forwarded to `clarity-check --min-score`. |
| `ClarityMaxHighIssues` | `2147483647` | Forwarded to `clarity-check --max-high-issues`. |
| `ClarityWarnOnly` | `false` | When `true`, forward `--warn-only` (gate failures become warnings). |

Invalid `NarrativeTraceLevel`, `NarrativeTraceFormat`, or
`NarrativeTraceClaritySource` values fail the build **early** (via the
`_NarrativeTraceValidateConfig` target, before `Build` / `VSTest` /
`ClarityScan`) with a clear message, instead of silently forwarding a typo
to the test host.

> The `NarrativeTraceFormat` allow-list here (`markdown`/`text`/`mermaid`/
> `plantuml`) is the build-side trace-render format, which differs from the
> runtime `NARRATIVETRACE_FORMAT` env values (`Markdown`/`Text`/`Prose`/
> `Json`) documented in the [Configuration Guide](configuration.md).

### Targets

| Target | Depends on | What it runs |
|---|---|---|
| `ClarityScan` | `Build` | `clarity-scan --assembly $(TargetPath) --output-dir $(NarrativeTraceOutputDir)` |
| `ClarityAggregate` | — | `clarity-aggregate --input-dir $(NarrativeTraceOutputDir) --output-dir $(NarrativeTraceOutputDir)` |
| `ClarityCheck` | `ClarityScan` or `ClarityAggregate` (per `NarrativeTraceClaritySource`) | `clarity-check --results … --min-score … --max-high-issues … [--warn-only]` |

Run the gate as part of a build:

```bash
# Static scan producer (default):
dotnet build /t:ClarityCheck /p:ClarityMinScore=0.80 /p:ClarityMaxHighIssues=0

# Score real captured traces instead of the static scan:
dotnet test   /p:NarrativeTraceOutput=true
dotnet build  /t:ClarityCheck /p:NarrativeTraceClaritySource=runtime /p:ClarityMinScore=0.80
```

When `NarrativeTraceClaritySource=runtime`, `ClarityCheck` depends on
`ClarityAggregate` (which merges the per-test `*.clarity.json` files) rather
than `ClarityScan`. Produce those per-test files by running the test suite
with `NarrativeTraceOutput=true` first, so the aggregate has something to
merge.

### Incremental check (stamp file)

`ClarityCheck` is incremental. It declares:

- **Inputs:** `$(NarrativeTraceOutputDir)/clarity-results.json`
- **Outputs:** `$(NarrativeTraceOutputDir)/clarity-check.stamp`

After a successful check it touches the `.stamp` file. On the next build,
MSBuild compares timestamps and **skips the re-check entirely when
`clarity-results.json` has not changed** — so an unchanged project doesn't
pay for the gate twice. Delete the stamp (or the output directory) to force
a re-check.

### Trace output to the test host

When `NarrativeTraceOutput=true`, the `_NarrativeTraceExportEnv` target (run
before `VSTest`) appends the runtime configuration to
`VSTestEnvironmentVariables`, so the test host sees:

```
NARRATIVETRACE_OUTPUT=true
NARRATIVETRACE_OUTPUT_DIR=$(NarrativeTraceOutputDir)
NARRATIVETRACE_FORMAT=$(NarrativeTraceFormat)
NARRATIVETRACE_LEVEL=$(NarrativeTraceLevel)
```

```bash
dotnet test /p:NarrativeTraceOutput=true /p:NarrativeTraceLevel=NARRATIVE
```

## CI recipes

### Static clarity gate (no test run required)

Scan the built assembly and fail below a threshold — the leanest gate.

```bash
dotnet build -c Release
dotnet-narrativetrace clarity-scan \
    --assembly bin/Release/net10.0/MyApp.dll --output-dir narrativetrace
dotnet-narrativetrace clarity-check \
    --results narrativetrace/clarity-results.json \
    --min-score 0.80 --max-high-issues 0
```

Or, letting MSBuild drive the whole chain in one command:

```bash
dotnet build /t:ClarityCheck /p:ClarityMinScore=0.80 /p:ClarityMaxHighIssues=0
```

### Runtime clarity gate (score real traces)

Run tests to emit per-test captures, then gate on the aggregate:

```bash
dotnet test /p:NarrativeTraceOutput=true
dotnet build /t:ClarityCheck \
    /p:NarrativeTraceClaritySource=runtime \
    /p:ClarityMinScore=0.80 /p:ClarityMaxHighIssues=0
```

### Ratchet without breaking the build

Report clarity as a warning while you drive the score up, then flip
`ClarityWarnOnly` off once you clear the bar:

```bash
dotnet build /t:ClarityCheck \
    /p:ClarityMinScore=0.85 /p:ClarityWarnOnly=true
```

## See also

- [Installation Guide](installation.md) — packages and integration paths (Options F & G)
- [Configuration Guide](configuration.md) — tracing levels, env vars, and the MSBuild property reference
- [Clarity Guide](clarity.md) — the scoring model, `clarity-results.json` contract, and gate semantics
