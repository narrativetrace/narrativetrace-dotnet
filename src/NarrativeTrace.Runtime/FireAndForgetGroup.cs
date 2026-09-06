// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using System.Threading;
using System.Threading.Tasks;

namespace NarrativeTrace.Runtime;

/// <summary>
/// Launches background work that is never awaited, leaving a visible marker in
/// the parent trace where it was spawned.
/// </summary>
/// <remarks>
/// <para>
/// The counterpart to <see cref="ForkJoinGroup"/> for work nobody waits for.
/// Because there is no join, the launched branches cannot be merged back
/// reliably — so <see cref="Create"/> immediately grafts a synthetic
/// <c>fire-and-forget</c> node into the parent trace. That marker is the point:
/// without it, background work would be invisible in the narrative, and a
/// reader would see a gap with no explanation.
/// </para>
/// <para>
/// The marker is recorded as <see cref="Incomplete"/> and stays that way. This
/// is honest rather than a defect — the launcher genuinely does not know whether
/// the work finished. Use <see cref="ChildRoots"/> to inspect whatever has
/// completed so far.
/// </para>
/// <para>
/// <b>Not thread-safe in itself</b> — like <see cref="ForkJoinGroup"/>, the
/// bookkeeping list is unsynchronized, so call <see cref="Launch"/> from the
/// owning thread.
/// </para>
/// </remarks>
public sealed class FireAndForgetGroup
{
    private static int _groupCounter;

    private readonly INarrativeContext _context;
    private readonly List<LaunchedTask> _launched = new();

    /// <summary>
    /// This group's identifier, carried by the launcher marker and by every span
    /// the launched work captures. Unique within the process.
    /// </summary>
    public string GroupId { get; }

    private FireAndForgetGroup(
        INarrativeContext context, string groupId)
    {
        _context = context;
        GroupId = groupId;
    }

    /// <summary>
    /// Opens a fire-and-forget group and immediately marks the launch point in
    /// the parent trace.
    /// </summary>
    /// <param name="context">The context to graft the launcher marker into.</param>
    /// <param name="launchingClassName">
    /// The class shown on the marker node. Defaults to the empty string, which
    /// renders an unattributed marker — pass the launching type's name to make
    /// the trace legible.
    /// </param>
    /// <returns>A group ready to <see cref="Launch"/> work from.</returns>
    /// <remarks>
    /// The marker is grafted here, at creation — so a group that is created and
    /// never launched from still leaves a <c>fire-and-forget</c> node behind.
    /// </remarks>
    public static FireAndForgetGroup Create(
        INarrativeContext context,
        string launchingClassName = "")
    {
        var id = Interlocked.Increment(
            ref _groupCounter);
        var group = new FireAndForgetGroup(
            context, $"fanf-{id}");
        context.GraftChild(
            group.CreateLauncherNode(launchingClassName));
        return group;
    }

    /// <summary>
    /// Builds the synthetic launcher node that marks, in the parent trace,
    /// where background work was spawned: method <c>fire-and-forget</c>, no
    /// children, incomplete outcome, tagged with the group id and
    /// <see cref="ConcurrencyKind.FireAndForget"/>.
    /// </summary>
    private TraceNode CreateLauncherNode(string className)
    {
        var thread = Thread.CurrentThread;
        var signature = new MethodSignature(
            className, "fire-and-forget", []);
        var info = new ConcurrencyInfo(
            GroupId, "fire-and-forget", thread.ManagedThreadId,
            thread.Name, thread.IsThreadPoolThread,
            ConcurrencyKind.FireAndForget);
        return new TraceNode(
            signature, new Incomplete(), [], 0L, 0L, info);
    }

    /// <summary>Starts background work on the thread pool and returns immediately.</summary>
    /// <param name="fn">
    /// The work to run. Receives its own isolated child context and must capture
    /// through that argument rather than the outer context.
    /// </param>
    /// <remarks>
    /// Nothing waits for the work and nothing observes its result. An exception
    /// thrown inside <paramref name="fn"/> faults that branch's task, which is
    /// never awaited — so it is silently swallowed rather than surfacing here or
    /// crashing the process, and the branch is then excluded from
    /// <see cref="ChildRoots"/>. Handle errors inside <paramref name="fn"/>.
    /// </remarks>
    public void Launch(Action<INarrativeContext> fn)
    {
        var isolated = ConcurrencySupport.ChildContextOf(_context);
        var task = Task.Run(() =>
            RunLaunched(fn, isolated));
        _launched.Add(new LaunchedTask(
            task, isolated, _launched.Count));
        NotifyLaunched();
    }

    private void NotifyLaunched()
    {
        if (_context is IConcurrencyLifecycle lc)
        {
            lc.OnFireAndForgetLaunched(GroupId);
        }
    }

    /// <summary>Collects the traces of launched work that has already finished successfully.</summary>
    /// <returns>
    /// One tree per branch that has completed without faulting, in launch order.
    /// A point-in-time view: branches still running, and branches that threw, are
    /// both omitted — so an empty result means "nothing has succeeded yet", not
    /// "nothing was launched". Calling it again later can return more.
    /// </returns>
    /// <remarks>
    /// These trees are not grafted into the parent trace; inspect them directly.
    /// Mainly a testing and diagnostic aid — in production the launcher marker is
    /// usually all the parent trace should claim about work it never awaited.
    /// </remarks>
    public IReadOnlyList<TraceTree> ChildRoots()
    {
        var results = new List<TraceTree>();
        foreach (var launch in _launched)
        {
            if (launch.Task.IsCompleted
                && !launch.Task.IsFaulted)
            {
                results.Add(
                    launch.Context.CaptureTrace());
            }
        }

        return results;
    }

    private static void RunLaunched(
        Action<INarrativeContext> fn,
        INarrativeContext isolated)
    {
        fn(isolated);
    }

    private sealed class LaunchedTask
    {
        public Task Task { get; }
        public INarrativeContext Context { get; }
        public int Index { get; }

        public LaunchedTask(
            Task task,
            INarrativeContext context,
            int index)
        {
            Task = task;
            Context = context;
            Index = index;
        }
    }
}
