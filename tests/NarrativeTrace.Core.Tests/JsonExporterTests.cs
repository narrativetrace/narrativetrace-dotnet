// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Text.Json;
using NarrativeTrace.Core;
using NarrativeTrace.Runtime;
using Xunit;

namespace NarrativeTrace.Core.Tests;

public class JsonExporterTests
{
    private static readonly TraceMetadata DefaultMetadata =
        new("Test scenario", ScenarioResult.Success);

    [Fact]
    public void Single_method_produces_enter_and_exit_events()
    {
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Svc", "Run", []),
                new Returned(null), [], 0),
        ]);

        var json = JsonExporter.Export(tree, DefaultMetadata);
        var doc = JsonDocument.Parse(json);
        var events = doc.RootElement
            .GetProperty("events");

        Assert.Equal(2, events.GetArrayLength());
        Assert.Equal(
            "enter",
            events[0].GetProperty("type").GetString());
        Assert.Equal(
            "exit",
            events[1].GetProperty("type").GetString());
    }

    [Fact]
    public void Nested_call_carries_parent_span_id()
    {
        var child = new TraceNode(
            new MethodSignature("Repo", "Save", []),
            new Returned(null), [], 0);
        var root = new TraceNode(
            new MethodSignature("Svc", "Run", []),
            new Returned(null), [child], 0);
        var tree = new TraceTree([root]);

        var events = ParseEvents(tree);

        var rootSpanId = events[0].GetProperty("spanId").GetString();
        var childEnter = events[1];
        Assert.Equal(
            rootSpanId,
            childEnter.GetProperty("parentSpanId").GetString());
    }

    [Fact]
    public void Root_event_has_no_parent_span_id()
    {
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Svc", "Run", []),
                new Returned(null), [], 0),
        ]);

        var events = ParseEvents(tree);

        Assert.False(
            events[0].TryGetProperty("parentSpanId", out _));
    }

    [Fact]
    public void Parameters_serialized_in_event()
    {
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Svc", "Run",
                    [new ParameterCapture("id", "42", false)]),
                new Returned(null), [], 0),
        ]);

        var events = ParseEvents(tree);
        var parameters = events[0].GetProperty("parameters");

        Assert.Equal(1, parameters.GetArrayLength());
        Assert.Equal("id", parameters[0].GetProperty("name").GetString());
        Assert.Equal("42", parameters[0].GetProperty("value").GetString());
    }

    [Fact]
    public void Redacted_parameter_has_redacted_true_and_marker_value()
    {
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Auth", "Login",
                    [new ParameterCapture("pw", "hunter2", true)]),
                new Returned(null), [], 0),
        ]);

        var events = ParseEvents(tree);
        var param = events[0].GetProperty("parameters")[0];

        Assert.True(param.GetProperty("redacted").GetBoolean());
        Assert.Equal(
            RedactionPolicy.Marker,
            param.GetProperty("value").GetString());
    }

    [Fact]
    public void Exception_produces_exit_with_error_info()
    {
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Svc", "Fail", []),
                new Threw(new InvalidOperationException("boom")),
                [], 0),
        ]);

        var events = ParseEvents(tree);
        var exit = events[1];

        Assert.Equal("threw", exit.GetProperty("outcome").GetString());
        Assert.Equal(
            "InvalidOperationException",
            exit.GetProperty("errorType").GetString());
        Assert.Equal("boom", exit.GetProperty("errorMessage").GetString());
    }

    [Fact]
    public void Metadata_appears_in_output()
    {
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Svc", "Run", []),
                new Returned(null), [], 0),
        ]);
        var meta = new TraceMetadata(
            "Login flow",
            ScenarioResult.Success,
            TestClass: "AuthTests",
            TestMethod: "Login_succeeds",
            Framework: "xUnit",
            Timestamp: "2026-01-01T00:00:00Z");

        var json = JsonExporter.Export(tree, meta);
        var doc = JsonDocument.Parse(json);
        var scenario = doc.RootElement.GetProperty("scenario");

        Assert.Equal("Login flow", scenario.GetProperty("name").GetString());
        Assert.Equal("AuthTests", scenario.GetProperty("testClass").GetString());
        Assert.Equal("Login_succeeds", scenario.GetProperty("testMethod").GetString());
        Assert.Equal("xUnit", scenario.GetProperty("framework").GetString());
        Assert.Equal("2026-01-01T00:00:00Z", scenario.GetProperty("timestamp").GetString());
    }

    [Fact]
    public void Span_ids_pair_enter_and_exit_and_are_sequential()
    {
        var child = new TraceNode(
            new MethodSignature("Repo", "Save", []),
            new Returned(null), [], 0);
        var root = new TraceNode(
            new MethodSignature("Svc", "Run", []),
            new Returned(null), [child], 0);
        var tree = new TraceTree([root]);

        var events = ParseEvents(tree);

        Assert.Equal("1", events[0].GetProperty("spanId").GetString());
        Assert.Equal("2", events[1].GetProperty("spanId").GetString());
        Assert.Equal("2", events[2].GetProperty("spanId").GetString());
        Assert.Equal("1", events[3].GetProperty("spanId").GetString());
    }

    [Fact]
    public void Depth_tracks_nesting_level()
    {
        var child = new TraceNode(
            new MethodSignature("Repo", "Save", []),
            new Returned(null), [], 0);
        var root = new TraceNode(
            new MethodSignature("Svc", "Run", []),
            new Returned(null), [child], 0);
        var tree = new TraceTree([root]);

        var events = ParseEvents(tree);

        Assert.Equal(0, events[0].GetProperty("depth").GetInt32());
        Assert.Equal(1, events[1].GetProperty("depth").GetInt32());
    }

    [Fact]
    public void Empty_tree_produces_empty_events_array()
    {
        var tree = new TraceTree([]);

        var events = ParseEvents(tree);

        Assert.Empty(events);
    }

    [Fact]
    public void Output_is_valid_json_round_trip()
    {
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Svc", "Run",
                    [new ParameterCapture("x", "1", false)]),
                new Returned("\"ok\""), [], 1000),
        ]);

        var json = JsonExporter.Export(tree, DefaultMetadata);
        var exception = Record.Exception(
            () => JsonDocument.Parse(json));

        Assert.Null(exception);
    }

    [Fact]
    public void Sequential_node_has_no_concurrency_field()
    {
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Svc", "Run", []),
                new Returned(null), [], 0),
        ]);

        var events = ParseEvents(tree);
        var exit = events[1];

        Assert.False(
            exit.TryGetProperty(
                "concurrency", out _));
    }

    [Fact]
    public void Adopted_async_work_exports_the_additive_async_kind()
    {
        var info = new ConcurrencyInfo(
            "async-0000000000000001", "Notify.Send", 4,
            "async-worker", true,
            ConcurrencyKind.Async);
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Notify", "Send", []),
                new Returned(null), [], 0,
                Concurrency: info),
        ]);

        var events = ParseEvents(tree);
        var c = events[1].GetProperty("concurrency");

        // The value chapter-tree.schema.json enumerates alongside fork-join
        // and fire-and-forget.
        Assert.Equal("async", c.GetProperty("kind").GetString());
    }

    [Fact]
    public void Concurrent_node_includes_concurrency_with_all_fields()
    {
        var info = new ConcurrencyInfo(
            "fork-1", "Svc.Run", 4,
            ".NET TP Worker", true,
            ConcurrencyKind.ForkJoin);
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Svc", "Run", []),
                new Returned(null), [], 0,
                Concurrency: info),
        ]);

        var events = ParseEvents(tree);
        var exit = events[1];
        var c = exit.GetProperty("concurrency");

        Assert.Equal(
            "fork-1", c.GetProperty("groupId").GetString());
        Assert.Equal(
            "fork-join", c.GetProperty("kind").GetString());
        Assert.Equal(
            "Svc.Run",
            c.GetProperty("taskLabel").GetString());
        Assert.Equal(
            4, c.GetProperty("threadId").GetInt32());
        Assert.Equal(
            ".NET TP Worker",
            c.GetProperty("threadName").GetString());
        Assert.True(
            c.GetProperty(
                "isThreadPoolThread").GetBoolean());
    }

    [Fact]
    public void FireAndForget_node_includes_correct_kind()
    {
        var info = new ConcurrencyInfo(
            "fanf-1", "Bg.Work", 1, null, true,
            ConcurrencyKind.FireAndForget);
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Bg", "Work", []),
                new Returned(null), [], 0,
                Concurrency: info),
        ]);

        var events = ParseEvents(tree);
        var c = events[1]
            .GetProperty("concurrency");

        Assert.Equal(
            "fire-and-forget",
            c.GetProperty("kind").GetString());
    }

    [Fact]
    public void ThreadId_and_ThreadName_included()
    {
        var info = new ConcurrencyInfo(
            "fork-1", "A.Do", 7,
            "Worker-7", false,
            ConcurrencyKind.ForkJoin);
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("A", "Do", []),
                new Returned(null), [], 0,
                Concurrency: info),
        ]);

        var events = ParseEvents(tree);
        var c = events[1]
            .GetProperty("concurrency");

        Assert.Equal(
            7, c.GetProperty("threadId").GetInt32());
        Assert.Equal(
            "Worker-7",
            c.GetProperty("threadName").GetString());
    }

    [Fact]
    public void GroupId_matches_across_siblings()
    {
        var info = new ConcurrencyInfo(
            "fork-1", "X.Do", 1, null, true,
            ConcurrencyKind.ForkJoin);
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("A", "Do", []),
                new Returned(null), [], 0,
                Concurrency: info),
            new TraceNode(
                new MethodSignature("B", "Do", []),
                new Returned(null), [], 0,
                Concurrency: info),
        ]);

        var events = ParseEvents(tree);
        var g1 = events[1]
            .GetProperty("concurrency")
            .GetProperty("groupId").GetString();
        var g2 = events[3]
            .GetProperty("concurrency")
            .GetProperty("groupId").GetString();

        Assert.Equal(g1, g2);
    }

    [Fact]
    public void JSON_round_trip_preserves_concurrency_fields()
    {
        var info = new ConcurrencyInfo(
            "fork-1", "Svc.Run", 4,
            "Worker", true,
            ConcurrencyKind.ForkJoin);
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Svc", "Run", []),
                new Returned(null), [], 0,
                Concurrency: info),
        ]);

        var json = JsonExporter.Export(
            tree, DefaultMetadata);
        var doc = JsonDocument.Parse(json);
        var c = doc.RootElement
            .GetProperty("events")[1]
            .GetProperty("concurrency");

        Assert.Equal(
            "fork-1",
            c.GetProperty("groupId").GetString());
        Assert.Equal(
            "fork-join",
            c.GetProperty("kind").GetString());
    }

    [Fact]
    public void Root_with_span_context_emits_trace_block()
    {
        var sc = new SpanContext(
            new TraceId("0af7651916cd43dd8448eb211c80319c"),
            new SpanId("b7ad6b7169203331"),
            null,
            ServiceName: "OrderSvc");
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Svc", "Run", []),
                new Returned(null), [], 0, SpanContext: sc),
        ]);

        var json = JsonExporter.Export(tree, DefaultMetadata);
        var doc = JsonDocument.Parse(json);
        var trace = doc.RootElement.GetProperty("trace");

        Assert.Equal(
            "0af7651916cd43dd8448eb211c80319c",
            trace.GetProperty("traceId").GetString());
        Assert.Equal(
            "OrderSvc",
            trace.GetProperty("serviceName").GetString());
        Assert.Equal(
            sc.TraceId.HumanName,
            trace.GetProperty("traceName").GetString());
    }

    [Fact]
    public void Trace_block_carries_every_field_the_span_context_supplies()
    {
        var sc = new SpanContext(
            new TraceId("0af7651916cd43dd8448eb211c80319c"),
            new SpanId("b7ad6b7169203331"),
            null,
            ServiceName: "order-svc",
            ServiceVersion: "2.1.0",
            Environment: "staging")
        {
            HttpMethod = "POST",
            HttpRoute = new HttpRoute("/api/orders"),
            ClientIp = new ClientIp("client-ip-7"),
            EnduserId = new EnduserId("user-42"),
            SessionId = new SessionId("sess-9"),
            TenantId = new TenantId("tenant-a"),
        };
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Svc", "Run", []),
                new Returned(null), [], 0, SpanContext: sc),
        ]);

        var json = JsonExporter.Export(tree, DefaultMetadata);
        var trace = JsonDocument.Parse(json)
            .RootElement.GetProperty("trace");

        Assert.Equal("order-svc", trace.GetProperty("serviceName").GetString());
        Assert.Equal("2.1.0", trace.GetProperty("serviceVersion").GetString());
        Assert.Equal("staging", trace.GetProperty("environment").GetString());
        Assert.Equal("POST", trace.GetProperty("httpMethod").GetString());
        Assert.Equal("/api/orders", trace.GetProperty("httpRoute").GetString());
        Assert.Equal("client-ip-7", trace.GetProperty("clientIp").GetString());
        Assert.Equal("user-42", trace.GetProperty("enduserId").GetString());
        Assert.Equal("sess-9", trace.GetProperty("sessionId").GetString());
        Assert.Equal("tenant-a", trace.GetProperty("tenantId").GetString());
    }

    [Fact]
    public void Trace_block_names_the_trace_even_when_no_node_kept_a_span_context()
    {
        // Inverted, not deleted: this used to assert the block was omitted.
        // Owner decision 2026-08-31 — this document resolves identity through
        // the shared TraceIdentity, like the other two emitters, so a chapter
        // can no longer name a trace whose embedded tree names none.
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Svc", "Run", []),
                new Returned(null), [], 0),
        ]);

        var json = JsonExporter.Export(tree, DefaultMetadata);
        var trace = JsonDocument.Parse(json)
            .RootElement.GetProperty("trace");

        Assert.Matches(
            "^[0-9a-f]{32}$", trace.GetProperty("traceId").GetString());
        Assert.Matches(
            "^[a-z]+ [a-z]+ [a-z]+$",
            trace.GetProperty("traceName").GetString());
    }

    [Fact]
    public void Trace_block_carries_no_request_fields_when_there_was_no_context_to_inherit_them_from()
    {
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Svc", "Run", []),
                new Returned(null), [], 0),
        ]);

        var json = JsonExporter.Export(tree, DefaultMetadata);
        var trace = JsonDocument.Parse(json)
            .RootElement.GetProperty("trace");

        Assert.False(trace.TryGetProperty("serviceName", out _));
        Assert.False(trace.TryGetProperty("clientIp", out _));
        Assert.False(trace.TryGetProperty("httpRoute", out _));
    }

    [Fact]
    public void Trace_block_inherits_from_a_descendant_when_no_root_carries_one()
    {
        // Roots-only scanning was the other half of the divergence: a mixed
        // tree whose context sits on a child resolved to "no trace at all"
        // here while the other two emitters already inherited depth-first.
        var sc = new SpanContext(
            new TraceId("0af7651916cd43dd8448eb211c80319c"),
            new SpanId("b7ad6b7169203331"),
            null,
            ServiceName: "OrderSvc");
        var child = new TraceNode(
            new MethodSignature("Payment", "Charge", []),
            new Returned(null), [], 0, SpanContext: sc);
        var rootWithoutContext = new TraceNode(
            new MethodSignature("Svc", "Run", []),
            new Returned(null), [child], 0);

        var json = JsonExporter.Export(
            new TraceTree([rootWithoutContext]), DefaultMetadata);
        var trace = JsonDocument.Parse(json)
            .RootElement.GetProperty("trace");

        Assert.Equal(
            "0af7651916cd43dd8448eb211c80319c",
            trace.GetProperty("traceId").GetString());
        Assert.Equal(
            "OrderSvc", trace.GetProperty("serviceName").GetString());
    }

    [Fact]
    public void Version_is_exactly_1_0()
    {
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Svc", "Run", []),
                new Returned(null), [], 0),
        ]);

        var json = JsonExporter.Export(tree, DefaultMetadata);
        var doc = JsonDocument.Parse(json);

        Assert.Equal(
            "1.0",
            doc.RootElement.GetProperty("version").GetString());
    }

    [Fact]
    public void Output_is_indented_with_newlines()
    {
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Svc", "Run", []),
                new Returned(null), [], 0),
        ]);

        var json = JsonExporter.Export(tree, DefaultMetadata);

        Assert.Contains("\n", json);
        Assert.Contains("  ", json);
    }

    [Theory]
    [InlineData(ScenarioResult.Success, "success")]
    [InlineData(ScenarioResult.Error, "error")]
    public void Scenario_result_is_written_from_the_supplied_metadata(
        ScenarioResult supplied, string expected)
    {
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Svc", "Run", []),
                new Returned(null), [], 0),
        ]);

        var json = JsonExporter.Export(
            tree, new TraceMetadata("s", supplied));
        var doc = JsonDocument.Parse(json);

        Assert.Equal(
            expected,
            doc.RootElement.GetProperty("scenario")
                .GetProperty("result").GetString());
    }

    [Fact]
    public void Supplied_scenario_result_wins_over_what_the_trace_shows()
    {
        // The producer's verdict is authoritative and is NOT cross-checked
        // against the tree: a test can fail an assertion over a trace in which
        // nothing threw, and can pass over a trace that swallowed one.
        var threwButPassed = new TraceTree([
            new TraceNode(
                new MethodSignature("Svc", "Run", []),
                new Threw(new Exception("swallowed")), [], 0),
        ]);

        var json = JsonExporter.Export(
            threwButPassed,
            new TraceMetadata("s", ScenarioResult.Success));
        var doc = JsonDocument.Parse(json);

        Assert.Equal(
            "success",
            doc.RootElement.GetProperty("scenario")
                .GetProperty("result").GetString());
    }

    [Fact]
    public void Multi_root_duration_reports_the_first_root_like_java()
    {
        var ticks1 = 5 * TimeSpan.TicksPerMillisecond;
        var ticks2 = 3 * TimeSpan.TicksPerMillisecond;
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("A", "Do", []),
                new Returned(null), [], ticks1),
            new TraceNode(
                new MethodSignature("B", "Do", []),
                new Returned(null), [], ticks2),
        ]);

        var json = JsonExporter.Export(tree, DefaultMetadata);
        var doc = JsonDocument.Parse(json);
        var durationMs = doc.RootElement
            .GetProperty("scenario")
            .GetProperty("durationMs").GetInt64();

        // Java: tree.roots().get(0).durationMillis() — not a sum. A sum
        // double-counts roots that ran concurrently, which is exactly the
        // fire-and-forget case that produces multiple roots.
        Assert.Equal(5, durationMs);
    }

    [Fact]
    public void Scenario_durationMs_is_ticks_divided_by_TicksPerMillisecond()
    {
        var ticks = 7 * TimeSpan.TicksPerMillisecond;
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Svc", "Run", []),
                new Returned(null), [], ticks),
        ]);

        var json = JsonExporter.Export(tree, DefaultMetadata);
        var doc = JsonDocument.Parse(json);
        var durationMs = doc.RootElement
            .GetProperty("scenario")
            .GetProperty("durationMs").GetInt64();

        Assert.Equal(7, durationMs);
    }

    [Fact]
    public void Scenario_has_name_result_and_durationMs_properties()
    {
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Svc", "Run", []),
                new Returned(null), [], 0),
        ]);

        var json = JsonExporter.Export(tree, DefaultMetadata);
        var doc = JsonDocument.Parse(json);
        var scenario = doc.RootElement.GetProperty("scenario");

        Assert.True(scenario.TryGetProperty("name", out _));
        Assert.True(scenario.TryGetProperty("result", out _));
        Assert.True(scenario.TryGetProperty("durationMs", out _));
    }

    [Fact]
    public void Enter_event_has_exact_className_and_methodName()
    {
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("OrderService", "PlaceOrder", []),
                new Returned(null), [], 0),
        ]);

        var events = ParseEvents(tree);
        var enter = events[0];

        Assert.Equal(
            "OrderService",
            enter.GetProperty("className").GetString());
        Assert.Equal(
            "PlaceOrder",
            enter.GetProperty("methodName").GetString());
    }

    [Fact]
    public void Exit_event_has_exact_className_methodName_outcome_and_duration()
    {
        var ticks = 12 * TimeSpan.TicksPerMillisecond;
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("OrderService", "PlaceOrder", []),
                new Returned("ok"), [], ticks),
        ]);

        var events = ParseEvents(tree);
        var exit = events[1];

        Assert.Equal(
            "OrderService",
            exit.GetProperty("className").GetString());
        Assert.Equal(
            "PlaceOrder",
            exit.GetProperty("methodName").GetString());
        Assert.Equal(
            "returned",
            exit.GetProperty("outcome").GetString());
        Assert.Equal(
            12, exit.GetProperty("durationMs").GetInt64());
    }

    [Fact]
    public void Exit_event_depth_matches_nesting()
    {
        var child = new TraceNode(
            new MethodSignature("Repo", "Save", []),
            new Returned(null), [], 0);
        var root = new TraceNode(
            new MethodSignature("Svc", "Run", []),
            new Returned(null), [child], 0);
        var tree = new TraceTree([root]);

        var events = ParseEvents(tree);

        // exit events: child exit at index 2, root exit at index 3
        Assert.Equal(1, events[2].GetProperty("depth").GetInt32());
        Assert.Equal(0, events[3].GetProperty("depth").GetInt32());
    }

    [Fact]
    public void Null_threadName_writes_json_null()
    {
        var info = new ConcurrencyInfo(
            "fork-1", "Svc.Run", 4,
            null, true,
            ConcurrencyKind.ForkJoin);
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Svc", "Run", []),
                new Returned(null), [], 0,
                Concurrency: info),
        ]);

        var events = ParseEvents(tree);
        var c = events[1].GetProperty("concurrency");
        var threadName = c.GetProperty("threadName");

        Assert.Equal(
            JsonValueKind.Null, threadName.ValueKind);
    }

    [Fact]
    public void Returned_with_value_has_outcome_returned_and_returnValue()
    {
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Svc", "Compute", []),
                new Returned("42"), [], 0),
        ]);

        var events = ParseEvents(tree);
        var exit = events[1];

        Assert.Equal(
            "returned",
            exit.GetProperty("outcome").GetString());
        Assert.Equal(
            "42",
            exit.GetProperty("returnValue").GetString());
    }

    [Fact]
    public void Returned_without_value_has_outcome_returned_and_no_returnValue()
    {
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Svc", "DoWork", []),
                new Returned(null), [], 0),
        ]);

        var events = ParseEvents(tree);
        var exit = events[1];

        Assert.Equal(
            "returned",
            exit.GetProperty("outcome").GetString());
        Assert.False(
            exit.TryGetProperty("returnValue", out _));
    }

    [Fact]
    public void Incomplete_outcome_writes_incomplete_string()
    {
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Svc", "Hang", []),
                new Incomplete(), [], 0),
        ]);

        var events = ParseEvents(tree);
        var exit = events[1];

        Assert.Equal(
            "incomplete",
            exit.GetProperty("outcome").GetString());
    }

    [Fact]
    public void Exit_event_durationMs_converts_ticks_correctly()
    {
        var ticks = 25 * TimeSpan.TicksPerMillisecond;
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Svc", "Run", []),
                new Returned(null), [], ticks),
        ]);

        var events = ParseEvents(tree);
        var exit = events[1];

        Assert.Equal(
            25, exit.GetProperty("durationMs").GetInt64());
    }

    [Fact]
    public void Root_exit_event_has_no_parent_span_id()
    {
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Svc", "Run", []),
                new Returned(null), [], 0),
        ]);

        var events = ParseEvents(tree);

        Assert.False(
            events[1].TryGetProperty("parentSpanId", out _));
    }

    [Fact]
    public void Non_redacted_parameter_has_redacted_false()
    {
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Svc", "Run",
                    [new ParameterCapture("name", "alice", false)]),
                new Returned(null), [], 0),
        ]);

        var events = ParseEvents(tree);
        var param = events[0].GetProperty("parameters")[0];

        Assert.False(param.GetProperty("redacted").GetBoolean());
    }

    [Fact]
    public void Version_property_exists_in_output()
    {
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Svc", "Run", []),
                new Returned(null), [], 0),
        ]);

        var json = JsonExporter.Export(tree, DefaultMetadata);
        var doc = JsonDocument.Parse(json);

        Assert.True(
            doc.RootElement.TryGetProperty("version", out _));
    }

    // TraceNode.Children is a type, not a guarantee of acyclicity - a hand-built or replayed tree
    // can hold a genuine reference cycle. Export must terminate rather than recurse the call stack
    // forever — the same unbounded-recursion defect fixed in the java flagship.
    [Fact]
    public void Export_does_not_hang_on_a_cyclic_tree()
    {
        var aChildren = new List<TraceNode>();
        var bChildren = new List<TraceNode>();
        var a = new TraceNode(
            new MethodSignature("Svc", "A", []), new Returned(null), aChildren.AsReadOnly(), 0);
        var b = new TraceNode(
            new MethodSignature("Svc", "B", []), new Returned(null), bChildren.AsReadOnly(), 0);
        aChildren.Add(b);
        bChildren.Add(a);

        var json = JsonExporter.Export(new TraceTree([a]), DefaultMetadata);

        var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.TryGetProperty("events", out _));
    }

    private static List<JsonElement> ParseEvents(TraceTree tree)
    {
        var json = JsonExporter.Export(tree, DefaultMetadata);
        var doc = JsonDocument.Parse(json);
        var events = doc.RootElement.GetProperty("events");
        var list = new List<JsonElement>();
        foreach (var e in events.EnumerateArray())
        {
            list.Add(e);
        }

        return list;
    }
}
