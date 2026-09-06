// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using Xunit;

namespace NarrativeTrace.Core.Tests;

public class TreeWalkTests
{
    [Fact]
    public void Any_returns_false_for_an_empty_forest()
    {
        Assert.False(TreeWalk.Any([], _ => true));
    }

    [Fact]
    public void Any_finds_a_matching_root()
    {
        var node = MakeNode("Target");

        Assert.True(TreeWalk.Any([node], n => n.Signature.MethodName == "Target"));
    }

    [Fact]
    public void Any_finds_a_matching_descendant()
    {
        var grandchild = MakeNode("Target");
        var child = MakeNode("Child", [grandchild]);
        var root = MakeNode("Root", [child]);

        Assert.True(TreeWalk.Any([root], n => n.Signature.MethodName == "Target"));
    }

    [Fact]
    public void Any_returns_false_when_nothing_matches()
    {
        var root = MakeNode("Root", [MakeNode("Child")]);

        Assert.False(TreeWalk.Any([root], n => n.Signature.MethodName == "Nope"));
    }

    [Fact]
    public void FindFirst_returns_the_shallowest_leftmost_match_in_pre_order()
    {
        var first = MakeNode("A");
        var second = MakeNode("A");
        var root = MakeNode("Root", [first, second]);

        var found = TreeWalk.FindFirst([root], n => n.Signature.MethodName == "A");

        Assert.Same(first, found);
    }

    [Fact]
    public void FindFirst_checks_a_node_before_descending_into_its_children()
    {
        var child = MakeNode("A");
        var root = MakeNode("A", [child]);

        var found = TreeWalk.FindFirst([root], n => n.Signature.MethodName == "A");

        Assert.Same(root, found);
    }

    [Fact]
    public void FindFirst_returns_null_when_nothing_matches()
    {
        var root = MakeNode("Root");

        Assert.Null(TreeWalk.FindFirst([root], n => n.Signature.MethodName == "Nope"));
    }

    [Fact]
    public void Any_does_not_hang_on_a_self_cycle()
    {
        var cyclic = CyclicSelfNode();

        Assert.False(TreeWalk.Any([cyclic], n => n.Signature.MethodName == "Nope"));
    }

    [Fact]
    public void Any_does_not_hang_on_a_two_node_cycle()
    {
        var (a, _) = CyclicPair();

        Assert.False(TreeWalk.Any([a], n => n.Signature.MethodName == "Nope"));
    }

    [Fact]
    public void Any_still_finds_a_match_that_precedes_a_cycle()
    {
        var (a, _) = CyclicPair();

        Assert.True(TreeWalk.Any([a], n => n.Signature.MethodName == "A"));
    }

    [Fact]
    public void A_diamond_shared_subtree_is_not_misflagged_as_a_cycle()
    {
        var shared = MakeNode("Shared");
        var left = MakeNode("Left", [shared]);
        var right = MakeNode("Right", [shared]);
        var root = MakeNode("Root", [left, right]);

        // A genuine diamond (the same instance reachable by two different
        // paths) must still be found — it is shared, not cyclic, and a
        // cycle guard that treats "already seen anywhere" as a cycle would
        // wrongly refuse to descend into `right`'s copy of `shared`.
        Assert.True(TreeWalk.Any([root], n => n.Signature.MethodName == "Shared"));
    }

    [Fact]
    public void Any_does_not_overflow_the_stack_on_a_hundred_thousand_deep_chain()
    {
        var deep = DeepChain(100_000);

        // Real call-stack recursion over a chain this deep would crash the
        // process with an uncatchable StackOverflowException; this must not
        // throw at all, let alone crash.
        var result = TreeWalk.Any([deep], n => n.Signature.MethodName == "Nope");

        Assert.False(result);
    }

    [Fact]
    public void FindFirst_stops_descending_a_chain_past_MaxDepth()
    {
        var deep = DeepChain(TreeWalk.MaxDepth + 500);

        // The marker planted at the very bottom of the chain sits well past
        // MaxDepth, so a bounded walk must never reach it.
        var found = TreeWalk.FindFirst([deep], n => n.Signature.MethodName == "Bottom");

        Assert.Null(found);
    }

    [Fact]
    public void Bound_returns_an_equivalent_forest_for_a_small_acyclic_tree()
    {
        var child = MakeNode("Child");
        var root = MakeNode("Root", [child]);

        var bounded = TreeWalk.Bound([root]);

        var boundedRoot = Assert.Single(bounded);
        Assert.Equal("Root", boundedRoot.Signature.MethodName);
        var boundedChild = Assert.Single(boundedRoot.Children);
        Assert.Equal("Child", boundedChild.Signature.MethodName);
    }

    // The overwhelming majority of real traces are already within bounds. Bound must not pay for
    // a full defensive copy on every call — a renderer's own benchmark would regress on ordinary
    // input for the sake of a hostile one. Reference equality on the returned list is exactly what
    // proves the fast path was taken and no per-node rebuild happened.
    [Fact]
    public void Bound_returns_the_original_list_unchanged_when_already_safe()
    {
        var root = MakeNode("Root", [MakeNode("Child")]);
        IReadOnlyList<TraceNode> roots = [root];

        var bounded = TreeWalk.Bound(roots);

        Assert.Same(roots, bounded);
    }

    [Fact]
    public void Bound_replaces_a_cyclic_edge_with_a_cycle_marker_leaf()
    {
        var (a, _) = CyclicPair();

        var bounded = TreeWalk.Bound([a]);

        var boundedA = Assert.Single(bounded);
        var boundedB = Assert.Single(boundedA.Children);
        var marker = Assert.Single(boundedB.Children);
        Assert.Equal(TreeWalk.CycleMarker, marker.Signature.MethodName);
        Assert.Empty(marker.Children);
    }

    [Fact]
    public void Bound_replaces_a_node_past_MaxDepth_with_a_depth_limit_marker_leaf()
    {
        var deep = DeepChain(TreeWalk.MaxDepth + 50);

        var bounded = TreeWalk.Bound([deep]);

        Assert.True(TreeWalk.Any(bounded, n => n.Signature.MethodName == TreeWalk.DepthLimitMarker));
        Assert.False(TreeWalk.Any(bounded, n => n.Signature.MethodName == "Bottom"));
    }

    [Fact]
    public void Bound_never_recurses_the_call_stack_on_a_hundred_thousand_deep_chain()
    {
        var deep = DeepChain(100_000);

        var bounded = TreeWalk.Bound([deep]);

        Assert.Single(bounded);
    }

    [Fact]
    public void Bound_preserves_a_diamond_without_treating_it_as_a_cycle()
    {
        var shared = MakeNode("Shared");
        var left = MakeNode("Left", [shared]);
        var right = MakeNode("Right", [shared]);
        var root = MakeNode("Root", [left, right]);

        var bounded = TreeWalk.Bound([root]);

        var boundedRoot = Assert.Single(bounded);
        Assert.Equal(2, boundedRoot.Children.Count);
        Assert.All(boundedRoot.Children, c => Assert.Equal("Shared", Assert.Single(c.Children).Signature.MethodName));
    }

    private static TraceNode MakeNode(string methodName, IReadOnlyList<TraceNode>? children = null)
    {
        var sig = new MethodSignature(
            "Svc", methodName, Array.Empty<ParameterCapture>());
        return new TraceNode(
            sig, new Returned("ok"), children ?? Array.Empty<TraceNode>(), 0L);
    }

    private static TraceNode CyclicSelfNode()
    {
        var children = new List<TraceNode>();
        var node = new TraceNode(
            new MethodSignature("Svc", "A", Array.Empty<ParameterCapture>()),
            new Returned("ok"), children.AsReadOnly(), 0L);
        children.Add(node);
        return node;
    }

    private static (TraceNode A, TraceNode B) CyclicPair()
    {
        var aChildren = new List<TraceNode>();
        var bChildren = new List<TraceNode>();
        var a = new TraceNode(
            new MethodSignature("Svc", "A", Array.Empty<ParameterCapture>()),
            new Returned("ok"), aChildren.AsReadOnly(), 0L);
        var b = new TraceNode(
            new MethodSignature("Svc", "B", Array.Empty<ParameterCapture>()),
            new Returned("ok"), bChildren.AsReadOnly(), 0L);
        aChildren.Add(b);
        bChildren.Add(a);
        return (a, b);
    }

    private static TraceNode DeepChain(int depth)
    {
        var node = MakeNode("Bottom");
        for (var i = 0; i < depth; i++)
        {
            node = MakeNode("Link", [node]);
        }

        return node;
    }
}
