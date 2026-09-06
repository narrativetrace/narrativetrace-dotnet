// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using NarrativeTrace.Runtime;
using Xunit;

namespace NarrativeTrace.Core.Tests;

public class ConcurrencyIdentityPropagationTests
{
    [Fact]
    public async Task Forked_child_span_shares_parent_trace_id()
    {
        var ctx = new SyncNarrativeContext(
            new NarrativeTraceConfig());
        var h0 = ctx.EnterMethod("Parent", "Run", []);
        var parentTraceId = ctx.CurrentTraceId;
        var group = ForkJoinGroup.Create(ctx);

        group.Fork(iso =>
        {
            var h = iso.EnterMethod("Child", "Do", []);
            iso.ExitMethodWithReturn("ok", h);
            return 1;
        });
        await group.JoinAsync();
        ctx.ExitMethodWithReturn(null, h0);

        var child = ctx.CaptureTrace().Roots[0].Children[0];
        Assert.Equal(parentTraceId, child.SpanContext!.TraceId);
    }

    [Fact]
    public async Task FireAndForget_child_span_shares_parent_trace_id()
    {
        var ctx = new SyncNarrativeContext(
            new NarrativeTraceConfig());
        var h0 = ctx.EnterMethod("Parent", "Run", []);
        var parentTraceId = ctx.CurrentTraceId;
        var done = new TaskCompletionSource<bool>();
        var group = FireAndForgetGroup.Create(ctx);

        group.Launch(iso =>
        {
            var h = iso.EnterMethod("Bg", "Work", []);
            iso.ExitMethodWithReturn(null, h);
            done.SetResult(true);
        });
        await done.Task;
        await WaitForChildRoot(group);
        ctx.ExitMethodWithReturn(null, h0);

        var childRoot = group.ChildRoots()[0].Roots[0];
        Assert.Equal(
            parentTraceId, childRoot.SpanContext!.TraceId);
    }

    [Fact]
    public async Task Forked_child_suppresses_parameter_values_at_Summary()
    {
        var ctx = new SyncNarrativeContext(
            new NarrativeTraceConfig(TracingLevel.Summary));
        var h0 = ctx.EnterMethod("Parent", "Run", []);
        var group = ForkJoinGroup.Create(ctx);

        group.Fork(iso =>
        {
            var h = iso.EnterMethod(
                "Child", "Do",
                [new ParameterCapture("id", "42", false)]);
            iso.ExitMethodWithReturn("ok", h);
            return 1;
        });
        await group.JoinAsync();
        ctx.ExitMethodWithReturn(null, h0);

        var child = ctx.CaptureTrace().Roots[0].Children[0];
        Assert.Equal(
            "", child.Signature.Parameters[0].RenderedValue);
    }

    [Fact]
    public async Task Forked_child_captures_nothing_when_parent_is_Off()
    {
        var ctx = new SyncNarrativeContext(
            new NarrativeTraceConfig(TracingLevel.Off));
        var group = ForkJoinGroup.Create(ctx);

        group.Fork(iso =>
        {
            var h = iso.EnterMethod("Child", "Do", []);
            iso.ExitMethodWithReturn("ok", h);
            return 1;
        });
        await group.JoinAsync();

        Assert.True(ctx.CaptureTrace().IsEmpty);
    }

    [Fact]
    public async Task Forked_child_carries_request_user_and_service_context()
    {
        var ctx = new SyncNarrativeContext(
            new NarrativeTraceConfig(
                serviceIdentity: new ServiceIdentity(
                    "orders", "1.2.3", "prod")));
        ctx.SetRequestContext(
            "POST", new HttpRoute("/checkout"),
            new ClientIp("10.0.0.1"));
        ctx.SetUserContext(
            new EnduserId("u1"), new SessionId("s1"),
            new TenantId("acme"));
        var h0 = ctx.EnterMethod("Parent", "Run", []);
        var group = ForkJoinGroup.Create(ctx);

        group.Fork(iso =>
        {
            var h = iso.EnterMethod("Child", "Do", []);
            iso.ExitMethodWithReturn("ok", h);
            return 1;
        });
        await group.JoinAsync();
        ctx.ExitMethodWithReturn(null, h0);

        var span = ctx.CaptureTrace()
            .Roots[0].Children[0].SpanContext!;
        AssertInheritedContext(span);
    }

    [Fact]
    public async Task FireAndForget_child_carries_request_user_and_service_context()
    {
        var ctx = new SyncNarrativeContext(
            new NarrativeTraceConfig(
                serviceIdentity: new ServiceIdentity(
                    "orders", "1.2.3", "prod")));
        ctx.SetRequestContext(
            "POST", new HttpRoute("/checkout"),
            new ClientIp("10.0.0.1"));
        ctx.SetUserContext(
            new EnduserId("u1"), new SessionId("s1"),
            new TenantId("acme"));
        var done = new TaskCompletionSource<bool>();
        var group = FireAndForgetGroup.Create(ctx);

        group.Launch(iso =>
        {
            var h = iso.EnterMethod("Bg", "Work", []);
            iso.ExitMethodWithReturn(null, h);
            done.SetResult(true);
        });
        await done.Task;
        await WaitForChildRoot(group);

        var span = group.ChildRoots()[0].Roots[0].SpanContext!;
        AssertInheritedContext(span);
    }

    [Fact]
    public void FireAndForget_Create_grafts_launcher_node_into_parent_frame()
    {
        var ctx = new SyncNarrativeContext(
            new NarrativeTraceConfig());
        var h0 = ctx.EnterMethod("Svc", "Run", []);

        var group = FireAndForgetGroup.Create(ctx, "Svc");
        ctx.ExitMethodWithReturn(null, h0);

        var launcher = Assert.Single(
            ctx.CaptureTrace().Roots[0].Children);
        Assert.Equal(
            "fire-and-forget", launcher.Signature.MethodName);
        Assert.Empty(launcher.Children);
        Assert.IsType<Incomplete>(launcher.Outcome);
        Assert.NotNull(launcher.Concurrency);
        Assert.Equal(
            ConcurrencyKind.FireAndForget,
            launcher.Concurrency!.Kind);
        Assert.Equal(
            group.GroupId, launcher.Concurrency.GroupId);
    }

    /// <summary>
    /// Polls until the group's launched background work is visible in
    /// <see cref="FireAndForgetGroup.ChildRoots"/>, instead of a fixed sleep.
    /// </summary>
    /// <remarks>
    /// Awaiting the launched work's own <c>TaskCompletionSource</c> only proves
    /// the test body inside <c>Launch</c> ran to its last statement — it races
    /// against <see cref="Task.Run(Action)"/>'s own bookkeeping marking that
    /// task <c>Completed</c>, which is what <c>ChildRoots</c> actually checks. A
    /// fixed <c>Task.Delay</c> here was found flaky under CPU contention (a
    /// concurrent <c>Coverage</c> run starves the thread pool just long enough
    /// to blow a 10ms budget); polling adapts to how loaded the host actually is
    /// while still resolving in a couple of milliseconds on a quiet one.
    /// </remarks>
    private static async Task WaitForChildRoot(FireAndForgetGroup group)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (group.ChildRoots().Count == 0)
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException(
                    "Fire-and-forget child root did not appear within 5s.");
            }

            await Task.Delay(5);
        }
    }

    private static void AssertInheritedContext(SpanContext span)
    {
        Assert.Equal("orders", span.ServiceName);
        Assert.Equal("1.2.3", span.ServiceVersion);
        Assert.Equal("prod", span.Environment);
        Assert.Equal("POST", span.HttpMethod);
        Assert.Equal("/checkout", span.HttpRoute!.Value);
        Assert.Equal("10.0.0.1", span.ClientIp!.Value);
        Assert.Equal("u1", span.EnduserId!.Value);
        Assert.Equal("s1", span.SessionId!.Value);
        Assert.Equal("acme", span.TenantId!.Value);
    }
}
