// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using Xunit;

namespace NarrativeTrace.Core.Tests;

/// <summary>
/// The AI-safe structural trace artifact (ADR-002): the developer-authored
/// shape of a scenario — class/method/parameter names, call hierarchy, outcome
/// kinds — with zero runtime values, zero timings, zero identifiers.
/// Deterministic by construction so the artifact is byte-diffable (approval
/// baselines, conformance fixtures) and safe to hand to an AI agent.
/// </summary>
public sealed class StructuralTraceRendererTests
{
    private static TraceTree TreeOf(params TraceNode[] roots)
    {
        return new TraceTree(roots);
    }

    private static ParameterCapture Param(string name, string value)
    {
        return new ParameterCapture(name, value, false);
    }


    private static ConcurrencyInfo Fork(string threadName, int threadId)
    {
        return new ConcurrencyInfo(
            "fork-1", threadName, threadId, threadName, true,
            ConcurrencyKind.ForkJoin);
    }


    private static ConcurrencyInfo Async(string groupId)
    {
        return new ConcurrencyInfo(
            groupId, "task", 7, "async-worker", true,
            ConcurrencyKind.Async);
    }

    private static ConcurrencyInfo Background(string taskLabel)
    {
        return new ConcurrencyInfo(
            "fanf-1", taskLabel, 9, "pool-9", true,
            ConcurrencyKind.FireAndForget);
    }

    [Fact]
    public void Leaf_call_renders_names_and_outcome_kind_without_values()
    {
        var node = new TraceNode(
            new MethodSignature(
                "OrderService", "PlaceOrder",
                [Param("customerId", "\"C-123\""), Param("quantity", "2")]),
            new Returned("\"order-42\""),
            [],
            412 * TimeSpan.TicksPerMillisecond);

        var result = StructuralTraceRenderer.Render(TreeOf(node));

        Assert.Equal(
            "- OrderService.PlaceOrder(customerId, quantity) → value\n",
            result);
        Assert.DoesNotContain("C-123", result, StringComparison.Ordinal);
        Assert.DoesNotContain("412", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Thrown_exception_renders_type_but_never_the_message()
    {
        var node = new TraceNode(
            new MethodSignature("PaymentGateway", "Charge", []),
            new Threw(new InvalidOperationException("card 4111-1111 declined")),
            [],
            TimeSpan.TicksPerMillisecond);

        var result = StructuralTraceRenderer.Render(TreeOf(node));

        Assert.Equal(
            "- PaymentGateway.Charge() !! InvalidOperationException\n", result);
        Assert.DoesNotContain("4111", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Incomplete_call_renders_the_in_flight_marker()
    {
        var node = new TraceNode(
            new MethodSignature("Svc", "Hang", []), new Incomplete(), [], 0);

        var result = StructuralTraceRenderer.Render(TreeOf(node));

        Assert.Equal("- Svc.Hang() ?? incomplete\n", result);
    }

    [Fact]
    public void Void_call_renders_no_outcome_kind()
    {
        var node = new TraceNode(
            new MethodSignature("AuditSink", "Record", [Param("entry", "\"e\"")]),
            new Returned(null),
            [],
            TimeSpan.TicksPerMillisecond);

        var result = StructuralTraceRenderer.Render(TreeOf(node));

        Assert.Equal("- AuditSink.Record(entry)\n", result);
    }

    [Fact]
    public void Child_calls_nest_by_indentation_in_capture_order()
    {
        var validate = new TraceNode(
            new MethodSignature(
                "ExpenseValidator", "EnsureValid", [Param("expense", "\"e\"")]),
            new Returned(null), [], TimeSpan.TicksPerMillisecond);
        var store = new TraceNode(
            new MethodSignature("TripLedger", "RecordExpense", []),
            new Returned(null), [], TimeSpan.TicksPerMillisecond);
        var parent = new TraceNode(
            new MethodSignature(
                "TripSettlementService", "RecordExpense",
                [Param("tripName", "\"Ski\"")]),
            new Returned(null), [validate, store], TimeSpan.TicksPerMillisecond);

        var result = StructuralTraceRenderer.Render(TreeOf(parent));

        Assert.Equal(
            "- TripSettlementService.RecordExpense(tripName)\n"
            + "  - ExpenseValidator.EnsureValid(expense)\n"
            + "  - TripLedger.RecordExpense()\n",
            result);
    }

    [Fact]
    public void Fork_members_render_sorted_by_signature_without_thread_identity()
    {
        var stock = new TraceNode(
            new MethodSignature("StockService", "Check", []),
            new Returned("true"), [], 5 * TimeSpan.TicksPerMillisecond, 0,
            Fork("pool-1-thread-2", 22));
        var discount = new TraceNode(
            new MethodSignature("DiscountEngine", "Calculate", []),
            new Returned("0.15"), [], 9 * TimeSpan.TicksPerMillisecond, 0,
            Fork("pool-1-thread-1", 21));
        var parent = new TraceNode(
            new MethodSignature("CheckoutService", "Quote", []),
            new Returned("\"quote\""),
            [stock, discount],
            20 * TimeSpan.TicksPerMillisecond);

        var result = StructuralTraceRenderer.Render(TreeOf(parent));

        Assert.Equal(
            "- CheckoutService.Quote() → value\n"
            + "  ~ fork [2]\n"
            + "    - DiscountEngine.Calculate() → value\n"
            + "    - StockService.Check() → value\n",
            result);
        Assert.DoesNotContain("pool-1", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Fire_and_forget_launch_renders_the_marker_with_its_children()
    {
        var launcher = new TraceNode(
            new MethodSignature("NotificationService", "fire-and-forget", []),
            new Incomplete(), [], 0, 0, Background("fire-and-forget"));
        var work = new TraceNode(
            new MethodSignature("NotificationService", "Send", []),
            new Returned(null), [], TimeSpan.TicksPerMillisecond, 0,
            Background("Send"));
        var parent = new TraceNode(
            new MethodSignature("CheckoutService", "Complete", []),
            new Returned(null), [launcher, work], TimeSpan.TicksPerMillisecond);

        var result = StructuralTraceRenderer.Render(TreeOf(parent));

        Assert.Equal(
            "- CheckoutService.Complete()\n"
            + "  ~ fire-and-forget\n"
            + "    - NotificationService.Send()\n",
            result);
        Assert.DoesNotContain("pool-9", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Document_form_carries_only_the_stable_scenario_header()
    {
        var node = new TraceNode(
            new MethodSignature("Svc", "Run", []),
            new Returned(null), [], TimeSpan.TicksPerMillisecond);

        var result = StructuralTraceRenderer.RenderDocument(
            TreeOf(node), "Weekend trip settles with three transfers");

        Assert.Equal(
            "scenario: Weekend trip settles with three transfers\n"
            + "\n"
            + "- Svc.Run()\n",
            result);
    }

    [Fact]
    public void Identical_behavior_renders_byte_identically_despite_values()
    {
        var first = new TraceNode(
            new MethodSignature(
                "OrderService", "PlaceOrder", [Param("customerId", "\"C-1\"")]),
            new Returned("\"order-1\""), [], 10 * TimeSpan.TicksPerMillisecond);
        var second = new TraceNode(
            new MethodSignature(
                "OrderService", "PlaceOrder",
                [Param("customerId", "\"C-99999\"")]),
            new Returned("\"order-2\""), [], 999 * TimeSpan.TicksPerMillisecond);

        Assert.Equal(
            StructuralTraceRenderer.Render(TreeOf(first)),
            StructuralTraceRenderer.Render(TreeOf(second)));
    }

    [Fact]
    public void Redacted_parameter_contributes_its_name_and_never_its_value()
    {
        var node = new TraceNode(
            new MethodSignature(
                "LoginService", "Authenticate",
                [new ParameterCapture("password", "hunter2", true)]),
            new Returned("true"), [], TimeSpan.TicksPerMillisecond);

        var result = StructuralTraceRenderer.Render(TreeOf(node));

        Assert.Equal(
            "- LoginService.Authenticate(password) → value\n", result);
        Assert.DoesNotContain("hunter2", result, StringComparison.Ordinal);
        Assert.DoesNotContain(
            RedactionPolicy.Marker, result, StringComparison.Ordinal);
    }

    [Fact]
    public void Narration_and_error_context_never_reach_the_artifact()
    {
        var node = new TraceNode(
            new MethodSignature(
                "Svc", "Run", [], "charges the card for 42 EUR",
                "while charging card 4111"),
            new Threw(new InvalidOperationException("boom")),
            [],
            TimeSpan.TicksPerMillisecond);

        var result = StructuralTraceRenderer.Render(TreeOf(node));

        Assert.Equal("- Svc.Run() !! InvalidOperationException\n", result);
    }

    [Fact]
    public void Identifiers_pass_through_control_character_sanitization()
    {
        var node = new TraceNode(
            new MethodSignature(
                "Ord\ner", "Pl\tace", [Param("cu\u001bst", "\"v\"")]),
            new Returned(null), [], 0);

        var result = StructuralTraceRenderer.RenderDocument(
            TreeOf(node), "scen\nario");

        Assert.Equal(
            "scenario: scen\\nario\n\n- Ord\\ner.Pl\\tace(cu\\u001bst)\n",
            result);
    }

    [Fact]
    public void Empty_trace_renders_nothing_and_a_bare_document_header()
    {
        var empty = TreeOf();

        Assert.Equal(string.Empty, StructuralTraceRenderer.Render(empty));
        Assert.Equal(
            "scenario: nothing happened\n\n",
            StructuralTraceRenderer.RenderDocument(empty, "nothing happened"));
    }

    [Fact]
    public void Every_root_renders_in_capture_order()
    {
        var first = new TraceNode(
            new MethodSignature("Zed", "Last", []), new Returned(null), [], 0);
        var second = new TraceNode(
            new MethodSignature("Abe", "First", []), new Returned(null), [], 0);

        var result = StructuralTraceRenderer.Render(TreeOf(first, second));

        Assert.Equal("- Zed.Last()\n- Abe.First()\n", result);
    }

    [Fact]
    public void Fork_members_of_one_class_sort_by_method_name()
    {
        var beta = new TraceNode(
            new MethodSignature("Repo", "Save", []), new Returned(null), [], 0,
            0, Fork("t-2", 2));
        var alpha = new TraceNode(
            new MethodSignature("Repo", "Load", []), new Returned(null), [], 0,
            0, Fork("t-1", 1));
        var parent = new TraceNode(
            new MethodSignature("Svc", "Run", []), new Returned(null),
            [beta, alpha], 0);

        var result = StructuralTraceRenderer.Render(TreeOf(parent));

        Assert.Equal(
            "- Svc.Run()\n  ~ fork [2]\n    - Repo.Load()\n    - Repo.Save()\n",
            result);
    }

    [Fact]
    public void Async_members_render_under_an_async_marker_sorted_by_signature()
    {
        var second = new TraceNode(
            new MethodSignature("Email", "Send", []), new Returned(null), [], 0,
            0, Async("async-1"));
        var first = new TraceNode(
            new MethodSignature("Audit", "Record", []), new Returned(null), [], 0,
            0, Async("async-1"));
        var parent = new TraceNode(
            new MethodSignature("Svc", "Run", []), new Returned(null),
            [second, first], 0);

        var result = StructuralTraceRenderer.Render(TreeOf(parent));

        Assert.Equal(
            "- Svc.Run()\n  ~ async [2]\n    - Audit.Record()\n    - Email.Send()\n",
            result);
    }

    [Fact]
    public void Async_roots_are_partitioned_exactly_as_async_children_are()
    {
        // Work that outlived its caller is a root, and its capture order is
        // the scheduler's — the artifact must not pin it.
        var sequential = new TraceNode(
            new MethodSignature("Svc", "Run", []), new Returned(null), [], 0);
        var second = new TraceNode(
            new MethodSignature("Email", "Send", []), new Returned(null), [], 0,
            0, Async("async-1"));
        var first = new TraceNode(
            new MethodSignature("Audit", "Record", []), new Returned(null), [], 0,
            0, Async("async-1"));

        var result = StructuralTraceRenderer.Render(
            TreeOf(sequential, second, first));

        Assert.Equal(
            "- Svc.Run()\n~ async [2]\n  - Audit.Record()\n  - Email.Send()\n",
            result);
    }

    [Fact]
    public void Two_async_groups_render_as_two_markers()
    {
        var fromFirstCall = new TraceNode(
            new MethodSignature("Audit", "Record", []), new Returned(null), [], 0,
            0, Async("async-1"));
        var fromSecondCall = new TraceNode(
            new MethodSignature("Email", "Send", []), new Returned(null), [], 0,
            0, Async("async-2"));

        var result = StructuralTraceRenderer.Render(
            TreeOf(fromFirstCall, fromSecondCall));

        Assert.Equal(
            "~ async [1]\n  - Audit.Record()\n"
            + "~ async [1]\n  - Email.Send()\n",
            result);
    }

    [Fact]
    public void A_fork_group_still_renders_under_the_fork_marker()
    {
        var member = new TraceNode(
            new MethodSignature("Repo", "Load", []), new Returned(null), [], 0,
            0, Fork("t-1", 1));

        var result = StructuralTraceRenderer.Render(TreeOf(member));

        Assert.Equal("~ fork [1]\n  - Repo.Load()\n", result);
    }

    [Fact]
    public void Output_uses_lf_only_and_ends_with_a_trailing_newline()
    {
        var child = new TraceNode(
            new MethodSignature("B", "Two", []), new Returned(null), [], 0);
        var root = new TraceNode(
            new MethodSignature("A", "One", []), new Returned(null), [child], 0);

        var result = StructuralTraceRenderer.RenderDocument(
            TreeOf(root), "a scenario");

        Assert.DoesNotContain("\r", result, StringComparison.Ordinal);
        Assert.EndsWith("\n", result, StringComparison.Ordinal);
    }

    /// <summary>
    /// A <see cref="Threw"/> carrying no exception instance has no type name to
    /// emit, and the format defines no token for "threw something unknown". The
    /// renderer degrades to silence exactly like every other renderer here,
    /// rather than inventing a token for a state the runtime never builds.
    /// </summary>
    [Fact]
    public void Thrown_outcome_without_an_exception_emits_no_outcome_kind()
    {
        var node = new TraceNode(
            new MethodSignature("Svc", "Run", []), new Threw(null), [], 0);

        Assert.Equal(
            "- Svc.Run()\n", StructuralTraceRenderer.Render(TreeOf(node)));
    }

    [Fact]
    public void Fire_and_forget_with_no_captured_work_renders_a_bare_marker()
    {
        var launcher = new TraceNode(
            new MethodSignature("Svc", "fire-and-forget", []),
            new Incomplete(), [], 0, 0, Background("fire-and-forget"));
        var parent = new TraceNode(
            new MethodSignature("Svc", "Run", []), new Returned(null),
            [launcher], 0);

        var result = StructuralTraceRenderer.Render(TreeOf(parent));

        Assert.Equal("- Svc.Run()\n  ~ fire-and-forget\n", result);
    }

    [Fact]
    public void Calls_below_a_fork_member_keep_nesting_under_that_member()
    {
        var deep = new TraceNode(
            new MethodSignature("Repo", "Query", []), new Returned("1"), [], 0);
        var member = new TraceNode(
            new MethodSignature("Pricing", "Quote", []), new Returned("2"),
            [deep], 0, 0, Fork("t-1", 1));
        var parent = new TraceNode(
            new MethodSignature("Svc", "Run", []), new Returned(null),
            [member], 0);

        var result = StructuralTraceRenderer.Render(TreeOf(parent));

        Assert.Equal(
            "- Svc.Run()\n  ~ fork [1]\n    - Pricing.Quote() → value\n"
            + "      - Repo.Query() → value\n",
            result);
    }

    // TraceNode.Children is a type, not a guarantee of acyclicity - a hand-built or replayed tree
    // can hold a genuine reference cycle. Render must degrade with a marker rather than recurse
    // the call stack forever — the same unbounded-recursion defect fixed in the java flagship.
    [Fact]
    public void Render_does_not_hang_on_a_cyclic_tree()
    {
        var aChildren = new List<TraceNode>();
        var bChildren = new List<TraceNode>();
        var a = new TraceNode(
            new MethodSignature("Svc", "A", []), new Returned(null), aChildren.AsReadOnly(), 0);
        var b = new TraceNode(
            new MethodSignature("Svc", "B", []), new Returned(null), bChildren.AsReadOnly(), 0);
        aChildren.Add(b);
        bChildren.Add(a);

        var result = StructuralTraceRenderer.Render(TreeOf(a));

        Assert.Contains(TreeWalk.CycleMarker, result, StringComparison.Ordinal);
    }
}
