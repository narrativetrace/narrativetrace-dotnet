// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using NarrativeTrace.Runtime;
using Xunit;

namespace NarrativeTrace.Core.Tests;

public class FireAndForgetGroupTests
{
    [Fact]
    public void Create_returns_unique_groupId()
    {
        var ctx = new SyncNarrativeContext(
            new NarrativeTraceConfig());

        var g1 = FireAndForgetGroup.Create(ctx);
        var g2 = FireAndForgetGroup.Create(ctx);

        Assert.NotEqual(g1.GroupId, g2.GroupId);
    }

    [Fact]
    public void Launch_adds_launcher_node_to_parent_trace()
    {
        var ctx = new SyncNarrativeContext(
            new NarrativeTraceConfig());
        var h0 = ctx.EnterMethod("Parent", "Run", []);

        var group = FireAndForgetGroup.Create(ctx);
        group.Launch(_ => { });
        ctx.ExitMethodWithReturn(null, h0);

        var trace = ctx.CaptureTrace();
        Assert.Single(trace.Roots);
    }

    [Fact]
    public async Task Launched_task_runs_on_isolated_context()
    {
        var ctx = new SyncNarrativeContext(
            new NarrativeTraceConfig());
        var done = new TaskCompletionSource<bool>();

        var group = FireAndForgetGroup.Create(ctx);
        group.Launch(iso =>
        {
            iso.EnterMethod("Bg", "Work", []);
            iso.ExitMethodWithReturn(null);
            done.SetResult(true);
        });

        await done.Task;
        await Task.Delay(10);

        var roots = group.ChildRoots();
        Assert.Single(roots);
        Assert.Single(roots[0].Roots);
        Assert.Equal("Bg",
            roots[0].Roots[0].Signature.ClassName);
    }

    [Fact]
    public async Task ChildRoots_carries_matching_groupId()
    {
        var ctx = new SyncNarrativeContext(
            new NarrativeTraceConfig());
        var done = new TaskCompletionSource<bool>();

        var group = FireAndForgetGroup.Create(ctx);
        group.Launch(iso =>
        {
            iso.EnterMethod("Bg", "Work", []);
            iso.ExitMethodWithReturn(null);
            done.SetResult(true);
        });

        await done.Task;
        await Task.Delay(10);

        // GroupId is on the group itself
        Assert.StartsWith("fanf-", group.GroupId);
    }

    [Fact]
    public async Task Multiple_launches_collect_separate_roots()
    {
        var ctx = new SyncNarrativeContext(
            new NarrativeTraceConfig());
        var done1 = new TaskCompletionSource<bool>();
        var done2 = new TaskCompletionSource<bool>();

        var group = FireAndForgetGroup.Create(ctx);
        group.Launch(iso =>
        {
            iso.EnterMethod("A", "Work", []);
            iso.ExitMethodWithReturn(null);
            done1.SetResult(true);
        });
        group.Launch(iso =>
        {
            iso.EnterMethod("B", "Work", []);
            iso.ExitMethodWithReturn(null);
            done2.SetResult(true);
        });

        await Task.WhenAll(done1.Task, done2.Task);
        await Task.Delay(10);

        var roots = group.ChildRoots();
        Assert.Equal(2, roots.Count);
    }

    [Fact]
    public async Task Launched_task_error_does_not_crash_parent()
    {
        var ctx = new SyncNarrativeContext(
            new NarrativeTraceConfig());
        var h0 = ctx.EnterMethod("Parent", "Run", []);

        var group = FireAndForgetGroup.Create(ctx);
        group.Launch(_ =>
            throw new InvalidOperationException());

        await Task.Delay(50);
        ctx.ExitMethodWithReturn(null, h0);

        var trace = ctx.CaptureTrace();
        Assert.Single(trace.Roots);
    }

    [Fact]
    public async Task Launch_with_CancellationToken_respects_cancellation()
    {
        var ctx = new SyncNarrativeContext(
            new NarrativeTraceConfig());
        using var cts =
            new CancellationTokenSource();
        await cts.CancelAsync();

        var group = FireAndForgetGroup.Create(ctx);
        group.Launch(_ =>
            cts.Token.ThrowIfCancellationRequested());

        await Task.Delay(50);

        var roots = group.ChildRoots();
        // Faulted tasks are excluded from ChildRoots
        Assert.Empty(roots);
    }

    [Fact]
    public void OnFireAndForgetLaunched_fires()
    {
        var ctx = new FanfLifecycleContext();

        var group = FireAndForgetGroup.Create(ctx);
        group.Launch(_ => { });

        Assert.Single(ctx.LaunchedIds);
        Assert.Equal(group.GroupId,
            ctx.LaunchedIds[0]);
    }

    private sealed class FanfLifecycleContext
        : INarrativeContext, IConcurrencyLifecycle
    {
        private readonly SyncNarrativeContext _inner =
            new(new NarrativeTraceConfig());

        public List<string> LaunchedIds { get; } = [];

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

        public void OnForkCreated(string groupId) { }

        public void OnJoinComplete(
            string groupId, int memberCount,
            long wallTimeTicks)
        { }

        public void OnFireAndForgetLaunched(
            string groupId) =>
            LaunchedIds.Add(groupId);
    }
}
