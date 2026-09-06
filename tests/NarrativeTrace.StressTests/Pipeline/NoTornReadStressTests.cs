// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Collections.Generic;
using NarrativeTrace.Core;
using NarrativeTrace.Runtime;
using Xunit;

namespace NarrativeTrace.StressTests.Pipeline;

/// <summary>
/// Invariant 2 — no torn/partial reads: a drain or snapshot sees
/// prefix-consistent state, never a half-written slot or a partially
/// visible append. The .NET mirror of Java's <c>EventStoreSnapshotTest</c>.
/// </summary>
/// <remarks>
/// <b>Expected safe, verified rather than assumed.</b> <see cref="EventStore.Add"/>
/// and <see cref="EventStore.Events"/> are both serialized on the same
/// instance lock, and <c>Events()</c> hands back a copy
/// (<c>ToArray()</c>), never the live list — a reader can never observe a
/// list mid-append. These tests hold that claim to real concurrent
/// contention.
/// </remarks>
[Trait("Category", "Stress")]
public sealed class NoTornReadStressTests
{
    [Fact]
    public void One_reader_snapshot_racing_an_appender_is_never_torn()
    {
        for (var trial = 0; trial < StressIterations.Count; trial++)
        {
            const int published = 4;
            var store = new EventStore();
            var events = StressEvents.Sequence(published);
            IReadOnlyList<TraceEvent>? snapshot = null;

            StressRace.RunOnce(
                () => Append(store, events),
                () => snapshot = store.Events());

            AssertIntactPrefix(snapshot!, published, trial);
            Assert.Equal(published, store.Events().Count);
        }
    }

    [Fact]
    public void Three_readers_snapshotting_concurrently_are_each_never_torn()
    {
        for (var trial = 0; trial < StressIterations.Count; trial++)
        {
            const int published = 4;
            var store = new EventStore();
            var events = StressEvents.Sequence(published);
            IReadOnlyList<TraceEvent>? a = null, b = null, c = null;

            StressRace.RunOnce(
                () => Append(store, events),
                () => a = store.Events(),
                () => b = store.Events(),
                () => c = store.Events());

            AssertIntactPrefix(a!, published, trial);
            AssertIntactPrefix(b!, published, trial);
            AssertIntactPrefix(c!, published, trial);
        }
    }

    private static void Append(EventStore store, EnterEvent[] events)
    {
        foreach (var e in events)
        {
            store.Add(e);
        }
    }

    private static void AssertIntactPrefix(
        IReadOnlyList<TraceEvent> snapshot, int published, int trial)
    {
        var tally = new DeliveryTally(published);
        foreach (var e in snapshot)
        {
            tally.Accept(e);
        }

        Assert.True(
            tally.Intact && tally.Monotonic,
            $"trial {trial}: torn, duplicate or out-of-order snapshot element");
    }
}
