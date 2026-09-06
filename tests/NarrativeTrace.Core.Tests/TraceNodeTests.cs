// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using NarrativeTrace.Runtime;
using Xunit;

namespace NarrativeTrace.Core.Tests;

public class TraceNodeTests
{
    [Fact]
    public void Holds_signature_outcome_children_and_duration()
    {
        var signature = new MethodSignature(
            "Svc", "Run", Array.Empty<ParameterCapture>());
        var outcome = new Returned("ok");
        var children = Array.Empty<TraceNode>();

        var node = new TraceNode(signature, outcome, children, 12345L);

        Assert.Same(signature, node.Signature);
        Assert.Same(outcome, node.Outcome);
        Assert.Empty(node.Children);
        Assert.Equal(12345L, node.DurationTicks);
    }

    [Fact]
    public void HasAnyError_returns_true_when_node_threw()
    {
        var node = MakeNode(new Threw(new Exception("fail")));

        Assert.True(TraceNode.HasAnyError(new[] { node }));
    }

    [Fact]
    public void HasAnyError_returns_false_when_all_returned()
    {
        var node = MakeNode(new Returned("ok"));

        Assert.False(TraceNode.HasAnyError(new[] { node }));
    }

    [Fact]
    public void HasAnyError_finds_errors_in_nested_children()
    {
        var failChild = MakeNode(new Threw(new Exception("deep")));
        var parent = MakeNode(
            new Returned("ok"), new[] { failChild });

        Assert.True(TraceNode.HasAnyError(new[] { parent }));
    }

    // TraceNode.Children is IReadOnlyList<TraceNode> — a type, not a guarantee
    // of acyclicity. A caller that keeps the concrete mutable List<TraceNode>
    // around can mutate it after construction to create a genuine reference
    // cycle. HasAnyError walks the whole forest recursively, so this must
    // terminate rather than overflow the call stack.
    [Fact]
    public void HasAnyError_does_not_hang_on_a_cyclic_tree_with_no_error()
    {
        var aChildren = new List<TraceNode>();
        var bChildren = new List<TraceNode>();
        var a = MakeNode(new Returned("ok")) with { Children = aChildren.AsReadOnly() };
        var b = MakeNode(new Returned("ok")) with { Children = bChildren.AsReadOnly() };
        aChildren.Add(b);
        bChildren.Add(a);

        Assert.False(TraceNode.HasAnyError(new[] { a }));
    }

    [Fact]
    public void HasAnyError_finds_an_error_that_precedes_a_cycle()
    {
        var aChildren = new List<TraceNode>();
        var bChildren = new List<TraceNode>();
        var a = MakeNode(new Threw(new InvalidOperationException("fail"))) with
        {
            Children = aChildren.AsReadOnly(),
        };
        var b = MakeNode(new Returned("ok")) with { Children = bChildren.AsReadOnly() };
        aChildren.Add(b);
        bChildren.Add(a);

        Assert.True(TraceNode.HasAnyError(new[] { a }));
    }

    [Fact]
    public void HasAnyError_does_not_overflow_the_stack_on_a_hundred_thousand_deep_chain()
    {
        var node = MakeNode(new Returned("ok"));
        for (var i = 0; i < 100_000; i++)
        {
            node = MakeNode(new Returned("ok"), new[] { node });
        }

        Assert.False(TraceNode.HasAnyError(new[] { node }));
    }

    [Fact]
    public void TraceNode_without_StartTimestamp_defaults_to_zero()
    {
        var node = MakeNode(new Returned("ok"));

        Assert.Equal(0L, node.StartTimestamp);
    }

    [Fact]
    public void TraceNode_with_explicit_StartTimestamp_preserves_value()
    {
        var sig = new MethodSignature(
            "Svc", "Run", Array.Empty<ParameterCapture>());
        var node = new TraceNode(
            sig, new Returned("ok"),
            Array.Empty<TraceNode>(), 100L, 42000L);

        Assert.Equal(42000L, node.StartTimestamp);
    }

    [Fact]
    public void TraceNode_without_Concurrency_defaults_to_null()
    {
        var node = MakeNode(new Returned("ok"));

        Assert.Null(node.Concurrency);
    }

    [Fact]
    public void ConcurrencyInfo_record_preserves_all_fields()
    {
        var info = new ConcurrencyInfo(
            "group-1", "Svc.Run", 42,
            "Worker", true, ConcurrencyKind.ForkJoin);

        Assert.Equal("group-1", info.GroupId);
        Assert.Equal("Svc.Run", info.TaskLabel);
        Assert.Equal(42, info.ThreadId);
        Assert.Equal("Worker", info.ThreadName);
        Assert.True(info.IsThreadPoolThread);
        Assert.Equal(ConcurrencyKind.ForkJoin, info.Kind);
    }

    [Fact]
    public void ConcurrencyInfo_ThreadName_is_nullable()
    {
        var info = new ConcurrencyInfo(
            "g", "t", 1, null, false,
            ConcurrencyKind.ForkJoin);

        Assert.Null(info.ThreadName);
    }

    [Fact]
    public void ConcurrencyKind_has_ForkJoin_and_FireAndForget()
    {
        Assert.Equal(0, (int)ConcurrencyKind.ForkJoin);
        Assert.Equal(1, (int)ConcurrencyKind.FireAndForget);
    }

    [Fact]
    public void SyncNarrativeContext_populates_StartTimestamp_from_frame()
    {
        var ctx = new SyncNarrativeContext(
            new NarrativeTraceConfig());

        ctx.EnterMethod("Svc", "Run", []);
        ctx.ExitMethodWithReturn(null);

        var node = ctx.CaptureTrace().Roots[0];
        Assert.True(node.StartTimestamp > 0);
    }

    [Fact]
    public void StartTimestamp_increases_across_sequential_calls()
    {
        var ctx = new SyncNarrativeContext(
            new NarrativeTraceConfig());

        ctx.EnterMethod("Svc", "A", []);
        ctx.ExitMethodWithReturn(null);
        ctx.EnterMethod("Svc", "B", []);
        ctx.ExitMethodWithReturn(null);

        var roots = ctx.CaptureTrace().Roots;
        Assert.True(roots[1].StartTimestamp
            >= roots[0].StartTimestamp);
    }

    private static TraceNode MakeNode(
        TraceOutcome outcome,
        IReadOnlyList<TraceNode>? children = null)
    {
        var sig = new MethodSignature(
            "Svc", "Run", Array.Empty<ParameterCapture>());
        return new TraceNode(
            sig, outcome, children ?? Array.Empty<TraceNode>(), 0L);
    }
}
