# Security testing

NarrativeTrace reads input it does not control. Configuration arrives from
environment variables no deployment script validates, and — above all — the
value renderer walks arbitrary object graphs eagerly, including third-party
DTOs whose `ToString()` can throw, recurse or block. The narrative it writes
is then read by a language model as often as by a person.

This document describes the suite that attacks all of that on purpose:
`NarrativeTrace.SecurityTests`, a test-only project that depends on every
project which renders or emits, so one assertion can reach the whole output
surface. It mirrors the Java runtime's `narrativetrace-security-tests`
module target for target and case for case, adapted where this runtime's own
seams differ from the JVM's — every adaptation is recorded with evidence
rather than silently dropped; see "What this runtime does not have" below.

Two tiers, one of them a real gate today:

| Tier | What it is | When it runs | Cost |
|---|---|---|---|
| **A — structured fuzz** | FsCheck properties fed by a shared hostile corpus | every `./build.sh Verify` | seconds |
| **B — coverage-guided fuzz** | SharpFuzz-instrumented targets driven by AFL++ | manual/scheduled `./build.sh Fuzz` | seconds / minutes; instrumentation-only where the `afl++` package is not provisioned (see below) |

**Every NarrativeTrace runtime mirrors these targets and this corpus.** The
corpus is data, copied between repositories verbatim the way the conformance
schemas are, so the same hostile case hits every renderer; only the corpus
reader and the object-graph builder are written per language.

## Running it

```bash
./build.sh Test --target NarrativeTrace.SecurityTests   # the suite on its own (Tier A)
./build.sh Verify                                        # Tier A, as part of the full gate
./build.sh Fuzz                                           # Tier B — see "Tier B" below
```

## The oracles

A crash is not the only defect, and "it did not throw" is not an oracle.
Every target asserts from this list; this runtime implements the same seven
Java's does, minus the one target (traceparent parsing) this runtime does not
have at all.

1. **No uncaught exception.** A hostile input degrades — it never
   propagates. This is the pipeline contract in one line: an observability
   failure must never become an application failure. Where a member
   declares a guard, the oracle is *the declared result or the declared
   exception, never a third thing*.
2. **Bounded time and size.** A narration costs O(size) of its input. No
   input hangs, and no input produces unbounded output.
3. **Well-formedness, read back by the consumer's own parser.** JSON parses
   with `System.Text.Json` *and* validates against the canonical
   `chapter-tree` schema; a Mermaid diagram has one statement per line and
   no raw control characters; Markdown frontmatter parses as YAML. An
   escaper that is merely plausible passes every eyeball test until the day
   it does not.
4. **Redaction.** A value marked `[NotTraced]` — or matched by the
   name-based deny-list — appears in **no** output of **any** format at
   **any** depth. The suite plants a fresh random token behind the
   annotation, renders the graph, drives every emitter the product ships,
   and asserts the token is in no byte of any of them, whole or by prefix.
   A partial leak through a truncating emitter is still a leak.
5. **Idempotence.** Rendering the same input twice produces the same bytes,
   so nothing has leaked in from time, an identity hash or an iteration
   order.
6. **No thread or hook left behind.** Rendering starts no background
   thread. N/A-by-construction in this runtime rather than actively checked:
   .NET has no portable "enumerate all live threads" API the way
   `Thread.getAllStackTraces()` is on the JVM, and the render path starts no
   `Task.Run`/timer to begin with — recorded as evidence
   (`Oracles.NoLibraryThreadLeft`), not silently skipped.
7. **AI-consumer containment.** An instruction-shaped value comes back as
   *exactly one value* when the output is parsed or lexed again. It never
   terminates the enclosing JSON string, Mermaid label, Markdown fence or
   frontmatter block, and never appears outside the format's value
   delimiters — so text saying "ignore previous instructions" stays inert
   data to a model reading the narrative. The oracle is deliberately not a
   filter: filtering prose is a losing game, and a narrative that quietly
   rewrites what your code returned is worse than one that quotes it
   safely.

The seventh is checked by comparing against a **benign baseline**. The same
trace is rendered twice, once with a harmless value and once with the
payload, and the document's shape, the Mermaid statement count, the
frontmatter key set and the code-fence counts must match. A well-formedness
check alone would happily accept a forged field; a shape comparison is what
makes "one value" mean something.

## The targets

In priority order, matching the Java runtime's suite numbering so the two ledgers
read across repositories.

| # | Target | The oracle that matters most | This runtime |
|---|---|---|---|
| 1 | `Traceparent` and any other wire reader | never throws; round-trips what it accepts | **N/A** — no header parser exists anywhere in this runtime; W3C header parsing is an explicit non-goal here (and in the Java runtime). `TraceparentParsingPropertyTests` is a single evidence-bearing test that fails loudly if this ever changes without the target being ported for real. |
| 2 | `ValueRenderer` over hostile object graphs | redaction, at any depth, through any container | `ValueRendererRedactionPropertyTests` |
| 3 | Every output format | well-formedness, bounded size | `OutputFormatPropertyTests` |
| 4 | Template parsing and rendering | a redacted path or object renders the marker | `TemplateRedactionPropertyTests` |
| 5 | Clarity and glossary scanners | scores stay inside their range; no crash on a name bytecode/IL produced | `ScannerPropertyTests` |
| 6 | Configuration loading | the declared result or the declared exception | `ConfigurationPropertyTests` |
| 7 | Every output format, again | AI-consumer containment | `InjectionContainmentPropertyTests` |

## What this runtime does not have

Recorded with evidence, not silently dropped:

- **No W3C `traceparent`/`tracestate` header parser** (target 1). Confirmed
  absent by repository-wide grep.
  The corpus's `headers.json` (30 traceparent + 9 tracestate cases) is still
  copied verbatim, so a future parser lands with its fuzz cases already in
  place.
- **Multi-level template paths** (`{a.b.c}`) do not resolve — this runtime's
  placeholder grammar is one level only (`{root}` or `{root.prop}`), a
  pre-existing, already-documented gap (see the repository backlog). The
  corpus's `redacted-mid-path`/`redacted-deep-path` cases therefore cannot
  exercise real multi-level redaction here; replaying them still proves the
  universal invariants (never throws, never leaks) but not depth-redaction,
  which is why `TemplateRedactionPropertyTests` asserts the marker only via
  generated single-level paths, not the corpus replay.
- **Case-sensitive property matching.** The corpus's template paths are
  written in Java's javaBean casing (`card.cvv`); this runtime's properties are
  ordinary C# PascalCase (`Card.Cvv`). None of the corpus's own redacted-path
  cases resolve against this runtime's fixtures for that reason alone — see
  `Corpus/TemplateResolution.cs`'s remarks for the full explanation and why
  it is a legitimate convention difference, not a defect.
- **No `ElementNoteComposer` wiring.** The type exists (with its own
  tokenless-identifier guard, the same guard every runtime carries) but is not
  called from `ClarityAnalyzer`'s scoring path — dead code, not reachable,
  so the crash that guard fixes (scoring a name with no readable word) does
  not reproduce here, by a different mechanism.

## The hostile corpus

`tests/NarrativeTrace.SecurityTests/HostileCorpus/` holds six JSON fixtures
with a `README.md` beside them describing every case shape — copied
byte-for-byte from the Java runtime except where this runtime has added a
case (or a file) of its own (see "Findings" below), each marked in its own
commit.

| File | What it holds |
|---|---|
| `strings.json` | hostile scalar values: control characters, bidi and zero-width, combining sequences, unpaired surrogates, template lookalikes, JSON/Mermaid/Markdown/YAML metacharacters, values up to 1 MiB |
| `headers.json` | `traceparent` and `tracestate` values (corpus only — no parser to drive them against in this runtime; see above) |
| `templates.json` | `[Narrated]`/`[OnError]` templates: nesting, unterminated braces, paths into redacted members at every depth, unicode identifiers, a whole-object placeholder (added by this runtime), a whole-object placeholder whose redacted component sits past the renderer's field/depth cap |
| `graphs.json` | declarative object-graph *shapes*: depth, width, cycles, self-reference, wrapper chains, throwing/blocking/recursive `ToString`, huge collections |
| `tree-shapes.json` | declarative `TraceNode` **call-tree** shapes (added by this runtime, 2026-09-04 tree-walk mirror): a legitimate deep chain and cyclic rings — the shape `TraceNode.Children` itself can carry, kept separate from `graphs.json` since that file is documented as feeding specifically the value renderer, not the tree walk |
| `injection.json` | prompt-injection payloads arriving as captured values: override phrasings, role and turn markers, tool-call lookalikes, link exfiltration, fence and frontmatter terminators, homoglyph variants |

### Adding a case

1. Append an object to the relevant array with a stable kebab-case `id` and
   a `description` that says **what breaks**, not what the bytes are.
2. Write hostile characters as `\uXXXX` escapes. Use `repeat` for anything
   large.
3. For an object graph, describe the *shape* (`layers` or `kind`) rather
   than serialising an object — the corpus `README.md` has the table of
   shapes. Set `payload: "secret-record"` to have the builder plant a
   redaction sentinel inside it.
4. Run the `NarrativeTrace.SecurityTests` project. The new case is picked
   up by every property that reads that fixture; nothing needs registering.
5. Note the addition in the repository backlog for back-porting to the
   Java master copy — this repository cannot write to `narrative-trace-java`
   directly.

## Tier B — coverage-guided fuzzing

`./build.sh Fuzz` instruments the top two targets (the value renderer, JSON
emission) with [SharpFuzz](https://github.com/Metalnem/sharpfuzz) and drives
them under `afl-fuzz` for a budgeted duration each
(`./build.sh Fuzz --fuzz-seconds 300` to change the budget; default 60s).

**Verified honestly, not assumed:**

- SharpFuzz's IL instrumentation is pure .NET (restored as a local tool via
  `dotnet tool restore`) and works in any container with NuGet access —
  confirmed by byte comparison of the instrumented assembly (grows by
  roughly 60%; real coverage-tracking IL, not a no-op).
- `afl-fuzz` is a native binary this repository does not ship in its own
  image; it is provisioned by `.devcontainer/Dockerfile` (Ubuntu's `afl++`
  package — genuinely packaged for this container's arm64 host, confirmed
  by installing and running it; an earlier read of this gap traced to a
  stale/never-refreshed apt cache, not an unpackaged platform). A CI job
  that wants the real coverage-guided loop builds from that image (or
  points `AFL_FUZZ` at another driver); `./build.sh Fuzz` detects the
  driver's absence, logs a clear message, and returns without failing
  rather than pretending to fuzz.
- Once `afl-fuzz` is on `PATH`, a second gap applies: AFL's own
  `check_binary()` inspects the invoked binary — here, the generic `dotnet`
  host, not the IL-instrumented DLL it loads — for compile-time AFL
  instrumentation markers, and aborts every run with "No instrumentation
  detected" because SharpFuzz's coverage signal lives in the managed IL,
  carried to afl-fuzz over the shared-memory forkserver protocol
  `SharpFuzz.Fuzzer.OutOfProcess.Run` implements
  (`fuzz/NarrativeTrace.Fuzz/Program.cs`), never in the native host binary.
  This is SharpFuzz's own documented shape — its README's own `afl-fuzz`
  invocation sets `AFL_SKIP_BIN_CHECK=1` for the identical reason, so the
  `Fuzz` target sets it too.
- **Proven fuzzing, not just instrumentation.** A real 20-second run against
  the renderer target in this container (default corpus) produced AFL's own
  `fuzzer_stats`: `execs_done: 187372`, `execs_per_sec: 9363.92`,
  `edges_found: 76`, `total_edges: 65536`, `bitmap_cvg: 0.12%`,
  `saved_crashes: 0` — a real coverage-guided loop executing the
  instrumented target at thousands of runs per second. The `Fuzz` target now
  reads and prints these same fields after every run and fails the build if
  `execs_done` is zero, so a fuzz job that returns instantly without doing
  any real work cannot report success silently.

`Fuzz` is deliberately not a dependency of `Verify` — the same relationship
`Benchmark` has to the gate. Tier A is what runs on every commit.

## From a crash to a regression test

1. **Reproduce it.** Add the input to the hostile corpus (see "Adding a
   case" above) with an id that says what it is
   (`whole-object-placeholder`, not `crash-8f3a`). The Tier A suite now
   replays it on every run, so it is a regression test from the moment it
   lands.
2. **Understand it before fixing it.** The bytes are one instance; the
   defect is a class. A corpus case describing the *shape* means every runtime
   inherits it and the generated half of Tier A explores around it.
3. **Fix it in the project that owns it**, with a named unit test there.
   The security suite proves the property across projects; the owning
   project's test is what a future reader finds when they change that code.
4. **Commit the corpus case and the fix together**, so the repository never
   contains a case that is known to fail — and note the addition for
   the shared corpus's master copy, which this repository cannot write to
   directly.

## Findings — this runtime's audit against the fixes the Java runtime landed

The Java runtime's security fuzz suite found and fixed nine defects. Every
one was checked against this runtime, not assumed safe by default. A
degraded render falling back to a value's own `ToString()` landed here
too: `NarrationResolver.PlainValue` had the exact "safe form carries no
marker ⇒ nothing is hidden" shortcut Java's own fix disproved.

## What the suite does not do

- It does not replace the property tests inside each project. Those own
  their project's behaviour; this owns the invariants that only hold across
  projects.
- It does not fuzz third-party libraries, only NarrativeTrace's own readers,
  renderers and writers.
- It does not assert on the *content* of a narrative, only on its structure
  and its containment. What a trace says is the rest of the test suite's
  business.
