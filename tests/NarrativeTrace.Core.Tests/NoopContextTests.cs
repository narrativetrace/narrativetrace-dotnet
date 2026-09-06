// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using Xunit;

namespace NarrativeTrace.Core.Tests;

public class NoopContextTests
{
    [Fact]
    public void IsActive_returns_false()
    {
        Assert.False(NoopContext.Instance.IsActive);
    }

    [Fact]
    public void Methods_are_safe_no_ops()
    {
        var ctx = NoopContext.Instance;

        ctx.EnterMethod("Svc", "Run", Array.Empty<ParameterCapture>());
        ctx.ExitMethodWithReturn("ok");
        ctx.ExitMethodWithException(new Exception("fail"));
        ctx.Reset();

        var tree = ctx.CaptureTrace();
        Assert.True(tree.IsEmpty);
    }

    [Fact]
    public void Snapshot_activate_and_dispose_are_safe_no_ops()
    {
        var snapshot = NoopContext.Instance.Snapshot();

        Assert.NotNull(snapshot);
        using var scope = snapshot.Activate();
        Assert.NotNull(scope);
    }

    [Fact]
    public void EnterMethod_returns_empty_span_id()
    {
        var handle = NoopContext.Instance.EnterMethod(
            "Svc", "Run", []);

        Assert.True(handle.IsEmpty);
    }

    [Fact]
    public void DetachFrame_is_safe_noop()
    {
        var ctx = NoopContext.Instance;
        ctx.DetachFrame(SpanId.Empty);
        ctx.DetachFrame(SpanIdGenerator.GenerateSpanId());

        Assert.True(ctx.CaptureTrace().IsEmpty);
    }

    [Fact]
    public void Handle_methods_are_safe_no_ops()
    {
        var ctx = NoopContext.Instance;
        var handle = SpanIdGenerator.GenerateSpanId();

        ctx.ExitMethodWithReturn("ok", handle);
        ctx.ExitMethodWithException(
            new Exception("fail"), handle);

        Assert.True(ctx.CaptureTrace().IsEmpty);
    }

    [Fact]
    public void ParentOf_returns_null()
    {
        Assert.Null(
            NoopContext.Instance.ParentOf(
                SpanIdGenerator.GenerateSpanId()));
    }

    [Fact]
    public void RunScoped_returns_fn_result()
    {
        var result =
            NoopContext.Instance.RunScoped(
                SpanId.Empty, () => 42);

        Assert.Equal(42, result);
    }

    [Fact]
    public void Identity_surface_is_inert()
    {
        var ctx = NoopContext.Instance;

        Assert.False(ctx.CapturesParameterValues);
        Assert.Null(ctx.CurrentTraceId);
        Assert.Null(ctx.StoryId);
        Assert.Null(ctx.ChapterId);
    }

    [Fact]
    public void EnsureTraceId_returns_a_stable_id_without_exposing_it()
    {
        var ctx = NoopContext.Instance;

        var id = ctx.EnsureTraceId();

        Assert.False(id.IsEmpty);
        Assert.Equal(id, ctx.EnsureTraceId());
        Assert.Null(ctx.CurrentTraceId);
    }

    [Fact]
    public void Structured_and_context_overloads_are_safe_no_ops()
    {
        var ctx = NoopContext.Instance;

        ctx.ExitMethodWithReturn(
            "ok", new RenderedValue.LongVal(1));
        ctx.ExitMethodWithException(
            new Exception("fail"), "error context");

        Assert.True(ctx.CaptureTrace().IsEmpty);
    }

    [Fact]
    public void Request_and_user_context_are_safe_no_ops()
    {
        var ctx = NoopContext.Instance;

        ctx.SetRequestContext(
            "GET", new HttpRoute("/r"), new ClientIp("1.1.1.1"));
        ctx.SetUserContext(
            new EnduserId("u"), new SessionId("s"), new TenantId("t"));

        Assert.True(ctx.CaptureTrace().IsEmpty);
    }

    [Fact]
    public void CreateConcurrentChild_returns_the_shared_instance()
    {
        var child = ((IConcurrentChildFactory)NoopContext.Instance)
            .CreateConcurrentChild();

        Assert.Same(NoopContext.Instance, child);
    }

    [Fact]
    public void GraftChild_does_nothing()
    {
        var node = new TraceNode(
            new MethodSignature("Svc", "Run", []),
            new Returned(null), [], 0);

        var ex = Record.Exception(
            () => NoopContext.Instance.GraftChild(node));

        Assert.Null(ex);
    }
}
