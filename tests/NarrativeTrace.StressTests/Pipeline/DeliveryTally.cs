// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;

namespace NarrativeTrace.StressTests.Pipeline;

/// <summary>
/// Accumulates deliveries from a drain and answers whether they were exact:
/// no duplicate, no out-of-range or null tag, arriving in tag order. The .NET
/// mirror of Java's <c>DeliveryTally</c>.
/// </summary>
/// <remarks>
/// Deliberately not thread-safe — a trial shares one instance between (at
/// most) an actor's drain and the arbiter's, and the two never run
/// concurrently by construction (<see cref="StressRace.RunOnce"/> joins every
/// actor before the caller's arbiter step runs), which is exactly how a
/// double-delivery between the two calls gets caught.
/// </remarks>
internal sealed class DeliveryTally(int published)
{
    private readonly bool[] _seen = new bool[published];
    private long _previousTag = -1;

    /// <summary>How many times <see cref="Accept"/> was called.</summary>
    internal int Delivered { get; private set; }

    /// <summary>True iff every delivery so far was distinct, in-range and non-null.</summary>
    internal bool Intact { get; private set; } = true;

    /// <summary>True iff every valid delivery so far arrived in strictly increasing tag order.</summary>
    /// <remarks>Only meaningful for a single-producer scenario.</remarks>
    internal bool Monotonic { get; private set; } = true;

    /// <summary>Records one delivered event.</summary>
    internal void Accept(TraceEvent? traceEvent)
    {
        Delivered++;
        var tag = StressEvents.TagOf(traceEvent);
        if (tag < 0 || tag >= _seen.Length || _seen[tag])
        {
            Intact = false;
            return;
        }

        _seen[tag] = true;
        Monotonic = Monotonic && tag > _previousTag;
        _previousTag = tag;
    }
}
