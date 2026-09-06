// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using Xunit;

namespace NarrativeTrace.Core.Tests;

public class MarkdownRendererTests
{
    [Fact]
    public void Single_method_produces_bullet_with_class_and_method()
    {
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("OrderService", "PlaceOrder", []),
                new Returned(null), [], 0),
        ]);

        var result = MarkdownRenderer.Render(tree);

        Assert.Contains(
            "- **OrderService.PlaceOrder**()",
            result);
    }

    [Fact]
    public void Parameters_render_inline_with_backticks()
    {
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Svc", "Run",
                    [new ParameterCapture("id", "42", false)]),
                new Returned(null), [], 0),
        ]);

        var result = MarkdownRenderer.Render(tree);

        Assert.Contains("id: `42`", result);
    }

    [Fact]
    public void Parameter_value_with_backtick_widens_the_fence()
    {
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Svc", "Run",
                    [new ParameterCapture("q", "a`b", false)]),
                new Returned(null), [], 0),
        ]);

        var result = MarkdownRenderer.Render(tree);

        Assert.Contains("q: `` a`b ``", result);
    }

    [Fact]
    public void Return_value_shown_with_arrow()
    {
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Svc", "Get", []),
                new Returned("\"ok\""), [], 0),
        ]);

        var result = MarkdownRenderer.Render(tree);

        Assert.Contains("\u2192 `\"ok\"`", result);
    }

    [Fact]
    public void Return_value_with_backtick_widens_the_fence()
    {
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Svc", "Get", []),
                new Returned("a`b"), [], 0),
        ]);

        var result = MarkdownRenderer.Render(tree);

        Assert.Contains("\u2192 `` a`b ``", result);
    }

    [Fact]
    public void Error_shown_with_cross_mark()
    {
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Svc", "Fail", []),
                new Threw(new InvalidOperationException("boom")),
                [], 0),
        ]);

        var result = MarkdownRenderer.Render(tree);

        Assert.Contains(
            "\u274c InvalidOperationException: boom",
            result);
    }

    [Fact]
    public void Error_shows_the_on_error_context_when_present()
    {
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Svc", "Charge", [], null,
                    "Payment declined for customer C-1234"),
                new Threw(new InvalidOperationException("boom")),
                [], 0),
        ]);

        var result = MarkdownRenderer.Render(tree);

        Assert.Contains(
            "Payment declined for customer C-1234",
            result);
    }

    [Fact]
    public void On_error_context_is_not_shown_for_a_successful_return()
    {
        // [OnError] context is resolved on entry and stored regardless of
        // outcome; it must surface only when the method actually throws.
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Svc", "Charge", [], null,
                    "would-be error context"),
                new Returned("\"ok\""),
                [], 0),
        ]);

        var result = MarkdownRenderer.Render(tree);

        Assert.DoesNotContain("would-be error context", result);
    }

    [Fact]
    public void Narration_html_is_escaped()
    {
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Svc", "Run", [],
                    Narration: "notes <b>bold</b>"),
                new Returned(null), [], 0),
        ]);

        var result = MarkdownRenderer.Render(tree);

        Assert.Contains("notes &lt;b&gt;bold&lt;/b&gt;", result);
        Assert.DoesNotContain("<b>bold</b>", result);
    }

    [Fact]
    public void Exception_message_html_is_escaped()
    {
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Svc", "Fail", []),
                new Threw(new InvalidOperationException(
                    "<img onerror=x>")),
                [], 0),
        ]);

        var result = MarkdownRenderer.Render(tree);

        Assert.Contains("&lt;img onerror=x&gt;", result);
        Assert.DoesNotContain("<img onerror=x>", result);
    }

    [Fact]
    public void Nested_calls_indent_correctly()
    {
        var child = new TraceNode(
            new MethodSignature("Repo", "Save", []),
            new Returned(null), [], 0);
        var root = new TraceNode(
            new MethodSignature("Svc", "Run", []),
            new Returned(null), [child], 0);
        var tree = new TraceTree([root]);

        var result = MarkdownRenderer.Render(tree);

        Assert.Contains("- **Svc.Run**()", result);
        Assert.Contains("  - **Repo.Save**()", result);
    }

    [Fact]
    public void Parent_return_renders_inline_without_a_closing_repeat()
    {
        var child = new TraceNode(
            new MethodSignature("Repo", "Save", []),
            new Returned(null), [], 0);
        var root = new TraceNode(
            new MethodSignature("Svc", "Run", []),
            new Returned("\"result\""), [child], 0);
        var tree = new TraceTree([root]);

        var result = MarkdownRenderer.Render(tree);

        // A normal return reads on the entry line; repeating it after the
        // child block adds a line without adding information.
        Assert.Contains("- **Svc.Run**() → `\"result\"`", result);
        Assert.DoesNotContain("  - → ", result);
    }

    [Fact]
    public void Parent_incomplete_renders_closing_in_flight_bullet()
    {
        var child = new TraceNode(
            new MethodSignature("Repo", "Save", []),
            new Returned(null), [], 0);
        var root = new TraceNode(
            new MethodSignature("Svc", "Run", []),
            new Incomplete(), [child], 0);
        var tree = new TraceTree([root]);

        var result = MarkdownRenderer.Render(tree);

        Assert.Contains("  - ⏳ in-flight", result);
    }

    [Fact]
    public void Slow_call_shows_warning()
    {
        var slowTicks = TimeSpan.FromMilliseconds(300).Ticks;
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Svc", "Slow", []),
                new Returned(null), [], slowTicks),
        ]);

        var result = MarkdownRenderer.Render(tree);

        Assert.Contains("\u26a0\ufe0f", result);
        Assert.Contains("300ms", result);
    }

    [Fact]
    public void Fast_node_still_shows_duration_dash_ms()
    {
        var ticks = TimeSpan.FromMilliseconds(50).Ticks;
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Svc", "Quick", []),
                new Returned(null), [], ticks),
        ]);

        var result = MarkdownRenderer.Render(tree);

        Assert.Contains("— 50ms", result);
        Assert.DoesNotContain("⚠️", result);
    }

    [Fact]
    public void Node_exactly_at_threshold_is_not_flagged_slow()
    {
        var ticks = TimeSpan.FromMilliseconds(200).Ticks;
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Svc", "Edge", []),
                new Returned(null), [], ticks),
        ]);

        var result = MarkdownRenderer.Render(
            tree, new MarkdownOptions(SlowThresholdMs: 200));

        Assert.Contains("— 200ms", result);
        Assert.DoesNotContain("slow", result);
    }

    [Fact]
    public void Slow_node_marker_reads_dash_ms_warning_slow()
    {
        var ticks = TimeSpan.FromMilliseconds(300).Ticks;
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Svc", "Slow", []),
                new Returned(null), [], ticks),
        ]);

        var result = MarkdownRenderer.Render(tree);

        Assert.Contains("— 300ms ⚠️ slow", result);
    }

    [Fact]
    public void Frontmatter_includes_scenario_name()
    {
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Svc", "Run", []),
                new Returned(null), [], 0),
        ]);

        var result = MarkdownRenderer.Render(
            tree, new MarkdownOptions(ScenarioName: "Login flow"));

        Assert.Contains("scenario: Login flow", result);
    }

    [Fact]
    public void Frontmatter_includes_trace_id_and_name_from_the_root_span()
    {
        var tid = new TraceId("0af7651916cd43dd8448eb211c80319c");
        var sc = new SpanContext(tid, new SpanId("0000000000000001"), null);
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Svc", "Run", []),
                new Returned(null), [], 0, SpanContext: sc),
        ]);

        var result = MarkdownRenderer.Render(tree);

        Assert.Contains(
            "trace_id: 0af7651916cd43dd8448eb211c80319c", result);
        Assert.Contains($"trace_name: {tid.HumanName}", result);
    }

    [Fact]
    public void Frontmatter_omits_trace_id_when_the_trace_has_no_span()
    {
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Svc", "Run", []),
                new Returned(null), [], 0),
        ]);

        var result = MarkdownRenderer.Render(tree);

        Assert.DoesNotContain("trace_id:", result);
    }

    [Fact]
    public void Frontmatter_reports_error_count_and_entry_point()
    {
        var child = new TraceNode(
            new MethodSignature("Repo", "Save", []),
            new Threw(new InvalidOperationException("boom")), [], 0);
        var root = new TraceNode(
            new MethodSignature("OrderService", "PlaceOrder", []),
            new Returned(null), [child], 0);
        var tree = new TraceTree([root]);

        var result = MarkdownRenderer.Render(tree);

        Assert.Contains("error_count: 1", result);
        Assert.Contains("entry_point: OrderService.PlaceOrder", result);
    }

    [Fact]
    public void Frontmatter_includes_method_count()
    {
        var child = new TraceNode(
            new MethodSignature("Repo", "Save", []),
            new Returned(null), [], 0);
        var root = new TraceNode(
            new MethodSignature("Svc", "Run", []),
            new Returned(null), [child], 0);
        var tree = new TraceTree([root]);

        var result = MarkdownRenderer.Render(tree);

        Assert.Contains("method_count: 2", result);
    }

    [Fact]
    public void Frontmatter_declares_type_trace()
    {
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Svc", "Run", []),
                new Returned(null), [], 0),
        ]);

        var result = MarkdownRenderer.Render(tree);

        Assert.Contains("type: trace", result);
    }

    [Fact]
    public void Narration_in_italics()
    {
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature(
                    "Svc", "Run", [],
                    Narration: "Placing order for Alice"),
                new Returned(null), [], 0),
        ]);

        var result = MarkdownRenderer.Render(tree);

        Assert.Contains("*Placing order for Alice*", result);
    }

    [Fact]
    public void Empty_tree_produces_minimal_output()
    {
        var tree = new TraceTree([]);

        var result = MarkdownRenderer.Render(tree);

        Assert.Contains("method_count: 0", result);
        Assert.Contains("result: success", result);
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

        var result = MarkdownRenderer.Render(tree);

        Assert.Contains("a: `1`, b: `2`", result);
    }

    [Fact]
    public void Scenario_containing_colon_is_yaml_quoted()
    {
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Svc", "Run", []),
                new Returned(null), [], 0),
        ]);
        var options = new MarkdownOptions(
            ScenarioName: "deploy: production");

        var result = MarkdownRenderer.Render(tree, options);

        Assert.Contains(
            "scenario: \"deploy: production\"", result);
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

        var result = MarkdownRenderer.Render(tree);

        Assert.Contains("password: `[REDACTED]`", result);
        Assert.DoesNotContain("hunter2", result);
    }

    [Fact]
    public void Markdown_renders_fork_marker_with_task_count()
    {
        var info = MakeForkInfo("fork-1");
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

        var result = MarkdownRenderer.Render(tree);

        Assert.Contains(
            "\u2442 fork [2 tasks]", result);
    }

    [Fact]
    public void Markdown_renders_join_marker_with_wall_time()
    {
        var info = MakeForkInfo("fork-1");
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("P", "Run", []),
                new Returned(null),
                [MakeConcurrentNode(
                    "A", "Do", info, 5_000_000)],
                0),
        ]);

        var result = MarkdownRenderer.Render(tree);

        Assert.Contains("\u2443 join", result);
    }

    [Fact]
    public void Markdown_renders_members_with_arrow_prefix()
    {
        var info = MakeForkInfo("fork-1");
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("P", "Run", []),
                new Returned(null),
                [MakeConcurrentNode("A", "Do", info)],
                0),
        ]);

        var result = MarkdownRenderer.Render(tree);

        Assert.Contains(
            "\u21a6 **A.Do**", result);
    }

    [Fact]
    public void Markdown_sorts_members_by_ClassName_MethodName()
    {
        var info = MakeForkInfo("fork-1");
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

        var result = MarkdownRenderer.Render(tree);
        var aPos = result.IndexOf("A.Do", StringComparison.Ordinal);
        var zPos = result.IndexOf("Z.Do", StringComparison.Ordinal);

        Assert.True(aPos < zPos);
    }

    [Fact]
    public void Markdown_renders_fire_and_forget_section()
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

        var result = MarkdownRenderer.Render(tree);

        Assert.Contains(
            "\u2933 fire-and-forget", result);
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

        var result = MarkdownRenderer.Render(tree);

        Assert.Contains(
            "⤳ fire-and-forget [thread: 7]", result);
        Assert.Contains(
            "[launched, result not captured]", result);
        Assert.DoesNotContain("Bg.fire-and-forget", result);
    }

    [Fact]
    public void Markdown_renders_thread_id_for_concurrent_members()
    {
        var info = new ConcurrencyInfo(
            "fork-1", "A.Do", 42, null, true,
            ConcurrencyKind.ForkJoin);
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("P", "Run", []),
                new Returned(null),
                [MakeConcurrentNode("A", "Do", info)],
                0),
        ]);

        var result = MarkdownRenderer.Render(tree);

        Assert.Contains("[thread: 42]", result);
    }

    [Fact]
    public void Markdown_shows_awaited_sequentially_annotation()
    {
        var info = MakeForkInfo("fork-1");
        var ms = TimeSpan.TicksPerMillisecond;
        // Non-overlapping: A runs 0–100ms, B runs 100–200ms
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

        var result = MarkdownRenderer.Render(tree);

        Assert.Contains(
            "awaited sequentially", result);
    }

    [Fact]
    public void Markdown_shows_sequential_async_optimization_hint()
    {
        var info = MakeForkInfo("fork-1");
        var ms = TimeSpan.TicksPerMillisecond;
        // A: 100ms, B: 200ms → total 300ms, parallel 200ms
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

        var result = MarkdownRenderer.Render(tree);

        Assert.Contains(
            "could save 100ms with Task.WhenAll",
            result);
    }

    private static ConcurrencyInfo MakeForkInfo(
        string groupId)
    {
        return new ConcurrencyInfo(
            groupId, "A.Do", 1, null, true,
            ConcurrencyKind.ForkJoin);
    }

    [Fact]
    public void Explicit_options_overrides_default_threshold()
    {
        var ticks = TimeSpan.FromMilliseconds(150).Ticks;
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Svc", "Run", []),
                new Returned(null), [], ticks),
        ]);

        var withCustom = MarkdownRenderer.Render(
            tree, new MarkdownOptions(SlowThresholdMs: 100));
        var withDefault = MarkdownRenderer.Render(tree);

        Assert.Contains("\u26a0\ufe0f", withCustom);
        Assert.DoesNotContain("\u26a0\ufe0f", withDefault);
    }

    [Fact]
    public void Document_header_renders_trace_scenario_duration_and_call_flow()
    {
        var ticks = TimeSpan.FromMilliseconds(150).Ticks;
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Svc", "Run", []),
                new Returned(null), [], ticks),
        ]);

        var result = MarkdownRenderer.RenderDocument(
            tree,
            new TraceMetadata("Place order", ScenarioResult.Success));

        Assert.Contains("## Trace: Svc.Run", result);
        Assert.Contains("**Scenario:** Place order", result);
        Assert.Contains(
            "**Duration:** 150ms | **Result:** PASSED", result);
        Assert.Contains("### Call Flow", result);
        // Header sits between the frontmatter fence and the node bullets.
        var headerIdx = result.IndexOf(
            "## Trace:", StringComparison.Ordinal);
        var bulletIdx = result.IndexOf(
            "- **Svc.Run**", StringComparison.Ordinal);
        Assert.True(
            headerIdx > 0 && headerIdx < bulletIdx,
            "document header must precede the node bullets");
    }

    [Fact]
    public void Document_header_reports_the_supplied_result_not_the_tree()
    {
        // Java renders metadata.result().displayName(). The wire spelling
        // (success/error) must never reach human-facing prose, and the verdict
        // is the producer's — not "did anything throw".
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Svc", "Fail", []),
                new Threw(new InvalidOperationException("x")), [], 0),
        ]);

        var failed = MarkdownRenderer.RenderDocument(
            tree, new TraceMetadata("s", ScenarioResult.Error));
        Assert.Contains("**Result:** FAILED", failed);

        // Same throwing tree, but the producer says it passed.
        var passed = MarkdownRenderer.RenderDocument(
            tree, new TraceMetadata("s", ScenarioResult.Success));
        Assert.Contains("**Result:** PASSED", passed);
    }

    [Fact]
    public void Body_only_render_omits_frontmatter_fence()
    {
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Svc", "Run", []),
                new Returned(null), [], 0),
        ]);

        var result = MarkdownRenderer.Render(
            tree, new MarkdownOptions(IncludeFrontmatter: false));

        Assert.DoesNotContain("---", result);
        Assert.Contains("- **Svc.Run**()", result);
    }

    [Fact]
    public void Frontmatter_starts_with_triple_dash()
    {
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Svc", "Run", []),
                new Returned(null), [], 0),
        ]);

        var result = MarkdownRenderer.Render(tree);

        Assert.StartsWith("---", result);
    }

    [Fact]
    public void Tree_with_threw_produces_result_error()
    {
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Svc", "Fail", []),
                new Threw(new InvalidOperationException("x")),
                [], 0),
        ]);

        var result = MarkdownRenderer.Render(tree);

        Assert.Contains("result: error", result);
    }

    [Fact]
    public void Tree_without_threw_produces_result_success()
    {
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Svc", "Run", []),
                new Returned(null), [], 0),
        ]);

        var result = MarkdownRenderer.Render(tree);

        Assert.Contains("result: success", result);
    }

    [Fact]
    public void Each_node_on_separate_line()
    {
        var child = new TraceNode(
            new MethodSignature("Repo", "Save", []),
            new Returned(null), [], 0);
        var root = new TraceNode(
            new MethodSignature("Svc", "Run", []),
            new Returned(null), [child], 0);
        var tree = new TraceTree([root]);

        var result = MarkdownRenderer.Render(tree);
        var lines = result.Split('\n');

        var runLine = Array.FindIndex(
            lines, l => l.Contains("Svc.Run"));
        var saveLine = Array.FindIndex(
            lines, l => l.Contains("Repo.Save"));

        Assert.True(runLine >= 0);
        Assert.True(saveLine >= 0);
        Assert.NotEqual(runLine, saveLine);
    }

    [Fact]
    public void Depth_two_has_four_spaces_indent()
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

        var result = MarkdownRenderer.Render(tree);

        Assert.Contains("    - **Db.Query**", result);
    }

    [Fact]
    public void Fork_marker_shows_exact_task_count()
    {
        var info = MakeForkInfo("fork-1");
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

        var result = MarkdownRenderer.Render(tree);

        Assert.Contains("[3 tasks]", result);
    }

    [Fact]
    public void Non_sequential_fork_has_no_awaited_text()
    {
        var info = MakeForkInfo("fork-1");
        var ms = TimeSpan.TicksPerMillisecond;
        // Overlapping: both start at 0 and run 100ms
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

        var result = MarkdownRenderer.Render(tree);

        Assert.DoesNotContain("awaited", result);
    }

    [Fact]
    public void Sequential_with_zero_savings_shows_awaited_but_no_could_save()
    {
        var info = MakeForkInfo("fork-1");
        var ms = TimeSpan.TicksPerMillisecond;
        // A: 100ms, B: 100ms, non-overlapping → total=200, max=100, savings=100
        // For savings=0 we need total=max, e.g., A: 100ms, B: 0ms
        // But we need 2 members and non-overlapping.
        // A runs 0–100ms (100ms), B runs 100–100ms (0ms) → total=100, max=100, savings=0
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

        var result = MarkdownRenderer.Render(tree);

        Assert.Contains("awaited sequentially", result);
        Assert.DoesNotContain("could save", result);
    }

    [Fact]
    public void Join_marker_with_zero_wall_time_has_no_ms_suffix()
    {
        var info = MakeForkInfo("fork-1");
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("P", "Run", []),
                new Returned(null),
                [MakeConcurrentNode("A", "Do", info, 0)],
                0),
        ]);

        var result = MarkdownRenderer.Render(tree);

        Assert.Contains("\u2443 join", result);
        // No wall-time suffix on the join marker (it renders "join \u2014 Nms").
        Assert.DoesNotContain("join \u2014", result);
    }

    [Fact]
    public void Join_marker_with_positive_wall_time_shows_ms()
    {
        var info = MakeForkInfo("fork-1");
        var ms = TimeSpan.TicksPerMillisecond;
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("P", "Run", []),
                new Returned(null),
                [MakeConcurrentNode(
                    "A", "Do", info, 500 * ms)],
                0),
        ]);

        var result = MarkdownRenderer.Render(tree);

        Assert.Contains(
            "\u2443 join \u2014 500ms", result);
    }

    [Fact]
    public void Join_marker_shows_wait_analysis_between_members()
    {
        var info = MakeForkInfo("fork-1");
        var ms = TimeSpan.TicksPerMillisecond;
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("P", "Run", []),
                new Returned(null),
                [
                    MakeConcurrentNode("Slow", "Do", info, 300 * ms),
                    MakeConcurrentNode("Fast", "Do", info, 100 * ms),
                ],
                0),
        ]);

        var result = MarkdownRenderer.Render(tree);

        Assert.Contains(
            "(waited 200ms for Slow after Fast)", result);
    }

    [Fact]
    public void Wait_analysis_finds_slowest_when_it_is_not_first()
    {
        var info = MakeForkInfo("fork-1");
        var ms = TimeSpan.TicksPerMillisecond;
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("P", "Run", []),
                new Returned(null),
                [
                    MakeConcurrentNode("Fast", "Do", info, 100 * ms),
                    MakeConcurrentNode("Slow", "Do", info, 300 * ms),
                ],
                0),
        ]);

        var result = MarkdownRenderer.Render(tree);

        Assert.Contains(
            "(waited 200ms for Slow after Fast)", result);
    }

    [Fact]
    public void Wait_analysis_omitted_when_members_have_equal_duration()
    {
        var info = MakeForkInfo("fork-1");
        var ms = TimeSpan.TicksPerMillisecond;
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("P", "Run", []),
                new Returned(null),
                [
                    MakeConcurrentNode("A", "Do", info, 200 * ms),
                    MakeConcurrentNode("B", "Do", info, 200 * ms),
                ],
                0),
        ]);

        var result = MarkdownRenderer.Render(tree);

        Assert.DoesNotContain("waited", result);
    }

    [Fact]
    public void Wall_time_uses_max_not_sum()
    {
        var info = MakeForkInfo("fork-1");
        var ms = TimeSpan.TicksPerMillisecond;
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("P", "Run", []),
                new Returned(null),
                [
                    MakeConcurrentNode(
                        "A", "Do", info, 300 * ms),
                    MakeConcurrentNode(
                        "B", "Do", info, 200 * ms),
                ],
                0),
        ]);

        var result = MarkdownRenderer.Render(tree);

        Assert.Contains("300ms", result);
        Assert.DoesNotContain("500ms", result);
    }

    [Fact]
    public void Same_class_different_methods_sorted_by_method()
    {
        var info = MakeForkInfo("fork-1");
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("P", "Run", []),
                new Returned(null),
                [
                    MakeConcurrentNode("Svc", "Zebra", info),
                    MakeConcurrentNode("Svc", "Alpha", info),
                ],
                0),
        ]);

        var result = MarkdownRenderer.Render(tree);
        var alphaPos = result.IndexOf(
            "Svc.Alpha", StringComparison.Ordinal);
        var zebraPos = result.IndexOf(
            "Svc.Zebra", StringComparison.Ordinal);

        Assert.True(alphaPos < zebraPos);
    }

    [Fact]
    public void Just_above_threshold_shows_slow_warning()
    {
        var ticks =
            TimeSpan.FromMilliseconds(201).Ticks;
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Svc", "Run", []),
                new Returned(null), [], ticks),
        ]);

        var result = MarkdownRenderer.Render(tree);

        Assert.Contains("\u26a0\ufe0f", result);
        Assert.Contains("201ms", result);
    }

    [Fact]
    public void Below_threshold_no_slow_warning()
    {
        var ticks =
            TimeSpan.FromMilliseconds(199).Ticks;
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Svc", "Run", []),
                new Returned(null), [], ticks),
        ]);

        var result = MarkdownRenderer.Render(tree);

        Assert.DoesNotContain("\u26a0\ufe0f", result);
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

        var result = MarkdownRenderer.Render(tree);

        Assert.Contains("(x: `1`, y: `2`)", result);
        Assert.DoesNotContain("(, ", result);
    }

    [Fact]
    public void Null_narration_produces_no_italic_line()
    {
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature(
                    "Svc", "Run", [],
                    Narration: null),
                new Returned(null), [], 0),
        ]);

        var result = MarkdownRenderer.Render(tree);
        var lines = result.Split('\n');

        Assert.DoesNotContain(
            lines, l => l.TrimStart().StartsWith('*'));
    }

    [Fact]
    public void Present_narration_produces_italic_line()
    {
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature(
                    "Svc", "Run", [],
                    Narration: "Doing work"),
                new Returned(null), [], 0),
        ]);

        var result = MarkdownRenderer.Render(tree);

        Assert.Contains("*Doing work*", result);
    }

    [Fact]
    public void Incomplete_outcome_shows_hourglass()
    {
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Svc", "Run", []),
                new Incomplete(), [], 0),
        ]);

        var result = MarkdownRenderer.Render(tree);

        Assert.Contains("\u23f3 incomplete", result);
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

        var result = MarkdownRenderer.Render(tree);

        Assert.Contains(
            "- \u2933 fire-and-forget", result);
    }

    [Fact]
    public void Fork_members_indented_deeper_than_fork()
    {
        var info = MakeForkInfo("fork-1");
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("P", "Run", []),
                new Returned(null),
                [MakeConcurrentNode("A", "Do", info)],
                0),
        ]);

        var result = MarkdownRenderer.Render(tree);
        var lines = result.Split('\n');

        var forkLine = Array.Find(
            lines, l => l.Contains("\u2442 fork"));
        var memberLine = Array.Find(
            lines, l => l.Contains("\u21a6"));

        Assert.NotNull(forkLine);
        Assert.NotNull(memberLine);

        var forkIndent = forkLine!.Length
            - forkLine.TrimStart().Length;
        var memberIndent = memberLine!.Length
            - memberLine.TrimStart().Length;

        Assert.True(memberIndent > forkIndent);
    }

    [Fact]
    public void Frontmatter_ends_with_triple_dash()
    {
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Svc", "Run", []),
                new Returned(null), [], 0),
        ]);

        var result = MarkdownRenderer.Render(tree);
        var lines = result.Split('\n');

        Assert.Equal("---", lines[0]);
        var secondDash = Array.FindIndex(
            lines, 1, l => l == "---");
        Assert.True(secondDash > 0);
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

        var result = MarkdownRenderer.Render(new TraceTree([a]));

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
