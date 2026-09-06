// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Core;

/// <summary>
/// Notifications a context receives about the concurrent groups running inside
/// it, so a fork renders as one parallel block rather than scattered siblings.
/// </summary>
/// <remarks>
/// <para>
/// Implemented by contexts that understand concurrency and called by the
/// fork helpers (<c>ForkJoinGroup</c>, <c>FireAndForgetGroup</c>) — it is a
/// notification sink, not something application code calls directly. Callers
/// should type-test for it, since not every
/// <see cref="INarrativeContext"/> implements it.
/// </para>
/// <para>
/// <b>Ordering contract.</b> <see cref="OnForkCreated"/> or
/// <see cref="OnFireAndForgetLaunched"/> must precede any span captured in the
/// group, and <see cref="OnJoinComplete"/> closes a fork-join group. A
/// fire-and-forget group has no completion callback at all — by definition
/// nothing waits for it — so it is never followed by
/// <see cref="OnJoinComplete"/>.
/// </para>
/// </remarks>
public interface IConcurrencyLifecycle
{
    /// <summary>Signals that a fork-join group is about to spawn its branches.</summary>
    /// <param name="groupId">
    /// The group's identifier, matching the
    /// <see cref="ConcurrencyInfo.GroupId"/> its branches will carry. Unique
    /// within a trace.
    /// </param>
    void OnForkCreated(string groupId);

    /// <summary>Signals that every branch of a fork-join group has finished and been joined.</summary>
    /// <param name="groupId">The group identifier previously passed to <see cref="OnForkCreated"/>.</param>
    /// <param name="memberCount">
    /// How many branches the group ran. May exceed the number of spans actually
    /// captured, since a branch that traced nothing still counts as a member.
    /// </param>
    /// <param name="wallTimeTicks">
    /// Elapsed wall-clock time for the whole group in
    /// <see cref="TimeSpan"/> ticks — the duration of the slowest branch plus
    /// join overhead, not the sum of the branches. Comparing it against that sum
    /// is what reveals a fork that did not actually parallelize.
    /// </param>
    void OnJoinComplete(
        string groupId, int memberCount,
        long wallTimeTicks);

    /// <summary>Signals that fire-and-forget work has been launched without being awaited.</summary>
    /// <param name="groupId">The group's identifier, carried by any spans the work captures.</param>
    /// <remarks>
    /// Has no completion counterpart: the launched work may still be running
    /// when the trace is captured, in which case its spans are
    /// <see cref="Incomplete"/> or absent entirely.
    /// </remarks>
    void OnFireAndForgetLaunched(string groupId);
}
