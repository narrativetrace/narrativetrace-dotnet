// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Runtime;
using Xunit;

namespace NarrativeTrace.StressTests.Pipeline;

/// <summary>
/// Invariant 5 — close()/dispose() racing publish and flush: idempotent,
/// nothing silently lost uncounted, the drain mechanism terminates. The
/// .NET mirror of Java's <c>CloseIdempotenceTest</c> and
/// <c>CloseRacingPublishTest</c>.
/// </summary>
/// <remarks>
/// <b>Expected safe, verified rather than assumed.</b>
/// <see cref="BufferedEventConsumer.Dispose"/> is CAS-guarded
/// (<c>Interlocked.Exchange(ref _closed, 1)</c>), so only one concurrent
/// call ever runs its body — the rest return immediately as no-ops.
/// <see cref="BufferedEventConsumer.Flush"/> never checks whether the
/// consumer has been disposed, so a flush racing (or following) a dispose
/// always still drains whatever the ring holds — unlike the pre-fix Java
/// defect these tests' names echo (a closed reactive
/// <c>SubmissionPublisher</c> made a post-close flush throw; fixed
/// 2026-09-01 per the Java jcstress suite's own Javadoc). These tests hold
/// both claims to real concurrent contention.
/// </remarks>
[Trait("Category", "Stress")]
public sealed class CloseDisposeStressTests
{
    [Fact]
    public void Concurrent_dispose_calls_are_idempotent_and_drain_exactly_once()
    {
        for (var trial = 0; trial < StressIterations.Count; trial++)
        {
            var consumer = new BufferedEventConsumer(16, startConsumer: false);
            consumer.Publish(StressEvents.Tagged(0));

            StressRace.RunOnce(
                () => consumer.Dispose(),
                () => consumer.Dispose(),
                () => consumer.Dispose(),
                () => consumer.Dispose());

            Assert.Single(consumer.Events());
            Assert.Equal(0, consumer.DroppedCount);
        }
    }

    [Fact]
    public void Publish_racing_dispose_is_never_silently_lost()
    {
        for (var trial = 0; trial < StressIterations.Count; trial++)
        {
            var consumer = new BufferedEventConsumer(16, startConsumer: false);
            var evt = StressEvents.Tagged(0);

            StressRace.RunOnce(
                () => consumer.Publish(evt),
                () => consumer.Dispose());
            consumer.Flush(); // the arbiter's one more flush, after both actors joined

            Assert.Single(consumer.Events());
            Assert.Equal(0, consumer.DroppedCount);
        }
    }
}
