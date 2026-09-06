// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using NarrativeTrace.Runtime;
using Xunit;

namespace NarrativeTrace.Core.Tests;

public class SyncNarrativeContextTests
{
    [Fact]
    public void Fresh_context_has_empty_trace()
    {
        var config = new NarrativeTraceConfig();
        var ctx = new SyncNarrativeContext(config);

        var trace = ctx.CaptureTrace();

        Assert.True(trace.IsEmpty);
    }

    [Fact]
    public void Enter_and_exit_produces_single_node_trace()
    {
        var ctx = new SyncNarrativeContext(new NarrativeTraceConfig());

        ctx.EnterMethod("Svc", "Run", []);
        ctx.ExitMethodWithReturn("42");

        var trace = ctx.CaptureTrace();
        Assert.Single(trace.Roots);
        var node = trace.Roots[0];
        Assert.Equal("Svc", node.Signature.ClassName);
        Assert.Equal("Run", node.Signature.MethodName);
        Assert.Equal(new Returned("42"), node.Outcome);
    }

    [Fact]
    public void Exit_with_structured_return_preserves_it_on_the_node()
    {
        var ctx = new SyncNarrativeContext(new NarrativeTraceConfig());
        var structured = new RenderedValue.LongVal(42);

        ctx.EnterMethod("Svc", "Run", []);
        ctx.ExitMethodWithReturn("42", structured);

        var node = ctx.CaptureTrace().Roots[0];
        var returned = Assert.IsType<Returned>(node.Outcome);
        Assert.Equal("42", returned.RenderedValue);
        Assert.Same(structured, returned.StructuredValue);
    }

    [Fact]
    public void Events_are_fanned_out_to_the_real_time_sink()
    {
        using var sink =
            new BufferedEventConsumer(1024, startConsumer: false);
        var ctx = new SyncNarrativeContext(
            new NarrativeTraceConfig(), sink);

        ctx.EnterMethod("Svc", "Run", []);
        ctx.ExitMethodWithReturn("ok");
        sink.Flush();

        Assert.Equal(2, sink.Events().Count);
    }

    [Fact]
    public void Capture_still_works_when_a_sink_is_attached()
    {
        using var sink =
            new BufferedEventConsumer(1024, startConsumer: false);
        var ctx = new SyncNarrativeContext(
            new NarrativeTraceConfig(), sink);

        ctx.EnterMethod("Svc", "Run", []);
        ctx.ExitMethodWithReturn("ok");

        Assert.Single(ctx.CaptureTrace().Roots);
    }

    [Fact]
    public void Captured_span_context_carries_story_and_chapter_id()
    {
        var ctx = new SyncNarrativeContext(new NarrativeTraceConfig());

        ctx.EnterMethod("Svc", "Run", []);
        ctx.ExitMethodWithReturn(null);

        var sc = ctx.CaptureTrace().Roots[0].SpanContext;
        Assert.NotNull(sc);
        Assert.Equal(ctx.StoryId, sc!.StoryId);
        Assert.Equal(ctx.ChapterId, sc.ChapterId);
        Assert.NotNull(sc.StoryId);
    }

    [Fact]
    public void Nested_calls_produce_parent_child_tree()
    {
        var ctx = new SyncNarrativeContext(new NarrativeTraceConfig());

        ctx.EnterMethod("Outer", "Run", []);
        ctx.EnterMethod("Inner", "Do", []);
        ctx.ExitMethodWithReturn(null);
        ctx.ExitMethodWithReturn(null);

        var trace = ctx.CaptureTrace();
        Assert.Single(trace.Roots);
        var parent = trace.Roots[0];
        Assert.Equal("Outer", parent.Signature.ClassName);
        Assert.Single(parent.Children);
        Assert.Equal("Inner", parent.Children[0].Signature.ClassName);
    }

    [Fact]
    public void Exception_exit_produces_threw_outcome()
    {
        var ctx = new SyncNarrativeContext(new NarrativeTraceConfig());
        var ex = new InvalidOperationException("boom");

        ctx.EnterMethod("Svc", "Fail", []);
        ctx.ExitMethodWithException(ex);

        var node = ctx.CaptureTrace().Roots[0];
        var threw = Assert.IsType<Threw>(node.Outcome);
        Assert.Same(ex, threw.Error);
    }

    [Fact]
    public void Exception_exit_can_attach_error_context_to_the_node()
    {
        var ctx = new SyncNarrativeContext(new NarrativeTraceConfig());

        ctx.EnterMethod("Svc", "Fail", []);
        ctx.ExitMethodWithException(
            new InvalidOperationException("boom"),
            "Failed processing order-42");

        var node = ctx.CaptureTrace().Roots[0];
        Assert.Equal(
            "Failed processing order-42",
            node.Signature.ErrorContext);
    }

    [Fact]
    public void Exception_exit_error_context_overrides_enter_time_context()
    {
        var ctx = new SyncNarrativeContext(new NarrativeTraceConfig());

        ctx.EnterMethod("Svc", "Fail", [],
            new MethodOptions(ErrorContext: "stale enter-time context"));
        ctx.ExitMethodWithException(
            new InvalidOperationException("boom"),
            "resolved against thrown type");

        var node = ctx.CaptureTrace().Roots[0];
        Assert.Equal(
            "resolved against thrown type",
            node.Signature.ErrorContext);
    }

    [Fact]
    public void Duration_is_non_negative()
    {
        var ctx = new SyncNarrativeContext(new NarrativeTraceConfig());

        ctx.EnterMethod("Svc", "Run", []);
        ctx.ExitMethodWithReturn(null);

        Assert.True(ctx.CaptureTrace().Roots[0].DurationTicks >= 0);
    }

    [Fact]
    public void Reset_clears_all_state()
    {
        var ctx = new SyncNarrativeContext(new NarrativeTraceConfig());

        ctx.EnterMethod("Svc", "Run", []);
        ctx.ExitMethodWithReturn(null);
        ctx.Reset();

        Assert.True(ctx.CaptureTrace().IsEmpty);
    }

    [Fact]
    public void Narrative_level_suppresses_parameter_values_keeps_names()
    {
        var config = new NarrativeTraceConfig(TracingLevel.Narrative);
        var ctx = new SyncNarrativeContext(config);

        ctx.EnterMethod("Svc", "Run",
            [new ParameterCapture("orderId", "\"A1\"", false)]);
        ctx.ExitMethodWithReturn(null);

        var param = ctx.CaptureTrace().Roots[0].Signature.Parameters[0];
        Assert.Equal("orderId", param.Name);
        Assert.Equal("", param.RenderedValue);
    }

    [Fact]
    public void Detail_level_retains_parameter_values()
    {
        var ctx = new SyncNarrativeContext(
            new NarrativeTraceConfig(TracingLevel.Detail));

        ctx.EnterMethod("Svc", "Run",
            [new ParameterCapture("orderId", "\"A1\"", false)]);
        ctx.ExitMethodWithReturn(null);

        var param = ctx.CaptureTrace().Roots[0].Signature.Parameters[0];
        Assert.Equal("\"A1\"", param.RenderedValue);
    }

    [Theory]
    [InlineData(TracingLevel.Detail, true)]
    [InlineData(TracingLevel.Narrative, false)]
    [InlineData(TracingLevel.Summary, false)]
    public void CapturesParameterValues_is_true_only_at_detail(
        TracingLevel level, bool expected)
    {
        var ctx = new SyncNarrativeContext(
            new NarrativeTraceConfig(level));

        Assert.Equal(expected, ctx.CapturesParameterValues);
    }

    [Fact]
    public void Captured_root_node_carries_span_context_with_trace_id()
    {
        var ctx = new SyncNarrativeContext(new NarrativeTraceConfig());

        ctx.EnterMethod("Svc", "Run", []);
        ctx.ExitMethodWithReturn(null);

        var node = ctx.CaptureTrace().Roots[0];
        Assert.NotNull(node.SpanContext);
        Assert.False(node.SpanContext!.TraceId.IsEmpty);
        Assert.False(node.SpanContext.SpanId.IsEmpty);
        Assert.Null(node.SpanContext.ParentSpanId);
    }

    [Fact]
    public void Child_span_context_parent_id_matches_parent_span_id()
    {
        var ctx = new SyncNarrativeContext(new NarrativeTraceConfig());

        ctx.EnterMethod("Outer", "Run", []);
        ctx.EnterMethod("Inner", "Do", []);
        ctx.ExitMethodWithReturn(null);
        ctx.ExitMethodWithReturn(null);

        var parent = ctx.CaptureTrace().Roots[0];
        var child = parent.Children[0];
        Assert.Equal(
            parent.SpanContext!.SpanId,
            child.SpanContext!.ParentSpanId);
    }

    [Fact]
    public void All_nodes_in_one_capture_share_the_same_trace_id()
    {
        var ctx = new SyncNarrativeContext(new NarrativeTraceConfig());

        ctx.EnterMethod("Outer", "Run", []);
        ctx.EnterMethod("Inner", "Do", []);
        ctx.ExitMethodWithReturn(null);
        ctx.ExitMethodWithReturn(null);

        var parent = ctx.CaptureTrace().Roots[0];
        var child = parent.Children[0];
        Assert.Equal(
            parent.SpanContext!.TraceId,
            child.SpanContext!.TraceId);
    }

    [Fact]
    public void Reset_starts_a_new_trace_id()
    {
        var ctx = new SyncNarrativeContext(new NarrativeTraceConfig());

        ctx.EnterMethod("Svc", "Run", []);
        ctx.ExitMethodWithReturn(null);
        var first = ctx.CaptureTrace().Roots[0].SpanContext!.TraceId;

        ctx.Reset();
        ctx.EnterMethod("Svc", "Run", []);
        ctx.ExitMethodWithReturn(null);
        var second = ctx.CaptureTrace().Roots[0].SpanContext!.TraceId;

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Story_id_derives_from_first_root_enter_and_is_stable()
    {
        var ctx = new SyncNarrativeContext(new NarrativeTraceConfig());

        Assert.Null(ctx.StoryId);

        ctx.EnterMethod("OrderService", "PlaceOrder", []);
        ctx.EnterMethod("Repo", "Save", []);

        Assert.Equal("OrderService.PlaceOrder", ctx.StoryId);
        Assert.Equal("OrderService.PlaceOrder", ctx.ChapterId);

        ctx.ExitMethodWithReturn(null);
        ctx.ExitMethodWithReturn(null);

        // A second root does not overwrite the story id.
        ctx.EnterMethod("Other", "Thing", []);
        Assert.Equal("OrderService.PlaceOrder", ctx.StoryId);
    }

    [Fact]
    public void Reset_clears_story_id()
    {
        var ctx = new SyncNarrativeContext(new NarrativeTraceConfig());

        ctx.EnterMethod("Svc", "Run", []);
        ctx.ExitMethodWithReturn(null);
        ctx.Reset();

        Assert.Null(ctx.StoryId);
    }

    [Fact]
    public void Request_and_user_context_flow_onto_span_context()
    {
        var ctx = new SyncNarrativeContext(new NarrativeTraceConfig());
        ctx.SetRequestContext(
            "GET", new HttpRoute("/api/orders"),
            new ClientIp("203.0.113.5"));
        ctx.SetUserContext(
            new EnduserId("user-42"), new SessionId("sess-9"),
            new TenantId("acme"));

        ctx.EnterMethod("Svc", "Run", []);
        ctx.ExitMethodWithReturn(null);

        var span = ctx.CaptureTrace().Roots[0].SpanContext!;
        Assert.Equal("GET", span.HttpMethod);
        Assert.Equal("/api/orders", span.HttpRoute!.Value);
        Assert.Equal("203.0.113.5", span.ClientIp!.Value);
        Assert.Equal("user-42", span.EnduserId!.Value);
        Assert.Equal("acme", span.TenantId!.Value);
    }

    [Fact]
    public void Service_identity_flows_onto_span_context()
    {
        var identity = new ServiceIdentity("orders", "1.2.3", "prod");
        var config = new NarrativeTraceConfig(
            TracingLevel.Detail, identity);
        var ctx = new SyncNarrativeContext(config);

        ctx.EnterMethod("Svc", "Run", []);
        ctx.ExitMethodWithReturn(null);

        var span = ctx.CaptureTrace().Roots[0].SpanContext!;
        Assert.Equal("orders", span.ServiceName);
        Assert.Equal("1.2.3", span.ServiceVersion);
        Assert.Equal("prod", span.Environment);
    }

    [Fact]
    public void CaptureTrace_returns_snapshot_unaffected_by_later_calls()
    {
        var ctx = new SyncNarrativeContext(new NarrativeTraceConfig());

        ctx.EnterMethod("Svc", "First", []);
        ctx.ExitMethodWithReturn(null);
        var snapshot = ctx.CaptureTrace();

        ctx.EnterMethod("Svc", "Second", []);
        ctx.ExitMethodWithReturn(null);

        Assert.Single(snapshot.Roots);
        Assert.Equal("First", snapshot.Roots[0].Signature.MethodName);
    }

    [Fact]
    public void Multiple_captures_return_equivalent_state()
    {
        var ctx = new SyncNarrativeContext(new NarrativeTraceConfig());

        ctx.EnterMethod("Svc", "Run", []);
        ctx.ExitMethodWithReturn(null);

        var first = ctx.CaptureTrace();
        var second = ctx.CaptureTrace();

        Assert.Equal(first.Roots.Count, second.Roots.Count);
        Assert.Equal(
            first.Roots[0].Signature,
            second.Roots[0].Signature);
        Assert.Equal(
            first.Roots[0].Outcome,
            second.Roots[0].Outcome);
    }

    [Fact]
    public void Mismatched_exit_without_enter_is_silent_noop()
    {
        var ctx = new SyncNarrativeContext(new NarrativeTraceConfig());

        ctx.ExitMethodWithReturn("stray");
        ctx.ExitMethodWithException(new Exception("stray"));

        Assert.True(ctx.CaptureTrace().IsEmpty);
    }

    [Fact]
    public void Level_off_makes_enter_exit_noop()
    {
        var config = new NarrativeTraceConfig(TracingLevel.Off);
        var ctx = new SyncNarrativeContext(config);

        ctx.EnterMethod("Svc", "Run", []);
        ctx.ExitMethodWithReturn(null);

        Assert.True(ctx.CaptureTrace().IsEmpty);
    }

    [Fact]
    public void Config_level_change_takes_effect_immediately()
    {
        var config = new NarrativeTraceConfig(TracingLevel.Off);
        var ctx = new SyncNarrativeContext(config);

        ctx.EnterMethod("Svc", "Ignored", []);
        ctx.ExitMethodWithReturn(null);
        Assert.True(ctx.CaptureTrace().IsEmpty);

        config.Level = TracingLevel.Detail;

        ctx.EnterMethod("Svc", "Captured", []);
        ctx.ExitMethodWithReturn(null);
        Assert.Single(ctx.CaptureTrace().Roots);
    }

    [Fact]
    public void Deep_nesting_works_correctly()
    {
        var ctx = new SyncNarrativeContext(new NarrativeTraceConfig());
        const int depth = 15;

        for (var i = 0; i < depth; i++)
        {
            ctx.EnterMethod("Svc", $"Level{i}", []);
        }

        for (var i = 0; i < depth; i++)
        {
            ctx.ExitMethodWithReturn(null);
        }

        var trace = ctx.CaptureTrace();
        Assert.Single(trace.Roots);

        var node = trace.Roots[0];
        for (var i = 1; i < depth; i++)
        {
            Assert.Single(node.Children);
            node = node.Children[0];
            Assert.Equal($"Level{i}", node.Signature.MethodName);
        }

        Assert.Empty(node.Children);
    }

    // A snapshot propagates lineage into a worker; it is not a time machine
    // back to the capture as it stood. Activation therefore opens a *fresh*
    // capture — inverted from the save/restore behaviour this port shipped
    // before the 2026-08-31 propagation audit, exactly as Java inverted its
    // equivalents when ADR-013 landed.
    [Fact]
    public void Snapshot_activate_opens_a_fresh_capture_for_the_flow()
    {
        var ctx = new SyncNarrativeContext(new NarrativeTraceConfig());

        ctx.EnterMethod("Svc", "First", []);
        ctx.ExitMethodWithReturn(null);
        var snapshot = ctx.Snapshot();

        ctx.EnterMethod("Svc", "Second", []);
        ctx.ExitMethodWithReturn(null);
        Assert.Equal(2, ctx.CaptureTrace().Roots.Count);

        using (snapshot.Activate())
        {
            Assert.Empty(ctx.CaptureTrace().Roots);

            ctx.EnterMethod("Svc", "Worker", []);
            ctx.ExitMethodWithReturn(null);

            var trace = ctx.CaptureTrace();
            Assert.Single(trace.Roots);
            Assert.Equal(
                "Worker",
                trace.Roots[0].Signature.MethodName);
        }
    }

    [Fact]
    public void Scope_dispose_restores_the_origin_and_adopts_the_workers_span()
    {
        var ctx = new SyncNarrativeContext(new NarrativeTraceConfig());

        ctx.EnterMethod("Svc", "First", []);
        ctx.ExitMethodWithReturn(null);
        var snapshot = ctx.Snapshot();

        ctx.EnterMethod("Svc", "Second", []);
        ctx.ExitMethodWithReturn(null);

        var scope = snapshot.Activate();
        ctx.EnterMethod("Svc", "Worker", []);
        ctx.ExitMethodWithReturn(null);

        scope.Dispose();

        var roots = ctx.CaptureTrace().Roots;
        Assert.Equal(3, roots.Count);
        Assert.Equal("First", roots[0].Signature.MethodName);
        Assert.Equal("Second", roots[1].Signature.MethodName);
        Assert.Equal("Worker", roots[2].Signature.MethodName);
    }

    [Fact]
    public void Disposing_a_scope_twice_does_not_adopt_the_worker_twice()
    {
        var ctx = new SyncNarrativeContext(new NarrativeTraceConfig());
        ctx.EnterMethod("Svc", "First", []);
        ctx.ExitMethodWithReturn(null);
        var scope = ctx.Snapshot().Activate();
        ctx.EnterMethod("Svc", "Worker", []);
        ctx.ExitMethodWithReturn(null);

        scope.Dispose();
        scope.Dispose();

        Assert.Equal(2, ctx.CaptureTrace().Roots.Count);
    }

    [Theory]
    [InlineData(TracingLevel.Off, false)]
    [InlineData(TracingLevel.Errors, true)]
    [InlineData(TracingLevel.Summary, true)]
    [InlineData(TracingLevel.Narrative, true)]
    [InlineData(TracingLevel.Detail, true)]
    public void IsActive_reflects_config_level(
        TracingLevel level, bool expected)
    {
        var ctx = new SyncNarrativeContext(
            new NarrativeTraceConfig(level));

        Assert.Equal(expected, ctx.IsActive);
    }

    [Fact]
    public void Level_detail_keeps_all_nodes()
    {
        var ctx = new SyncNarrativeContext(
            new NarrativeTraceConfig(TracingLevel.Detail));

        ctx.EnterMethod("Outer", "Run", []);
        ctx.EnterMethod("Inner", "Do", []);
        ctx.ExitMethodWithReturn(null);
        ctx.ExitMethodWithReturn(null);

        var trace = ctx.CaptureTrace();
        Assert.Single(trace.Roots);
        Assert.Single(trace.Roots[0].Children);
    }

    [Fact]
    public void Level_narrative_keeps_all_nodes()
    {
        var ctx = new SyncNarrativeContext(
            new NarrativeTraceConfig(TracingLevel.Narrative));

        ctx.EnterMethod("Outer", "Run", []);
        ctx.EnterMethod("Inner", "Do", []);
        ctx.ExitMethodWithReturn(null);
        ctx.ExitMethodWithReturn(null);

        var trace = ctx.CaptureTrace();
        Assert.Single(trace.Roots);
        Assert.Single(trace.Roots[0].Children);
    }

    [Fact]
    public void Level_errors_discards_successful_calls()
    {
        var ctx = new SyncNarrativeContext(
            new NarrativeTraceConfig(TracingLevel.Errors));

        ctx.EnterMethod("Svc", "Ok", []);
        ctx.ExitMethodWithReturn(null);

        Assert.True(ctx.CaptureTrace().IsEmpty);
    }

    [Fact]
    public void Level_errors_keeps_failed_calls()
    {
        var ctx = new SyncNarrativeContext(
            new NarrativeTraceConfig(TracingLevel.Errors));

        ctx.EnterMethod("Svc", "Fail", []);
        ctx.ExitMethodWithException(new Exception("boom"));

        var trace = ctx.CaptureTrace();
        Assert.Single(trace.Roots);
        Assert.IsType<Threw>(trace.Roots[0].Outcome);
    }

    [Fact]
    public void Level_summary_prunes_intermediate_nodes()
    {
        var ctx = new SyncNarrativeContext(
            new NarrativeTraceConfig(TracingLevel.Summary));

        ctx.EnterMethod("Root", "Run", []);
        ctx.EnterMethod("Middle", "Process", []);
        ctx.EnterMethod("Leaf", "Do", []);
        ctx.ExitMethodWithReturn(null);
        ctx.ExitMethodWithReturn(null);
        ctx.ExitMethodWithReturn(null);

        var trace = ctx.CaptureTrace();
        Assert.Single(trace.Roots);
        var root = trace.Roots[0];
        Assert.Equal("Root", root.Signature.ClassName);
        Assert.Single(root.Children);
        Assert.Equal("Leaf", root.Children[0].Signature.ClassName);
        Assert.Empty(root.Children[0].Children);
    }

    [Fact]
    public void Level_summary_keeps_root_leaf_node_unchanged()
    {
        var ctx = new SyncNarrativeContext(
            new NarrativeTraceConfig(TracingLevel.Summary));

        ctx.EnterMethod("Solo", "Run", []);
        ctx.ExitMethodWithReturn("done");

        var trace = ctx.CaptureTrace();
        Assert.Single(trace.Roots);
        Assert.Equal("Solo", trace.Roots[0].Signature.ClassName);
        Assert.Empty(trace.Roots[0].Children);
    }

    [Fact]
    public void Level_summary_collects_leaves_from_multiple_branches()
    {
        var ctx = new SyncNarrativeContext(
            new NarrativeTraceConfig(TracingLevel.Summary));

        ctx.EnterMethod("Root", "Run", []);
        ctx.EnterMethod("Mid", "A", []);
        ctx.EnterMethod("LeafA", "Do", []);
        ctx.ExitMethodWithReturn(null);
        ctx.ExitMethodWithReturn(null);
        ctx.EnterMethod("Mid", "B", []);
        ctx.EnterMethod("LeafB", "Do", []);
        ctx.ExitMethodWithReturn(null);
        ctx.ExitMethodWithReturn(null);
        ctx.ExitMethodWithReturn(null);

        var trace = ctx.CaptureTrace();
        Assert.Single(trace.Roots);
        var root = trace.Roots[0];
        Assert.Equal(2, root.Children.Count);
        Assert.Equal("LeafA", root.Children[0].Signature.ClassName);
        Assert.Equal("LeafB", root.Children[1].Signature.ClassName);
    }

    [Fact]
    public void Level_errors_keeps_ancestor_chain_of_failed_child()
    {
        var ctx = new SyncNarrativeContext(
            new NarrativeTraceConfig(TracingLevel.Errors));

        ctx.EnterMethod("Outer", "Run", []);
        ctx.EnterMethod("Inner", "Fail", []);
        ctx.ExitMethodWithException(new Exception("boom"));
        ctx.ExitMethodWithReturn(null);

        var trace = ctx.CaptureTrace();
        Assert.Single(trace.Roots);
        Assert.Equal("Outer", trace.Roots[0].Signature.ClassName);
        Assert.Single(trace.Roots[0].Children);
        Assert.IsType<Threw>(trace.Roots[0].Children[0].Outcome);
    }

    [Fact]
    public void EnterMethod_returns_distinct_non_empty_span_ids()
    {
        var ctx = new SyncNarrativeContext(
            new NarrativeTraceConfig());

        var h0 = ctx.EnterMethod("Svc", "A", []);
        ctx.ExitMethodWithReturn(null);
        var h1 = ctx.EnterMethod("Svc", "B", []);
        ctx.ExitMethodWithReturn(null);

        Assert.False(h0.IsEmpty);
        Assert.False(h1.IsEmpty);
        Assert.NotEqual(h0, h1);
    }

    [Fact]
    public void EnterMethod_returns_empty_span_id_when_level_is_off()
    {
        var config = new NarrativeTraceConfig(TracingLevel.Off);
        var ctx = new SyncNarrativeContext(config);

        var handle = ctx.EnterMethod("Svc", "Run", []);

        Assert.True(handle.IsEmpty);
    }

    [Fact]
    public void ExitMethodWithReturn_with_handle_completes_correct_frame()
    {
        var ctx = new SyncNarrativeContext(
            new NarrativeTraceConfig());

        var h0 = ctx.EnterMethod("Svc", "First", []);
        var h1 = ctx.EnterMethod("Svc", "Second", []);

        ctx.ExitMethodWithReturn("second-val", h1);
        ctx.ExitMethodWithReturn("first-val", h0);

        var trace = ctx.CaptureTrace();
        Assert.Single(trace.Roots);
        var root = trace.Roots[0];
        Assert.Equal("First", root.Signature.MethodName);
        var returned = Assert.IsType<Returned>(root.Outcome);
        Assert.Equal("first-val", returned.RenderedValue);
        Assert.Single(root.Children);
        Assert.Equal("Second",
            root.Children[0].Signature.MethodName);
    }

    [Fact]
    public void ExitMethodWithReturn_without_handle_uses_lifo()
    {
        var ctx = new SyncNarrativeContext(
            new NarrativeTraceConfig());

        ctx.EnterMethod("Svc", "Outer", []);
        ctx.EnterMethod("Svc", "Inner", []);
        ctx.ExitMethodWithReturn("inner-val");
        ctx.ExitMethodWithReturn("outer-val");

        var trace = ctx.CaptureTrace();
        Assert.Single(trace.Roots);
        Assert.Equal("Outer",
            trace.Roots[0].Signature.MethodName);
        Assert.Single(trace.Roots[0].Children);
        Assert.Equal("Inner",
            trace.Roots[0].Children[0].Signature.MethodName);
    }

    [Fact]
    public void ExitMethodWithException_with_handle_completes_correct_frame()
    {
        var ctx = new SyncNarrativeContext(
            new NarrativeTraceConfig());
        var ex = new InvalidOperationException("boom");

        var h0 = ctx.EnterMethod("Svc", "First", []);
        var h1 = ctx.EnterMethod("Svc", "Second", []);

        ctx.ExitMethodWithException(ex, h1);
        ctx.ExitMethodWithReturn(null, h0);

        var trace = ctx.CaptureTrace();
        Assert.Single(trace.Roots);
        Assert.IsType<Threw>(
            trace.Roots[0].Children[0].Outcome);
    }

    [Fact]
    public void DetachFrame_removes_frame_from_active_stack()
    {
        var ctx = new SyncNarrativeContext(
            new NarrativeTraceConfig());

        var h0 = ctx.EnterMethod("Svc", "Detached", []);
        ctx.DetachFrame(h0);
        var h1 = ctx.EnterMethod("Svc", "After", []);
        ctx.ExitMethodWithReturn(null, h1);
        ctx.ExitMethodWithReturn(null, h0);

        var trace = ctx.CaptureTrace();
        Assert.Equal(2, trace.Roots.Count);
    }

    [Fact]
    public void New_enter_after_detach_does_not_nest_under_detached()
    {
        var ctx = new SyncNarrativeContext(
            new NarrativeTraceConfig());

        var h0 = ctx.EnterMethod("Svc", "Async", []);
        ctx.DetachFrame(h0);
        var h1 = ctx.EnterMethod("Svc", "Next", []);
        ctx.ExitMethodWithReturn(null, h1);
        ctx.ExitMethodWithReturn(null, h0);

        var trace = ctx.CaptureTrace();
        var names = trace.Roots
            .Select(r => r.Signature.MethodName).ToList();
        Assert.Contains("Async", names);
        Assert.Contains("Next", names);
        Assert.All(trace.Roots, r => Assert.Empty(r.Children));
    }

    [Fact]
    public void Detached_frame_completed_later_produces_correct_root()
    {
        var ctx = new SyncNarrativeContext(
            new NarrativeTraceConfig());

        var h0 = ctx.EnterMethod("Svc", "Deferred", []);
        ctx.DetachFrame(h0);
        ctx.ExitMethodWithReturn("done", h0);

        var trace = ctx.CaptureTrace();
        Assert.Single(trace.Roots);
        Assert.Equal("Deferred",
            trace.Roots[0].Signature.MethodName);
        var returned = Assert.IsType<Returned>(
            trace.Roots[0].Outcome);
        Assert.Equal("done", returned.RenderedValue);
    }

    [Fact]
    public void Children_accumulated_before_detach_are_preserved()
    {
        var ctx = new SyncNarrativeContext(
            new NarrativeTraceConfig());

        var h0 = ctx.EnterMethod("Svc", "Parent", []);
        var hChild = ctx.EnterMethod("Svc", "Child", []);
        ctx.ExitMethodWithReturn(null, hChild);
        ctx.DetachFrame(h0);
        ctx.ExitMethodWithReturn(null, h0);

        var trace = ctx.CaptureTrace();
        Assert.Single(trace.Roots);
        Assert.Single(trace.Roots[0].Children);
        Assert.Equal("Child",
            trace.Roots[0].Children[0].Signature.MethodName);
    }

    [Fact]
    public void Two_overlapping_detach_complete_produce_sibling_roots()
    {
        var ctx = new SyncNarrativeContext(
            new NarrativeTraceConfig());

        var h0 = ctx.EnterMethod("Svc", "Fast", []);
        ctx.DetachFrame(h0);
        var h1 = ctx.EnterMethod("Svc", "Slow", []);
        ctx.DetachFrame(h1);

        ctx.ExitMethodWithReturn("fast-val", h0);
        ctx.ExitMethodWithReturn("slow-val", h1);

        var trace = ctx.CaptureTrace();
        Assert.Equal(2, trace.Roots.Count);
        var names = trace.Roots
            .Select(r => r.Signature.MethodName).ToList();
        Assert.Contains("Fast", names);
        Assert.Contains("Slow", names);
    }

    [Fact]
    public void Overlapping_calls_with_shared_parent_attach_correctly()
    {
        var ctx = new SyncNarrativeContext(
            new NarrativeTraceConfig());

        var hParent = ctx.EnterMethod("Svc", "Parent", []);
        var hA = ctx.EnterMethod("Svc", "A", []);
        ctx.DetachFrame(hA);
        var hB = ctx.EnterMethod("Svc", "B", []);
        ctx.DetachFrame(hB);

        ctx.ExitMethodWithReturn(null, hA);
        ctx.ExitMethodWithReturn(null, hB);
        ctx.ExitMethodWithReturn(null, hParent);

        var trace = ctx.CaptureTrace();
        Assert.Single(trace.Roots);
        Assert.Equal("Parent",
            trace.Roots[0].Signature.MethodName);
        Assert.Equal(2, trace.Roots[0].Children.Count);
    }

    [Fact]
    public void Completed_frame_whose_parent_already_exited_nests_under_parent()
    {
        var ctx = new SyncNarrativeContext(
            new NarrativeTraceConfig());

        var hParent = ctx.EnterMethod("Svc", "Parent", []);
        var hChild = ctx.EnterMethod("Svc", "Child", []);
        ctx.DetachFrame(hChild);
        ctx.ExitMethodWithReturn(null, hParent);

        ctx.ExitMethodWithReturn("late", hChild);

        var trace = ctx.CaptureTrace();
        Assert.Single(trace.Roots);
        Assert.Equal("Parent",
            trace.Roots[0].Signature.MethodName);
        Assert.Single(trace.Roots[0].Children);
        Assert.Equal("Child",
            trace.Roots[0].Children[0].Signature.MethodName);
    }

    [Fact]
    public void ParentOf_returns_parent_for_nested_handle()
    {
        var ctx = new SyncNarrativeContext(
            new NarrativeTraceConfig());

        var h0 = ctx.EnterMethod("Outer", "Run", []);
        var h1 = ctx.EnterMethod("Inner", "Do", []);

        Assert.Equal(h0, ctx.ParentOf(h1));

        ctx.ExitMethodWithReturn(null, h1);
        ctx.ExitMethodWithReturn(null, h0);
    }

    [Fact]
    public void ParentOf_returns_null_for_root_and_unknown()
    {
        var ctx = new SyncNarrativeContext(
            new NarrativeTraceConfig());

        var h0 = ctx.EnterMethod("Svc", "Run", []);

        Assert.Null(ctx.ParentOf(h0));
        Assert.Null(ctx.ParentOf(SpanIdGenerator.GenerateSpanId()));

        ctx.ExitMethodWithReturn(null, h0);
    }

    [Fact]
    public void RunScoped_sets_and_restores_scopedParent()
    {
        var ctx = new SyncNarrativeContext(
            new NarrativeTraceConfig());

        var h0 = ctx.EnterMethod("Outer", "Run", []);

        var innerHandle = ctx.RunScoped(h0, () =>
        {
            var h = ctx.EnterMethod("Inner", "Do", []);
            ctx.ExitMethodWithReturn(null, h);
            return h;
        });

        Assert.Equal(h0, ctx.ParentOf(innerHandle));
        ctx.ExitMethodWithReturn(null, h0);
    }

    [Fact]
    public void ScopedParent_takes_priority_over_activeStack()
    {
        var ctx = new SyncNarrativeContext(
            new NarrativeTraceConfig());

        var h0 = ctx.EnterMethod("A", "Run", []);
        var h1 = ctx.EnterMethod("B", "Run", []);

        var h2 = ctx.RunScoped(h0, () =>
            ctx.EnterMethod("C", "Run", []));

        Assert.Equal(h0, ctx.ParentOf(h2));
        Assert.NotEqual(h1, ctx.ParentOf(h2));

        ctx.ExitMethodWithReturn(null, h2);
        ctx.ExitMethodWithReturn(null, h1);
        ctx.ExitMethodWithReturn(null, h0);
    }

    [Fact]
    public async Task Cross_thread_exit_appends_to_same_event_list()
    {
        var ctx = new SyncNarrativeContext(
            new NarrativeTraceConfig());

        var h0 = ctx.EnterMethod("Svc", "Async", []);
        ctx.DetachFrame(h0);

        await Task.Run(() =>
            ctx.ExitMethodWithReturn("done", h0));

        var trace = ctx.CaptureTrace();
        Assert.Single(trace.Roots);
        Assert.Equal("Async",
            trace.Roots[0].Signature.MethodName);
        var returned = Assert.IsType<Returned>(
            trace.Roots[0].Outcome);
        Assert.Equal("done", returned.RenderedValue);
    }

    [Fact]
    public void GraftChild_appends_GraftEvent()
    {
        var ctx = new SyncNarrativeContext(
            new NarrativeTraceConfig());

        var h0 = ctx.EnterMethod("Parent", "Run", []);
        var grafted = new TraceNode(
            new MethodSignature("Grafted", "Do", []),
            new Returned(null), [], 100);
        ctx.GraftChild(grafted);
        ctx.ExitMethodWithReturn(null, h0);

        var trace = ctx.CaptureTrace();
        Assert.Single(trace.Roots);
        Assert.Single(trace.Roots[0].Children);
        Assert.Equal("Grafted",
            trace.Roots[0].Children[0].Signature.ClassName);
    }

    [Fact]
    public void GraftChild_attaches_to_roots_when_no_frame_open()
    {
        var ctx = new SyncNarrativeContext(
            new NarrativeTraceConfig());

        var grafted = new TraceNode(
            new MethodSignature("Grafted", "Do", []),
            new Returned(null), [], 100);
        ctx.GraftChild(grafted);

        var trace = ctx.CaptureTrace();
        Assert.Single(trace.Roots);
        Assert.Equal("Grafted",
            trace.Roots[0].Signature.ClassName);
    }

    [Fact]
    public void GraftChild_preserves_existing_children_order()
    {
        var ctx = new SyncNarrativeContext(
            new NarrativeTraceConfig());

        var h0 = ctx.EnterMethod("Parent", "Run", []);
        var h1 = ctx.EnterMethod("Child", "A", []);
        ctx.ExitMethodWithReturn(null, h1);
        var grafted = new TraceNode(
            new MethodSignature("Grafted", "B", []),
            new Returned(null), [], 100);
        ctx.GraftChild(grafted);
        ctx.ExitMethodWithReturn(null, h0);

        var children = ctx.CaptureTrace()
            .Roots[0].Children;
        Assert.Equal(2, children.Count);
        Assert.Equal("A",
            children[0].Signature.MethodName);
        Assert.Equal("B",
            children[1].Signature.MethodName);
    }

    [Fact]
    public void ExitMethodWithReturn_is_noop_when_level_off()
    {
        var config = new NarrativeTraceConfig(TracingLevel.Off);
        var ctx = new SyncNarrativeContext(config);

        // EnterMethod returns SpanId.Empty when Off
        var handle = ctx.EnterMethod("Svc", "Run", []);
        Assert.True(handle.IsEmpty);

        // ExitMethodWithReturn with unresolvable handle should
        // return early (no-op)
        ctx.ExitMethodWithReturn("value", handle);

        Assert.True(ctx.CaptureTrace().IsEmpty);
    }

    [Fact]
    public void ExitMethodWithException_is_noop_when_level_off()
    {
        var config = new NarrativeTraceConfig(TracingLevel.Off);
        var ctx = new SyncNarrativeContext(config);

        var handle = ctx.EnterMethod("Svc", "Fail", []);
        Assert.True(handle.IsEmpty);

        // ExitMethodWithException with unresolvable handle should
        // return early (no-op)
        ctx.ExitMethodWithException(
            new Exception("boom"), handle);

        Assert.True(ctx.CaptureTrace().IsEmpty);
    }

    [Fact]
    public void Reset_clears_active_stack_and_parent_map()
    {
        var ctx = new SyncNarrativeContext(
            new NarrativeTraceConfig());

        // Build up state: nested calls with parent map
        var h0 = ctx.EnterMethod("Outer", "Run", []);
        var h1 = ctx.EnterMethod("Inner", "Do", []);
        // Verify parent map is populated
        Assert.Equal(h0, ctx.ParentOf(h1)!.Value);

        ctx.Reset();

        // After reset, parent map should be cleared
        Assert.Null(ctx.ParentOf(h0));
        Assert.Null(ctx.ParentOf(h1));

        // After reset, active stack should be cleared
        // so a new enter creates a root (not nested)
        ctx.EnterMethod("Fresh", "Start", []);
        ctx.ExitMethodWithReturn(null);

        var trace = ctx.CaptureTrace();
        Assert.Single(trace.Roots);
        Assert.Equal("Fresh",
            trace.Roots[0].Signature.ClassName);
        Assert.Empty(trace.Roots[0].Children);
    }

    [Fact]
    public void Auto_resolve_returns_last_handle_from_stack()
    {
        var ctx = new SyncNarrativeContext(
            new NarrativeTraceConfig());

        ctx.EnterMethod("Svc", "First", []);
        ctx.EnterMethod("Svc", "Second", []);
        ctx.EnterMethod("Svc", "Third", []);

        // Exit without handle should resolve to the
        // last entry in the active stack (h2)
        ctx.ExitMethodWithReturn("third-val");

        // Now the last active should be h1
        ctx.ExitMethodWithReturn("second-val");

        // Now the last active should be h0
        ctx.ExitMethodWithReturn("first-val");

        var trace = ctx.CaptureTrace();
        Assert.Single(trace.Roots);
        var root = trace.Roots[0];
        Assert.Equal("First", root.Signature.MethodName);
        var rootReturned = Assert.IsType<Returned>(
            root.Outcome);
        Assert.Equal("first-val",
            rootReturned.RenderedValue);

        Assert.Single(root.Children);
        var mid = root.Children[0];
        Assert.Equal("Second", mid.Signature.MethodName);
        var midReturned = Assert.IsType<Returned>(
            mid.Outcome);
        Assert.Equal("second-val",
            midReturned.RenderedValue);

        Assert.Single(mid.Children);
        var leaf = mid.Children[0];
        Assert.Equal("Third", leaf.Signature.MethodName);
        var leafReturned = Assert.IsType<Returned>(
            leaf.Outcome);
        Assert.Equal("third-val",
            leafReturned.RenderedValue);
    }

    [Fact]
    public void Snapshot_activate_dispose_roundtrip_hands_the_workers_span_back()
    {
        var ctx = new SyncNarrativeContext(
            new NarrativeTraceConfig());

        // Build initial state
        ctx.EnterMethod("Svc", "A", []);
        ctx.ExitMethodWithReturn("a-val");
        Assert.Single(ctx.CaptureTrace().Roots);

        // Take a snapshot of the lineage as it stands
        var snapshot = ctx.Snapshot();

        // Mutate the context
        ctx.EnterMethod("Svc", "B", []);
        ctx.ExitMethodWithReturn("b-val");
        Assert.Equal(2, ctx.CaptureTrace().Roots.Count);

        // Activating opens a fresh capture: the origin's spans are not the
        // worker's to report
        var scope = snapshot.Activate();
        Assert.Empty(ctx.CaptureTrace().Roots);

        // What the worker traces, the worker reports — and only that
        ctx.EnterMethod("Svc", "C", []);
        ctx.ExitMethodWithReturn("c-val");
        var activatedTrace = ctx.CaptureTrace();
        Assert.Single(activatedTrace.Roots);
        Assert.Equal("C",
            activatedTrace.Roots[0].Signature.MethodName);

        // Dispose restores the origin, which now also answers for C
        scope.Dispose();
        var restoredTrace = ctx.CaptureTrace();
        Assert.Equal(3, restoredTrace.Roots.Count);
        Assert.Equal("A",
            restoredTrace.Roots[0].Signature.MethodName);
        Assert.Equal("B",
            restoredTrace.Roots[1].Signature.MethodName);
        Assert.Equal("C",
            restoredTrace.Roots[2].Signature.MethodName);
    }
}
