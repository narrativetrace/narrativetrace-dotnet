// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Core;

/// <summary>
/// An event stream a consumer can attach a live subscriber to.
/// </summary>
/// <remarks>
/// <para>
/// Separate from <see cref="IEventPipeline"/> on purpose: publishing and
/// retaining events is the pipeline's job, while <em>observing</em> them as
/// they are drained is an optional capability — a trivial synchronous store has
/// nothing to notify. Splitting it keeps the pipeline contract implementable
/// without a subscriber list, and lets a layer that only wants to listen
/// (the <c>ILogger</c> bridge, the OpenTelemetry live listener) depend on the
/// listening half alone.
/// </para>
/// <para>
/// Subscribers are best-effort observers of a best-effort pipeline: an
/// implementation under load may shed events before any subscriber sees them,
/// so a subscriber must never be the only record of something that matters.
/// </para>
/// </remarks>
public interface IEventSubscribable
{
    /// <summary>Registers a subscriber notified as events are drained.</summary>
    /// <param name="subscriber">
    /// Called once per drained event. Implementations isolate subscribers from
    /// each other, so a throwing subscriber does not stop the others.
    /// </param>
    void Subscribe(Action<TraceEvent> subscriber);
}
