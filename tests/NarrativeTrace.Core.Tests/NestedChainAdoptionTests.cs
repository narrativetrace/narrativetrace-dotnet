// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using NarrativeTrace.Runtime;
using Xunit;

namespace NarrativeTrace.Core.Tests;

/// <summary>
/// Pins the nested chain: origin → worker → grandchild, where the worker itself
/// activates a snapshot of its own capture.
/// </summary>
/// <remarks>
/// The .NET mirror of Java's <c>NestedChainAdoptionTest</c>. Hand-over that
/// carried only a child's <i>own</i> spans would stop one hop short: the
/// grandchild's calls would reach the worker and stop there, invisible to the
/// origin — the only flow anyone captures on. A chain of two async hops is not
/// exotic: a controller dispatches to a service that dispatches to a client.
/// <para>
/// Three latches, no sleeping. The grandchild publishes and blocks before
/// closing its scope; the worker blocks before closing its own. That holds the
/// chain at each of the three instants that matter — all scopes open, the
/// grandchild closed, everything closed — and the same tree is asserted at all
/// three, so a span that appears and then vanishes (or is counted twice) fails.
/// </para>
/// </remarks>
public sealed class NestedChainAdoptionTests : IDisposable
{
    private const string OriginCall = "PlaceOrder";
    private const string WorkerCall = "NotifyOrderPlaced";
    private const string GrandchildCall = "Send";

    private readonly SyncNarrativeContext _context =
        new(new NarrativeTraceConfig());

    private readonly Chain _chain = new();

    public void Dispose()
    {
        _chain.ReleaseEverything();
        _context.Reset();
    }

    [Fact]
    public void The_grandchilds_call_is_visible_to_the_origin_while_every_scope_is_still_open()
    {
        _chain.StartFrom(_context);

        Assert.Equal(
            [OriginCall, WorkerCall, GrandchildCall],
            MethodNames(_context.CaptureTrace()));
    }

    [Fact]
    public void The_grandchilds_call_stays_visible_once_its_own_scope_closes()
    {
        _chain.StartFrom(_context);

        _chain.CloseGrandchild();

        Assert.Equal(
            [OriginCall, WorkerCall, GrandchildCall],
            MethodNames(_context.CaptureTrace()));
    }

    [Fact]
    public void The_grandchilds_call_stays_visible_once_every_scope_closes()
    {
        _chain.StartFrom(_context);

        _chain.CloseGrandchild();
        _chain.CloseWorker();

        Assert.Equal(
            [OriginCall, WorkerCall, GrandchildCall],
            MethodNames(_context.CaptureTrace()));
    }

    [Fact]
    public void The_worker_closing_before_its_grandchild_still_hands_the_grandchild_over()
    {
        _chain.StartFrom(_context);

        // The order a framework actually produces when the outer task returns
        // first: the worker's scope closes while its own child is still running.
        _chain.CloseWorker();

        Assert.Equal(
            [OriginCall, WorkerCall, GrandchildCall],
            MethodNames(_context.CaptureTrace()));

        _chain.CloseGrandchild();

        Assert.Equal(
            [OriginCall, WorkerCall, GrandchildCall],
            MethodNames(_context.CaptureTrace()));
    }

    [Fact]
    public void The_calls_nest_origin_to_worker_to_grandchild()
    {
        _chain.StartFrom(_context);
        _chain.CloseGrandchild();
        _chain.CloseWorker();

        var tree = _context.CaptureTrace();

        var origin = Assert.Single(tree.Roots);
        Assert.Equal(OriginCall, origin.Signature.MethodName);
        var worker = Assert.Single(origin.Children);
        Assert.Equal(WorkerCall, worker.Signature.MethodName);
        var grandchild = Assert.Single(worker.Children);
        Assert.Equal(GrandchildCall, grandchild.Signature.MethodName);
    }

    [Fact]
    public void Every_call_in_the_chain_carries_the_origins_trace_id()
    {
        _chain.StartFrom(_context);
        _chain.CloseGrandchild();
        _chain.CloseWorker();

        var traceIds = TraceIds(_context.CaptureTrace());

        Assert.Equal(3, traceIds.Count);
        Assert.All(traceIds, id => Assert.Equal(traceIds[0], id));
    }

    [Fact]
    public void A_grandchild_behind_an_unadopting_worker_reaches_neither_the_worker_nor_the_origin()
    {
        _chain.WithWorkerAdoption(false).StartFrom(_context);

        // The worker publishes its own children; its subtree must not be
        // counted twice.
        Assert.Equal(
            [OriginCall], MethodNames(_context.CaptureTrace()));

        _chain.CloseGrandchild();
        _chain.CloseWorker();

        Assert.Equal(
            [OriginCall], MethodNames(_context.CaptureTrace()));
    }

    [Fact]
    public void A_grandchild_activated_without_adoption_stays_out_of_the_origins_trace()
    {
        _chain.WithGrandchildAdoption(false).StartFrom(_context);

        Assert.Equal(
            [OriginCall, WorkerCall],
            MethodNames(_context.CaptureTrace()));

        _chain.CloseGrandchild();
        _chain.CloseWorker();

        // What the child never joined, it cannot hand over.
        Assert.Equal(
            [OriginCall, WorkerCall],
            MethodNames(_context.CaptureTrace()));
    }

    [Fact]
    public void A_whole_chain_over_the_ceiling_is_refused_and_counted()
    {
        // A ceiling of one span: the worker's hand-over carries itself plus the
        // grandchild it adopted, so it is refused at the hop where it stops
        // fitting rather than stranding the grandchild as a parentless root.
        var context = new SyncNarrativeContext(
            new NarrativeTraceConfig(), sink: null, adoptionCeiling: 1);
        _chain.StartFrom(context);
        _chain.CloseGrandchild();
        _chain.CloseWorker();

        Assert.Equal([OriginCall], MethodNames(context.CaptureTrace()));

        var loss = ((ITraceLossSource)context).TraceLoss;
        Assert.Equal(1, loss.RefusedScopes);
        Assert.Equal(2, loss.RefusedSpans);
        Assert.False(loss.IsLossless);
    }

    internal static IReadOnlyList<string> MethodNames(TraceTree tree)
    {
        var names = new List<string>();
        Collect(tree.Roots, node => node.Signature.MethodName, names);
        return names;
    }

    private static IReadOnlyList<string> TraceIds(TraceTree tree)
    {
        var ids = new List<string>();
        Collect(
            tree.Roots,
            node => node.SpanContext?.TraceId.ToString(),
            ids);
        return ids;
    }

    private static void Collect(
        IReadOnlyList<TraceNode> nodes,
        Func<TraceNode, string?> select,
        List<string> into)
    {
        for (var i = 0; i < nodes.Count; i++)
        {
            if (select(nodes[i]) is { } value)
            {
                into.Add(value);
            }

            Collect(nodes[i].Children, select, into);
        }
    }

    /// <summary>
    /// Two threads held open by latches: the worker activates a snapshot of the
    /// origin, the grandchild a snapshot of the worker. Each blocks after
    /// publishing its call and closes only when released, so the test controls
    /// which scopes are open at the moment it captures.
    /// </summary>
    private sealed class Chain
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

        private readonly ManualResetEventSlim _grandchildPublished = new(false);
        private readonly ManualResetEventSlim _releaseGrandchild = new(false);
        private readonly ManualResetEventSlim _grandchildClosed = new(false);
        private readonly ManualResetEventSlim _releaseWorker = new(false);
        private readonly ManualResetEventSlim _workerClosed = new(false);

        private Thread? _worker;
        private volatile Thread? _grandchild;
        private bool _workerAdopts = true;
        private bool _grandchildAdopts = true;

        internal Chain WithWorkerAdoption(bool adopts)
        {
            _workerAdopts = adopts;
            return this;
        }

        internal Chain WithGrandchildAdoption(bool adopts)
        {
            _grandchildAdopts = adopts;
            return this;
        }

        internal void StartFrom(SyncNarrativeContext context)
        {
            context.EnterMethod("OrderService", OriginCall, []);
            var snapshot = context.Snapshot();
            context.ExitMethodWithReturn("\"ORD-1\"");

            _worker = new Thread(() => RunWorker(context, snapshot))
            {
                Name = "async-notify-1",
                IsBackground = true,
            };
            _worker.Start();
            Assert.True(_grandchildPublished.Wait(Timeout));
        }

        /// <summary>
        /// Deliberately does <i>not</i> join its grandchild before closing: a
        /// worker whose own scope ends while its child is still running is the
        /// ordering a framework actually produces, and it is the ordering that
        /// decides whether hand-over carries the whole chain.
        /// </summary>
        private void RunWorker(
            SyncNarrativeContext context, IContextSnapshot snapshot)
        {
            using (Activated(snapshot, _workerAdopts))
            {
                context.EnterMethod("NotificationService", WorkerCall, []);
                var nested = context.Snapshot();
                context.ExitMethodWithReturn("true");
                _grandchild = new Thread(() => RunGrandchild(context, nested))
                {
                    Name = "async-email-1",
                    IsBackground = true,
                };
                _grandchild.Start();
                _releaseWorker.Wait(Timeout);
            }

            _workerClosed.Set();
        }

        private void RunGrandchild(
            SyncNarrativeContext context, IContextSnapshot snapshot)
        {
            using (Activated(snapshot, _grandchildAdopts))
            {
                context.EnterMethod("EmailGateway", GrandchildCall, []);
                context.ExitMethodWithReturn("true");
                _grandchildPublished.Set();
                _releaseGrandchild.Wait(Timeout);
            }

            _grandchildClosed.Set();
        }

        private static IContextScope Activated(
            IContextSnapshot snapshot, bool adopts) =>
            adopts ? snapshot.Activate() : snapshot.ActivateWithoutAdoption();

        internal void CloseGrandchild()
        {
            _releaseGrandchild.Set();
            Assert.True(_grandchildClosed.Wait(Timeout));
        }

        internal void CloseWorker()
        {
            _releaseWorker.Set();
            Assert.True(_workerClosed.Wait(Timeout));
        }

        internal void ReleaseEverything()
        {
            _releaseGrandchild.Set();
            _releaseWorker.Set();
            _worker?.Join(Timeout);
            _grandchild?.Join(Timeout);
        }
    }
}
