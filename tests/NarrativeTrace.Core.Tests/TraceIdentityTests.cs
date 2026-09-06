// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using Xunit;

namespace NarrativeTrace.Core.Tests;

/// <summary>
/// The one identity resolution every exporter reads: adopt → inherit →
/// generate for the trace id, inherit-or-derive for the story and chapter.
/// </summary>
public class TraceIdentityTests
{
    private const string TraceHex = "0af7651916cd43dd8448eb211c80319c";
    private const string OtherHex = "4bf92f3577b34da6a3ce929d0e0e4736";
    private const string SpanHex = "b7ad6b7169203331";

    [Fact]
    public void The_id_the_tree_carries_is_adopted()
    {
        var identity = TraceIdentity.Of(
            new TraceTree([Node()], new TraceId(TraceHex)));

        Assert.Equal(TraceHex, identity.TraceId.Value);
    }

    [Fact]
    public void A_context_deep_in_the_tree_supplies_the_id_and_the_service()
    {
        var deep = Node(Context(TraceHex) with { ServiceName = "OrderSvc" });
        var tree = new TraceTree([
            Node(),
            Node() with { Children = [Node() with { Children = [deep] }] },
        ]);

        var identity = TraceIdentity.Of(tree);

        Assert.Equal(TraceHex, identity.TraceId.Value);
        Assert.Equal("OrderSvc", identity.Inherited!.ServiceName);
    }

    [Fact]
    public void An_empty_tree_generates_an_id_so_its_chapter_can_validate()
    {
        var identity = TraceIdentity.Of(new TraceTree([]));

        Assert.Matches("^[0-9a-f]{32}$", identity.TraceId.Value);
        Assert.False(identity.TraceId.IsEmpty);
        Assert.Null(identity.Inherited);
    }

    [Fact]
    public void An_empty_tree_takes_the_unknown_story_and_chapter()
    {
        var identity = TraceIdentity.Of(new TraceTree([]));

        Assert.Equal("unknown", identity.StoryId);
        Assert.Equal("unknown", identity.ChapterId);
        Assert.Equal(TraceIdentity.UnknownStory, identity.StoryId);
    }

    [Fact]
    public void The_story_is_derived_from_the_first_root_level_call()
    {
        var tree = new TraceTree([
            Node("OrderService", "PlaceOrder"),
            Node("PaymentService", "Charge"),
        ]);

        var identity = TraceIdentity.Of(tree);

        Assert.Equal("OrderService.PlaceOrder", identity.StoryId);
        Assert.Equal("OrderService.PlaceOrder", identity.ChapterId);
    }

    [Fact]
    public void An_inherited_story_beats_the_derived_one()
    {
        var context = Context(TraceHex) with { StoryId = "checkout", ChapterId = "pay" };
        var tree = new TraceTree([Node("OrderService", "PlaceOrder", context)]);

        var identity = TraceIdentity.Of(tree);

        Assert.Equal("checkout", identity.StoryId);
        Assert.Equal("pay", identity.ChapterId);
    }

    [Fact]
    public void A_chapter_without_one_of_its_own_equals_the_story()
    {
        var context = Context(TraceHex) with { StoryId = "checkout" };
        var tree = new TraceTree([Node("OrderService", "PlaceOrder", context)]);

        var identity = TraceIdentity.Of(tree);

        Assert.Equal("checkout", identity.ChapterId);
    }

    [Fact]
    public void The_trace_name_is_derived_from_the_resolved_id()
    {
        var identity = TraceIdentity.Of(
            new TraceTree([Node()], new TraceId(TraceHex)));

        Assert.Equal(new TraceId(TraceHex).HumanName, identity.TraceName);
        Assert.Matches("^[a-z]+ [a-z]+ [a-z]+$", identity.TraceName);
    }

    [Fact]
    public void A_context_free_tree_still_gets_a_trace_name()
    {
        var identity = TraceIdentity.Of(new TraceTree([Node()]));

        Assert.Matches("^[a-z]+ [a-z]+ [a-z]+$", identity.TraceName);
    }

    [Fact]
    public void A_null_tree_is_rejected()
    {
        Assert.Throws<ArgumentNullException>(() => TraceIdentity.Of(null!));
    }

    // A hand-built or deserialized tree can be cyclic — TraceNode.Children is a
    // type, not a guarantee of acyclicity (a caller that keeps the concrete
    // mutable List<TraceNode> around can mutate it after construction).
    // Resolving identity walks the whole forest for the first SpanContext, so
    // this must terminate rather than recurse the call stack forever — a real
    // C# StackOverflowException here is uncatchable and kills the process
    // before any renderer runs.
    [Fact]
    public void A_cyclic_tree_with_no_context_anywhere_still_generates_an_id()
    {
        var (a, _) = CyclicPair();

        var identity = TraceIdentity.Of(new TraceTree([a]));

        Assert.Matches("^[0-9a-f]{32}$", identity.TraceId.Value);
        Assert.Null(identity.Inherited);
    }

    [Fact]
    public void A_hundred_thousand_deep_context_free_chain_does_not_overflow_the_stack()
    {
        var deep = DeepChain(100_000, context: null);

        var identity = TraceIdentity.Of(new TraceTree([deep]));

        Assert.Matches("^[0-9a-f]{32}$", identity.TraceId.Value);
        Assert.Null(identity.Inherited);
    }

    // Documents the actual tradeoff a bounded walk makes, honestly: a context
    // planted past TreeWalk.MaxDepth is never inherited. Compare with
    // A_context_deep_in_the_tree_supplies_the_id_and_the_service above, which
    // plants one at depth 4 and does find it.
    [Fact]
    public void A_context_planted_past_MaxDepth_is_not_inherited()
    {
        var deep = DeepChain(TreeWalk.MaxDepth + 50, Context(TraceHex));

        var identity = TraceIdentity.Of(new TraceTree([deep]));

        Assert.Null(identity.Inherited);
        Assert.NotEqual(TraceHex, identity.TraceId.Value);
    }

    private static (TraceNode A, TraceNode B) CyclicPair()
    {
        var aChildren = new List<TraceNode>();
        var bChildren = new List<TraceNode>();
        var a = Node("Svc", "A") with { Children = aChildren.AsReadOnly() };
        var b = Node("Svc", "B") with { Children = bChildren.AsReadOnly() };
        aChildren.Add(b);
        bChildren.Add(a);
        return (a, b);
    }

    // Builds a chain `depth` links deep, with `bottom` (default a plain leaf,
    // or one carrying `context`) at the very end.
    private static TraceNode DeepChain(int depth, SpanContext? context)
    {
        var node = context is null ? Node() : Node(context);
        for (var i = 0; i < depth; i++)
        {
            node = Node() with { Children = [node] };
        }

        return node;
    }

    [Fact]
    public void A_node_with_its_own_context_reports_it()
    {
        var identity = TraceIdentity.Of(new TraceTree([Node()]));
        var own = Context(OtherHex) with
        {
            StoryId = "own-story",
            ChapterId = "own-chapter",
            ServiceName = "PaymentSvc",
        };

        var reported = identity.For(Node("PaymentService", "Charge", own));

        Assert.Equal(OtherHex, reported.TraceId.Value);
        Assert.Equal("own-story", reported.StoryId);
        Assert.Equal("own-chapter", reported.ChapterId);
        Assert.Equal("PaymentSvc", reported.Inherited!.ServiceName);
    }

    [Fact]
    public void A_node_without_a_context_reports_the_trees_identity()
    {
        var identity = TraceIdentity.Of(
            new TraceTree([Node()], new TraceId(TraceHex)));

        Assert.Same(identity, identity.For(Node("PaymentService", "Charge")));
    }

    [Fact]
    public void A_node_whose_context_carries_no_ids_falls_back_to_the_tree()
    {
        var identity = TraceIdentity.Of(
            new TraceTree([Node("OrderService", "PlaceOrder")]));
        var incomplete = new SpanContext(
            TraceId.Empty, new SpanId(SpanHex), null);

        var reported = identity.For(Node("PaymentService", "Charge", incomplete));

        Assert.Equal(identity.TraceId, reported.TraceId);
        Assert.Equal("OrderService.PlaceOrder", reported.StoryId);
        Assert.Equal("OrderService.PlaceOrder", reported.ChapterId);
    }

    [Fact]
    public void A_null_node_is_rejected()
    {
        var identity = TraceIdentity.Of(new TraceTree([Node()]));

        Assert.Throws<ArgumentNullException>(() => identity.For(null!));
    }

    private static TraceNode Node(
        string className = "Svc", string methodName = "Run", SpanContext? context = null)
    {
        return new TraceNode(
            new MethodSignature(className, methodName, []),
            new Returned("ok"),
            [],
            0L,
            SpanContext: context);
    }

    private static TraceNode Node(SpanContext context) => Node("Svc", "Run", context);

    private static SpanContext Context(string traceHex)
    {
        return new SpanContext(new TraceId(traceHex), new SpanId(SpanHex), null);
    }
}
