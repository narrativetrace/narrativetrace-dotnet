// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;

namespace NarrativeTrace.Runtime;

/// <summary>
/// Thread-safe in-memory sink of drained trace events. Buffered consumers and
/// subscribers use it as their retained event history.
/// </summary>
/// <remarks>
/// All access is serialized on the instance lock. A background drain thread
/// calls <see cref="Add"/> while request threads call <see cref="Events"/> and
/// <see cref="Clear"/>; without synchronization the backing list would corrupt.
/// The lock is uncontended on the synchronous path.
/// </remarks>
public sealed class EventStore
{
    private readonly object _lock = new();
    private readonly List<TraceEvent> _events = [];

    /// <summary>Appends an event. Thread-safe.</summary>
    /// <param name="traceEvent">The event to retain.</param>
    public void Add(TraceEvent traceEvent)
    {
        lock (_lock)
        {
            _events.Add(traceEvent);
        }
    }

    /// <summary>Returns a snapshot of the retained events in insertion order.</summary>
    /// <returns>
    /// A copy, not a live view — later additions are not reflected, so the
    /// result is safe to enumerate while other threads publish. Copying costs
    /// O(n) per call; do not call it in a loop.
    /// </returns>
    public IReadOnlyList<TraceEvent> Events()
    {
        lock (_lock)
        {
            return _events.ToArray();
        }
    }

    /// <summary>Discards every retained event. Snapshots already handed out are unaffected.</summary>
    public void Clear()
    {
        lock (_lock)
        {
            _events.Clear();
        }
    }
}
