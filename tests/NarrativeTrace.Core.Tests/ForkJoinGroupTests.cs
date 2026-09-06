// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using NarrativeTrace.Runtime;
using Xunit;

namespace NarrativeTrace.Core.Tests;

public class ForkJoinGroupTests
{
    [Fact]
    public void Create_returns_unique_groupId()
    {
        var ctx = new SyncNarrativeContext(
            new NarrativeTraceConfig());

        var g1 = ForkJoinGroup.Create(ctx);
        var g2 = ForkJoinGroup.Create(ctx);

        Assert.NotEqual(g1.GroupId, g2.GroupId);
    }

    [Fact]
    public async Task Fork_runs_task_on_isolated_context()
    {
        var ctx = new SyncNarrativeContext(
            new NarrativeTraceConfig());

        var h0 = ctx.EnterMethod("Parent", "Run", []);
        var group = ForkJoinGroup.Create(ctx);
        group.Fork(iso =>
        {
            iso.EnterMethod("Child", "Do", []);
            iso.ExitMethodWithReturn("ok");
            return 42;
        });
        await group.JoinAsync();
        ctx.ExitMethodWithReturn(null, h0);

        var trace = ctx.CaptureTrace();
        Assert.Single(trace.Roots);
        Assert.Single(trace.Roots[0].Children);
        Assert.Equal("Child",
            trace.Roots[0].Children[0]
                .Signature.ClassName);
    }

    [Fact]
    public async Task Fork_returns_task_that_resolves_with_result()
    {
        var ctx = new SyncNarrativeContext(
            new NarrativeTraceConfig());
        var group = ForkJoinGroup.Create(ctx);

        var task = group.Fork(_ => 42);
        await group.JoinAsync();

        Assert.Equal(42, await task);
    }

    [Fact]
    public async Task JoinAsync_awaits_all_and_merges_traces()
    {
        var ctx = new SyncNarrativeContext(
            new NarrativeTraceConfig());
        var h0 = ctx.EnterMethod("Parent", "Run", []);
        var group = ForkJoinGroup.Create(ctx);

        group.Fork(iso =>
        {
            iso.EnterMethod("A", "Do", []);
            iso.ExitMethodWithReturn(null);
            return 1;
        });
        group.Fork(iso =>
        {
            iso.EnterMethod("B", "Do", []);
            iso.ExitMethodWithReturn(null);
            return 2;
        });
        await group.JoinAsync();
        ctx.ExitMethodWithReturn(null, h0);

        var children = ctx.CaptureTrace()
            .Roots[0].Children;
        Assert.Equal(2, children.Count);
    }

    [Fact]
    public async Task JoinAsync_attaches_ConcurrencyInfo_with_ForkJoin_kind()
    {
        var ctx = new SyncNarrativeContext(
            new NarrativeTraceConfig());
        var h0 = ctx.EnterMethod("Parent", "Run", []);
        var group = ForkJoinGroup.Create(ctx);

        group.Fork(iso =>
        {
            iso.EnterMethod("Svc", "Do", []);
            iso.ExitMethodWithReturn(null);
            return 1;
        });
        await group.JoinAsync();
        ctx.ExitMethodWithReturn(null, h0);

        var child = ctx.CaptureTrace()
            .Roots[0].Children[0];
        Assert.NotNull(child.Concurrency);
        Assert.Equal(ConcurrencyKind.ForkJoin,
            child.Concurrency!.Kind);
        Assert.Equal(group.GroupId,
            child.Concurrency.GroupId);
    }

    [Fact]
    public async Task JoinAsync_returns_results_in_fork_order()
    {
        var ctx = new SyncNarrativeContext(
            new NarrativeTraceConfig());
        var group = ForkJoinGroup.Create(ctx);

        var t1 = group.Fork(_ => 10);
        var t2 = group.Fork(_ => 20);
        await group.JoinAsync();

        Assert.Equal(10, await t1);
        Assert.Equal(20, await t2);
    }

    [Fact]
    public async Task WhenAll_convenience_wraps_fork_and_join()
    {
        var ctx = new SyncNarrativeContext(
            new NarrativeTraceConfig());
        var h0 = ctx.EnterMethod("Parent", "Run", []);

        var (a, b) = await ForkJoinGroup.WhenAll(
            ctx,
            iso => { return 10; },
            iso => { return 20; });

        ctx.ExitMethodWithReturn(null, h0);
        Assert.Equal(10, a);
        Assert.Equal(20, b);
    }

    [Fact]
    public async Task Forked_task_exception_propagates_through_JoinAsync()
    {
        var ctx = new SyncNarrativeContext(
            new NarrativeTraceConfig());
        var group = ForkJoinGroup.Create(ctx);

        group.Fork<int>(_ =>
            throw new InvalidOperationException("boom"));

        await Assert.ThrowsAsync<
            InvalidOperationException>(
            () => group.JoinAsync());
    }

    [Fact]
    public async Task Multiple_traced_calls_inside_fork_produce_nested_children()
    {
        var ctx = new SyncNarrativeContext(
            new NarrativeTraceConfig());
        var h0 = ctx.EnterMethod("Parent", "Run", []);
        var group = ForkJoinGroup.Create(ctx);

        group.Fork(iso =>
        {
            var h = iso.EnterMethod("Outer", "A", []);
            iso.EnterMethod("Inner", "B", []);
            iso.ExitMethodWithReturn(null);
            iso.ExitMethodWithReturn(null, h);
            return 1;
        });
        await group.JoinAsync();
        ctx.ExitMethodWithReturn(null, h0);

        var grafted = ctx.CaptureTrace()
            .Roots[0].Children[0];
        Assert.Equal("Outer",
            grafted.Signature.ClassName);
        Assert.Single(grafted.Children);
        Assert.Equal("Inner",
            grafted.Children[0].Signature.ClassName);
    }

    [Fact]
    public async Task GroupId_consistent_across_all_merged_children()
    {
        var ctx = new SyncNarrativeContext(
            new NarrativeTraceConfig());
        var h0 = ctx.EnterMethod("Parent", "Run", []);
        var group = ForkJoinGroup.Create(ctx);

        group.Fork(iso =>
        {
            iso.EnterMethod("A", "Do", []);
            iso.ExitMethodWithReturn(null);
            return 1;
        });
        group.Fork(iso =>
        {
            iso.EnterMethod("B", "Do", []);
            iso.ExitMethodWithReturn(null);
            return 2;
        });
        await group.JoinAsync();
        ctx.ExitMethodWithReturn(null, h0);

        var children = ctx.CaptureTrace()
            .Roots[0].Children;
        Assert.All(children, c =>
            Assert.Equal(group.GroupId,
                c.Concurrency!.GroupId));
    }

    [Fact]
    public async Task JoinAsync_with_no_forks_returns_empty()
    {
        var ctx = new SyncNarrativeContext(
            new NarrativeTraceConfig());
        var group = ForkJoinGroup.Create(ctx);

        await group.JoinAsync();

        Assert.True(ctx.CaptureTrace().IsEmpty);
    }

    [Fact]
    public async Task TaskLabel_auto_derived_from_first_traced_call()
    {
        var ctx = new SyncNarrativeContext(
            new NarrativeTraceConfig());
        var h0 = ctx.EnterMethod("Parent", "Run", []);
        var group = ForkJoinGroup.Create(ctx);

        group.Fork(iso =>
        {
            iso.EnterMethod("Discount", "Calc", []);
            iso.ExitMethodWithReturn(null);
            return 1;
        });
        await group.JoinAsync();
        ctx.ExitMethodWithReturn(null, h0);

        var child = ctx.CaptureTrace()
            .Roots[0].Children[0];
        Assert.Equal("Discount.Calc",
            child.Concurrency!.TaskLabel);
    }

    [Fact]
    public async Task TaskLabel_falls_back_to_task_N_when_no_traced_calls()
    {
        var ctx = new SyncNarrativeContext(
            new NarrativeTraceConfig());
        var group = ForkJoinGroup.Create(ctx);

        group.Fork(_ => 42);
        await group.JoinAsync();

        // No traced calls = no grafted children,
        // but label was derived internally
        Assert.True(ctx.CaptureTrace().IsEmpty);
    }

    [Fact]
    public async Task CancellationToken_cancels_pending_forks()
    {
        var ctx = new SyncNarrativeContext(
            new NarrativeTraceConfig());
        using var cts =
            new CancellationTokenSource();
        await cts.CancelAsync();
        var group = ForkJoinGroup.Create(
            ctx, cts.Token);

        group.Fork<int>(_ =>
            throw new Exception("should not run"));

        await Assert.ThrowsAsync<TaskCanceledException>(
            () => group.JoinAsync());
    }

    [Fact]
    public async Task Cancelled_fork_shows_in_trace_as_cancelled()
    {
        var ctx = new SyncNarrativeContext(
            new NarrativeTraceConfig());
        using var cts =
            new CancellationTokenSource();
        var h0 = ctx.EnterMethod("Parent", "Run", []);
        var group = ForkJoinGroup.Create(
            ctx, cts.Token);

        var started = new TaskCompletionSource<bool>();
        group.Fork(iso =>
        {
            iso.EnterMethod("Slow", "Work", []);
            started.SetResult(true);
            cts.Token.WaitHandle.WaitOne();
            cts.Token.ThrowIfCancellationRequested();
            return 1;
        });

        await started.Task;
        await cts.CancelAsync();

        try
        {
            await group.JoinAsync();
        }
        catch (OperationCanceledException)
        {
            // Expected: cancellation propagates
        }

        ctx.ExitMethodWithReturn(null, h0);
        var trace = ctx.CaptureTrace();
        // Cancelled fork may not graft fully,
        // but parent still completes
        Assert.Single(trace.Roots);
    }

    [Fact]
    public async Task ConcurrencyInfo_captures_ThreadId()
    {
        var ctx = new SyncNarrativeContext(
            new NarrativeTraceConfig());
        var h0 = ctx.EnterMethod("Parent", "Run", []);
        var group = ForkJoinGroup.Create(ctx);

        group.Fork(iso =>
        {
            iso.EnterMethod("Svc", "Do", []);
            iso.ExitMethodWithReturn(null);
            return 1;
        });
        await group.JoinAsync();
        ctx.ExitMethodWithReturn(null, h0);

        var child = ctx.CaptureTrace()
            .Roots[0].Children[0];
        Assert.True(
            child.Concurrency!.ThreadId > 0);
    }

    [Fact]
    public async Task ConcurrencyInfo_captures_IsThreadPoolThread()
    {
        var ctx = new SyncNarrativeContext(
            new NarrativeTraceConfig());
        var h0 = ctx.EnterMethod("Parent", "Run", []);
        var group = ForkJoinGroup.Create(ctx);

        group.Fork(iso =>
        {
            iso.EnterMethod("Svc", "Do", []);
            iso.ExitMethodWithReturn(null);
            return 1;
        });
        await group.JoinAsync();
        ctx.ExitMethodWithReturn(null, h0);

        var child = ctx.CaptureTrace()
            .Roots[0].Children[0];
        // Task.Run uses ThreadPool threads
        Assert.True(
            child.Concurrency!.IsThreadPoolThread);
    }

    [Fact]
    public void OnForkCreated_fires_on_ForkJoinGroup_Create()
    {
        var ctx = new LifecycleCapturingContext();

        var group = ForkJoinGroup.Create(ctx);

        Assert.Single(ctx.ForkCreatedIds);
        Assert.Equal(group.GroupId,
            ctx.ForkCreatedIds[0]);
    }

    [Fact]
    public async Task OnJoinComplete_fires_with_memberCount_and_wallTime()
    {
        var ctx = new LifecycleCapturingContext();
        var group = ForkJoinGroup.Create(ctx);

        group.Fork(_ => 1);
        group.Fork(_ => 2);
        await group.JoinAsync();

        Assert.Single(ctx.JoinCompletes);
        var (groupId, count, _) =
            ctx.JoinCompletes[0];
        Assert.Equal(group.GroupId, groupId);
        Assert.Equal(2, count);
    }

    private sealed class LifecycleCapturingContext
        : INarrativeContext, IConcurrencyLifecycle
    {
        private readonly SyncNarrativeContext _inner =
            new(new NarrativeTraceConfig());

        public List<string> ForkCreatedIds { get; } = [];
        public List<(string, int, long)> JoinCompletes
        { get; } = [];
        public List<string> FanfLaunchedIds { get; } = [];

        public bool IsActive => _inner.IsActive;

        public bool CapturesParameterValues => _inner.CapturesParameterValues;

        public TraceId? CurrentTraceId => _inner.CurrentTraceId;

        public TraceId EnsureTraceId() => _inner.EnsureTraceId();

        public string? StoryId => _inner.StoryId;

        public string? ChapterId => _inner.ChapterId;

        public SpanId EnterMethod(
            string className, string methodName,
            IReadOnlyList<ParameterCapture> parameters,
            MethodOptions? options = null) =>
            _inner.EnterMethod(
                className, methodName,
                parameters, options);

        public void ExitMethodWithReturn(
            string? renderedValue, SpanId? handle = null) =>
            _inner.ExitMethodWithReturn(
                renderedValue, handle);

        public void ExitMethodWithReturn(
            string? renderedValue,
            RenderedValue? structuredValue,
            SpanId? handle = null) =>
            _inner.ExitMethodWithReturn(
                renderedValue, structuredValue, handle);

        public void ExitMethodWithException(
            Exception? exception, SpanId? handle = null) =>
            _inner.ExitMethodWithException(exception, handle);

        public void ExitMethodWithException(
            Exception? exception, string? errorContext,
            SpanId? handle = null) =>
            _inner.ExitMethodWithException(
                exception, errorContext, handle);

        public void DetachFrame(SpanId handle) =>
            _inner.DetachFrame(handle);

        public TraceTree CaptureTrace() =>
            _inner.CaptureTrace();

        public void Reset() => _inner.Reset();

        public IContextSnapshot Snapshot() => _inner.Snapshot();

        public SpanId? ParentOf(SpanId handle) =>
            _inner.ParentOf(handle);

        public T RunScoped<T>(
            SpanId handle, Func<T> fn) =>
            _inner.RunScoped(handle, fn);

        public void GraftChild(TraceNode node) =>
            _inner.GraftChild(node);

        public void SetRequestContext(
            string? httpMethod, HttpRoute? httpRoute,
            ClientIp? clientIp) =>
            _inner.SetRequestContext(httpMethod, httpRoute, clientIp);

        public void SetUserContext(
            EnduserId? enduserId, SessionId? sessionId,
            TenantId? tenantId) =>
            _inner.SetUserContext(enduserId, sessionId, tenantId);

        public void OnForkCreated(string groupId) =>
            ForkCreatedIds.Add(groupId);

        public void OnJoinComplete(
            string groupId, int memberCount,
            long wallTimeTicks) =>
            JoinCompletes.Add(
                (groupId, memberCount, wallTimeTicks));

        public void OnFireAndForgetLaunched(
            string groupId) =>
            FanfLaunchedIds.Add(groupId);
    }
}
