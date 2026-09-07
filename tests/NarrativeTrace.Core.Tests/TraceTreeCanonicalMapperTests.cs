// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using NarrativeTrace.Runtime;

using Xunit;

namespace NarrativeTrace.Core.Tests;

/// <summary>
/// Flattening a finished tree into canonical entries — the input of the
/// per-test <c>.canonical.json</c> artifact and of the cross-runtime conformance
/// fixtures, so determinism matters as much as shape.
/// </summary>
public class TraceTreeCanonicalMapperTests
{
    private const string TraceHex = "0af7651916cd43dd8448eb211c80319c";
    private const string SpanHex = "b7ad6b7169203331";

    [Fact]
    public void One_node_yields_an_enter_then_an_exit_sharing_its_span()
    {
        var entries = TraceTreeCanonicalMapper.FromTree(
            new TraceTree([Node("OrderService", "PlaceOrder")]));

        Assert.Equal(2, entries.Count);
        Assert.Equal("method_enter", entries[0].NtEventType);
        Assert.Equal("method_exit", entries[1].NtEventType);
        Assert.Equal(entries[0].SpanId, entries[1].SpanId);
        Assert.Null(entries[0].ParentSpanId);
        Assert.Equal("OrderService", entries[0].CodeNamespace);
        Assert.Equal("PlaceOrder", entries[0].CodeFunction);
    }

    [Fact]
    public void Children_are_flattened_depth_first_inside_their_parents_span()
    {
        var child = Node("PaymentService", "Charge");
        var root = Node("OrderService", "PlaceOrder") with { Children = [child] };

        var entries = TraceTreeCanonicalMapper.FromTree(new TraceTree([root]));

        Assert.Equal(
            ["OrderService", "PaymentService", "PaymentService", "OrderService"],
            entries.Select(e => e.CodeNamespace));
        Assert.Equal(entries[0].SpanId, entries[1].ParentSpanId);
        Assert.Equal(entries[0].SpanId, entries[2].ParentSpanId);
        Assert.Null(entries[0].ParentSpanId);
        Assert.Null(entries[3].ParentSpanId);
    }

    [Fact]
    public void A_context_free_tree_gets_a_real_unique_identity()
    {
        // Was: the synthetic constant 00000000000000000000000000000001, retired
        // 2026-08-30 because it made two unrelated captures indistinguishable —
        // the one job trace_id exists to do. Span ids stay sequential: they only
        // have to be unique inside the tree, and folding the unique fields for a
        // golden comparison is the conformance normalizer's job (backlog item 6).
        var tree = new TraceTree([
            Node("OrderService", "PlaceOrder") with
            {
                Children = [Node("PaymentService", "Charge")],
            },
        ]);

        var first = TraceTreeCanonicalMapper.FromTree(tree);
        var second = TraceTreeCanonicalMapper.FromTree(tree);

        Assert.Matches("^[0-9a-f]{32}$", first[0].TraceId);
        Assert.NotEqual("00000000000000000000000000000001", first[0].TraceId);
        Assert.Equal("0000000000000001", first[0].SpanId);
        Assert.Equal("0000000000000002", first[1].SpanId);
        Assert.Equal(first.Select(e => e.SpanId), second.Select(e => e.SpanId));
        Assert.Equal("OrderService.PlaceOrder", first[0].NtStoryId);
    }

    [Fact]
    public void One_tree_reads_as_one_trace_however_often_it_is_flattened()
    {
        var tree = new TraceTree([
            Node("OrderService", "PlaceOrder") with
            {
                Children = [Node("PaymentService", "Charge")],
            },
        ]);

        var first = TraceTreeCanonicalMapper.FromTree(tree);
        var second = TraceTreeCanonicalMapper.FromTree(tree);

        Assert.All(first, entry => Assert.Equal(tree.TraceId.Value, entry.TraceId));
        Assert.Equal(first.Select(e => e.TraceId), second.Select(e => e.TraceId));
    }

    [Fact]
    public void Two_context_free_trees_never_share_a_trace_id()
    {
        var first = TraceTreeCanonicalMapper.FromTree(
            new TraceTree([Node("OrderService", "PlaceOrder")]));
        var second = TraceTreeCanonicalMapper.FromTree(
            new TraceTree([Node("OrderService", "PlaceOrder")]));

        Assert.NotEqual(first[0].TraceId, second[0].TraceId);
        Assert.Equal(first[0].NtStoryId, second[0].NtStoryId);
        Assert.Equal(first[0].NtChapterId, second[0].NtChapterId);
    }

    [Fact]
    public void A_context_free_entry_is_named_after_the_trace_it_reports()
    {
        var entry = TraceTreeCanonicalMapper.FromTree(
            new TraceTree([Node("OrderService", "PlaceOrder")]))[0];

        Assert.Equal(new TraceId(entry.TraceId!).HumanName, entry.NtTraceName);
    }

    [Fact]
    public void A_node_that_lost_its_context_is_not_stamped_with_a_second_trace()
    {
        var contextual = Node("OrderService", "PlaceOrder") with
        {
            SpanContext = new SpanContext(
                new TraceId(TraceHex), new SpanId(SpanHex), null),
            Children = [Node("PaymentService", "Charge")],
        };

        var entries = TraceTreeCanonicalMapper.FromTree(new TraceTree([contextual]));

        Assert.All(entries, entry => Assert.Equal(TraceHex, entry.TraceId));
        Assert.All(
            entries,
            entry => Assert.Equal(new TraceId(TraceHex).HumanName, entry.NtTraceName));
    }

    [Fact]
    public void A_real_context_anywhere_is_inherited_by_every_entry()
    {
        var contextual = Node("PaymentService", "Charge") with
        {
            SpanContext = new SpanContext(
                new TraceId(TraceHex), new SpanId(SpanHex), null,
                ServiceName: "OrderSvc", Environment: "test"),
        };
        var root = Node("OrderService", "PlaceOrder") with { Children = [contextual] };

        var entries = TraceTreeCanonicalMapper.FromTree(new TraceTree([root]));

        Assert.All(entries, entry => Assert.Equal(TraceHex, entry.TraceId));
        Assert.All(entries, entry => Assert.Equal("OrderSvc", entry.Service));
        Assert.All(entries, entry => Assert.Equal("test", entry.Environment));
        Assert.Equal(SpanHex, entries[1].SpanId);
    }

    [Fact]
    public void A_context_free_tree_is_stamped_with_the_unknown_service()
    {
        var entries = TraceTreeCanonicalMapper.FromTree(
            new TraceTree([Node("OrderService", "PlaceOrder")]));

        Assert.All(
            entries,
            entry => Assert.Equal(CanonicalEntryMapper.UnknownService, entry.Service));
    }

    [Fact]
    public void The_enter_entry_carries_the_captured_parameters()
    {
        var signature = new MethodSignature(
            "OrderService",
            "PlaceOrder",
            [
                new ParameterCapture("customerId", "\"C1\"", false),
                new ParameterCapture("card", "4111", true),
            ]);
        var entries = TraceTreeCanonicalMapper.FromTree(
            new TraceTree([Node(signature, new Returned("\"ok\""))]));

        var parameters = entries[0].NtParameters!;
        Assert.Equal("\"C1\"", parameters[0].RenderedValue);
        Assert.Equal("[REDACTED]", parameters[1].RenderedValue);
        Assert.True(parameters[1].Redacted);
        Assert.Null(entries[1].NtParameters);
    }

    [Fact]
    public void The_exit_entry_carries_the_outcome_and_the_span_duration()
    {
        var node = Node("OrderService", "PlaceOrder") with
        {
            DurationTicks = TimeSpan.TicksPerMillisecond * 7,
        };

        var exit = TraceTreeCanonicalMapper.FromTree(new TraceTree([node]))[1];

        Assert.Equal("success", exit.NtOutcome);
        Assert.Equal("\"done\"", exit.NtReturnValue);
        Assert.Equal(7, exit.DurationMs);
        Assert.Equal("trace", exit.Level);
    }

    [Fact]
    public void A_thrown_exit_reports_the_error_at_error_level()
    {
        var node = Node(
            new MethodSignature("OrderService", "PlaceOrder", []),
            new Threw(new InvalidOperationException("declined")));

        var exit = TraceTreeCanonicalMapper.FromTree(new TraceTree([node]))[1];

        Assert.Equal("error", exit.Level);
        Assert.Equal("failure", exit.NtOutcome);
        Assert.Equal("InvalidOperationException", exit.ExceptionType);
        Assert.Equal("declined", exit.ExceptionMessage);
        Assert.Equal("!! InvalidOperationException: declined", exit.Message);
    }

    [Fact]
    public void An_incomplete_exit_reports_the_incomplete_outcome()
    {
        var node = Node(
            new MethodSignature("OrderService", "PlaceOrder", []), new Incomplete());

        var exit = TraceTreeCanonicalMapper.FromTree(new TraceTree([node]))[1];

        Assert.Equal("incomplete", exit.NtOutcome);
        Assert.Null(exit.NtReturnValue);
    }

    [Fact]
    public void The_enter_entry_carries_the_declared_identity_of_its_method()
    {
        var signature = new MethodSignature(
            "OrderService",
            "PlaceOrder",
            [new ParameterCapture("customerId", "\"C1\"", false, null, "System.String")],
            Narration: "Opening for C1",
            ErrorContext: null,
            Namespace: "Acme.Billing",
            ReturnType: "System.String",
            NarrationTemplate: "Opening for {customerId}");

        var enter = TraceTreeCanonicalMapper.FromTree(
            new TraceTree([Node(signature, new Returned("\"ok\""))]))[0];

        Assert.Equal("Acme.Billing", enter.NtPackage);
        Assert.Equal("System.String", enter.NtReturnType);
        Assert.Equal("Opening for {customerId}", enter.NtNarrationTemplate);
        Assert.Equal("System.String", enter.NtParameters![0].DeclaredType);
    }

    [Fact]
    public void A_thrown_exit_names_the_exceptions_namespace()
    {
        var node = Node(
            new MethodSignature("OrderService", "PlaceOrder", []),
            new Threw(new InvalidOperationException("declined")));

        var exit = TraceTreeCanonicalMapper.FromTree(new TraceTree([node]))[1];

        Assert.Equal("System", exit.NtExceptionPackage);
    }

    [Fact]
    public void A_signature_without_declared_identity_reports_none()
    {
        var enter = TraceTreeCanonicalMapper.FromTree(
            new TraceTree([Node("OrderService", "PlaceOrder")]))[0];

        Assert.Null(enter.NtPackage);
        Assert.Null(enter.NtReturnType);
        Assert.Null(enter.NtNarrationTemplate);
        Assert.Null(enter.NtExceptionPackage);
    }

    [Fact]
    public void An_empty_tree_yields_no_entries()
    {
        Assert.Empty(TraceTreeCanonicalMapper.FromTree(new TraceTree([])));
    }

    [Fact]
    public void A_null_tree_is_rejected()
    {
        Assert.Throws<ArgumentNullException>(
            () => TraceTreeCanonicalMapper.FromTree(null!));
    }

    [Fact]
    public void Every_entry_of_a_flattened_tree_validates_against_the_entry_schema()
    {
        var signature = new MethodSignature(
            "OrderService",
            "PlaceOrder",
            [new ParameterCapture("customerId", "\"C1\"", false)]);
        var tree = new TraceTree([
            Node(signature, new Returned("\"ok\"")) with
            {
                Children =
                [
                    Node(
                        new MethodSignature("PaymentService", "Charge", []),
                        new Threw(new InvalidOperationException("declined"))),
                ],
            },
        ]);

        foreach (var entry in TraceTreeCanonicalMapper.FromTree(tree))
        {
            SchemaValidator.AssertValid(
                "entry.schema.json", CanonicalEntrySerializer.ToJson(entry));
        }
    }

    // TraceNode.Children is a type, not a guarantee of acyclicity - a hand-built or replayed tree
    // can hold a genuine reference cycle. FromTree must terminate rather than recurse the call
    // stack forever — the same unbounded-recursion defect fixed in the java flagship.
    [Fact]
    public void FromTree_does_not_hang_on_a_cyclic_tree()
    {
        var aChildren = new List<TraceNode>();
        var bChildren = new List<TraceNode>();
        var a = new TraceNode(
            new MethodSignature("Svc", "A", []), new Returned(null), aChildren.AsReadOnly(), 0);
        var b = new TraceNode(
            new MethodSignature("Svc", "B", []), new Returned(null), bChildren.AsReadOnly(), 0);
        aChildren.Add(b);
        bChildren.Add(a);

        var entries = TraceTreeCanonicalMapper.FromTree(new TraceTree([a]));

        Assert.NotEmpty(entries);
    }

    private static TraceNode Node(string className, string methodName) =>
        Node(new MethodSignature(className, methodName, []), new Returned("\"done\""));

    private static TraceNode Node(MethodSignature signature, TraceOutcome outcome) =>
        new(signature, outcome, [], 0);
}
