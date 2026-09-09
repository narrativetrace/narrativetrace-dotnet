# Security tooling

[`security-testing.md`](security-testing.md) covers the fuzz/property suite
that attacks this library's own rendering and parsing logic
(`NarrativeTrace.SecurityTests`, mirrored across every runtime). This document
covers the other half: static and dependency scanning over the repository
itself — secrets, dangerous code patterns, and known-vulnerable packages.
None of it is mirrored across runtimes; it is this repository's own supply-chain
and secrets hygiene.

Every scanner below is a named `./build.sh` target (see `build/Build.cs`).
CI YAML (the private CI config, `.github/workflows/ci.yml`) only ever invokes
the target by name — no scan version, flag or threshold lives in a CI file
(THIN-CI rule). The cadence split is the same reason in every case: **offline
and fast → rides `Verify`, every commit; needs the network → a separate CI
tier, never per commit.**

| Tool | Target | Cadence | Why |
|---|---|---|---|
| gitleaks (staged diff) | n/a — `.githooks/pre-commit` | every local commit | instant; blocks before a secret is ever committed |
| gitleaks (full history) | `SecretsScan` | every `Verify` (every push) | git-log mode reads only committed objects — no network; ~2 s over this repo's ~800 commits |
| CA-security (CA5xxx) | rides `Analyze`/`Verify` | every `Verify` (every push) | pure Roslyn analysis — no network |
| Semgrep (`p/csharp`) | `Semgrep` | MR + scheduled CI | the ruleset is fetched from the semgrep registry over the network on every run |
| OSV-Scanner | `OsvScan` | schedule/web CI only | queries the osv.dev API |
| `dotnet list package --vulnerable` | `VulnerablePackages` | schedule/web CI only | queries nuget.org |

`scripts/install-security-tools.sh` provisions the pinned versions in CI; it
is CI-only (wants root, fails loudly on a download error) — the opposite of
what a developer's machine should do when a tool happens to be missing.

## A skipped scan is not a clean scan

`SecretsScan`, `Semgrep` and `OsvScan` used to warn and pass outright when
their binary was absent — a green build that looked like "secrets/static
analysis passed" when nothing was checked, the exact failure class a family
release retrospective pinned as rule 2 ("a graceful-skip tool must prove it
has ever run") and the one that bit this repository's own first release,
where a secrets scanner had gracefully skipped for the project's entire
life. Since 2026-09-08, all three behave the same way when their binary is
missing (`ScannerGateSupport` in `build/`, unit-tested in
`tests/BuildScript.Tests`):

- **Locally**: the target **warns** ("a skipped scan is NOT a clean scan")
  and still passes, so a machine without the tools keeps a working build.
- **In CI (the bare `CI` environment variable, which GitLab CI and GitHub
  Actions both set automatically), or under `--security-required`**: the
  target **fails** — a job meant to provide security assurance must mean the
  scan ran.
- Every outcome is recorded under
  `artifacts/security/scan-status/<tool>.status` as `ran-clean` or
  `skipped: <reason>` (no file at all reads as `never-ran`), so "ran clean"
  and "never ran" stay distinguishable after the fact.

## gitleaks — secrets

Two layers, closing each other's gap:

- **Pre-commit hook** (`.githooks/pre-commit`) runs `gitleaks protect
  --staged` on every commit, over every staged file regardless of type (not
  just `.cs`) — it scans the diff about to be committed and blocks on a
  finding. Bypass deliberately with `git commit --no-verify`.
- **`SecretsScan`** runs `gitleaks detect` (default git-log mode) over the
  full commit history and rides `Verify` — it catches anything that made it
  into history through a path that skipped the hook (a fresh clone with
  hooks not installed, `--no-verify`, or a commit made before this tooling
  existed).

A known-fake secret in a test fixture (this repository's redaction tests
construct JWT- and API-key-shaped literals on purpose, to prove the redactor
catches them) is suppressed with an inline `// gitleaks:allow` comment on the
literal, or a fingerprinted `.gitleaksignore` entry with a one-line reason —
never a blanket path exclusion.

**Evidence (2026-09-02, this container):** a full-history sweep
(`gitleaks detect --source .`) over all ~813 commits found **zero leaks** in
~2 s — consistent with the regex sweep this repository had recently. A
separate, informational working-tree scan (`--no-git`, not part of any gate)
additionally surfaced two JWT-shaped literals in *not-yet-committed* test
files under `tests/NarrativeTrace.Core.Tests/` (`RedactionPolicyTests.cs`,
`RedactionValueShapeRenderingTests.cs`) belonging to unrelated in-flight
work — both are intentional fake-secret fixtures for the redactor's own
tests, not real leaks. No `.gitleaksignore` entry was added for them here:
gitleaks fingerprints in git-history mode are commit-specific, so an entry
written against an uncommitted file cannot be stable — whoever commits that
work should add the inline `gitleaks:allow` marker (or the fingerprinted
ignore entry, once the commit SHA exists) alongside it.

## CA-security (CA5xxx)

`Directory.Build.props` sets `<AnalysisModeSecurity>All</AnalysisModeSecurity>`.
The default `AnalysisLevel=latest-recommended` leaves most of the CA5xxx
security category (crypto misuse, XML XXE, deserialization, injection) off
by default — these are dataflow-heavy rules considered too noisy for the
general recommended set. This repository turns the whole category on
instead of cherry-picking; it costs nothing extra to run (pure Roslyn
analysis, already part of every `Analyze`/`Verify`) and a rule that never
fires costs nothing to have enabled.

**Evidence (2026-09-02):** a full solution build with `AnalysisModeSecurity=All`
produced **zero** CA5xxx/CA3xxx findings. A real finding gets fixed, or — if
it is a false positive — promotes to a scoped `#pragma warning disable
<rule> // reason` at the flagged line, the same idiom already used for
`S3877`/`S3871`/`S1144` elsewhere in this codebase; nothing is promoted to
`error` in `.editorconfig` (the CA1305 precedent) until a real finding
justifies it.

## Semgrep — `p/csharp`

Community OSS ruleset only (owner ruling: custom rules are out of scope —
"tooling public, findings private"). Run as `semgrep --config=p/csharp
--metrics=off --error --json --output <path> <repo>`; `--error` is required
for a nonzero exit on a finding (semgrep's default is 0 even with findings).

**Evidence (2026-09-02):** the first run found 2 findings, both
`csharp.lang.security.filesystem.unsafe-path-combine` at
`src/NarrativeTrace.Core/SuiteReportWriter.cs:49,51` — `Path.Combine(outputDir,
<compile-time-constant filename>)`. Triaged as noise: `outputDir` is a
caller-supplied report directory from test/build configuration, never
externally-facing input, and both filenames are compile-time constants — the
rule cannot see that trust boundary. Suppressed inline with
`// nosemgrep: csharp.lang.security.filesystem.unsafe-path-combine.unsafe-path-combine`
on each flagged line, with the one-line reason in a comment above both
calls. Re-run after the suppression: **0 findings.**

**Evidence (2026-09-07):** the glossary translation stack earned the same rule
once more, at `src/NarrativeTrace.Glossary/GlossaryLoader.cs:96` —
`Path.Combine(baseDirectory, BaseDirectoryFileName)` feeding
`File.ReadAllText`. Same triage, and the rule's own mechanism rules it out:
`Path.Combine`'s traversal hazard is a *trailing* segment that reroots the
path (`Path.Combine("/safe", "/etc/passwd")` is `/etc/passwd`), whereas the
tainted metavariable here is the **leading** directory and the trailing
segment is the compile-time constant `glossary.json`. The read is therefore
always `<baseDirectory>/glossary.json`; no caller-supplied value can name a
different file, and `Path.GetFileName` — the only sanitizer the rule accepts —
would discard the directory and break discovery outright. Every in-tree caller
passes `AppContext.BaseDirectory` (`GlossaryLoader.Load()`,
`DemoTraces.WriteTranslated`) or a test temp directory. Suppressed inline on
the flagged line with the reason above it; the invariant is pinned by
`GlossaryLoaderTests.Base_directory_selects_no_file_other_than_glossary_json`,
so a change that makes the probed name caller-derived fails a test rather than
silently re-earning the finding. Re-run after the suppression: **0 findings**
across 248 files.

The `NARRATIVETRACE_GLOSSARY_PATH` override beside it reads an arbitrary
absolute path and is *not* flagged (no `Path.Combine`), correctly: it is
process configuration, the same trust tier as Java's
`narrativetrace.glossary.path` system property, and pointing it at a file is
the whole point of the key.

## OSV-Scanner + `dotnet list package --vulnerable`

Two independent sources for the same question — OSV-Scanner reads every
`*.csproj`/`Directory.Build.props` `PackageReference` directly (no lockfile
needed; this repository does not use `packages.lock.json`) against the OSV
database, `dotnet list package --vulnerable --include-transitive` walks the
restored dependency graph against NuGet's own advisory feed. Neither
overlaps `DependencyAudit` (NuGet's build-time `NuGetAudit`/`NuGetAuditMode`
gate, already `Yes`/blocking, offline via the local package cache) exactly —
OSV and the NuGet advisory feed are different sources that can each catch
what the other misses.

**Evidence (2026-09-02):** `osv-scanner scan source --recursive .` and
`dotnet list package --vulnerable --include-transitive` both report **zero**
vulnerable packages across every project in the solution.

## What a maintainer must do that this container could not

This container has no root, no `apt`/`pip`/`ensurepip`, no `go`, and no
`docker` — none of gitleaks, osv-scanner or semgrep are installable as system
packages here. The evidence above was still gathered for real, not skipped:
gitleaks and osv-scanner are single static binaries fetched directly from
their GitHub release assets into `/tmp` (arm64 — this container's
architecture); semgrep needed a `python3 -m venv` + a manually bootstrapped
`pip` (via `https://bootstrap.pypa.io/get-pip.py`, since the system Python
has no `ensurepip` module) before `pip install semgrep` would run. None of
that setup is committed — `scripts/install-security-tools.sh` is the
committed, CI-facing equivalent, written for a normal root-and-apt CI
container rather than this sandbox.

A maintainer setting up a new local dev container that wants these scans to
run for real (rather than the local warn-and-pass) should install:

- **gitleaks** — a release binary on `PATH` (see
  <https://github.com/gitleaks/gitleaks/releases>), or `brew install
  gitleaks`.
- **osv-scanner** — a release binary on `PATH` (see
  <https://github.com/google/osv-scanner/releases>), or `brew install
  osv-scanner`.
- **semgrep** — `pip install semgrep` (or `pipx install semgrep`, or `brew
  install semgrep`) in an environment where `pip` is available.

CI provisions all three itself via `scripts/install-security-tools.sh`
(pinned versions there, not in CI YAML) — a maintainer never needs any of
the above just to land a change; it only helps a scan run locally instead of
degrading to its warn-and-pass fallback.
