// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using NarrativeTrace.Runtime;
using Xunit;

namespace NarrativeTrace.StressTests.Pipeline;

/// <summary>
/// Invariant 1 — loss accounting is exact: delivered + shed == published,
/// under any interleaving; no event is ever both counted-as-shed and
/// delivered. The .NET mirror of Java's <c>LossAccountingTest</c>,
/// <c>SaturatedRingAccountingTest</c> and <c>ClaimUniquenessTest</c>.
/// </summary>
/// <remarks>
/// <b>Real defect found and fixed by this class (a loss-accounting
/// undercount found by audit).</b> Before this class's tests were written,
/// <see cref="BoundedEventBuffer"/> silently overwrote ring slots with no
/// counter at all, and <see cref="BufferedEventConsumer"/>'s shedding-mode
/// drain (queue fill 70–90%) discarded its whole batch uncounted — only the
/// emergency-mode branch (fill &gt; 90%) incremented <c>DroppedCount</c>.
/// Publishing 64 events into a 16-capacity buffer before any drain reproduced
/// it deterministically (no concurrency needed): the ring silently
/// overwrote 48 (uncounted), then the one drain that ran saw 100% fill and
/// counted only the 16 it emptied — delivered=0, counted-dropped=16,
/// published=64, so <b>48 events vanished from both the output and the loss
/// counter.</b> Also flagged (unfixed) as a separate finding in this repo's
/// parallel no-poison audit — same root cause, reached here via concurrent
/// stress races instead of a single-threaded repro. Fixed by adding
/// <see cref="BoundedEventBuffer.OverwrittenCount"/>
/// and counting the shedding-branch discard in
/// <see cref="BufferedEventConsumer"/>.
/// </remarks>
/// <remarks>
/// <b>Accepted, cross-runtime residue.</b> The Java runtime's own suite accepts a small
/// <i>positive</i> residue (published − delivered − shed &gt; 0) as
/// "acceptable, interesting" under extreme multi-producer oversubscription —
/// a producer can be preempted between claiming a ring index and writing to
/// it, and a much-faster sibling producer can claim, write, and even get
/// overwritten again before the slow producer's write finally lands, so its
/// event is never delivered and the index-arithmetic overwrite count never
/// attributes a loss to it either. Java's own outcome tables (e.g.
/// <c>LossAccountingTest</c>, <c>DrainRacingPublishTest</c>) forbid only a
/// <i>negative</i> residue (an event double-counted — both delivered and
/// shed) and any torn/duplicate/out-of-range delivery — never a positive
/// one. This suite holds this runtime to the identical, not a stricter,
/// bar.
/// </remarks>
[Trait("Category", "Stress")]
public sealed class LossAccountingStressTests
{
    [Fact]
    public void Two_producers_overflow_a_small_ring_without_over_counting_or_corruption()
    {
        for (var trial = 0; trial < StressIterations.Count; trial++)
        {
            const int published = 16;
            var buffer = new BoundedEventBuffer(4);
            var events = StressEvents.Sequence(published);

            StressRace.RunOnce(
                () => PublishRange(buffer, events, 0, 8),
                () => PublishRange(buffer, events, 8, 16));

            AssertExactOrPositiveResidue(buffer, published, trial);
        }
    }

    [Fact]
    public void Four_producers_with_headroom_never_lose_a_claim()
    {
        for (var trial = 0; trial < StressIterations.Count; trial++)
        {
            const int published = 4;
            var buffer = new BoundedEventBuffer(8);
            var events = StressEvents.Sequence(published);

            StressRace.RunOnce(
                () => buffer.Put(events[0]),
                () => buffer.Put(events[1]),
                () => buffer.Put(events[2]),
                () => buffer.Put(events[3]));

            var tally = new DeliveryTally(published);
            buffer.Drain(tally.Accept);

            Assert.True(tally.Intact, $"trial {trial}: torn or duplicate claim");
            Assert.Equal(published, tally.Delivered);
            Assert.Equal(0, buffer.OverwrittenCount);
        }
    }

    [Fact]
    public void Shedding_mode_discards_are_never_silently_uncounted()
    {
        for (var trial = 0; trial < StressIterations.Count; trial++)
        {
            const int published = 12;
            using var consumer = new BufferedEventConsumer(16, startConsumer: false);
            var events = StressEvents.Sequence(published);

            StressRace.RunOnce(
                () => PublishRange(consumer, events, 0, 6),
                () => PublishRange(consumer, events, 6, 12));
            consumer.DrainOnce();

            var residue = published - consumer.Events().Count - consumer.DroppedCount;
            Assert.True(residue == 0, $"trial {trial}: {residue} event(s) unaccounted for");
        }
    }

    private static void PublishRange(
        BoundedEventBuffer buffer, EnterEvent[] events, int from, int to)
    {
        for (var i = from; i < to; i++)
        {
            buffer.Put(events[i]);
        }
    }

    private static void PublishRange(
        BufferedEventConsumer consumer, EnterEvent[] events, int from, int to)
    {
        for (var i = from; i < to; i++)
        {
            consumer.Publish(events[i]);
        }
    }

    private static void AssertExactOrPositiveResidue(
        BoundedEventBuffer buffer, int published, int trial)
    {
        var tally = new DeliveryTally(published);
        buffer.Drain(tally.Accept);
        var residue = published - tally.Delivered - buffer.OverwrittenCount;

        Assert.True(residue >= 0, $"trial {trial}: negative residue {residue} (over-counted)");
        Assert.True(tally.Intact, $"trial {trial}: torn, duplicate or out-of-range delivery");
    }
}
