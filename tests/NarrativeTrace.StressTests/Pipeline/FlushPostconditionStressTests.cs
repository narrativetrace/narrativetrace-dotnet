// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Runtime;
using Xunit;

namespace NarrativeTrace.StressTests.Pipeline;

/// <summary>
/// Invariant 6 — <see cref="BufferedEventConsumer.Flush"/>'s postcondition
/// holds under concurrent publish: everything published-before a flush call
/// is in the store after it. The .NET mirror of Java's
/// <c>FlushRacingPublishTest</c>.
/// </summary>
/// <remarks>
/// A flush that races a concurrent publish has a deliberately <b>weak</b>
/// postcondition: if the flushing thread's own event landed in a ring slot
/// behind another producer's still-unwritten claim, that one flush call can
/// legitimately see nothing at all — the single-consumer drain stops at the
/// first not-yet-published slot rather than skipping ahead, so a later,
/// already-written slot stays undrained until the next flush. Java accepts
/// this as <c>ACCEPTABLE_INTERESTING</c>, not forbidden. The <b>strong</b>
/// postcondition this test actually holds this runtime to is the one flush
/// that runs strictly after every publish has already returned (this
/// suite's own <see cref="StressRace.RunOnce"/> joins every actor first):
/// that flush must always account for everything, exactly once.
/// </remarks>
[Trait("Category", "Stress")]
public sealed class FlushPostconditionStressTests
{
    [Fact]
    public void The_flush_that_follows_every_publish_accounts_for_everything()
    {
        for (var trial = 0; trial < StressIterations.Count; trial++)
        {
            const int published = 2;
            var consumer = new BufferedEventConsumer(16, startConsumer: false);
            var events = StressEvents.Sequence(published);

            StressRace.RunOnce(
                () =>
                {
                    consumer.Publish(events[0]);
                    consumer.Flush(); // a racing flush — weak postcondition, not asserted on
                },
                () => consumer.Publish(events[1]));
            consumer.Flush(); // strictly after both publishes returned — strong postcondition

            var tally = new DeliveryTally(published);
            foreach (var e in consumer.Events())
            {
                tally.Accept(e);
            }

            Assert.Equal(published, tally.Delivered);
            Assert.True(tally.Intact, $"trial {trial}: duplicate or corrupted delivery in the store");
        }
    }
}
