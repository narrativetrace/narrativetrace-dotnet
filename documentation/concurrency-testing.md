# Concurrency testing

The dual-path pipeline — the synchronous listener, the buffered
best-effort path, the lock-free ring, the event store — is the core of
this project: every trace this runtime emits passes through it. Its defects
are the quiet kind. A torn read, a stranded tail, a miscounted loss never
throw; they just make the trace, or the number telling you how much of it
you lost, wrong. A single-threaded unit test cannot see them by
construction.

This document describes the suite that races that core on purpose:
`NarrativeTrace.StressTests`, a hand-rolled interleaving harness mirroring
the Java runtime's `narrativetrace-jcstress` module — invariant for
invariant, not tool for tool. jcstress is a JVM harness (bytecode
instrumentation, a forked-process scheduler); nothing like it exists for
.NET. **What this runtime mirrors is the invariant table below, not the
tool.** A runtime that cannot yet run a scenario still owes the invariant.

Two tiers, the same split this repository already uses for mutation
testing and fuzzing:

| Tier | What it is | When it runs | Cost |
|---|---|---|---|
| **Short** | Every `[Fact]` loops a bounded, seeded repetition count (`StressIterations`, default 300) | every `./build.sh Verify` | seconds |
| **Long** | The identical project, repetition count raised via `NARRATIVETRACE_STRESS_ITERATIONS` | manual/scheduled `./build.sh Stress` | minutes |

**Every NarrativeTrace runtime mirrors these invariants.** The Java runtime's suite
own outcome tables (`ACCEPTABLE` / `ACCEPTABLE_INTERESTING` / `FORBIDDEN`)
are the cross-runtime contract for what "correct under a race" means for this
core; only the harness is written per platform — jcstress's `@Actor`/
`@Arbiter` annotations become `StressRace.RunOnce`'s co-started
`Action` delegates plus a `Task.WaitAll` join, matching jcstress's own
guarantee that the arbiter step runs strictly after every actor finishes.

## Running it

```bash
./build.sh Test --target NarrativeTrace.StressTests   # the suite on its own (short tier)
./build.sh Verify                                       # short tier, as part of the full gate
./build.sh Stress                                        # long tier — see "The long sweep" below
```

## The invariants

In the order the cross-runtime stress-testing convention states them. This runtime's
concurrency model is real OS threads + the .NET thread pool (`Task.Run`) —
the closest analogue to Java's real-thread jcstress model of the runtimes this
product ships.

| # | Invariant | This runtime |
|---|---|---|
| 1 | Loss accounting is exact: delivered + shed == published, under any interleaving; no event both counted-as-shed and delivered | `LossAccountingStressTests` — **real defect found and fixed**, see "Findings" below |
| 2 | No torn/partial reads: a drain or snapshot sees prefix-consistent state | `NoTornReadStressTests` — verified safe (one lock, copy-out reads) |
| 3 | Lazy ring allocation races allocate exactly once | **N/A** — `BoundedEventBuffer`'s whole ring is allocated eagerly in the constructor (`_slots = new Slot[rounded]`), matching Java's own eager allocation; there is no lazy path for a race to hit. No test needed to prove the absence of a mechanism; the constructor is the evidence. |
| 4 | The tail is never stranded: a publish around the drain mechanism's stop/park moment is always eventually drained | `TailNeverStrandedStressTests` — verified safe (the drain loop never parks indefinitely; a missed wake-up costs one bounded sleep interval, never forever) |
| 5 | close()/dispose() racing publish and flush: idempotent, nothing silently lost uncounted, the drain mechanism terminates | `CloseDisposeStressTests` — verified safe (CAS-guarded `Dispose`; `Flush` never checks disposal state) |
| 6 | flush()'s post-condition holds under concurrent publish: everything published-before is in the store after | `FlushPostconditionStressTests` — verified safe for the strong form (a flush strictly after every publish returned); the weak form (a flush racing a publish) matches Java's own `ACCEPTABLE_INTERESTING`, not asserted on |
| 7 | The adoption/item-44 seams under concurrency: capture racing scope-close sees spans through exactly one side; no partial batch adoption at the ceiling; reset racing publish is safe | `AdoptionSeamStressTests` — verified safe (`AdoptionLedger.Adopt` performs the release-and-adopt step atomically under one lock, unlike Java's two separate synchronized calls — no hand-over window exists to race into) |

## The targets

| Class | Java mirror | What it races |
|---|---|---|
| `Pipeline.ConcurrentPublicationStressTests` | `ConcurrentProducersTest` | 2 producers, headroom to spare — the harness smoke test |
| `Pipeline.LossAccountingStressTests` | `LossAccountingTest`, `SaturatedRingAccountingTest`, `ClaimUniquenessTest` | 2 and 4 producers overflowing `BoundedEventBuffer`; the shedding-fill discard path in `BufferedEventConsumer` |
| `Pipeline.NoTornReadStressTests` | `EventStoreSnapshotTest` | one and three readers snapshotting `EventStore` while a writer appends |
| `Pipeline.TailNeverStrandedStressTests` | `ConsumerParkWakeupTest` | a publish racing the real background drain thread |
| `Pipeline.CloseDisposeStressTests` | `CloseIdempotenceTest`, `CloseRacingPublishTest` | concurrent `Dispose()` calls; a publish racing a `Dispose()` |
| `Pipeline.FlushPostconditionStressTests` | `FlushRacingPublishTest` | two producers, one flushing mid-race, one more flush after both join |
| `Context.AdoptionSeamStressTests` | `LiveChildHandOverTest`, `AdoptionCeilingTest` | a live child's hand-over racing a reader; two ceiling-crossing batches adopting concurrently |
| `Pipeline.DrainRacingPublishStressTests` | `DrainRacingPublishTest` | one producer publishing eight events into a four-slot ring while a drain races both publication and overwrite — the seqlock scenario; see "Findings" below |

## The harness

`StressRace.RunOnce(params Action[] actors)` — a `Barrier` co-starts a
fixed set of actor delegates, `Task.WaitAll` joins them before returning.
The .NET stand-in for jcstress's own low-level actor scheduling: no
attempt is made to reproduce jcstress's biased-locking/C2-stress-scheduling
tricks, since a bare thread-pool `Task.Run` plus real repetition (hundreds
of trials per `[Fact]`) is what this platform actually offers. A single
trial almost never hits the race; the loop is what does.

`Pipeline.StressEvents` / `Pipeline.DeliveryTally` are the direct .NET
mirrors of Java's identically-named jcstress helpers: identity-tagged
events built before the race starts (never inside an actor, which would
widen the window under test), and a delivery tally that flags a torn,
duplicate, out-of-range or out-of-order delivery without throwing — an
exception would hide the exact finding the tally exists to surface.

`Context.FakeReportableCapture` is the stress suite's own minimal
`IReportableCapture`, matching the `AdoptionLedgerTests.FakeCapture`
precedent already established in `NarrativeTrace.Core.Tests` — it races
`AdoptionLedger` directly rather than through a full `AsyncNarrativeContext`,
which needs `InternalsVisibleTo` (granted on
`NarrativeTrace.Runtime.csproj`) since the ledger is `internal`.

## What this runtime's concurrency model changes

Recorded with evidence, not silently dropped:

- **No lazy ring allocation** (invariant 3) — see the table above.
- **`AdoptionLedger.Adopt` is one atomic call**, not two. Java's
  `TraceStack.adopt()` and `unregisterLiveChild()` are separate
  `synchronized` methods with a hand-over window between them that
  `LiveChildHandOverTest` exists to police; this runtime's `Adopt` releases
  the live registration and adds to the adopted set under the same lock in
  one call, so there is no window to race into by construction. The
  mirrored test still holds that design claim to real contention rather
  than trusting the source reading alone.
- **A coarser lock, not a seqlock, protects `AdoptionLedger` and
  `EventStore`.** Java's finer-grained mechanisms (VarHandles, seqlock-style
  sequence checks) have .NET analogues only in `BoundedEventBuffer`
  (`Interlocked`/`Volatile`); the context- and store-level registries use a
  plain `lock`, which is provably safe but does not need the same
  torn-read defenses a lock-free structure does.
- **The drain loop is a bounded-sleep poll, not a park/unpark primitive.**
  This is *why* invariant 4 is safe by construction here rather than
  merely tested for it — see the invariant table.

## The long sweep

`./build.sh Stress` re-runs `NarrativeTrace.StressTests` with
`NARRATIVETRACE_STRESS_ITERATIONS` raised (`--stress-sweep-iterations` to
change the count; default 200,000). Deliberately **not** a dependency of
`Verify` — the same relationship `Benchmark` and `Fuzz` have to the gate:
racing the same interleaving hundreds of thousands of times is minutes,
not seconds.

## From a race to a regression test

1. **Reproduce it as a stress case**, looping the actor shape that hits it
   (see "The harness" above); name the `[Fact]` after the invariant it
   proves, not the symptom.
2. **Fix it in the project that owns it.** The stress suite proves the
   invariant holds under real contention; a unit test in the owning
   project (see `BoundedEventBufferTests`/`BufferedEventConsumerTests` for
   the loss-accounting finding below) is what a future reader finds when
   they change that code deterministically, single-threaded.
3. **One commit per finding**, full gate green after each. **Report
   findings via the repository's issue tracker or security policy** — a
   concurrency defect found in one NarrativeTrace runtime is worth checking
   for in the others.

## Findings — this run's audit against the seven invariants

- **Invariant 1 (loss accounting) — real defect, fixed.**
  `BoundedEventBuffer` silently overwrote ring slots with no counter at
  all, and `BufferedEventConsumer`'s overloaded-fill drain discarded its
  whole batch uncounted except at the highest fill band. Publishing 64
  events into a 16-capacity buffer before any drain reproduced it
  deterministically (no concurrency needed): 48 events vanished from both
  the output and the loss counter. Same root cause as a separate finding in
  this repository's parallel no-poison audit, reached here independently
  via concurrent stress races. Fixed: `BoundedEventBuffer.OverwrittenCount`,
  wired into `BufferedEventConsumer.DroppedCount`.
- **Invariants 2, 4, 5, 6, 7 — verified safe, no defect.** Each holds by a
  specific, named construction detail (a shared lock, a bounded-sleep
  poll, a CAS guard, an atomic adopt-and-release) rather than by luck; see
  the invariant table above for which.
- **Invariant 3 — N/A**, eager allocation; see the invariant table.
- **The seqlock defect (outside the seven-invariant table) — a known java
  defect, ported unfixed, now fixed and pinned.** `BoundedEventBuffer`'s
  slot protocol was one sequence store guarding one data field: the
  consumer's check proved the producer *had* finished writing the slot,
  not that it still held that event, so a producer that lapped the ring
  between the check and the read could deliver a later generation's event
  under the wrong index — and deliver it again when the index caught up —
  while the event that belonged there vanished with the loss counter
  reading zero. Exactly the shape the Java runtime measured 1,732 forbidden
  samples against on a four-slot ring before its own fix; flagged here as a
  live, unlogged instance of the same defect. Fixed the same way: `Put` marks
  the slot
  with the claim sequence before writing the event, separated from the
  event write by a full fence (`Thread.MemoryBarrier` — .NET has no
  store-store-only primitive);
  the reader re-reads the sequence behind its own fence after the event
  read and, if it changed, counts the slot as overwritten and steps over
  it rather than delivering it. Pinned deterministically via a second test
  seam (`BoundedEventBufferTests.Drain_skips_a_slot_overwritten_between_the_check_and_the_read`,
  `...Poll_answers_null_and_counts_the_loss_for_a_slot_overwritten_mid_read`)
  and under real races (`Pipeline.DrainRacingPublishStressTests`).

## What the suite does not do

- It does not replace `BoundedEventBufferTests`/`BufferedEventConsumerTests`/
  `AdoptionLedgerTests` and the rest of the single-threaded unit suite.
  Those prove behavior deterministically; this suite proves it holds under
  real, unpredictable thread interleaving, which a single-threaded test
  cannot see by construction.
- It does not attempt jcstress's exhaustive JVM-level scheduling tricks
  (biased locking on/off, forced GC, C2 compiler stress flags). Real
  repetition against the .NET thread pool is the substitute this platform
  offers.
- It does not assert on timing or throughput. That is `NarrativeTrace.Benchmarks`'
  job; this owns correctness under contention, not speed.
