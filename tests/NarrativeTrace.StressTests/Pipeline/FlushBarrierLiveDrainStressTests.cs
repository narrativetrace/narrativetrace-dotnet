// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Runtime;
using Xunit;

namespace NarrativeTrace.StressTests.Pipeline;

/// <summary>
/// Invariant 6 with a <b>live background drain thread</b>, unlike the
/// <c>startConsumer: false</c> shape <see cref="FlushPostconditionStressTests"/>
/// uses: concurrent producers publish, join, and then a single flush must
/// account for everything — store, shed count, <em>and subscriber
/// delivery</em> — even though the background cycle can already be mid-step
/// when the flush lands. The .NET mirror of Python's
/// <c>TestFlushPostConditionUnderConcurrentPublish</c>, widened to also
/// assert delivery: the seam this port's own gap (fixed alongside this test)
/// was actually in — see
/// <c>NarrativeTrace.Core.Tests.BufferedEventConsumerTests.Flush_waits_out_an_in_flight_background_notify_before_returning</c>
/// for the deterministic proof of the mechanism this exercises statistically.
/// </summary>
[Trait("Category", "Stress")]
public sealed class FlushBarrierLiveDrainStressTests
{
    private const int Published = 64;
    private const int WorkerCount = 8;
    private const int PerWorker = Published / WorkerCount;

    /// <summary>
    /// Roomy ring: normal mode only, so every accepted event is both stored
    /// and delivered. Pins the store side (already exact before this fix)
    /// together with the delivery side (the actual gap) in one accounting
    /// check, the way the family's own invariant 6 test does for storage
    /// alone.
    /// </summary>
    [Fact]
    public void Flush_after_concurrent_publishers_join_accounts_for_store_and_delivery()
    {
        for (var trial = 0; trial < StressIterations.Count; trial++)
        {
            using var consumer = new BufferedEventConsumer(Published * 4, startConsumer: true);
            var delivered = new DeliveryTally(Published);
            consumer.Subscribe(delivered.Accept);

            StressRace.RunOnce(BuildPublishers(consumer));
            consumer.Flush();

            AssertAccountedForAndDelivered(consumer, delivered, trial);
        }
    }

    /// <summary>
    /// A ring too small for the burst forces the shed branch on some drain
    /// cycles while the background thread and the joining flush contend for
    /// the same events — the "also check the shed-count branch for the same
    /// window" half of the fix. Residue (published − stored − dropped) may
    /// legitimately be <em>positive</em> under this much oversubscription
    /// (a producer preempted between claiming a slot and writing it can be
    /// lapped again before its write lands, per <see cref="LossAccountingStressTests"/>'s
    /// documented, cross-runtime-accepted outcome) but never negative — that
    /// would mean an event both delivered and shed, or shed twice.
    /// </summary>
    [Fact]
    public void Flush_after_concurrent_publishers_join_accounts_under_forced_shedding()
    {
        for (var trial = 0; trial < StressIterations.Count; trial++)
        {
            using var consumer = new BufferedEventConsumer(16, startConsumer: true);
            var delivered = new DeliveryTally(Published);
            consumer.Subscribe(delivered.Accept);

            StressRace.RunOnce(BuildPublishers(consumer));
            consumer.Flush();

            AssertAccountedForAndDelivered(consumer, delivered, trial);
        }
    }

    private static void AssertAccountedForAndDelivered(
        BufferedEventConsumer consumer, DeliveryTally delivered, int trial)
    {
        var stored = consumer.Events().Count;
        var residue = Published - stored - consumer.DroppedCount;

        Assert.True(residue >= 0, $"trial {trial}: negative residue {residue} (over-counted)");
        Assert.Equal(stored, delivered.Delivered);
        Assert.True(
            delivered.Intact,
            $"trial {trial}: duplicate or corrupted delivery -- flush did not wait out the in-flight drain step");
    }

    private static Action[] BuildPublishers(BufferedEventConsumer consumer)
    {
        var events = StressEvents.Sequence(Published);
        var actors = new Action[WorkerCount];
        for (var w = 0; w < WorkerCount; w++)
        {
            var start = w * PerWorker;
            actors[w] = () =>
            {
                for (var i = start; i < start + PerWorker; i++)
                {
                    consumer.Publish(events[i]);
                }
            };
        }

        return actors;
    }
}
