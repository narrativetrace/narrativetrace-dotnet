// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;

namespace NarrativeTrace.Runtime;

/// <summary>
/// Fan-out pipeline with one synchronous path and one optional best-effort
/// path. The default composition point for attaching an immediate listener
/// while still retaining a buffered store for later tree capture.
/// </summary>
/// <remarks>
/// The synchronous listener runs inline on the caller thread — keep it fast.
/// <see cref="Publish"/> isolates each path: a throwing listener or consumer
/// is swallowed and never reaches application code, and never prevents the
/// other path from receiving the event. That guarantee is load-bearing for
/// deferred async exits, where an exception escaping the exit callback would
/// leave the caller's task incomplete forever.
/// </remarks>
public sealed class DualPathPipeline : IEventPipeline, IEventLossCounter
{
    private readonly Action<TraceEvent>? _synchronousListener;
    private readonly Action<TraceEvent>? _bestEffortConsumer;
    private readonly BufferedEventConsumer? _store;

    /// <summary>Creates a pipeline with only a synchronous listener and no store.</summary>
    /// <param name="synchronousListener">
    /// Runs inline on the publishing thread — keep it fast. May be
    /// <see langword="null"/>, which makes the pipeline discard everything.
    /// </param>
    /// <remarks>
    /// With no store, <see cref="Events"/> always returns empty, so a context
    /// using this pipeline as its capture store can never build a trace tree.
    /// Pair it with a <see cref="BufferedEventConsumer"/> if you need both.
    /// </remarks>
    public DualPathPipeline(Action<TraceEvent>? synchronousListener)
        : this(synchronousListener, (BufferedEventConsumer?)null)
    {
    }

    /// <summary>Creates a pipeline fanning out to a synchronous listener and a buffered store.</summary>
    /// <param name="synchronousListener">Runs inline on the publishing thread; may be <see langword="null"/>.</param>
    /// <param name="consumer">
    /// The best-effort buffered store backing <see cref="Events"/>,
    /// <see cref="Flush"/>, <see cref="Clear"/> and <see cref="Dispose"/>. May
    /// be <see langword="null"/>, in which case those four become no-ops.
    /// Ownership transfers: disposing this pipeline disposes the consumer.
    /// </param>
    public DualPathPipeline(
        Action<TraceEvent>? synchronousListener,
        BufferedEventConsumer? consumer)
    {
        _synchronousListener = synchronousListener;
        _bestEffortConsumer = consumer is null ? null : consumer.Publish;
        _store = consumer;
    }

    /// <summary>
    /// Composes with an arbitrary best-effort consumer that exposes no store,
    /// mirroring Java's <c>Consumer</c>-based path. A throwing consumer is
    /// isolated exactly like the buffered path.
    /// </summary>
    public DualPathPipeline(
        Action<TraceEvent>? synchronousListener,
        Action<TraceEvent>? bestEffortConsumer)
    {
        _synchronousListener = synchronousListener;
        _bestEffortConsumer = bestEffortConsumer;
        _store = null;
    }

    /// <summary>Whether the best-effort path retains events for later retrieval.</summary>
    public bool HasStore => _store is not null;

    /// <inheritdoc/>
    /// <remarks>
    /// The buffered store's count, or zero when there is no store to shed
    /// from — a fan-out to a synchronous listener alone never drops.
    /// </remarks>
    public long DroppedEventCount => _store?.DroppedCount ?? 0;

    /// <summary>Delivers an event to both paths, isolating each from the other's failures.</summary>
    /// <param name="traceEvent">The event to fan out.</param>
    /// <remarks>
    /// Never throws. An exception from either path is swallowed and the other
    /// path still receives the event — load-bearing for deferred async exits,
    /// where an escaping exception would leave the caller's task permanently
    /// incomplete. The corollary is that a broken listener fails silently.
    /// </remarks>
    public void Publish(TraceEvent traceEvent)
    {
        RunSafely(_synchronousListener, traceEvent);
        RunSafely(_bestEffortConsumer, traceEvent);
    }

    /// <summary>Drains the buffered store so published events become visible to <see cref="Events"/>.</summary>
    /// <remarks>No-op when the pipeline was built without a store.</remarks>
    public void Flush()
    {
        _store?.Flush();
    }

    /// <summary>Returns a snapshot of the events retained by the buffered store.</summary>
    /// <returns>
    /// The retained events, or an empty list when there is no store. Call
    /// <see cref="Flush"/> first — events still in the ring buffer are not
    /// included, so an un-flushed read can legitimately come back short.
    /// </returns>
    public IReadOnlyList<TraceEvent> Events()
    {
        return _store?.Events() ?? [];
    }

    /// <summary>Discards the store's retained events. No-op without a store.</summary>
    public void Clear()
    {
        _store?.Clear();
    }

    /// <summary>Disposes the buffered store, stopping its drain thread. No-op without a store.</summary>
    /// <remarks>Idempotent. The synchronous listener is not owned and is left alone.</remarks>
    public void Dispose()
    {
        _store?.Dispose();
    }

    private static void RunSafely(
        Action<TraceEvent>? path, TraceEvent traceEvent)
    {
        if (path is null)
        {
            return;
        }

        try
        {
            path(traceEvent);
        }
        catch (Exception)
        {
            // Observability failure must never become an application failure.
        }
    }
}
