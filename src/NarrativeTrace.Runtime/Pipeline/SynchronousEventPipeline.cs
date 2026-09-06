// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;

namespace NarrativeTrace.Runtime;

/// <summary>
/// Trivial synchronous <see cref="IEventPipeline"/>: published events are
/// retained immediately in an in-memory list with no buffering or background
/// draining. This is the default capture store for a narrative context —
/// isolated per async fork by copying its events into a fresh instance.
/// </summary>
public sealed class SynchronousEventPipeline : IEventPipeline
{
    private readonly object _lock = new();
    private readonly List<TraceEvent> _events;

    /// <summary>Creates an empty pipeline.</summary>
    public SynchronousEventPipeline()
    {
        _events = [];
    }

    /// <summary>Creates a pipeline pre-seeded with a copy of the given events.</summary>
    public SynchronousEventPipeline(IEnumerable<TraceEvent> seed)
    {
        _events = [.. seed];
    }

    /// <summary>
    /// Retains an event immediately. Thread-safe, and never drops — unlike the
    /// bounded <see cref="BufferedEventConsumer"/>, this grows without limit, so
    /// it suits a bounded trace and not an unbounded stream.
    /// </summary>
    /// <param name="traceEvent">The event to retain.</param>
    public void Publish(TraceEvent traceEvent)
    {
        lock (_lock)
        {
            _events.Add(traceEvent);
        }
    }

    /// <summary>No-op: events are visible the moment they are published.</summary>
    public void Flush()
    {
    }

    /// <summary>Returns a snapshot of the retained events in publish order.</summary>
    /// <returns>A copy, safe to enumerate while other threads publish.</returns>
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

    /// <summary>
    /// No-op: this pipeline owns no thread, timer or unmanaged handle. Present
    /// only to satisfy <see cref="IEventPipeline"/>, and safe to call any number
    /// of times — it does not clear retained events.
    /// </summary>
    public void Dispose()
    {
    }
}
