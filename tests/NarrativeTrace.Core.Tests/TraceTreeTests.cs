// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using Xunit;

namespace NarrativeTrace.Core.Tests;

public class TraceTreeTests
{
    private const string TraceHex = "0af7651916cd43dd8448eb211c80319c";
    private const string OtherHex = "4bf92f3577b34da6a3ce929d0e0e4736";

    [Fact]
    public void IsEmpty_when_no_roots()
    {
        var tree = new TraceTree(Array.Empty<TraceNode>());

        Assert.True(tree.IsEmpty);
    }

    [Fact]
    public void Is_not_empty_with_roots()
    {
        var tree = new TraceTree(new[] { Node() });

        Assert.False(tree.IsEmpty);
    }

    [Fact]
    public void An_empty_tree_keeps_no_trace_id()
    {
        var tree = new TraceTree(Array.Empty<TraceNode>());

        Assert.True(tree.TraceId.IsEmpty);
    }

    [Fact]
    public void A_tree_with_nodes_and_no_context_generates_a_real_trace_id()
    {
        var tree = new TraceTree(new[] { Node() });

        Assert.Matches("^[0-9a-f]{32}$", tree.TraceId.Value);
        Assert.NotEqual("00000000000000000000000000000001", tree.TraceId.Value);
        Assert.False(tree.TraceId.IsEmpty);
    }

    [Fact]
    public void Two_context_free_trees_never_share_a_generated_trace_id()
    {
        var first = new TraceTree(new[] { Node() });
        var second = new TraceTree(new[] { Node() });

        Assert.NotEqual(first.TraceId, second.TraceId);
    }

    [Fact]
    public void A_generated_trace_id_is_resolved_once_and_kept()
    {
        var tree = new TraceTree(new[] { Node() });

        Assert.Equal(tree.TraceId, tree.TraceId);
        Assert.Equal(tree.TraceId, (tree with { }).TraceId);
    }

    [Fact]
    public void The_id_the_capturing_context_assigned_is_adopted()
    {
        var tree = new TraceTree(new[] { Node() }, new TraceId(TraceHex));

        Assert.Equal(TraceHex, tree.TraceId.Value);
    }

    [Fact]
    public void An_adopted_id_beats_a_context_found_in_the_tree()
    {
        var tree = new TraceTree(
            new[] { Node(Context(OtherHex)) }, new TraceId(TraceHex));

        Assert.Equal(TraceHex, tree.TraceId.Value);
    }

    [Fact]
    public void A_context_anywhere_in_the_tree_is_inherited_before_generating()
    {
        var nested = Node() with { Children = [Node(Context(TraceHex))] };

        var tree = new TraceTree(new[] { Node(), nested });

        Assert.Equal(TraceHex, tree.TraceId.Value);
    }

    [Fact]
    public void A_copy_that_gains_its_first_nodes_resolves_an_id()
    {
        // A record copy does not re-run the constructor, so `with` is the one
        // way to reach a tree that has nodes and no id — and each exporter of
        // such a tree would mint its own, naming two traces for one capture.
        var empty = new TraceTree(Array.Empty<TraceNode>());

        var grown = empty with { Roots = new[] { Node() } };

        Assert.False(grown.TraceId.IsEmpty);
        Assert.Matches("^[0-9a-f]{32}$", grown.TraceId.Value);
    }

    [Fact]
    public void A_copy_that_gains_nodes_carrying_a_context_inherits_it()
    {
        var empty = new TraceTree(Array.Empty<TraceNode>());

        var grown = empty with { Roots = new[] { Node(Context(TraceHex)) } };

        Assert.Equal(TraceHex, grown.TraceId.Value);
    }

    [Fact]
    public void A_copy_that_replaces_the_nodes_keeps_the_trace_it_identifies()
    {
        // Re-rooting is not re-tracing: the id already held is adopted, so a
        // filtered or regrafted copy still names the capture it came from.
        var tree = new TraceTree(new[] { Node() });

        var replaced = tree with { Roots = new[] { Node(), Node() } };

        Assert.Equal(tree.TraceId, replaced.TraceId);
    }

    [Fact]
    public void A_copy_emptied_of_its_nodes_keeps_the_id_it_was_given()
    {
        var tree = new TraceTree(new[] { Node() }, new TraceId(TraceHex));

        var emptied = tree with { Roots = Array.Empty<TraceNode>() };

        Assert.Equal(TraceHex, emptied.TraceId.Value);
    }

    [Fact]
    public void An_empty_tree_given_an_id_keeps_it()
    {
        // A level filter can empty a tree that really did run — the id it was
        // handed is real, so it is kept; only generation is withheld.
        var tree = new TraceTree(Array.Empty<TraceNode>(), new TraceId(TraceHex));

        Assert.Equal(TraceHex, tree.TraceId.Value);
    }

    private static TraceNode Node(SpanContext? context = null)
    {
        return new TraceNode(
            new MethodSignature("Svc", "Run", Array.Empty<ParameterCapture>()),
            new Returned("ok"),
            Array.Empty<TraceNode>(),
            0L,
            SpanContext: context);
    }

    private static SpanContext Context(string traceHex)
    {
        return new SpanContext(
            new TraceId(traceHex), new SpanId("b7ad6b7169203331"), null);
    }
}
