// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using NarrativeTrace.Runtime;
using Xunit;

namespace NarrativeTrace.Core.Tests;

/// <summary>
/// Work done on another thread under a propagated snapshot belongs to the
/// snapshotting flow's trace: the live event stream already narrates it, so the
/// captured tree must not silently lose it.
/// </summary>
/// <remarks>
/// The .NET mirror of Java's <c>AsyncSnapshotAdoptionTest</c> — same eight
/// cases, same order, so the two ledgers read across repositories. Worker
/// threads are dedicated and joined rather than pooled, and nothing here
/// sleeps: every hand-off is a <see cref="Thread.Join(int)"/> or a latch.
/// </remarks>
public sealed class AsyncSnapshotAdoptionTests : IDisposable
{
    private readonly SyncNarrativeContext _context =
        new(new NarrativeTraceConfig());

    public void Dispose()
    {
        _context.Reset();
    }

    [Fact]
    public void Async_work_started_after_the_parent_returned_becomes_a_second_root()
    {
        _context.EnterMethod("OrderService", "PlaceOrder", []);
        _context.ExitMethodWithReturn("\"ORD-1\"");
        RunUnderSnapshot(_context.Snapshot(), () =>
            TraceCall("NotificationService", "NotifyOrderPlaced"));

        var roots = _context.CaptureTrace().Roots;

        Assert.Equal(
            ["OrderService.PlaceOrder", "NotificationService.NotifyOrderPlaced"],
            Names(roots));
    }

    [Fact]
    public void Async_work_started_inside_the_parent_becomes_a_child()
    {
        _context.EnterMethod("OrderService", "PlaceOrder", []);
        RunUnderSnapshot(_context.Snapshot(), () =>
            TraceCall("NotificationService", "NotifyOrderPlaced"));
        _context.ExitMethodWithReturn("\"ORD-1\"");

        var roots = _context.CaptureTrace().Roots;

        Assert.Equal(["OrderService.PlaceOrder"], Names(roots));
        Assert.Equal(
            ["NotificationService.NotifyOrderPlaced"],
            Names(roots[0].Children));
    }

    [Fact]
    public void The_adopted_node_reports_the_thread_that_ran_it()
    {
        _context.EnterMethod("OrderService", "PlaceOrder", []);
        _context.ExitMethodWithReturn("\"ORD-1\"");
        RunUnderSnapshot(_context.Snapshot(), () =>
            TraceCall("NotificationService", "NotifyOrderPlaced"));

        var adopted = _context.CaptureTrace().Roots[1];

        Assert.NotNull(adopted.Concurrency);
        Assert.Equal("async-worker", adopted.Concurrency!.ThreadName);
    }

    [Fact]
    public void Nested_async_calls_keep_their_own_shape()
    {
        _context.EnterMethod("OrderService", "PlaceOrder", []);
        RunUnderSnapshot(_context.Snapshot(), () =>
        {
            _context.EnterMethod("NotificationService", "NotifyOrderPlaced", []);
            _context.EnterMethod("EmailGateway", "Send", []);
            _context.ExitMethodWithReturn("true");
            _context.ExitMethodWithReturn("true");
        });
        _context.ExitMethodWithReturn("\"ORD-1\"");

        var root = _context.CaptureTrace().Roots[0];

        Assert.Equal(
            ["NotificationService.NotifyOrderPlaced"], Names(root.Children));
        Assert.Equal(
            ["EmailGateway.Send"], Names(root.Children[0].Children));
    }

    [Fact]
    public void Work_on_a_thread_that_never_activated_the_snapshot_stays_out()
    {
        _context.EnterMethod("OrderService", "PlaceOrder", []);
        _context.ExitMethodWithReturn("\"ORD-1\"");
        var unrelated = new SyncNarrativeContext(new NarrativeTraceConfig());
        RunOnWorker(() =>
        {
            unrelated.EnterMethod("Unrelated", "BackgroundSweep", []);
            unrelated.ExitMethodWithReturn("true");
        });

        var roots = _context.CaptureTrace().Roots;

        Assert.Equal(["OrderService.PlaceOrder"], Names(roots));
    }

    [Fact]
    public async Task Fork_group_children_are_not_counted_twice()
    {
        _context.EnterMethod("OrderService", "PlaceOrder", []);
        var group = ForkJoinGroup.Create(_context);
        group.Fork(ctx =>
        {
            ctx.EnterMethod("InventoryService", "Reserve", []);
            ctx.ExitMethodWithReturn("true");
            return true;
        });
        await group.JoinAsync();
        _context.ExitMethodWithReturn("\"ORD-1\"");

        var root = _context.CaptureTrace().Roots[0];

        Assert.Equal(["InventoryService.Reserve"], Names(root.Children));
    }

    [Fact]
    public void Adoption_dies_with_the_snapshotting_state()
    {
        _context.EnterMethod("OrderService", "PlaceOrder", []);
        _context.ExitMethodWithReturn("\"ORD-1\"");
        var snapshot = _context.Snapshot();
        _context.Reset();

        RunUnderSnapshot(snapshot, () =>
            TraceCall("NotificationService", "NotifyOrderPlaced"));

        Assert.Empty(_context.CaptureTrace().Roots);
    }

    [Fact]
    public void Many_async_children_all_land_in_the_tree()
    {
        _context.EnterMethod("OrderService", "PlaceOrder", []);
        for (var i = 0; i < 50; i++)
        {
            var name = "Notify" + i.ToString(
                System.Globalization.CultureInfo.InvariantCulture);
            RunUnderSnapshot(_context.Snapshot(), () =>
                TraceCall("NotificationService", name));
        }

        _context.ExitMethodWithReturn("\"ORD-1\"");

        var root = _context.CaptureTrace().Roots[0];

        Assert.Equal(50, root.Children.Count);
    }

    private void TraceCall(string className, string methodName)
    {
        _context.EnterMethod(className, methodName, []);
        _context.ExitMethodWithReturn("true");
    }

    private static void RunUnderSnapshot(
        IContextSnapshot snapshot, Action body)
    {
        RunOnWorker(() =>
        {
            using var scope = snapshot.Activate();
            body();
        });
    }

    private static void RunOnWorker(Action body)
    {
        var failure = default(Exception);
        var worker = new Thread(() =>
        {
            try
            {
                body();
            }
#pragma warning disable CA1031 // rethrown on the test thread below
            catch (Exception ex)
#pragma warning restore CA1031
            {
                failure = ex;
            }
        })
        { Name = "async-worker", IsBackground = true };
        worker.Start();
        Assert.True(worker.Join(TimeSpan.FromSeconds(5)));
        if (failure is not null)
        {
            throw new InvalidOperationException(
                "the async worker failed", failure);
        }
    }

    private static IReadOnlyList<string> Names(
        IReadOnlyList<TraceNode> nodes)
    {
        var names = new List<string>(nodes.Count);
        for (var i = 0; i < nodes.Count; i++)
        {
            names.Add(
                nodes[i].Signature.ClassName + "."
                + nodes[i].Signature.MethodName);
        }

        return names;
    }
}
