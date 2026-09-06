// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Runtime;
using Xunit;

namespace NarrativeTrace.StressTests.Pipeline;

/// <summary>
/// Sanity precursor to the loss-accounting races below: with headroom to
/// spare, concurrent publication into <see cref="BoundedEventBuffer"/> must
/// never lose or duplicate an event. The .NET mirror of Java's
/// <c>ConcurrentProducersTest</c>.
/// </summary>
/// <remarks>
/// Also this project's wiring smoke test — the first stress test written, to
/// prove <see cref="StressRace"/>/<see cref="StressIterations"/> and the
/// project's place in the <c>Test</c>/<c>Verify</c> sweeps before any
/// invariant-specific race is added on top.
/// </remarks>
[Trait("Category", "Stress")]
public sealed class ConcurrentPublicationStressTests
{
    [Fact]
    public void Two_producers_with_headroom_never_lose_or_duplicate()
    {
        for (var trial = 0; trial < StressIterations.Count; trial++)
        {
            var buffer = new BoundedEventBuffer(8);
            var events = StressEvents.Sequence(2);

            StressRace.RunOnce(
                () => buffer.Put(events[0]),
                () => buffer.Put(events[1]));

            var tally = new DeliveryTally(2);
            buffer.Drain(tally.Accept);

            Assert.True(tally.Intact, $"trial {trial}: torn or duplicate delivery");
            Assert.Equal(2, tally.Delivered);
        }
    }
}
