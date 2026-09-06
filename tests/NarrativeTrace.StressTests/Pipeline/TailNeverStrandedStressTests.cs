// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System;
using System.Threading.Tasks;
using NarrativeTrace.Runtime;
using Xunit;

namespace NarrativeTrace.StressTests.Pipeline;

/// <summary>
/// Invariant 4 — the tail is never stranded: a publish into an empty buffer
/// around the drain thread's stop/park moment is always eventually drained.
/// The .NET mirror of Java's <c>ConsumerParkWakeupTest</c>.
/// </summary>
/// <remarks>
/// <b>Expected safe, verified rather than assumed.</b> The Java scenario
/// targets a real risk in a park/unpark or wait/notify drain loop: a
/// producer can publish after the consumer's emptiness check but before it
/// parks, missing the wake-up and stranding the event until something else
/// happens to wake it. <see cref="BufferedEventConsumer"/>'s drain loop
/// (<c>DrainLoop</c>) has no such risk by construction — it never parks
/// indefinitely, it re-checks after a bounded
/// <see cref="BufferedEventConsumer.DrainIntervalMillis"/> sleep every
/// cycle, so a missed wake-up costs at most one sleep interval, never
/// forever. This test races a real publish directly against the real
/// background drain thread (not a synthetic single-cycle probe) to hold
/// that design claim to actual thread timing, with a generous bounded wait
/// as the test's own safety net rather than the invariant's.
/// </remarks>
[Trait("Category", "Stress")]
public sealed class TailNeverStrandedStressTests
{
    private static readonly TimeSpan BoundedWait = TimeSpan.FromSeconds(2);

    [Fact]
    public async Task Publish_racing_the_drain_loop_is_always_eventually_delivered()
    {
        for (var trial = 0; trial < StressIterations.Count; trial++)
        {
            using var consumer = new BufferedEventConsumer(4);
            var evt = StressEvents.Tagged(0);
            Task? whenReady = null;

            StressRace.RunOnce(
                () => consumer.Publish(evt),
                () => whenReady = consumer.WhenCountReached(1));

            var completed = await Task.WhenAny(whenReady!, Task.Delay(BoundedWait));
            Assert.True(
                ReferenceEquals(completed, whenReady),
                $"trial {trial}: publish was stranded past the bounded wait");
            Assert.Single(consumer.Events());
        }
    }
}
