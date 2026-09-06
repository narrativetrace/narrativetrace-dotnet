// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using Xunit;

namespace NarrativeTrace.Core.Tests;

public class IndentedTextRendererTests
{
    [Fact]
    public void Single_method_with_box_drawing()
    {
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature(
                    "OrderService", "PlaceOrder",
                    [new ParameterCapture("id", "\"42\"", false)]),
                new Returned("\"ok\""),
                [],
                0),
        ]);

        var result = IndentedTextRenderer.Render(tree);

        Assert.Contains(
            "OrderService.PlaceOrder(id: \"42\")",
            result);
    }

    [Fact]
    public void Indented_renders_exception_outcome_with_context_and_duration()
    {
        var ticks = TimeSpan.FromMilliseconds(40).Ticks;
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature(
                    "Svc", "Fail", [], ErrorContext: "while saving"),
                new Threw(new InvalidOperationException("boom")),
                [], ticks),
        ]);

        var result = IndentedTextRenderer.Render(tree);

        Assert.Contains(
            "!! InvalidOperationException: boom | while saving",
            result);
        Assert.Contains("— 40ms", result);
    }

    [Fact]
    public void Indented_renders_return_value_inline()
    {
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Svc", "Get", []),
                new Returned("\"ok\""), [], 0),
        ]);

        var result = IndentedTextRenderer.Render(tree);

        Assert.Contains("→ \"ok\"", result);
    }

    [Fact]
    public void Indented_renders_narration_as_comment_line()
    {
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature(
                    "Svc", "Run", [], Narration: "checking stock"),
                new Returned(null), [], 0),
        ]);

        var result = IndentedTextRenderer.Render(tree);

        Assert.Contains("// checking stock", result);
    }

    [Fact]
    public void Indented_renders_incomplete_as_in_flight()
    {
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Svc", "Hang", []),
                new Incomplete(), [], 0),
        ]);

        var result = IndentedTextRenderer.Render(tree);

        Assert.Contains("⏳ in-flight", result);
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

        var result = IndentedTextRenderer.Render(tree);

        Assert.Contains("password: [REDACTED]", result);
        Assert.DoesNotContain("hunter2", result);
    }

    [Fact]
    public void Nested_tree_with_correct_connectors()
    {
        var child = new TraceNode(
            new MethodSignature("Repo", "Save", []),
            new Returned(null), [], 0);
        var root = new TraceNode(
            new MethodSignature("Service", "Run", []),
            new Returned(null), [child], 0);
        var tree = new TraceTree([root]);

        var result = IndentedTextRenderer.Render(tree);
        var lines = result.TrimEnd().Split('\n');

        Assert.Equal(2, lines.Length);
        Assert.StartsWith("\u2514\u2500\u2500 Service.Run()", lines[0]);
        Assert.Contains("\u2514\u2500\u2500 Repo.Save()", lines[1]);
    }

    [Fact]
    public void Last_child_uses_corner_connector()
    {
        var child = new TraceNode(
            new MethodSignature("Repo", "Save", []),
            new Returned(null), [], 0);
        var root = new TraceNode(
            new MethodSignature("Service", "Run", []),
            new Returned(null), [child], 0);
        var tree = new TraceTree([root]);

        var result = IndentedTextRenderer.Render(tree);

        Assert.Contains("\u2514\u2500\u2500 Repo.Save()", result);
    }

    [Fact]
    public void Multiple_parameters_separated_by_commas()
    {
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Svc", "Add", [
                    new ParameterCapture("a", "1", false),
                    new ParameterCapture("b", "2", false),
                ]),
                new Returned(null), [], 0),
        ]);

        var result = IndentedTextRenderer.Render(tree);

        Assert.Contains("Svc.Add(a: 1, b: 2)", result);
    }

    [Fact]
    public void Non_last_child_uses_tee_connector()
    {
        var child1 = new TraceNode(
            new MethodSignature("Repo", "Load", []),
            new Returned(null), [], 0);
        var child2 = new TraceNode(
            new MethodSignature("Repo", "Save", []),
            new Returned(null), [], 0);
        var root = new TraceNode(
            new MethodSignature("Service", "Run", []),
            new Returned(null), [child1, child2], 0);
        var tree = new TraceTree([root]);

        var result = IndentedTextRenderer.Render(tree);

        Assert.Contains("\u251c\u2500\u2500 Repo.Load()", result);
        Assert.Contains("\u2514\u2500\u2500 Repo.Save()", result);
    }

    [Fact]
    public void IndentedText_renders_fork_join_markers()
    {
        var info = new ConcurrencyInfo(
            "fork-1", "A.Do", 1, null, true,
            ConcurrencyKind.ForkJoin);
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("P", "Run", []),
                new Returned(null),
                [
                    MakeConcurrentNode("A", "Do", info),
                    MakeConcurrentNode("B", "Do", info),
                ],
                0),
        ]);

        var result = IndentedTextRenderer.Render(tree);

        Assert.Contains("\u2442 fork", result);
        Assert.Contains("\u2443 join", result);
    }

    [Fact]
    public void IndentedText_renders_members_with_arrow_prefix()
    {
        var info = new ConcurrencyInfo(
            "fork-1", "A.Do", 1, null, true,
            ConcurrencyKind.ForkJoin);
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("P", "Run", []),
                new Returned(null),
                [MakeConcurrentNode("A", "Do", info)],
                0),
        ]);

        var result = IndentedTextRenderer.Render(tree);

        Assert.Contains("\u21a6 A.Do()", result);
    }

    [Fact]
    public void IndentedText_sorts_members_deterministically()
    {
        var info = new ConcurrencyInfo(
            "fork-1", "A.Do", 1, null, true,
            ConcurrencyKind.ForkJoin);
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("P", "Run", []),
                new Returned(null),
                [
                    MakeConcurrentNode("Z", "Do", info),
                    MakeConcurrentNode("A", "Do", info),
                ],
                0),
        ]);

        var result = IndentedTextRenderer.Render(tree);
        var aPos = result.IndexOf("A.Do", StringComparison.Ordinal);
        var zPos = result.IndexOf("Z.Do", StringComparison.Ordinal);

        Assert.True(aPos < zPos);
    }

    [Fact]
    public void IndentedText_renders_fire_and_forget_markers()
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

        var result = IndentedTextRenderer.Render(tree);

        Assert.Contains(
            "\u2933 fire-and-forget", result);
    }

    [Fact]
    public void IndentedText_shows_sequential_async_annotation()
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

        var result =
            IndentedTextRenderer.Render(tree);

        Assert.Contains(
            "awaited sequentially", result);
    }

    [Fact]
    public void Non_last_child_uses_tee_and_vertical_indent()
    {
        var grandchild = new TraceNode(
            new MethodSignature("Db", "Query", []),
            new Returned(null), [], 0);
        var child1 = new TraceNode(
            new MethodSignature("Repo", "Load", []),
            new Returned(null), [grandchild], 0);
        var child2 = new TraceNode(
            new MethodSignature("Repo", "Save", []),
            new Returned(null), [], 0);
        var root = new TraceNode(
            new MethodSignature("Svc", "Run", []),
            new Returned(null), [child1, child2], 0);
        var tree = new TraceTree([root]);

        var result = IndentedTextRenderer.Render(tree);

        Assert.Contains(
            "\u251c\u2500\u2500 Repo.Load()", result);
        Assert.Contains(
            "\u2502   ", result);
    }

    [Fact]
    public void Last_child_uses_corner_and_space_indent()
    {
        var grandchild = new TraceNode(
            new MethodSignature("Db", "Query", []),
            new Returned(null), [], 0);
        var child = new TraceNode(
            new MethodSignature("Repo", "Save", []),
            new Returned(null), [grandchild], 0);
        var root = new TraceNode(
            new MethodSignature("Svc", "Run", []),
            new Returned(null), [child], 0);
        var tree = new TraceTree([root]);

        var result = IndentedTextRenderer.Render(tree);

        // Last child uses └── connector
        Assert.Contains(
            "\u2514\u2500\u2500 Repo.Save()", result);
        // Grandchild under a last-child uses space indent
        Assert.Contains(
            "    \u2514\u2500\u2500 Db.Query()", result);
    }

    [Fact]
    public void Mixed_sequential_and_concurrent_render_in_order()
    {
        var info = new ConcurrencyInfo(
            "fork-1", "B.Do", 1, null, true,
            ConcurrencyKind.ForkJoin);
        var seqNode = new TraceNode(
            new MethodSignature("A", "First", []),
            new Returned(null), [], 0);
        var concNode = MakeConcurrentNode(
            "B", "Do", info);
        var seqNode2 = new TraceNode(
            new MethodSignature("C", "Last", []),
            new Returned(null), [], 0);
        var root = new TraceNode(
            new MethodSignature("P", "Run", []),
            new Returned(null),
            [seqNode, concNode, seqNode2], 0);
        var tree = new TraceTree([root]);

        var result = IndentedTextRenderer.Render(tree);

        var firstPos = result.IndexOf(
            "A.First", StringComparison.Ordinal);
        var forkPos = result.IndexOf(
            "\u2442 fork", StringComparison.Ordinal);
        var lastPos = result.IndexOf(
            "C.Last", StringComparison.Ordinal);

        Assert.True(firstPos >= 0);
        Assert.True(forkPos >= 0);
        Assert.True(lastPos >= 0);
        Assert.True(firstPos < forkPos);
        Assert.True(forkPos < lastPos);
    }

    [Fact]
    public void Fire_and_forget_exact_string()
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

        var result = IndentedTextRenderer.Render(tree);

        Assert.Contains(
            "\u2514\u2500\u2500 \u2933 fire-and-forget",
            result);
    }

    [Fact]
    public void Indented_join_marker_shows_wall_time()
    {
        var info = new ConcurrencyInfo(
            "fork-1", "A.Do", 1, null, true,
            ConcurrencyKind.ForkJoin);
        var ms = TimeSpan.TicksPerMillisecond;
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("P", "Run", []),
                new Returned(null),
                [MakeConcurrentNode("A", "Do", info, 500 * ms)],
                0),
        ]);

        var result = IndentedTextRenderer.Render(tree);

        Assert.Contains("⑃ join — 500ms", result);
    }

    [Fact]
    public void Fire_and_forget_launcher_without_body_shows_placeholder_and_thread()
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

        var result = IndentedTextRenderer.Render(tree);

        Assert.Contains(
            "⤳ fire-and-forget [thread: 7]", result);
        Assert.Contains(
            "[launched, result not captured]", result);
    }

    [Fact]
    public void Fork_header_exact_format_with_task_count()
    {
        var info = new ConcurrencyInfo(
            "fork-1", "A.Do", 1, null, true,
            ConcurrencyKind.ForkJoin);
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("P", "Run", []),
                new Returned(null),
                [
                    MakeConcurrentNode("A", "Do", info),
                    MakeConcurrentNode("B", "Do", info),
                    MakeConcurrentNode("C", "Do", info),
                ],
                0),
        ]);

        var result = IndentedTextRenderer.Render(tree);

        Assert.Contains(
            "\u251c\u2500\u2500 \u2442 fork [3 tasks]",
            result);
    }

    [Fact]
    public void Fork_members_use_vertical_line_indent()
    {
        var info = new ConcurrencyInfo(
            "fork-1", "A.Do", 1, null, true,
            ConcurrencyKind.ForkJoin);
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("P", "Run", []),
                new Returned(null),
                [MakeConcurrentNode("A", "Do", info)],
                0),
        ]);

        var result = IndentedTextRenderer.Render(tree);
        var lines = result.Split('\n');

        var memberLine = Array.Find(
            lines, l => l.Contains("\u21a6 A.Do"));
        Assert.NotNull(memberLine);
        Assert.Contains(
            "\u2502   \u21a6 A.Do()", memberLine);
    }

    [Fact]
    public void Join_line_exact_string()
    {
        var info = new ConcurrencyInfo(
            "fork-1", "A.Do", 1, null, true,
            ConcurrencyKind.ForkJoin);
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("P", "Run", []),
                new Returned(null),
                [MakeConcurrentNode("A", "Do", info)],
                0),
        ]);

        var result = IndentedTextRenderer.Render(tree);

        Assert.Contains(
            "\u2514\u2500\u2500 \u2443 join", result);
    }

    [Fact]
    public void Fork_member_format_with_arrow()
    {
        var info = new ConcurrencyInfo(
            "fork-1", "A.Do", 1, null, true,
            ConcurrencyKind.ForkJoin);
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("P", "Run", []),
                new Returned(null),
                [MakeConcurrentNode(
                    "MyClass", "Execute", info)],
                0),
        ]);

        var result = IndentedTextRenderer.Render(tree);

        Assert.Contains(
            "\u21a6 MyClass.Execute()", result);
    }

    [Fact]
    public void Parallel_fork_has_no_sequential_warning()
    {
        var info = new ConcurrencyInfo(
            "fork-1", "A.Do", 1, null, true,
            ConcurrencyKind.ForkJoin);
        var ms = TimeSpan.TicksPerMillisecond;
        // Overlapping: both start at 0
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("P", "Run", []),
                new Returned(null),
                [
                    MakeConcurrentNode(
                        "A", "Do", info, 100 * ms, 0),
                    MakeConcurrentNode(
                        "B", "Do", info, 100 * ms, 0),
                ],
                0),
        ]);

        var result = IndentedTextRenderer.Render(tree);

        Assert.DoesNotContain("awaited", result);
        Assert.DoesNotContain("could save", result);
    }

    [Fact]
    public void Sequential_with_zero_savings_shows_awaited_but_no_could_save()
    {
        var info = new ConcurrencyInfo(
            "fork-1", "A.Do", 1, null, true,
            ConcurrencyKind.ForkJoin);
        var ms = TimeSpan.TicksPerMillisecond;
        // A: 100ms at 0, B: 0ms at 100ms → total=100, max=100, savings=0
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
                        0, 100 * ms),
                ],
                0),
        ]);

        var result = IndentedTextRenderer.Render(tree);

        Assert.Contains("awaited sequentially", result);
        Assert.DoesNotContain("could save", result);
    }

    [Fact]
    public void Sequential_with_positive_savings_shows_could_save()
    {
        var info = new ConcurrencyInfo(
            "fork-1", "A.Do", 1, null, true,
            ConcurrencyKind.ForkJoin);
        var ms = TimeSpan.TicksPerMillisecond;
        // A: 100ms at 0, B: 200ms at 100ms
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
                        200 * ms, 100 * ms),
                ],
                0),
        ]);

        var result = IndentedTextRenderer.Render(tree);

        Assert.Contains(
            "could save 100ms with Task.WhenAll",
            result);
    }

    [Fact]
    public void Same_class_different_methods_sorted_by_method()
    {
        var info = new ConcurrencyInfo(
            "fork-1", "A.Do", 1, null, true,
            ConcurrencyKind.ForkJoin);
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("P", "Run", []),
                new Returned(null),
                [
                    MakeConcurrentNode(
                        "Svc", "Zebra", info),
                    MakeConcurrentNode(
                        "Svc", "Alpha", info),
                ],
                0),
        ]);

        var result = IndentedTextRenderer.Render(tree);
        var alphaPos = result.IndexOf(
            "Svc.Alpha", StringComparison.Ordinal);
        var zebraPos = result.IndexOf(
            "Svc.Zebra", StringComparison.Ordinal);

        Assert.True(alphaPos < zebraPos);
    }

    [Fact]
    public void First_parameter_has_no_leading_comma()
    {
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Svc", "Run", [
                    new ParameterCapture("x", "1", false),
                    new ParameterCapture("y", "2", false),
                ]),
                new Returned(null), [], 0),
        ]);

        var result = IndentedTextRenderer.Render(tree);

        Assert.Contains("Svc.Run(x: 1, y: 2)", result);
        Assert.DoesNotContain("(, ", result);
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

        var result = IndentedTextRenderer.Render(new TraceTree([a]));

        Assert.Contains(TreeWalk.CycleMarker, result, StringComparison.Ordinal);
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
