// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace NarrativeTrace.Runtime;

/// <summary>
/// Runs branches concurrently and merges their traces back under the span that
/// forked them, so a parallel section reads as one block instead of vanishing.
/// </summary>
/// <remarks>
/// <para>
/// Each branch captures into its own isolated child context — which is what
/// keeps concurrent spans from interleaving — and
/// <see cref="JoinAsync"/> grafts every branch's roots back into the parent
/// trace. Skipping the join means the branches' spans never reach the parent
/// trace at all: the work still runs, but the narrative loses it.
/// </para>
/// <para>
/// <b>Not thread-safe in itself.</b> The group tracks its branches in an
/// unsynchronized list, so <see cref="Fork{T}"/> and <see cref="JoinAsync"/>
/// must be called from the single thread that owns the group. The branches then
/// run concurrently; it is only the bookkeeping that is single-threaded.
/// </para>
/// <para>Single-use: create one group per parallel section, join it, and discard it.</para>
/// </remarks>
/// <example>
/// <code>
/// var group = ForkJoinGroup.Create(context);
/// var user = group.Fork(ctx => LoadUser(ctx, id));
/// var cart = group.Fork(ctx => LoadCart(ctx, id));
/// await group.JoinAsync();          // required: grafts both branches back
/// return Combine(await user, await cart);
/// </code>
/// </example>
public sealed class ForkJoinGroup
{
    private static int _groupCounter;

    private readonly INarrativeContext _context;
    private readonly CancellationToken _cancellation;
    private readonly List<ForkedTask> _forks = new();

    /// <summary>
    /// This group's identifier, carried by every span its branches capture and
    /// used to regroup them during rendering. Unique within the process.
    /// </summary>
    public string GroupId { get; }

    private ForkJoinGroup(
        INarrativeContext context,
        string groupId,
        CancellationToken cancellation)
    {
        _context = context;
        GroupId = groupId;
        _cancellation = cancellation;
    }

    /// <summary>Opens a fork-join group against a context.</summary>
    /// <param name="context">
    /// The context whose current span the branches will merge back under.
    /// Notified of the group's creation when it implements
    /// <see cref="IConcurrencyLifecycle"/>; a context that does not simply
    /// receives no notification.
    /// </param>
    /// <param name="cancellation">Cancels branches that have not yet started or completed.</param>
    /// <returns>A new, empty group ready to <see cref="Fork{T}"/> into.</returns>
    public static ForkJoinGroup Create(
        INarrativeContext context,
        CancellationToken cancellation = default)
    {
        var id = Interlocked.Increment(
            ref _groupCounter);
        var groupId = $"fork-{id}";
        NotifyForkCreated(context, groupId);
        return new ForkJoinGroup(
            context, groupId, cancellation);
    }

    private static void NotifyForkCreated(
        INarrativeContext context, string groupId)
    {
        if (context is IConcurrencyLifecycle lifecycle)
        {
            lifecycle.OnForkCreated(groupId);
        }
    }

    /// <summary>
    /// Runs two branches concurrently and joins them — the whole
    /// create/fork/join sequence for the common two-branch case.
    /// </summary>
    /// <typeparam name="T1">The first branch's result type.</typeparam>
    /// <typeparam name="T2">The second branch's result type.</typeparam>
    /// <param name="context">The context the branches merge back into.</param>
    /// <param name="fn1">The first branch. Receives its own isolated context — capture through that, not the outer one.</param>
    /// <param name="fn2">The second branch, likewise.</param>
    /// <param name="cancellation">Cancels branches that have not yet started or completed.</param>
    /// <returns>Both results, once both branches have completed and been grafted.</returns>
    /// <remarks>
    /// Prefer this to hand-rolling the sequence: it cannot forget the join. If a
    /// branch throws, the exception surfaces here and the branches already
    /// finished are still grafted.
    /// </remarks>
    public static async Task<(T1, T2)> WhenAll<T1, T2>(
        INarrativeContext context,
        Func<INarrativeContext, T1> fn1,
        Func<INarrativeContext, T2> fn2,
        CancellationToken cancellation = default)
    {
        var group = Create(context, cancellation);
        var t1 = group.Fork(fn1);
        var t2 = group.Fork(fn2);
        await group.JoinAsync();
        return (await t1, await t2);
    }

    /// <summary>Starts one branch of the group on the thread pool.</summary>
    /// <typeparam name="T">The branch's result type.</typeparam>
    /// <param name="fn">
    /// The branch's work. It is handed an <b>isolated child context</b> and must
    /// capture through that argument — capturing through the outer context
    /// instead defeats the isolation and interleaves spans.
    /// </param>
    /// <returns>
    /// A task for the branch's result. Awaiting it tells you the work finished;
    /// it does <b>not</b> graft the branch's trace. Only
    /// <see cref="JoinAsync"/> does that.
    /// </returns>
    /// <remarks>
    /// Call from the group's owning thread only. Branches start immediately
    /// rather than on join, so work begins as soon as this returns.
    /// </remarks>
    public Task<T> Fork<T>(
        Func<INarrativeContext, T> fn)
    {
        var isolated = ConcurrencySupport.ChildContextOf(_context);
        var fork = new ForkedTask(
            null!, isolated, _forks.Count);
        var task = Task.Run(() =>
        {
            fork.ThreadInfo = CaptureThread();
            return fn(isolated);
        }, _cancellation);
        fork.Task = task;
        _forks.Add(fork);
        return task;
    }

    /// <summary>
    /// Waits for every branch, grafts their traces under the parent span, and
    /// closes the group.
    /// </summary>
    /// <returns>A task completing once all branches have finished and been merged.</returns>
    /// <exception cref="AggregateException">One or more branches threw; surfaced by the underlying <see cref="Task.WhenAll(Task[])"/>.</exception>
    /// <remarks>
    /// The step that makes the branches visible in the narrative — a group that
    /// is never joined contributes nothing to the trace. Call it exactly once,
    /// and after every <see cref="Fork{T}"/> for the group has been issued;
    /// forking after the join leaves that branch unmerged. The wall time
    /// reported to <see cref="IConcurrencyLifecycle.OnJoinComplete"/> is the
    /// longest branch's, which is what lets a renderer spot a fork that did not
    /// actually run in parallel.
    /// </remarks>
    public async Task JoinAsync()
    {
        await Task.WhenAll(
            _forks.ConvertAll(f => f.Task));
        GraftAll();
        NotifyJoinComplete();
    }

    private void NotifyJoinComplete()
    {
        if (_context is not IConcurrencyLifecycle lc)
        {
            return;
        }

        var wallTicks = MaxDurationTicks();
        lc.OnJoinComplete(
            GroupId, _forks.Count, wallTicks);
    }

    private long MaxDurationTicks()
    {
        return _forks
            .SelectMany(f =>
                f.Context.CaptureTrace().Roots
                    .Select(r => r.DurationTicks))
            .DefaultIfEmpty(0)
            .Max();
    }

    private static ThreadSnapshot CaptureThread()
    {
        var t = Thread.CurrentThread;
        return new ThreadSnapshot
        {
            Id = t.ManagedThreadId,
            Name = t.Name,
            IsPool = t.IsThreadPoolThread,
        };
    }

    private void GraftAll()
    {
        foreach (var fork in _forks)
        {
            GraftFork(fork);
        }
    }

    private void GraftFork(ForkedTask fork)
    {
        var trace = fork.Context.CaptureTrace();
        var label = DeriveLabel(
            trace.Roots, fork.Index);
        var info = BuildConcurrencyInfo(
            label, fork.ThreadInfo);

        foreach (var root in trace.Roots)
        {
            _context.GraftChild(
                root with { Concurrency = info });
        }
    }

    private ConcurrencyInfo BuildConcurrencyInfo(
        string label, ThreadSnapshot thread)
    {
        return new ConcurrencyInfo(
            GroupId, label, thread.Id,
            thread.Name, thread.IsPool,
            ConcurrencyKind.ForkJoin);
    }

    private static string DeriveLabel(
        IReadOnlyList<TraceNode> roots, int index)
    {
        if (roots.Count > 0)
        {
            var sig = roots[0].Signature;
            return $"{sig.ClassName}.{sig.MethodName}";
        }

        return $"task-{index}";
    }

    private sealed class ForkedTask
    {
        public Task Task { get; set; }
        public INarrativeContext Context { get; }
        public int Index { get; }
        public ThreadSnapshot ThreadInfo { get; set; }

        public ForkedTask(
            Task task,
            INarrativeContext context,
            int index)
        {
            Task = task;
            Context = context;
            Index = index;
        }
    }

    internal readonly struct ThreadSnapshot
    {
        public int Id { get; init; }
        public string? Name { get; init; }
        public bool IsPool { get; init; }
    }
}
