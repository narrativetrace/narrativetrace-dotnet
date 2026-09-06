// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using Xunit;

namespace NarrativeTrace.Core.Tests;

public class ProseRendererTests
{
    [Fact]
    public void Single_method_produces_sentence()
    {
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("OrderService", "PlaceOrder",
                    [new ParameterCapture("orderId", "\"order-42\"", false)]),
                new Returned(null), [], 0),
        ]);

        var result = ProseRenderer.Render(tree);

        Assert.Contains("order service", result);
        Assert.Contains("place order", result);
        Assert.Contains("orderId: `\"order-42\"`", result);
    }

    [Fact]
    public void Redacted_parameter_renders_marker_even_if_value_captured()
    {
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Auth", "Login",
                    [new ParameterCapture("password", "hunter2", true)]),
                new Returned(null), [], 0),
        ]);

        var result = ProseRenderer.Render(tree);

        Assert.Contains("[REDACTED]", result);
        Assert.DoesNotContain("hunter2", result);
    }

    [Fact]
    public void CamelCase_method_name_splits_into_words()
    {
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Svc", "getOrderById", []),
                new Returned(null), [], 0),
        ]);

        var result = ProseRenderer.Render(tree);

        Assert.Contains("get order by id", result);
    }

    [Fact]
    public void Class_name_humanized()
    {
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("OrderService", "Run", []),
                new Returned(null), [], 0),
        ]);

        var result = ProseRenderer.Render(tree);

        Assert.Contains("The order service", result);
    }

    [Fact]
    public void Return_value_in_sentence()
    {
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Svc", "Compute", []),
                new Returned("42"), [], 0),
        ]);

        var result = ProseRenderer.Render(tree);

        Assert.Contains("and returns 42", result);
    }

    [Fact]
    public void Error_in_sentence()
    {
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Svc", "Fail", []),
                new Threw(new InvalidOperationException("boom")),
                [], 0),
        ]);

        var result = ProseRenderer.Render(tree);

        Assert.Contains(
            "but throws InvalidOperationException: boom",
            result);
    }

    [Fact]
    public void Exception_message_control_chars_are_sanitized()
    {
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Svc", "Fail", []),
                new Threw(
                    new InvalidOperationException("evil\nINFO forged")),
                [], 0),
        ]);

        var result = ProseRenderer.Render(tree);

        Assert.Contains("evil\\nINFO forged", result);
        Assert.DoesNotContain("evil\nINFO forged", result);
    }

    [Fact]
    public void Nested_calls_indent()
    {
        var child = new TraceNode(
            new MethodSignature("Repo", "Save", []),
            new Returned(null), [], 0);
        var root = new TraceNode(
            new MethodSignature("Svc", "Run", []),
            new Returned(null), [child], 0);
        var tree = new TraceTree([root]);

        var result = ProseRenderer.Render(tree);
        var lines = result.TrimEnd().Split('\n');

        Assert.Equal(2, lines.Length);
        Assert.StartsWith("The svc", lines[0]);
        Assert.StartsWith("  The repo", lines[1]);
    }

    [Fact]
    public void Multiple_parameters_with_commas()
    {
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Svc", "Add", [
                    new ParameterCapture("a", "1", false),
                    new ParameterCapture("b", "2", false),
                ]),
                new Returned(null), [], 0),
        ]);

        var result = ProseRenderer.Render(tree);

        Assert.Contains("a: `1`, b: `2`", result);
    }

    [Fact]
    public void Empty_tree_produces_empty_string()
    {
        var tree = new TraceTree([]);

        var result = ProseRenderer.Render(tree);

        Assert.Equal("", result);
    }

    [Fact]
    public void Prose_renders_concurrent_block()
    {
        var info = new ConcurrencyInfo(
            "fork-1", "A.Do", 1, null, true,
            ConcurrencyKind.ForkJoin);
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Parent", "Run", []),
                new Returned(null),
                [
                    MakeConcurrentNode("A", "Do", info),
                    MakeConcurrentNode("B", "Do", info),
                ],
                0),
        ]);

        var result = ProseRenderer.Render(tree);

        Assert.Contains("Concurrently:", result);
    }

    [Fact]
    public void Prose_lists_members_inside_block()
    {
        var info = new ConcurrencyInfo(
            "fork-1", "A.Do", 1, null, true,
            ConcurrencyKind.ForkJoin);
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Parent", "Run", []),
                new Returned(null),
                [MakeConcurrentNode("Svc", "Work", info)],
                0),
        ]);

        var result = ProseRenderer.Render(tree);

        Assert.Contains("svc", result);
        Assert.Contains("work", result);
    }

    [Fact]
    public void Prose_renders_fire_and_forget()
    {
        var info = new ConcurrencyInfo(
            "fanf-1", "Bg.Work", 1, null, true,
            ConcurrencyKind.FireAndForget);
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("P", "Run", []),
                new Returned(null),
                [MakeConcurrentNode("Bg", "Work", info)],
                0),
        ]);

        var result = ProseRenderer.Render(tree);

        Assert.Contains(
            "In the background:", result);
    }

    [Fact]
    public void Prose_fire_and_forget_without_body_shows_placeholder()
    {
        var info = new ConcurrencyInfo(
            "fanf-1", "fire-and-forget", 7, null, true,
            ConcurrencyKind.FireAndForget);
        var launcher = new TraceNode(
            new MethodSignature("Bg", "fire-and-forget", []),
            new Incomplete(), [], 0, 0, info);
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("P", "Run", []),
                new Returned(null), [launcher], 0),
        ]);

        var result = ProseRenderer.Render(tree);

        Assert.Contains("In the background:", result);
        Assert.Contains(
            "(launched, result not captured).", result);
    }

    [Fact]
    public void Prose_shows_sequential_async_hint()
    {
        var info = new ConcurrencyInfo(
            "fork-1", "A.Do", 1, null, true,
            ConcurrencyKind.ForkJoin);
        var ms = TimeSpan.TicksPerMillisecond;
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("P", "Run", []),
                new Returned(null),
                [
                    MakeConcurrentNode(
                        "A", "Do", info,
                        100 * ms, 0),
                    MakeConcurrentNode(
                        "B", "Do", info,
                        100 * ms, 100 * ms),
                ],
                0),
        ]);

        var result = ProseRenderer.Render(tree);

        Assert.Contains(
            "awaited sequentially", result);
    }

    [Fact]
    public void Depth_two_indentation_uses_four_spaces()
    {
        var grandchild = new TraceNode(
            new MethodSignature("Dao", "Query", []),
            new Returned(null), [], 0);
        var child = new TraceNode(
            new MethodSignature("Repo", "Find", []),
            new Returned(null), [grandchild], 0);
        var root = new TraceNode(
            new MethodSignature("Svc", "Run", []),
            new Returned(null), [child], 0);
        var tree = new TraceTree([root]);

        var result = ProseRenderer.Render(tree);
        var lines = result.TrimEnd().Split('\n');

        Assert.Equal(3, lines.Length);
        Assert.StartsWith("The svc", lines[0]);
        Assert.StartsWith("  The repo", lines[1]);
        Assert.StartsWith("    The dao", lines[2]);
        Assert.False(
            lines[2].StartsWith("     ", StringComparison.Ordinal));
    }

    [Fact]
    public void Concurrent_members_indented_deeper_than_label()
    {
        var info = new ConcurrencyInfo(
            "fork-1", "A.Do", 1, null, true,
            ConcurrencyKind.ForkJoin);
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Parent", "Run", []),
                new Returned(null),
                [
                    MakeConcurrentNode("A", "Do", info),
                    MakeConcurrentNode("B", "Do", info),
                ],
                0),
        ]);

        var result = ProseRenderer.Render(tree);
        var lines = result.TrimEnd().Split('\n');

        // Parent is depth 0
        // Concurrent label is depth 1 (2 spaces)
        // Members are depth 2 (4 spaces)
        var labelLine = lines.First(
            l => l.Contains("Concurrently:"));
        var memberLines = lines.Where(
            l => l.TrimStart().StartsWith("The a", StringComparison.Ordinal)
                 || l.TrimStart().StartsWith("The b", StringComparison.Ordinal))
            .ToList();

        Assert.StartsWith("  ", labelLine);
        Assert.All(memberLines, l =>
            Assert.StartsWith("    ", l));
    }

    [Fact]
    public void Savings_zero_shows_no_save_text()
    {
        var info = new ConcurrencyInfo(
            "fork-1", "A.Do", 1, null, true,
            ConcurrencyKind.ForkJoin);
        // Two members with same duration, non-overlapping
        // => sequential, total=200ms, max=100ms, savings=100
        // To get savings=0: total must equal parallelizable
        // That means all durations the same and total==max
        // But with 2 members that's impossible unless
        // duration is 0
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("P", "Run", []),
                new Returned(null),
                [
                    MakeConcurrentNode(
                        "A", "Do", info, 0, 0),
                    MakeConcurrentNode(
                        "B", "Do", info, 0, 1),
                ],
                0),
        ]);

        var result = ProseRenderer.Render(tree);

        Assert.Contains(
            "Concurrently (awaited sequentially):",
            result);
        Assert.DoesNotContain("could save", result);
    }

    [Fact]
    public void Savings_one_ms_shows_could_save_message()
    {
        var info = new ConcurrencyInfo(
            "fork-1", "A.Do", 1, null, true,
            ConcurrencyKind.ForkJoin);
        var ms = TimeSpan.TicksPerMillisecond;
        // Two members non-overlapping:
        // A: start=0, dur=1ms; B: start=1ms, dur=1ms
        // total=2ms, max=1ms, savings=1ms
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("P", "Run", []),
                new Returned(null),
                [
                    MakeConcurrentNode(
                        "A", "Do", info,
                        1 * ms, 0),
                    MakeConcurrentNode(
                        "B", "Do", info,
                        1 * ms, 1 * ms),
                ],
                0),
        ]);

        var result = ProseRenderer.Render(tree);

        Assert.Contains("could save 1ms", result);
        Assert.Contains(
            "awaited sequentially", result);
    }

    [Fact]
    public void Sentence_starts_with_the_prefix()
    {
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Svc", "Run", []),
                new Returned(null), [], 0),
        ]);

        var result = ProseRenderer.Render(tree);
        var line = result.TrimEnd().Split('\n')[0];

        Assert.StartsWith("The ", line);
    }

    [Fact]
    public void No_params_omits_with_text()
    {
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Svc", "Run", []),
                new Returned(null), [], 0),
        ]);

        var result = ProseRenderer.Render(tree);

        Assert.DoesNotContain(" with ", result);
    }

    [Fact]
    public void Single_param_includes_with_text()
    {
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Svc", "Run", [
                    new ParameterCapture("x", "1", false),
                ]),
                new Returned(null), [], 0),
        ]);

        var result = ProseRenderer.Render(tree);

        Assert.Contains(" with x: `1`", result);
    }

    [Fact]
    public void Multiple_params_separated_by_comma()
    {
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Svc", "Run", [
                    new ParameterCapture("a", "1", false),
                    new ParameterCapture("b", "2", false),
                    new ParameterCapture("c", "3", false),
                ]),
                new Returned(null), [], 0),
        ]);

        var result = ProseRenderer.Render(tree);

        Assert.Contains(
            "a: `1`, b: `2`, c: `3`", result);
        // Verify no comma before first param
        Assert.Contains(
            " with a: `1`", result);
    }

    [Fact]
    public void Incomplete_outcome_shows_but_is_incomplete()
    {
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Svc", "Run", []),
                new Incomplete(), [], 0),
        ]);

        var result = ProseRenderer.Render(tree);

        Assert.Contains("but is incomplete", result);
    }

    [Fact]
    public void Returning_node_includes_its_narration()
    {
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("OrderService", "PlaceOrder", [],
                    "Placing the customer order"),
                new Returned("\"ok\""), [], 0),
        ]);

        var result = ProseRenderer.Render(tree);

        Assert.Contains("Placing the customer order", result);
    }

    [Fact]
    public void Throwing_node_omits_narration_in_favor_of_error_context()
    {
        // Java parity: narration is shown for successful nodes; a throwing
        // node surfaces its error context instead.
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Svc", "Charge", [],
                    "some narration", "the error context"),
                new Threw(new InvalidOperationException("boom")),
                [], 0),
        ]);

        var result = ProseRenderer.Render(tree);

        Assert.DoesNotContain("some narration", result);
        Assert.Contains("(the error context)", result);
    }

    [Fact]
    public void Throwing_node_includes_the_on_error_context()
    {
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("PaymentService", "Charge", [], null,
                    "Payment declined for C-1234"),
                new Threw(new InvalidOperationException("boom")),
                [], 0),
        ]);

        var result = ProseRenderer.Render(tree);

        Assert.Contains("(Payment declined for C-1234)", result);
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

        var result = ProseRenderer.Render(new TraceTree([a]));

        Assert.Contains(TreeWalk.CycleMarker, result, StringComparison.Ordinal);
    }

    // Mirrors java's ProseRendererTest: an exception message now reads
    // through ExceptionMessage, so it is redacted like any other
    // credential-shaped value and cannot forge a line via a raw newline.
    [Fact]
    public void An_exception_message_cannot_forge_a_line_in_the_prose_narrative()
    {
        var node = new TraceNode(
            new MethodSignature("PaymentService", "Charge", []),
            new Threw(new InvalidOperationException(
                "declined\nThe audit service approved payment.")),
            [], 0);

        var rendered = ProseRenderer.Render(new TraceTree([node]));

        Assert.Single(rendered.Split('\n', StringSplitOptions.RemoveEmptyEntries));
        Assert.Contains(
            "declined\\nThe audit service approved payment.", rendered, StringComparison.Ordinal);
    }

    [Fact]
    public void A_credential_shaped_exception_message_is_redacted_in_the_prose_narrative()
    {
        const string jwt =
            "eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiJhZGEifQ.c2lnbmF0dXJl";
        var node = new TraceNode(
            new MethodSignature("TokenService", "Issue", []),
            new Threw(new InvalidOperationException(jwt)),
            [], 0);

        var rendered = ProseRenderer.Render(new TraceTree([node]));

        Assert.Contains(RedactionPolicy.Marker, rendered, StringComparison.Ordinal);
        Assert.DoesNotContain("eyJhbGciOiJIUzI1NiJ9", rendered, StringComparison.Ordinal);
    }

    private static TraceNode MakeConcurrentNode(
        string cls, string method,
        ConcurrencyInfo info,
        long durationTicks = 100,
        long startTimestamp = 0)
    {
        return new TraceNode(
            new MethodSignature(cls, method, []),
            new Returned(null), [], durationTicks,
            startTimestamp, info);
    }
}
