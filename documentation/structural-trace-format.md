# Structural Trace Format (`.nt`)

**English**

The AI-safe structural trace artifact (ADR-002): one file per test scenario
containing only the developer-authored *shape* of the behavior — zero
runtime values. This format is **cross-platform**: every NarrativeTrace
runtime emits the identical format, which is what lets approval traces and
conformance fixtures travel between platforms — golden fixtures from the
reference format are what this port's own conformance test
(`StructuralTraceConformanceTests`) pins its renderer against, byte for
byte.

## Files and naming

| File | Role |
|---|---|
| `<output-dir>/structural/<TestClass>/<scenario>.nt` | Emitted on the Markdown path; the file on disk is the **last-green baseline** — a non-green run compares against it (console delta, failure report) but never overwrites it. "Green" is the whole verdict: a test that passed but whose structure approval *rejected* ends red, so a rejected structure never becomes the baseline and reverting the change reports no delta |
| `<approvedDir>/<TestClass>/<scenario>.approved.nt` | The committed approved trace (`NarrativeApproval`; opt-in via `NARRATIVETRACE_APPROVAL=true`, directory configurable via `NARRATIVETRACE_APPROVED_DIR`, default `narratives`) — a passing test whose structure differs fails with a readable diff |
| `<scenario>.received.nt` | Written beside the approved trace on a mismatch (or when no approved trace exists yet); review it, then promote via the `Approve` build target (`./build.sh Approve`) |

The format extension is last (`.approved.nt` — the approval-testing idea,
applied to traces) so editors and diff viewers key off `.nt`. Note: `.nt`
collides with RDF N-Triples in some syntax-highlighting maps; register an
override in `.gitattributes` where it matters.

### Artifact identity (cross-platform)

`<scenario>` above is the **artifact identity** of one test invocation, and
every runtime spells it the same way — an artifact written by one runtime
is found under the same name by another:

- An ordinary test method is its slugged name: camel-case split on `_`,
  lowercased, everything outside `[a-z0-9_]` replaced with `_` —
  `PlacesOrder` → `places_order`.
- One invocation of a method that runs more than once (a `[Theory]` case,
  or an NUnit test NarrativeTrace auto-detects from
  `TestContext.TestAdapter.Arguments`) appends `-<index>-<label>`: the
  1-based invocation number zero-padded to three digits, then the
  invocation's display name through the same slug rule with runs of `_`
  collapsed and the ends trimmed — `equipment_can_be_found-002-find_tent`.
  A label that slugs to nothing is dropped, leaving
  `equipment_can_be_found-002`.
- `-` is the separator precisely because the slug alphabet cannot produce
  one. The index — not the label — is what makes the scheme
  collision-proof: two invocations always differ in it, so display names
  that differ only in characters a path cannot carry
  (`find/TENT` versus `find TENT`) still get separate files. The label is
  what makes the name readable.
- The name is stable across runs, machines and processes, which is what
  lets one invocation's `.approved.nt` be committed at all. Where a name
  exceeds the 255-byte path-element limit the *method* half is truncated
  and given eight hex characters of the (Java-specified, reimplemented
  here rather than `string.GetHashCode()`, which .NET randomizes per
  process) `String.hashCode` of the full slug — specified, therefore
  identical everywhere; a per-process hash would silently invalidate every
  approved trace it touched.

Because artifact names are derived rather than announced, a run also writes
`<outputDir>/manifest.json`: a top-level `run` object (`id`, `name` — the
run's own three-word phrase *(since 0.1.4, unreleased)*, see
[Configuration Guide §7](guides/configuration.md#7-logging-bridge-microsoftextensionslogging))
followed by one row per traced scenario naming its test, its invocation
number and every file it owns. Read that when you know the scenario and
want the file.

> An invocation's `scenario:` header is **not** its display name *(since
> 0.1.4, unreleased)*. A `[Theory]`/data-driven test's display name can
> interpolate arguments into itself, so this artifact — the value-free one
> — is titled by the method and the invocation number instead: `Equipment
> can be found #2`. A method that runs once keeps the display name it
> always had, so no committed baseline moves. The *filename* still carries
> the slugged label, because that is what tells two invocations apart on
> disk, and `manifest.json` — an index over the value-carrying artifacts
> too — names the scenario as the runner displayed it. Keep secrets out of
> display-name templates.

## Content

```
scenario: Weekend trip settles with three transfers

- TripSettlementService.RecordExpense(tripName, expense)
  - ExpenseValidator.EnsureValid(expense)
  - TripLedger.RecordExpense(tripName, expense)
- TripSettlementService.SettleTrip(tripName) → value
  - TripLedger.ExpensesOf(tripName) → value
  ~ fork [2]
    - BalanceCalculator.ComputeBalances(expenses) → value
    - StockService.Check() → value
```

- **Header:** `scenario: <humanized test name>` + blank line. Nothing
  else — no result, no trace ids/names, no dates. One invocation of a
  method that runs more than once is `scenario: <humanized method name>
  #<index>` — a display-name template's arguments never reach it.
- **Call line:** `ClassName.MethodName(paramName, paramName)` — names
  only, capture order, two-space indent per depth.
- **Outcome kinds:** non-void return ` → value`; void: nothing; thrown
  ` !! ExceptionSimpleName` (type is structure; the message is a value and
  never appears); unmatched enter ` ?? incomplete`.
- **Concurrency:** fork groups render `~ fork [n]` and work adopted from a
  propagated context snapshot renders `~ async [n]`, both with members
  **sorted by `Class.method`** — capture order across threads is the
  scheduler's choice, not behaviour, so the artifact states the set and
  nesting of concurrent work and never its order. Fire-and-forget renders
  `~ fire-and-forget` + children. Thread names/ids never appear.
- **Excluded by design:** all argument/return values, exception messages,
  durations, timestamps, thread identity, trace/span ids, trace names, run
  ids, run names *(since 0.1.4, unreleased — a test-suite run has a name
  too, see [Configuration Guide §7](guides/configuration.md#7-logging-bridge-microsoftextensionslogging);
  it never enters this format, an approved or received trace, an artifact
  filename, or a manifest per-scenario key)*, run results, and narration.
- **Encoding:** UTF-8 without a BOM, LF, trailing newline. Identifiers pass
  through control-character sanitization.

## Guarantees

1. **Deterministic:** identical behavior ⇒ byte-identical file. This is
   what makes the artifact the approved-trace baseline and the
   conformance-fixture golden format.
2. **Value-free:** zero prompt-injection surface, zero PII, minimal
   tokens — safe to hand to an AI agent by default.
3. **Division of labor:** the artifact asserts behavioral *shape*; value
   correctness remains the job of test assertions. A change that only
   alters a return value with identical structure does not change the
   artifact — by design.

Implemented in this repository by `NarrativeTrace.Core.StructuralTraceRenderer`,
fed by the same tree `TraceArtifactWriter` writes the `.md`/`.json`/`.mmd`
companions from, with last-green comparison, per-invocation identity and
approval traces in `ArtifactIdentity`, `ScenarioManifest`, `StructuralDelta`/
`ScenarioDelta` and `NarrativeApproval` (all `NarrativeTrace.Core`).

## See also

- [What to Commit](what-to-commit.md) — which of these files are reviewed,
  committed baselines and which are regenerate-only output.
- [Privacy and Redaction](privacy-and-redaction.md) — the structural
  artifact's value-free guarantee, and what it does not cover (test names).
- [Configuration Guide](guides/configuration.md) — `NARRATIVETRACE_APPROVAL`
  / `NARRATIVETRACE_APPROVED_DIR` and the rest of the `NARRATIVETRACE_*`
  surface.
