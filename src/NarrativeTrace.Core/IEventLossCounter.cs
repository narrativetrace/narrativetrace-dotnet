// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Core;

/// <summary>
/// Optional capability for a pipeline that can shed events under load and
/// counts what it shed.
/// </summary>
/// <remarks>
/// Separate from <see cref="IEventPipeline"/> for the same reason
/// <see cref="IEventSubscribable"/> is: a pipeline that never drops anything
/// has no counter to expose, and a contract that forced one would make every
/// implementation answer a constant zero. A context surfaces this through
/// <see cref="ITraceLossSource"/>, which is where a reporter should read it —
/// a counter nothing reads is how silent loss survives.
/// </remarks>
public interface IEventLossCounter
{
    /// <summary>
    /// Events this pipeline discarded rather than retained, since it was
    /// created. Monotonic, and never negative.
    /// </summary>
    long DroppedEventCount { get; }
}
