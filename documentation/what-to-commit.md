# What to Commit

**English** | [Español](es/que-incluir-en-el-commit.md) | [Português](pt-BR/o-que-incluir-no-commit.md) | [简体中文](zh-CN/应提交的内容.md)

Once tracing is running, you'll have generated files on disk. This page
says which ones are throwaway output and which ones are meant to be
reviewed and committed — verified against what this runtime's writers actually
produce, not assumed.

## The artifacts, one by one

| Artifact | Commit? | Why |
|---|---|---|
| `<output-dir>/traces/<Class>/<slug>.md` | No | Regenerated every run; the human-readable trace for one test. |
| `<output-dir>/traces/<Class>/<slug>.json` | No | The same trace as a JSON chapter document — regenerated every run. |
| `<output-dir>/diagrams/<Class>/<slug>.mmd` | No | Mermaid sequence diagram companion — regenerated every run. |
| `<output-dir>/structural/<Class>/<slug>.nt` | No, today | Value-free structural trace (names, hierarchy, outcome kind only). Deterministic and diffable by construction, but **nothing in this runtime reads it back yet** — there is no approval-mode or delta-comparison loop against a previous run, so there's no committed baseline for it to compare against. Regenerated every run like the rest. |
| `<output-dir>/traces/<Class>/<slug>.canonical.json` | No | Opt-in (`NARRATIVETRACE_CANONICAL_JSON=true`) conformance fixture, intended for testing NarrativeTrace itself against the canonical schema — not something an application project needs to keep. |
| `<output-dir>/traces/<Class>/<slug>.structural.json` | No | Opt-in (`NARRATIVETRACE_STRUCTURAL_JSON=true`) value-free entry array — same reasoning as `.canonical.json`. |
| `<output-dir>/clarity-results.json` | No | Generated suite-level clarity report (machine-readable). Appears whenever the suite fixture ran and accumulated at least one entry, independent of `NARRATIVETRACE_OUTPUT` — regenerate, don't commit. |
| `<output-dir>/clarity-report.md` | No | Same report, human-readable. |
| `clarity/clarity-scan-results.json` / `clarity-scan-report.md` (from `dotnet-narrativetrace clarity-scan`) | No | A static, reflection-only scan of a compiled assembly — regenerate in CI, don't commit. |
| `glossary.json` | **Yes**, if you use glossary harvesting | See below — this is the one artifact this runtime treats as a reviewed, hand-curated file. |
| `glossary.md` | **Yes**, alongside `glossary.json` | Human-readable rendering of the same file, rewritten only when the JSON's bytes change (anti-churn). |
| `<output-dir>/glossary-usage.json` | No | Volatile per-run usage statistics — regenerated, not curated. |

`<output-dir>` defaults to `./narrativetrace-output` when
`NARRATIVETRACE_OUTPUT_DIR` isn't set. Add it to `.gitignore` unless you
have a specific CI reason to archive it as a build artifact (which is a CI
retention decision, not a "commit to source control" one).

## Glossary harvesting is opt-in by file presence

Unlike everything else on this page, glossary harvesting doesn't spontaneously
create anything: `GlossarySuiteReporter` silently does nothing unless
`glossary.json` **already exists** at the location it resolves (an upward
directory search from the test run, the `NARRATIVETRACE_GLOSSARY`
environment variable, or the literal value `off` to disable the feature
outright). If you want harvesting, commit a starting file yourself:

```json
{
  "schemaVersion": 1,
  "contexts": {},
  "terms": []
}
```

From then on, every suite run harvests new vocabulary from its traces,
merges it additively into `glossary.json`, and regenerates `glossary.md` —
review the diff like any other hand-curated file. A malformed
`glossary.json` throws rather than being silently skipped, which is
deliberate: a typo in a committed, reviewed file should fail loudly.

## What this runtime does not have yet

Approval mode is not here yet: there are no `.approved.nt` / `.received.nt`
files, no `approve` verb, and nothing that compares one run's structural
trace against a previous one. The `.nt` structural artifact exists and is
deterministic, but every artifact on this page is regenerate-only output
today — there is no "committed baseline that fails a build on an
unreviewed behavior change" workflow to opt into yet.

## See also

- [Privacy and Redaction](privacy-and-redaction.md) — what's inside these
  files before you decide whether to archive them anywhere.
- [Configuration Guide](guides/configuration.md) — the `NARRATIVETRACE_*`
  variables that control where and whether these files are written.
