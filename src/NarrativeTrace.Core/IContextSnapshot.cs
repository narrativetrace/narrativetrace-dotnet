// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Core;

/// <summary>
/// A portable, re-activatable capture of a trace's lineage, for carrying a
/// trace across a thread or scheduling boundary.
/// </summary>
/// <remarks>
/// <para>
/// Ambient trace state does not automatically follow work handed to a thread
/// pool, a custom scheduler, or a long-lived background worker. Without a
/// snapshot that work starts an unrelated trace and its spans are lost from the
/// narrative. Take one with <see cref="INarrativeContext.Snapshot"/> on the
/// originating thread, then <see cref="Activate"/> it inside the continuation.
/// </para>
/// <para>
/// Capture is eager and activation is late: the snapshot records the lineage as
/// it was at the moment it was taken, so spans opened on the original thread
/// afterwards are not reflected in it. It is immutable and safe to share, and
/// may be activated more than once and from more than one thread — each
/// activation yields its own independent scope.
/// </para>
/// <para>
/// <b>Propagation is bidirectional.</b> The lineage travels into the worker,
/// and the work traced there comes back: the capture the snapshot was taken
/// from reports the worker's spans from the moment they are published — not
/// from the moment the scope closes, because a framework routinely hands
/// control back to the caller in between. Helpers that publish their own
/// children use <see cref="ActivateWithoutAdoption"/> so nothing is counted
/// twice.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var snapshot = context.Snapshot();
/// await Task.Run(() =>
/// {
///     using var scope = snapshot.Activate();
///     DoBackgroundWork(); // spans attach to the originating trace
/// });
/// </code>
/// </example>
public interface IContextSnapshot
{
    /// <summary>
    /// Installs this snapshot's lineage on the calling thread until the returned
    /// scope is disposed.
    /// </summary>
    /// <returns>
    /// A scope that restores the previous state on dispose. Never
    /// <see langword="null"/>; always consume it with <c>using</c>, since
    /// dropping it leaves the borrowed lineage installed on a pool thread that
    /// will later be reused for unrelated work.
    /// </returns>
    IContextScope Activate();

    /// <summary>
    /// Installs this snapshot's lineage without joining the originating
    /// capture: work traced under the returned scope inherits the trace id and
    /// parent span, but does <b>not</b> appear in the snapshotting flow's
    /// <see cref="INarrativeContext.CaptureTrace"/>.
    /// </summary>
    /// <returns>A scope that restores the previous state on dispose, exactly as <see cref="Activate"/> does.</returns>
    /// <remarks>
    /// For helpers that own their children's presentation and publish them
    /// themselves — a fork-join merge re-emits its branches under the fork's
    /// parent span, a fire-and-forget launcher hands its children out behind a
    /// launcher marker. Adopting as well would show the same work twice, or
    /// make a parent's tree depend on when a detached child happened to finish.
    /// It hands over nothing at all, its own grandchildren included.
    /// <para>
    /// Ordinary propagation — a queued work item, a background task, any manual
    /// snapshot — wants <see cref="Activate"/>: the async work belongs to the
    /// trace that launched it.
    /// </para>
    /// </remarks>
    IContextScope ActivateWithoutAdoption();
}
