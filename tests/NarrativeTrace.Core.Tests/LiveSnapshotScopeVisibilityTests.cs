// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using NarrativeTrace.Runtime;
using Xunit;

namespace NarrativeTrace.Core.Tests;

/// <summary>
/// Pins the async adoption race deterministically: a worker publishes its spans
/// long before its scope closes, and a framework can hand control back to the
/// caller in between — ASP.NET Core completes a request while a
/// <c>Task.Run</c> launched from it is still inside its scope.
/// </summary>
/// <remarks>
/// The .NET mirror of Java's <c>LiveSnapshotScopeVisibilityTest</c>. No
/// sleeping and no waiting for the worker: two latches hold it at the exact
/// instant between "the call is published" and "the scope closes", which is the
/// window the race lives in.
/// </remarks>
public sealed class LiveSnapshotScopeVisibilityTests
{
    private const string Caller = "PlaceOrder";
    private const string WorkerCall = "NotifyOrderPlaced";

    [Fact]
    public void The_workers_call_is_visible_to_the_origin_while_the_scope_is_still_open()
    {
        var context = new SyncNarrativeContext(new NarrativeTraceConfig());
        context.EnterMethod("OrderService", Caller, []);
        var snapshot = context.Snapshot();
        context.ExitMethodWithReturn("\"ORD-1\"");

        using var worker = new HeldWorker(context, snapshot);
        try
        {
            worker.StartAndAwaitPublication();

            Assert.Equal(
                [Caller, WorkerCall],
                MethodNames(context.CaptureTrace()));
        }
        finally
        {
            worker.ReleaseAndJoin();
        }

        Assert.Equal(
            [Caller, WorkerCall],
            MethodNames(context.CaptureTrace()));
    }

    [Fact]
    public void The_workers_call_nests_under_the_caller_while_the_scope_is_still_open()
    {
        var context = new SyncNarrativeContext(new NarrativeTraceConfig());
        context.EnterMethod("OrderService", Caller, []);
        var snapshot = context.Snapshot();
        context.ExitMethodWithReturn("\"ORD-1\"");

        using var worker = new HeldWorker(context, snapshot);
        try
        {
            worker.StartAndAwaitPublication();

            var tree = context.CaptureTrace();

            var root = Assert.Single(tree.Roots);
            Assert.Equal(Caller, root.Signature.MethodName);
            var child = Assert.Single(root.Children);
            Assert.Equal(WorkerCall, child.Signature.MethodName);
        }
        finally
        {
            worker.ReleaseAndJoin();
        }
    }

    [Fact]
    public void A_scope_activated_without_adoption_stays_out_of_the_origins_trace_open_or_closed()
    {
        var context = new SyncNarrativeContext(new NarrativeTraceConfig());
        context.EnterMethod("OrderService", Caller, []);
        var snapshot = context.Snapshot();
        context.ExitMethodWithReturn("\"ORD-1\"");

        using var worker = new HeldWorker(context, snapshot, adopt: false);
        try
        {
            worker.StartAndAwaitPublication();

            // Helpers that re-emit their own children must not have them
            // counted twice.
            Assert.Equal(
                [Caller], MethodNames(context.CaptureTrace()));
        }
        finally
        {
            worker.ReleaseAndJoin();
        }

        Assert.Equal([Caller], MethodNames(context.CaptureTrace()));
    }

    [Fact]
    public void Closing_the_scope_keeps_the_call_and_adds_no_second_copy()
    {
        var context = new SyncNarrativeContext(new NarrativeTraceConfig());
        context.EnterMethod("OrderService", Caller, []);
        var snapshot = context.Snapshot();
        context.ExitMethodWithReturn("\"ORD-1\"");

        RunWorkerToCompletion(context, snapshot);

        Assert.Equal(
            [Caller, WorkerCall],
            MethodNames(context.CaptureTrace()));
    }

    [Fact]
    public void The_workers_call_survives_the_collection_of_its_state()
    {
        var context = new SyncNarrativeContext(new NarrativeTraceConfig());
        context.EnterMethod("OrderService", Caller, []);
        var snapshot = context.Snapshot();
        context.ExitMethodWithReturn("\"ORD-1\"");
        RunWorkerToCompletion(context, snapshot);

        // The live registry holds the worker weakly, so only adoption makes the
        // visibility permanent — collecting it deliberately is the assertion.
#pragma warning disable S1215
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
#pragma warning restore S1215

        Assert.Equal(
            [Caller, WorkerCall],
            MethodNames(context.CaptureTrace()));
    }

    [Fact]
    public void A_snapshot_whose_origin_is_gone_is_silent_at_activation_and_at_close()
    {
        var context = new SyncNarrativeContext(new NarrativeTraceConfig());
        context.EnterMethod("OrderService", Caller, []);
        context.ExitMethodWithReturn("\"ORD-1\"");
        var snapshot = context.Snapshot();
        context.Reset();

        // Registration must skip a retired origin exactly as adoption does — an
        // orphaned worker must neither fail nor resurrect the state it left.
        RunWorkerToCompletion(context, snapshot);

        Assert.Empty(context.CaptureTrace().Roots);
    }

    private static void RunWorkerToCompletion(
        SyncNarrativeContext context, IContextSnapshot snapshot)
    {
        using var worker = new HeldWorker(context, snapshot);
        worker.StartAndAwaitPublication();
        worker.ReleaseAndJoin();
    }

    internal static IReadOnlyList<string> MethodNames(TraceTree tree)
    {
        var names = new List<string>();
        CollectMethodNames(tree.Roots, names);
        return names;
    }

    private static void CollectMethodNames(
        IReadOnlyList<TraceNode> nodes, List<string> names)
    {
        for (var i = 0; i < nodes.Count; i++)
        {
            names.Add(nodes[i].Signature.MethodName);
            CollectMethodNames(nodes[i].Children, names);
        }
    }

    /// <summary>
    /// A worker that traces one call, announces it, and then blocks
    /// <i>before</i> closing its scope — the state a detached task is in when
    /// the request that launched it has already returned.
    /// </summary>
    private sealed class HeldWorker : IDisposable
    {
        private readonly SyncNarrativeContext _context;
        private readonly IContextSnapshot _snapshot;
        private readonly bool _adopt;
        private readonly ManualResetEventSlim _published = new(false);
        private readonly ManualResetEventSlim _release = new(false);
        private readonly Thread _thread;

        internal HeldWorker(
            SyncNarrativeContext context,
            IContextSnapshot snapshot,
            bool adopt = true)
        {
            _context = context;
            _snapshot = snapshot;
            _adopt = adopt;
            _thread = new Thread(Run)
            {
                Name = "async-notify-1",
                IsBackground = true,
            };
        }

        internal void StartAndAwaitPublication()
        {
            _thread.Start();
            Assert.True(_published.Wait(TimeSpan.FromSeconds(5)));
        }

        internal void ReleaseAndJoin()
        {
            _release.Set();
            Assert.True(_thread.Join(TimeSpan.FromSeconds(5)));
        }

        public void Dispose()
        {
            _release.Set();
            _published.Dispose();
            _release.Dispose();
        }

        private void Run()
        {
            using var scope = _adopt
                ? _snapshot.Activate()
                : _snapshot.ActivateWithoutAdoption();
            _context.EnterMethod("NotificationService", WorkerCall, []);
            _context.ExitMethodWithReturn("true");
            _published.Set();
            _release.Wait(TimeSpan.FromSeconds(5));
        }
    }
}
