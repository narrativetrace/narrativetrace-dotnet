# The contract gate

`documentation/contract.yaml` is a machine-readable list of claims this
repository's documentation makes — a default, an entry point, a config
shape, or the effect a documented shape produces. `contract-probe/` proves
or disproves each one against a **published** NuGet install (nuget.org,
never a local feed, never this checkout's own build output), so a doc page
and the package someone actually downloaded can never quietly disagree
without a gate noticing.

Every entry describes the code it is committed with, exactly like every
other document in this repository (owner ruling 2026-09-24). **The contract
describes the code on `main`, which is the published code**: development is
trunk-based and publishing is one step — the public snapshot and the
artifacts go out from the same commit — so the claims `ContractCheck` reads
from `documentation/contract.yaml` in the working tree and the package it
installs and probes are the same code.

## What it catches

Four kinds of claim, each checked a different way:

| `kind` | What it proves | Example |
|---|---|---|
| `entry-point` | A package resolves at all, on the real registry, at the version under test | `NarrativeTrace.Core` |
| `reflectable-default` | A public API already says what the docs claim — no execution needed beyond calling it | `ConfigResolver.Resolve(...).Output` is `true` |
| `probed-default` | A default only visible by running a real traced call and reading what it produced | a `password` parameter redacts under the default policy |
| `config-shape` | A documented configuration shape produces the effect the docs claim | `NarrativeTrace.Proxy`'s published `.nuspec` depends on `.Runtime` |

There are two verdicts, `holds` and `fails` — nothing else. Because `main`
is the published code, every entry applies to the artifact under test; a
claim can never be "not applicable yet".

## Two gates, two cadences

- **`./build.sh ContractLint`** — part of `Verify`, every commit, no
  network. Validates `documentation/contract.yaml` itself: the schema
  parses, no two entries make the same claim, every entry's `probe` file
  exists, and every `page#anchor` pointer resolves to a heading that
  actually exists on that page.
- **`./build.sh ContractCheck`** — nightly, registry-backed, never per
  commit (the same "no network in the per-commit gate" rule
  [`security-tooling.md`](security-tooling.md) describes for the scanners).
  Resolves the version to install (the newest `v*` release tag by default, or
  `--contract-check-version`), reads `documentation/contract.yaml` **from the
  working tree**, installs the package into a **fresh temporary NuGet package
  cache** — never this checkout's own `~/.nuget/packages`, never a local feed
  standing in for the real answer — and runs `contract-probe/` against it.
  Exits non-zero on any `fails`.

Run the nightly gate by hand against a specific version:

```bash
./build.sh ContractCheck --contract-check-version <version>
./build.sh ContractCheck                                 # omit the version: the newest v* tag
```

A failure names the facts in one line, so a skim is enough:

```
documentation/contract.yaml: reflectable-output-default documented default "true"
but NarrativeTrace.Core <version> (published) reads "false"
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
dotnet run -c Release -p:ContractVersion=<version> --no-launch-profile -- \
    --version=<version> --contract=../documentation/contract.yaml --out=contract-result.json
```

Each contract entry dispatches to one probe class under
`contract-probe/Probes/` — the entry's `probe` field names it, and
`ContractLint` fails if that file does not exist. A probe returns one
observed string; the entry holds when it equals `documented_default` (or
`expected_effect`, the name the design note uses for a `config-shape`
entry — both land in the same field).

**Resolving newer APIs by name.** `contract-probe` compiles every probe
class together against whichever single version is under test. A probe must
never reference a type or member that a given release does not have, or the
project would fail to compile against that version at all. So a probe reaches
for API that only some releases carry — a source-level `[NotTraced]` on a
method, `ProxyOptions.Redaction`, `ResolvedConfig.Approval` and
`NarrativeTrace.Core.ArtifactIdentity` — by name through reflection
(`Type.GetType`, `PropertyInfo`, `ConstructorInfo`) rather than a direct
reference, so the one compiled probe assembly builds against every version
the nightly gate might run it against. See `NotTracedOnMethodRejectedProbe`,
`RedactionDisabledOptions`, `ApprovalDefaultProbe` and
`ReflectablePerInvocationIdentityProbe` for the pattern.

## Adding an entry

`contract.yaml` is updated **in the same commit** as the feature that ships
a new documented default. Adding one:

1. Write the sentence in the doc page first, in the present tense — the doc
   describes the code it is committed with.
2. Add the entry to `documentation/contract.yaml`: `id`, `kind`, `page`
   (the doc path and the anchor of the heading carrying the sentence),
   `claim`, `documented_default`/`expected_effect`, and `probe`.
3. Write the probe class under `contract-probe/Probes/`. Reach for any API
   through reflection rather than a direct reference (see "Resolving newer
   APIs by name" above) so the project still compiles against every version
   the nightly gate might run against.
4. `./build.sh ContractLint` — confirms the shape and the anchor.
5. Once the feature is released, `./build.sh ContractCheck` reads the same
   working-tree contract and confirms the new entry `holds` against the
   published package — land the entry with the feature and let the release
   close it.

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

## See also

- [Duplication Detection](duplication.md) — the other ratchet-style gate
  this repository runs the same way: a report every commit, an enforcement
  task wired into `Verify`.
- [Security Tooling](security-tooling.md) — the per-commit/nightly split
  this gate follows for the same reason (network calls do not belong in a
  gate every commit waits on).
- [Sixty Seconds](sixty-seconds.md) — the tutorial project the entry-point
  and config-shape probes exercise the same package chain against.
