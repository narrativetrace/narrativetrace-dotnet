// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using NarrativeTrace.Runtime;
using Xunit;

namespace NarrativeTrace.Core.Tests;

/// <summary>
/// Adopted work is tagged <see cref="ConcurrencyKind.Async"/> so a structural
/// artifact can assert the <i>set</i> of concurrent calls without pinning the
/// scheduler's dispatch order.
/// </summary>
/// <remarks>
/// The tag goes on the first span a flow opens under an activated snapshot and
/// nothing deeper, keyed by the launching span — the .NET mirror of Java's
/// item 31.
/// </remarks>
public sealed class AsyncConcurrencyTagTests : IDisposable
{
    private readonly SyncNarrativeContext _context =
        new(new NarrativeTraceConfig());

    public void Dispose() => _context.Reset();

    [Fact]
    public void The_first_span_opened_under_a_snapshot_is_tagged_async()
    {
        _context.EnterMethod("OrderService", "PlaceOrder", []);
        var snapshot = _context.Snapshot();
        _context.ExitMethodWithReturn("\"ORD-1\"");

        RunUnderSnapshot(snapshot, () =>
        {
            _context.EnterMethod("NotificationService", "Notify", []);
            _context.ExitMethodWithReturn("true");
        });

        var adopted = _context.CaptureTrace().Roots[0].Children[0];
        Assert.NotNull(adopted.Concurrency);
        Assert.Equal(ConcurrencyKind.Async, adopted.Concurrency!.Kind);
    }

    [Fact]
    public void Calls_nested_inside_the_adopted_span_stay_untagged()
    {
        _context.EnterMethod("OrderService", "PlaceOrder", []);
        var snapshot = _context.Snapshot();
        _context.ExitMethodWithReturn("\"ORD-1\"");

        RunUnderSnapshot(snapshot, () =>
        {
            _context.EnterMethod("NotificationService", "Notify", []);
            _context.EnterMethod("EmailGateway", "Send", []);
            _context.ExitMethodWithReturn("true");
            _context.ExitMethodWithReturn("true");
        });

        var adopted = _context.CaptureTrace().Roots[0].Children[0];
        // Everything below the first span is ordinary sequential work on that
        // flow — tagging it would invent a group per call.
        Assert.Null(adopted.Children[0].Concurrency);
    }

    [Fact]
    public void Every_async_child_of_one_call_shares_one_group()
    {
        _context.EnterMethod("OrderService", "PlaceOrder", []);
        var snapshot = _context.Snapshot();
        _context.ExitMethodWithReturn("\"ORD-1\"");

        RunUnderSnapshot(snapshot, () => TraceCall("Audit", "Record"));
        RunUnderSnapshot(snapshot, () => TraceCall("Email", "Send"));

        var children = _context.CaptureTrace().Roots[0].Children;
        Assert.Equal(2, children.Count);
        Assert.Equal(
            children[0].Concurrency!.GroupId,
            children[1].Concurrency!.GroupId);
    }

    [Fact]
    public void Snapshots_of_different_calls_produce_different_groups()
    {
        _context.EnterMethod("OrderService", "PlaceOrder", []);
        var first = _context.Snapshot();
        _context.ExitMethodWithReturn("\"ORD-1\"");
        _context.EnterMethod("OrderService", "ShipOrder", []);
        var second = _context.Snapshot();
        _context.ExitMethodWithReturn("\"ORD-1\"");

        RunUnderSnapshot(first, () => TraceCall("Audit", "Record"));
        RunUnderSnapshot(second, () => TraceCall("Email", "Send"));

        var roots = _context.CaptureTrace().Roots;
        Assert.NotEqual(
            roots[0].Children[0].Concurrency!.GroupId,
            roots[1].Children[0].Concurrency!.GroupId);
    }

    [Fact]
    public void Work_propagated_with_no_open_span_groups_per_trace()
    {
        _context.EnterMethod("OrderService", "PlaceOrder", []);
        _context.ExitMethodWithReturn("\"ORD-1\"");
        var snapshot = _context.Snapshot();

        RunUnderSnapshot(snapshot, () => TraceCall("Audit", "Record"));

        var adopted = _context.CaptureTrace().Roots[1];
        Assert.Equal(
            "async-" + _context.CurrentTraceId!.Value.ToString(),
            adopted.Concurrency!.GroupId);
    }

    [Fact]
    public void The_tag_records_the_thread_the_work_ran_on()
    {
        _context.EnterMethod("OrderService", "PlaceOrder", []);
        _context.ExitMethodWithReturn("\"ORD-1\"");

        RunUnderSnapshot(_context.Snapshot(), () => TraceCall("Audit", "Record"));

        var adopted = _context.CaptureTrace().Roots[1];
        Assert.Equal("async-worker", adopted.Concurrency!.ThreadName);
        Assert.Equal("Audit.Record", adopted.Concurrency.TaskLabel);
    }

    [Fact]
    public void Ordinary_calls_carry_no_concurrency_tag()
    {
        _context.EnterMethod("OrderService", "PlaceOrder", []);
        _context.ExitMethodWithReturn("\"ORD-1\"");

        Assert.Null(_context.CaptureTrace().Roots[0].Concurrency);
    }

    [Fact]
    public void Work_activated_without_adoption_is_still_the_flows_first_span()
    {
        _context.EnterMethod("OrderService", "PlaceOrder", []);
        var snapshot = _context.Snapshot();
        _context.ExitMethodWithReturn("\"ORD-1\"");
        TraceTree? workerView = null;

        RunOnWorker(() =>
        {
            using var scope = snapshot.ActivateWithoutAdoption();
            TraceCall("Audit", "Record");
            workerView = _context.CaptureTrace();
        });

        // The worker still sees its own call as the async entry point; what
        // changes is only that the origin never adopts it.
        Assert.Equal(
            ConcurrencyKind.Async,
            workerView!.Roots[0].Concurrency!.Kind);
        Assert.Single(_context.CaptureTrace().Roots);
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
        var worker = new Thread(() => body())
        {
            Name = "async-worker",
            IsBackground = true,
        };
        worker.Start();
        Assert.True(worker.Join(TimeSpan.FromSeconds(5)));
    }
}
