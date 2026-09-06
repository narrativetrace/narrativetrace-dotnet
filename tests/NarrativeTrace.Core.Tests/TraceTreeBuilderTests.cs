// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using Xunit;

namespace NarrativeTrace.Core.Tests;

public class TraceTreeBuilderTests
{
    [Fact]
    public void Single_enter_exit_pair_builds_one_root_node()
    {
        var sig = new MethodSignature("Svc", "Run", []);
        var events = new TraceEvent[]
        {
            TestEvents.Enter(0, -1, 1000L, sig),
            TestEvents.Exit(0, 2000L, new Returned("42")),
        };

        var tree = TraceTreeBuilder.Build(
            events, TracingLevel.Detail);

        Assert.Single(tree.Roots);
        var node = tree.Roots[0];
        Assert.Equal("Svc", node.Signature.ClassName);
        Assert.Equal("Run", node.Signature.MethodName);
        Assert.Equal(new Returned("42"), node.Outcome);
        Assert.Empty(node.Children);
    }

    [Fact]
    public void A_span_whose_parent_is_not_in_the_stream_becomes_a_root()
    {
        // The shape a worker sees of its own trace under a propagated
        // snapshot: the caller's span lives in another capture. Dropping the
        // node would lose work that demonstrably happened.
        var sig = new MethodSignature("Notify", "Send", []);
        var events = new TraceEvent[]
        {
            TestEvents.Enter(1, 0, 1000L, sig),
            TestEvents.Exit(1, 2000L, new Returned("true")),
        };

        var tree = TraceTreeBuilder.Build(events, TracingLevel.Detail);

        var root = Assert.Single(tree.Roots);
        Assert.Equal("Send", root.Signature.MethodName);
    }

    [Fact]
    public void An_enter_event_carries_its_concurrency_tag_onto_the_node()
    {
        var sig = new MethodSignature("Notify", "Send", []);
        var tag = new ConcurrencyInfo(
            "async-1", "Notify.Send", 7, "async-worker", true,
            ConcurrencyKind.Async);
        var events = new TraceEvent[]
        {
            new EnterEvent(TestEvents.Context(0, -1), 1000L, sig, tag),
            TestEvents.Exit(0, 2000L, new Returned("true")),
        };

        var tree = TraceTreeBuilder.Build(events, TracingLevel.Detail);

        Assert.Equal(tag, tree.Roots[0].Concurrency);
    }

    [Fact]
    public void Nested_enter_exit_pairs_build_parent_child_tree()
    {
        var outer = new MethodSignature("Outer", "Run", []);
        var inner = new MethodSignature("Inner", "Do", []);
        var events = new TraceEvent[]
        {
            TestEvents.Enter(0, -1, 1000L, outer),
            TestEvents.Enter(1, 0, 2000L, inner),
            TestEvents.Exit(1, 3000L, new Returned(null)),
            TestEvents.Exit(0, 4000L, new Returned(null)),
        };

        var tree = TraceTreeBuilder.Build(
            events, TracingLevel.Detail);

        Assert.Single(tree.Roots);
        var parent = tree.Roots[0];
        Assert.Equal("Outer", parent.Signature.ClassName);
        Assert.Single(parent.Children);
        Assert.Equal("Inner",
            parent.Children[0].Signature.ClassName);
    }

    [Fact]
    public void Sibling_enter_exit_pairs_build_multiple_roots()
    {
        var sigA = new MethodSignature("A", "Run", []);
        var sigB = new MethodSignature("B", "Run", []);
        var events = new TraceEvent[]
        {
            TestEvents.Enter(0, -1, 1000L, sigA),
            TestEvents.Exit(0, 2000L, new Returned(null)),
            TestEvents.Enter(1, -1, 3000L, sigB),
            TestEvents.Exit(1, 4000L, new Returned(null)),
        };

        var tree = TraceTreeBuilder.Build(
            events, TracingLevel.Detail);

        Assert.Equal(2, tree.Roots.Count);
        Assert.Equal("A", tree.Roots[0].Signature.ClassName);
        Assert.Equal("B", tree.Roots[1].Signature.ClassName);
    }

    [Fact]
    public void Enter_without_matching_exit_produces_Incomplete_node()
    {
        var sig = new MethodSignature("Svc", "Run", []);
        var events = new TraceEvent[]
        {
            TestEvents.Enter(0, -1, 1000L, sig),
        };

        var tree = TraceTreeBuilder.Build(
            events, TracingLevel.Detail);

        Assert.Single(tree.Roots);
        Assert.IsType<Incomplete>(tree.Roots[0].Outcome);
        Assert.Equal(0, tree.Roots[0].DurationTicks);
    }

    [Fact]
    public void Exit_without_matching_enter_is_ignored()
    {
        var events = new TraceEvent[]
        {
            TestEvents.Exit(99, 2000L, new Returned("stray")),
        };

        var tree = TraceTreeBuilder.Build(
            events, TracingLevel.Detail);

        Assert.True(tree.IsEmpty);
    }

    [Fact]
    public void Graft_event_attaches_node_to_specified_parent()
    {
        var parentSig = new MethodSignature(
            "Parent", "Run", []);
        var graftedNode = new TraceNode(
            new MethodSignature("Grafted", "Do", []),
            new Returned(null), [], 500);
        var events = new TraceEvent[]
        {
            TestEvents.Enter(0, -1, 1000L, parentSig),
            TestEvents.Graft(0, 1500L, graftedNode),
            TestEvents.Exit(0, 2000L, new Returned(null)),
        };

        var tree = TraceTreeBuilder.Build(
            events, TracingLevel.Detail);

        Assert.Single(tree.Roots);
        Assert.Single(tree.Roots[0].Children);
        Assert.Equal("Grafted",
            tree.Roots[0].Children[0].Signature.ClassName);
    }

    [Fact]
    public void Graft_event_with_parent_minus1_goes_to_roots()
    {
        var graftedNode = new TraceNode(
            new MethodSignature("Grafted", "Do", []),
            new Returned(null), [], 500);
        var events = new TraceEvent[]
        {
            TestEvents.Graft(-1, 1000L, graftedNode),
        };

        var tree = TraceTreeBuilder.Build(
            events, TracingLevel.Detail);

        Assert.Single(tree.Roots);
        Assert.Equal("Grafted",
            tree.Roots[0].Signature.ClassName);
    }

    [Fact]
    public void Graft_event_with_missing_parent_falls_back_to_root()
    {
        var graftedNode = new TraceNode(
            new MethodSignature("Orphan", "Do", []),
            new Returned(null), [], 500);
        var events = new TraceEvent[]
        {
            TestEvents.Graft(999, 1000L, graftedNode),
        };

        var tree = TraceTreeBuilder.Build(
            events, TracingLevel.Detail);

        Assert.Single(tree.Roots);
        Assert.Equal("Orphan",
            tree.Roots[0].Signature.ClassName);
    }

    [Fact]
    public void Errors_level_keeps_Threw_and_Incomplete_discards_Returned()
    {
        var sigOk = new MethodSignature("Ok", "Run", []);
        var sigFail = new MethodSignature("Fail", "Run", []);
        var sigPending = new MethodSignature(
            "Pending", "Run", []);
        var events = new TraceEvent[]
        {
            TestEvents.Enter(0, -1, 1000L, sigOk),
            TestEvents.Exit(0, 2000L, new Returned(null)),
            TestEvents.Enter(1, -1, 3000L, sigFail),
            TestEvents.Exit(1, 4000L,
                new Threw(new Exception("boom"))),
            TestEvents.Enter(2, -1, 5000L, sigPending),
        };

        var tree = TraceTreeBuilder.Build(
            events, TracingLevel.Errors);

        Assert.Equal(2, tree.Roots.Count);
        Assert.Contains(tree.Roots,
            n => n.Signature.ClassName == "Fail");
        Assert.Contains(tree.Roots,
            n => n.Signature.ClassName == "Pending");
        Assert.DoesNotContain(tree.Roots,
            n => n.Signature.ClassName == "Ok");
    }

    [Fact]
    public void Errors_level_retains_full_subtree_of_error_node()
    {
        var fail = new MethodSignature("Fail", "Run", []);
        var okChild = new MethodSignature("Ok", "Load", []);
        var leafChild = new MethodSignature("Leaf", "Read", []);
        var events = new TraceEvent[]
        {
            TestEvents.Enter(0, -1, 1000L, fail),
            TestEvents.Enter(1, 0, 2000L, okChild),
            TestEvents.Exit(1, 2500L, new Returned("a")),
            TestEvents.Enter(2, 0, 3000L, leafChild),
            TestEvents.Exit(2, 3500L, new Returned("b")),
            TestEvents.Exit(0, 4000L, new Threw(new Exception("boom"))),
        };

        var tree = TraceTreeBuilder.Build(
            events, TracingLevel.Errors);

        Assert.Single(tree.Roots);
        var failNode = tree.Roots[0];
        Assert.Equal("Fail", failNode.Signature.ClassName);
        Assert.Equal(2, failNode.Children.Count);
        Assert.Contains(failNode.Children,
            n => n.Signature.ClassName == "Ok");
        Assert.Contains(failNode.Children,
            n => n.Signature.ClassName == "Leaf");
    }

    [Fact]
    public void Summary_level_keeps_root_and_leaf_prunes_intermediate()
    {
        var root = new MethodSignature("Root", "Run", []);
        var mid = new MethodSignature("Mid", "Process", []);
        var leaf = new MethodSignature("Leaf", "Do", []);
        var events = new TraceEvent[]
        {
            TestEvents.Enter(0, -1, 1000L, root),
            TestEvents.Enter(1, 0, 2000L, mid),
            TestEvents.Enter(2, 1, 3000L, leaf),
            TestEvents.Exit(2, 4000L, new Returned(null)),
            TestEvents.Exit(1, 5000L, new Returned(null)),
            TestEvents.Exit(0, 6000L, new Returned(null)),
        };

        var tree = TraceTreeBuilder.Build(
            events, TracingLevel.Summary);

        Assert.Single(tree.Roots);
        Assert.Equal("Root",
            tree.Roots[0].Signature.ClassName);
        Assert.Single(tree.Roots[0].Children);
        Assert.Equal("Leaf",
            tree.Roots[0].Children[0].Signature.ClassName);
    }

    [Fact]
    public void Summary_level_retains_intermediate_error_frame()
    {
        var root = new MethodSignature("Root", "Run", []);
        var mid = new MethodSignature("Mid", "Process", []);
        var leaf = new MethodSignature("Leaf", "Do", []);
        var events = new TraceEvent[]
        {
            TestEvents.Enter(0, -1, 1000L, root),
            TestEvents.Enter(1, 0, 2000L, mid),
            TestEvents.Enter(2, 1, 3000L, leaf),
            TestEvents.Exit(2, 4000L, new Returned(null)),
            TestEvents.Exit(1, 5000L,
                new Threw(new Exception("mid failed"))),
            TestEvents.Exit(0, 6000L, new Returned(null)),
        };

        var tree = TraceTreeBuilder.Build(
            events, TracingLevel.Summary);

        Assert.Single(tree.Roots);
        Assert.Single(tree.Roots[0].Children);
        var retained = tree.Roots[0].Children[0];
        Assert.Equal("Mid", retained.Signature.ClassName);
        Assert.Single(retained.Children);
        Assert.Equal("Leaf",
            retained.Children[0].Signature.ClassName);
    }

    [Fact]
    public void Detail_level_keeps_all_nodes()
    {
        var root = new MethodSignature("Root", "Run", []);
        var mid = new MethodSignature("Mid", "Do", []);
        var leaf = new MethodSignature("Leaf", "Do", []);
        var events = new TraceEvent[]
        {
            TestEvents.Enter(0, -1, 1000L, root),
            TestEvents.Enter(1, 0, 2000L, mid),
            TestEvents.Enter(2, 1, 3000L, leaf),
            TestEvents.Exit(2, 4000L, new Returned(null)),
            TestEvents.Exit(1, 5000L, new Returned(null)),
            TestEvents.Exit(0, 6000L, new Returned(null)),
        };

        var tree = TraceTreeBuilder.Build(
            events, TracingLevel.Detail);

        Assert.Single(tree.Roots);
        Assert.Single(tree.Roots[0].Children);
        Assert.Single(
            tree.Roots[0].Children[0].Children);
    }

    [Fact]
    public void Grafted_node_preserves_internal_children()
    {
        var grandchild = new TraceNode(
            new MethodSignature("GC", "Run", []),
            new Returned(null), [], 100);
        var graftedNode = new TraceNode(
            new MethodSignature("Grafted", "Do", []),
            new Returned(null), [grandchild], 500);
        var parentSig = new MethodSignature(
            "Parent", "Run", []);
        var events = new TraceEvent[]
        {
            TestEvents.Enter(0, -1, 1000L, parentSig),
            TestEvents.Graft(0, 1500L, graftedNode),
            TestEvents.Exit(0, 2000L, new Returned(null)),
        };

        var tree = TraceTreeBuilder.Build(
            events, TracingLevel.Detail);

        var grafted = tree.Roots[0].Children[0];
        Assert.Single(grafted.Children);
        Assert.Equal("GC",
            grafted.Children[0].Signature.ClassName);
    }

    [Fact]
    public void Deep_nesting_builds_correct_multi_level_tree()
    {
        var events = new List<TraceEvent>();
        const int depth = 10;
        for (var i = 0; i < depth; i++)
        {
            events.Add(TestEvents.Enter(
                i, i - 1, i * 1000L,
                new MethodSignature(
                    "Svc", $"Level{i}", [])));
        }

        for (var i = depth - 1; i >= 0; i--)
        {
            events.Add(TestEvents.Exit(
                i, (i + depth) * 1000L,
                new Returned(null)));
        }

        var tree = TraceTreeBuilder.Build(
            events, TracingLevel.Detail);

        Assert.Single(tree.Roots);
        var node = tree.Roots[0];
        for (var i = 1; i < depth; i++)
        {
            Assert.Single(node.Children);
            node = node.Children[0];
            Assert.Equal($"Level{i}",
                node.Signature.MethodName);
        }

        Assert.Empty(node.Children);
    }

    [Fact]
    public void Duration_is_normalized_from_stopwatch_ticks_to_timespan_ticks()
    {
        var sig = new MethodSignature("Svc", "Run", []);
        var events = new TraceEvent[]
        {
            TestEvents.Enter(0, -1, 0L, sig),
            TestEvents.Exit(
                0,
                System.Diagnostics.Stopwatch.Frequency,
                new Returned(null)),
        };

        var tree = TraceTreeBuilder.Build(
            events, TracingLevel.Detail);

        // A one-second span (Stopwatch ticks) becomes one second of TimeSpan
        // ticks so downstream `/ TimeSpan.TicksPerMillisecond` yields 1000 ms.
        Assert.Equal(
            TimeSpan.TicksPerSecond, tree.Roots[0].DurationTicks);
    }
}
