// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Core;

/// <summary>
/// Convenience wrappers for executor and <c>Task.Run</c> call sites: activate
/// a snapshot around a delegate so trace events it emits attach to the
/// captured trace, then restore the previous context. Mirrors Java
/// <c>ContextSnapshot.wrap(Runnable/Callable/Supplier)</c>.
/// </summary>
public static class ContextSnapshotExtensions
{
    /// <summary>
    /// Wraps an action so it runs with this snapshot's trace lineage installed.
    /// </summary>
    /// <param name="snapshot">The captured lineage to activate around the call.</param>
    /// <param name="task">The work to wrap. Not invoked here — only when the returned action is called.</param>
    /// <returns>
    /// A new action to hand to the executor in place of the original. The
    /// wrapper is reusable: each invocation activates and releases its own
    /// scope, so the same wrapped action may be queued more than once.
    /// </returns>
    /// <remarks>
    /// Wrap at the point of <i>scheduling</i>, on the thread that owns the
    /// trace — wrapping inside the queued work is too late, since the lineage is
    /// already lost by then. Exceptions from <paramref name="task"/> propagate
    /// unchanged and the scope is still released.
    /// </remarks>
    /// <example>
    /// <code>
    /// var snapshot = context.Snapshot();
    /// ThreadPool.QueueUserWorkItem(_ => snapshot.Wrap(DoWork)());
    /// </code>
    /// </example>
    public static Action Wrap(
        this IContextSnapshot snapshot, Action task)
    {
        return () =>
        {
            using var scope = snapshot.Activate();
            task();
        };
    }

    /// <summary>
    /// Wraps a value-returning function so it runs with this snapshot's trace
    /// lineage installed.
    /// </summary>
    /// <typeparam name="T">The function's return type.</typeparam>
    /// <param name="snapshot">The captured lineage to activate around the call.</param>
    /// <param name="task">The work to wrap. Not invoked here — only when the returned function is called.</param>
    /// <returns>A new function returning whatever <paramref name="task"/> returns.</returns>
    /// <remarks>
    /// Note what this does <b>not</b> do for async work: wrapping a
    /// <c>Func&lt;Task&gt;</c> scopes only up to the first incomplete await,
    /// because the scope is released when the function returns its task, not
    /// when that task completes. For an async delegate, activate the snapshot
    /// inside the async method instead.
    /// </remarks>
    public static Func<T> Wrap<T>(
        this IContextSnapshot snapshot, Func<T> task)
    {
        return () =>
        {
            using var scope = snapshot.Activate();
            return task();
        };
    }
}
