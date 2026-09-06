// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Core;

/// <summary>
/// Ingest boundary for trace events. Implementations decide how published
/// events are buffered, drained, and retained — from a trivial synchronous
/// store to a bounded ring buffer with an adaptive background consumer.
/// </summary>
/// <remarks>
/// The contract lives in Core so any layer can accept a pipeline without
/// depending on a concrete threading implementation. <see cref="Flush"/>
/// makes all published events visible to <see cref="Events"/> (a no-op for
/// synchronous implementations; a drain barrier for buffered ones).
/// </remarks>
public interface IEventPipeline : IDisposable
{
    /// <summary>Submits an event for ingestion.</summary>
    void Publish(TraceEvent traceEvent);

    /// <summary>Makes all published events visible to <see cref="Events"/>.</summary>
    void Flush();

    /// <summary>Returns a point-in-time snapshot of retained events.</summary>
    IReadOnlyList<TraceEvent> Events();

    /// <summary>Discards all retained events and derived state.</summary>
    void Clear();
}
