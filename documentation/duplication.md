# Duplication detection

**English** | [Español](es/deteccion-de-duplicacion.md) | [Português](pt-BR/deteccao-de-duplicacao.md) | [简体中文](zh-CN/重复代码检测.md)

*(since 0.1.5)* — build-time tooling, not a runtime library
behavior: nothing here ships in the NuGet packages.

`./build.sh DuplicationReport` runs [jscpd](https://github.com/kucherenko/jscpd)
(a pinned exact version, invoked via `npx jscpd@<version>` — no such tool
ships already on this build's own dependency graph) over the main and
test C# sources and writes a report every commit. `./build.sh
DuplicationCheck` reads that report and enforces the duplication ratchet
described below; it is part of `Verify`.

## What is measured

- **Language: C# only, today** (the family-wide schema below covers every
  runtime).
- **Token floor: 60.** A match below 60 tokens is usually a coincidence —
  two unrelated methods that happen to share a short, common shape — not a
  structural copy worth acting on.
- **Identifiers and literals are ignored.** jscpd's `--ignore-identifiers
  --ignore-literals` flags then find *structural* duplication (the same
  shape with different names and values), not merely pasted text with the
  same names — confirmed against a fixture pair that differs only in
  identifier names and literal values: 0 clones without the two flags, 1
  clone (46% of the pair's lines) with them, at the same token floor.
  `--mode strict` is a different axis entirely (it *disables* jscpd's
  comment/whitespace normalization) and does not by itself ignore
  identifiers or literals.
- **Main and test sources are scanned separately.** The test tree
  (`tests/**` + `benchmarks/**`) is reported — its numbers are in
  `duplication.json` and the summary line — but it never gates
  `DuplicationCheck`. Test scaffolding legitimately repeats (setup, fixture
  builders, assertion blocks); a fixed threshold there would be noise, not
  signal.

## The ratchet, not a fixed percentage

A single "fail above N%" number is the wrong instrument: the right number
depends on the token floor and on how much of the tree is naturally
repetitive (data tables, generated code), so a fixed threshold ends up
either loose enough to never fire or tight enough to block unrelated work.
Instead, `DuplicationCheck` ratchets against a committed baseline,
`config/duplication/baseline.properties`:

- **Fails when the main-tree percentage rises more than 0.3 percentage
  points above the recorded baseline** — a small tolerance that absorbs
  token-count noise between runs, not real growth.
- **Fails when a non-exempt cluster is larger than the baseline's recorded
  largest cluster** — a single new large duplicate block is a finding on
  its own, even while the overall percentage stays flat.
- **The test tree never fails the check**, whatever its percentage.

Lower the baseline with the same commit that removes the duplication it
recorded. Never raise it to make a failure go away — add a reasoned
exemption instead (below), or leave the finding for a later pass.

## Exemptions are data

`config/duplication/exemptions.txt` lists deliberate duplication: code that
is intentionally structured as two parallel copies rather than one shared
abstraction. Each entry is a `globA :: globB` pair (matched against the
repository-root-relative path jscpd reports, via
`Microsoft.Extensions.FileSystemGlobbing` — already on this build's graph
through Nuke.Common) with a `# reason` line directly above it. A cluster is
exempt only when *every* one of its occurrences matches one of the pair's
two globs — a default-deny rule, so an unclassified cluster over the floor
is always a finding, never a silent pass. A pair with no reason above it,
or a malformed pair, fails the build outright rather than being ignored.

The first scan's one recorded exemption was the clarity module's word-list
dictionaries (`src/NarrativeTrace.Clarity/*Dictionary.cs`). A 2026-09-12
ruling (owner, aligning with this family's shared exemption categories)
widened this into three reasoned categories, kept as separate `globA ::
globB` lines rather than one combined pattern (this glob engine has no
brace alternation): **word tables named by file** — `TraceNamer.cs`'s
adjective/noun/verb tables, against themselves and against the clarity
dictionaries; **word tables identified by content shape rather than file
name** — `GenericTokenDetector.cs`'s and `RedactionPolicy.cs`'s
`HashSet`/array literal word lists, which cluster with each other and with
the dictionaries even though neither file matches the `*Dictionary` glob;
and **wide records** — `CanonicalEntry.cs` and `SpanContext.cs`, each a
single positional record with one nullable component per schema field,
exempted against themselves and each other since collapsing the per-field
shape would change the public constructor surface. Every one of
these pairs documents any known gap where the same glob also (necessarily)
covers real logic living beside the data it exempts, rather than silently
widening what "data, not logic" means.

## Reading the report

`artifacts/duplication/duplication.json` is the normalised result (the same
shape every NarrativeTrace runtime's duplication tooling emits, for
whichever languages it covers):

```json
{"tool":"jscpd","language":"csharp","minTokens":60,
 "main":{"tokensTotal":N,"tokensDuplicated":N,"percent":x.y,
         "clusters":[{"tokens":N,"lines":N,
                       "occurrences":[{"file":"…","startLine":N,"endLine":N}]}]},
 "test":{"...":"same shape"}}
```

`tokensDuplicated` is a **union**, not a sum over clusters — counting
tokens `× occurrences` double- and triple-counts a region that several
clusters cover (the exact bug class that measured over 300% duplication in
another runtime's first scan before its union fix), so `percent` can
never exceed 100%. jscpd tokenizes each file on its own rather than into
one shared corpus-wide stream, and its JSON report carries no
per-occurrence token index at all — only a start/end line per
occurrence, plus one token count for the whole matched fragment — so this
runtime's union runs over each file's own line ranges instead of a single
token-index space: occurrences are grouped by file, overlapping or
touching ranges within one file are merged, and each merged run is
credited once, at the *largest* token count among the ranges that formed
it, never their sum. This is exact whenever a file's duplicate ranges are
identical or non-overlapping — including the common "one span, several
partners" case a naive sum gets wrong — and only conservative for a
genuinely rare in-file overlap between two different clusters. See
`DuplicationReportSupport.UnionDuplicatedTokens` for the full reasoning.

The build log prints one summary line per run:

```
duplication: main 12.8% of tokens in 137 clusters (largest 1558 tokens
src/NarrativeTrace.Core/TraceNamer.cs:29 ↔ src/NarrativeTrace.Core/TraceNamer.cs:65)
· test 26.7% in 912 clusters (reported, not gated)
```

## A missing Node/npx is never a silent pass

jscpd is an external Node CLI, not a library already on this build's own
graph, so `DuplicationReport`/`DuplicationCheck` can hit an environment
with no Node at all. That follows the same
`ScannerGateSupport` convention `SecretsScan`/`Semgrep`/`OsvScan` already
use: locally, a missing `npx` **WARNs** and records a `skipped` status
under `artifacts/duplication/scan-status/` — never a silent green; in CI,
or wherever duplication tooling is required, absence **fails** the build.
`DuplicationCheck` itself fails loudly (not "no baseline, so pass") when
`DuplicationReport` produced no `duplication.json` — release-retrospective
rule 2: a graceful-skip tool must prove it has ever run.

Node availability by environment:

| Environment | Has Node? |
|---|---|
| The local development container | **Yes** — Node 22 (already provisioned in that container) |
| GitHub Actions CI (`ci.yml`, `ubuntu-latest`) | **Yes** — preinstalled on the runner image |
| Private CI (`mcr.microsoft.com/dotnet/sdk` image) | **No** by default — the `verify` job's `before_script` installs Node 22 via NodeSource, matching the dev container |
| Publish-verify container (the publish script, same bare SDK image, non-root) | **No** by default — a pinned Node tarball is downloaded on the host and bind-mounted read-only into the container (no root/apt needed for a non-root container run) |

## Adding an exemption

1. Run `./build.sh DuplicationReport` and find the cluster in
   `duplication.json` or the summary line.
2. Confirm it is deliberate — a genuine parallel structure kept apart on
   purpose, not duplication nobody has gotten around to removing.
3. Add a `# reason` line and a `globA :: globB` pair to
   `config/duplication/exemptions.txt`.
4. Re-run `./build.sh DuplicationCheck` to confirm it passes.

## Lowering the baseline

Remove the duplication, run `./build.sh DuplicationReport`, and update
`main.percent` / `main.largestCluster` in
`config/duplication/baseline.properties` to the newly measured numbers in
the same commit — the same "floored to measured" idiom this build already
uses for coverage and mutation-score floors.
