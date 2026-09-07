// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Runtime;
using Xunit;

namespace NarrativeTrace.StressTests.Pipeline;

/// <summary>
/// A drain racing a live producer sees a prefix, never a tear. The .NET mirror of java's jcstress
/// scenario <c>DrainRacingPublishTest</c> — the race that found the seqlock defect (this runtime's
/// mirror of the fix lives in <see cref="BoundedEventBuffer"/>).
/// </summary>
/// <remarks>
/// <para>
/// INTENT: This is the shape the default topology actually runs — one thread publishing while
/// another drains on demand from a flush. One producer publishes eight events into a four-slot
/// ring, so the drain is racing overwrite as well as publication, while a second actor drains once,
/// mid-flight. The arbiter drains the remainder into the same tally, so a delivery counted by both
/// would be caught.
/// </para>
/// <para>
/// Three things are asserted at once, because they fail differently:
/// </para>
/// <list type="bullet">
///   <item><b>Accounting</b> — published minus delivered minus overwritten must be zero. With a
///   single producer there is no stalled claim to strand the tail, so unlike a multi-producer
///   scenario this has no acceptable non-zero residue.</item>
///   <item><b>Integrity</b> — no null, no duplicate, no out-of-range event. A slot's event field is
///   written before its sequence is released and read after that sequence is acquired; if that
///   pairing were broken, a drain would hand back the previous generation's event or a null.</item>
///   <item><b>Prefix consistency</b> — one producer claims in publication order, so the tags a
///   drain delivers must strictly increase. A drain that jumped a gap without counting it, or
///   replayed a slot, shows up here even when the totals happen to balance.</item>
/// </list>
/// <para>
/// <b>@edgeCase</b> Exactly one actor drains concurrently with the producer.
/// <see cref="BoundedEventBuffer"/> is multi-producer/single-consumer, and a second concurrent
/// drain would break the contract the scenario is measuring rather than test it — the arbiter's
/// drain runs only after both actors have finished, matching jcstress's own ordering guarantee.
/// </para>
/// </remarks>
[Trait("Category", "Stress")]
public sealed class DrainRacingPublishStressTests
{
    private const int Published = 8;

    [Fact]
    public void A_drain_racing_a_live_producer_sees_a_prefix_never_a_tear()
    {
        for (var trial = 0; trial < StressIterations.Count; trial++)
        {
            var buffer = new BoundedEventBuffer(4);
            var events = StressEvents.Sequence(Published);
            var tally = new DeliveryTally(Published);

            StressRace.RunOnce(
                () =>
                {
                    foreach (var e in events)
                    {
                        buffer.Put(e);
                    }
                },
                () => buffer.Drain(tally.Accept));

            buffer.Drain(tally.Accept);

            var accounting = Published - tally.Delivered - buffer.OverwrittenCount;
            Assert.True(
                accounting == 0,
                $"trial {trial}: published - delivered - overwritten = {accounting} " +
                $"(delivered={tally.Delivered}, overwritten={buffer.OverwrittenCount}) -- " +
                "events vanished uncounted, or were double-counted");
            Assert.True(tally.Intact, $"trial {trial}: torn read -- a null, a duplicate, or an event delivered under the wrong index");
            Assert.True(tally.Monotonic, $"trial {trial}: non-prefix window -- the tags a single producer published arrived out of order");
        }
    }
}
