# The contract gate

`documentation/contract.yaml` is a machine-readable list of claims this
repository's documentation makes — a default, an entry point, a config
shape, or the effect a documented shape produces. `contract-probe/` proves
or disproves each one against a **published** NuGet install (nuget.org,
never a local feed, never this checkout's own build output), so a doc page
and the package someone actually downloaded can never quietly disagree
without a gate noticing.

This is the third leg of the docs-vs-published family, alongside the
`*(since X.Y.Z[, unreleased])*` markers inline in prose and the generated
banner under `llms.txt`'s own index — see those markers throughout
`documentation/*.md` for the disclosure half of the same problem. This page
is the enforcement half: a marker says "this is new"; the contract gate says
"and it is really true of what shipped."

## What it catches

Four kinds of claim, each checked a different way:

| `kind` | What it proves | Example |
|---|---|---|
| `entry-point` | A package resolves at all, on the real registry, at the version under test | `NarrativeTrace.Core` |
| `reflectable-default` | A public API already says what the docs claim — no execution needed beyond calling it | `ConfigResolver.Resolve(...).Output` is `true` |
| `probed-default` | A default only visible by running a real traced call and reading what it produced | a `password` parameter redacts under the default policy |
| `config-shape` | A documented configuration shape produces the effect the docs claim | `NarrativeTrace.Proxy`'s published `.nuspec` depends on `.Runtime` |

Each entry also carries `since`: the version the claim first holds. An entry
whose `since` is **later than the version actually installed** that run is
reported `not-applicable-before-since` — never `fails` — so a documented
default for a feature that has not shipped yet does not fail the gate before
its own release does. The exemption is keyed on the version genuinely
installed, never on this repository's own `Directory.Build.props` version,
which stays at the last published number until a release tag bumps it (see
the `*(since X.Y.Z, unreleased)*` markers already on many pages).

## Two gates, two cadences

- **`./build.sh ContractLint`** — part of `Verify`, every commit, no
  network. Validates `documentation/contract.yaml` itself: the schema
  parses, every `since` is a real version string, no two entries make the
  same claim, every entry's `probe` file exists, every `page#anchor` pointer
  resolves to a heading that actually exists on that page, and every
  `*(since X.Y.Z, unreleased)*` marker anywhere in the English docs has at
  least one contract entry recording that version — the mechanical link
  between the inline markers and this file.
- **`./build.sh ContractCheck`** — nightly, registry-backed, never per
  commit (the same "no network in the per-commit gate" rule
  [`security-tooling.md`](security-tooling.md) describes for the scanners).
  Resolves the version to check the way the `llms.txt` banner does (a fresh
  cache hit, else nuget.org's flat-container index for
  `NarrativeTrace.Core`), installs it into a **fresh temporary NuGet
  package cache** — never this checkout's own `~/.nuget/packages`, never a
  local feed standing in for the real answer — and runs `contract-probe/`
  against it. Exits non-zero on any `fails`.

Run the nightly gate by hand against a specific version:

```bash
./build.sh ContractCheck --contract-check-version 0.1.3
./build.sh ContractCheck                                 # omit the version: checks the last published one
```

A failure names all four facts in one line, so a skim is enough:

```
documentation/contract.yaml: reflectable-output-default documented default "true"
(since 0.1.5) but NarrativeTrace.Core 0.1.5 (published) reads "false"
```

## `contract-probe/`

A small, **standalone** .NET console project — its own `.csproj`, its own
`nuget.config` (`<clear/>` then exactly `nuget.org`) — deliberately not part
of `NarrativeTrace.sln` and never referenced as a project by anything that
is. That separation is the point: it consumes only `NarrativeTrace.*`
packages resolved from nuget.org at a version given on the command line (the
`ContractVersion` MSBuild property), so what it proves is true of what a
consumer would actually download, never of this checkout's own build
output. It ships in the public snapshot: it is real code proving a
documented claim, not private verification machinery.

Run it directly:

```bash
cd contract-probe
dotnet run -c Release -p:ContractVersion=0.1.3 --no-launch-profile -- \
    --version=0.1.3 --contract=../documentation/contract.yaml --out=contract-result.json
```

Each contract entry dispatches to one probe class under
`contract-probe/Probes/` — the entry's `probe` field names it, and
`ContractLint` fails if that file does not exist. A probe returns one
observed string; the entry holds when it equals `documented_default` (or
`expected_effect`, the name the design note uses for a `config-shape`
entry — both land in the same field).

**Compiling against an older published version.** `contract-probe` compiles
every probe class together against whichever single version is under test.
A probe for a claim whose `since` is newer than some other entry's therefore
still has to *compile* against that older package — even though it will
never *run* there (the applicability check skips it) — so a probe must never
reference a type or member that only exists in a newer release: a source-level
`[NotTraced]` on a method, `ProxyOptions.Redaction`, `ResolvedConfig.Approval`
and `NarrativeTrace.Core.ArtifactIdentity` are all new in `0.1.5` and are
found by name through reflection (`Type.GetType`, `PropertyInfo`,
`ConstructorInfo`) rather than a direct reference, precisely so the same
compiled probe assembly still builds against `0.1.3`. See
`NotTracedOnMethodRejectedProbe`, `RedactionDisabledOptions`,
`ApprovalDefaultProbe` and `ReflectablePerInvocationIdentityProbe` for the
pattern.

## Adding an entry

`contract.yaml` is updated **in the same commit** as the feature that ships
a new documented default. Adding one:

1. Write the sentence in the doc page first, with its
   `*(since X.Y.Z, unreleased)*` marker if the version has not tagged yet.
2. Add the entry to `documentation/contract.yaml`: `id`, `kind`, `page`
   (the doc path and the anchor of the heading carrying the sentence),
   `claim`, `since`, `documented_default`/`expected_effect`, and `probe`.
3. Write the probe class under `contract-probe/Probes/`. If the claim is
   newer than another entry's `since`, use reflection rather than a direct
   reference to the new API (see "Compiling against an older published
   version" above) so the project still compiles against the older version
   the nightly gate might run against next.
4. `./build.sh ContractLint` — confirms the shape, the anchor and the
   since-marker link.
5. `dotnet run -c Release -p:ContractVersion=<last published>` from
   `contract-probe/` — confirms the new entry reports
   `not-applicable-before-since` against today's published version (it
   should, if the feature has not released yet) and, once released, reports
   `holds` against the version it landed in.

## What this deliberately does not cover

- **Prose accuracy outside `contract.yaml`.** A documented explanation that
  is simply wrong, incomplete, or confusing is a different failure mode —
  review catches that, not a runtime probe.
- **Anything the sixty-seconds tutorial's own test already owns** — see
  `tests/NarrativeTrace.Examples.SixtySeconds.Tests`; `contract.yaml` is for
  defaults and shapes documented elsewhere, not a second copy of that
  page's own proof.
- **Full behavioral equivalence of a complex config object** — one named,
  checkable `expected_effect` per `config-shape` entry, never a spec of the
  whole feature the shape configures.
- **A marker correctly flagged `unreleased` for a version genuinely ahead of
  the one installed** — that is disclosure's job (the inline marker and the
  generated banner), not this gate's; a `since` later than the installed
  version is skipped, on purpose, every time.

## See also

- [Duplication Detection](duplication.md) — the other ratchet-style gate
  this repository runs the same way: a report every commit, an enforcement
  task wired into `Verify`.
- [Security Tooling](security-tooling.md) — the per-commit/nightly split
  this gate follows for the same reason (network calls do not belong in a
  gate every commit waits on).
- [Sixty Seconds](sixty-seconds.md) — the tutorial project the entry-point
  and config-shape probes exercise the same package chain against.
