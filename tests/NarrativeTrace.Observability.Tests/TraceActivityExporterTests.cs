// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Diagnostics;
using NarrativeTrace.Core;
using NarrativeTrace.Observability;
using Xunit;

namespace NarrativeTrace.Observability.Tests;

public sealed class TraceActivityExporterTests : IDisposable
{
    private readonly ActivityListener _listener;
    private readonly List<Activity> _activities = [];

    public TraceActivityExporterTests()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = s => s.Name == "NarrativeTrace",
            Sample = (ref ActivityCreationOptions<
                ActivityContext> _) =>
                ActivitySamplingResult.AllData,
            ActivityStopped = a => _activities.Add(a),
        };
        ActivitySource.AddActivityListener(_listener);
    }

    public void Dispose()
    {
        _listener.Dispose();
    }

    [Fact]
    public void Export_without_a_registered_listener_does_not_throw()
    {
        _listener.Dispose();
        var child = new TraceNode(
            new MethodSignature("Repo", "Save", []), new Returned(null), [], 0);
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Svc", "Run", []),
                new Returned(null), [child], 0),
        ]);

        var ex = Record.Exception(() => TraceActivityExporter.Export(tree));

        Assert.Null(ex);
        Assert.Empty(_activities);
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

        var ex = Record.Exception(() => TraceActivityExporter.Export(new TraceTree([a])));

        Assert.Null(ex);
        Assert.Contains(
            _activities,
            activity => activity.OperationName.Contains(TreeWalk.CycleMarker, StringComparison.Ordinal));
    }

    [Fact]
    public void Exports_trace_nodes_as_activities()
    {
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature(
                    "OrderService", "placeOrder", []),
                new Returned(null), [], 1000),
        ]);

        TraceActivityExporter.Export(tree);

        Assert.Single(_activities);
        Assert.Equal(
            "OrderService.placeOrder",
            _activities[0].DisplayName);
    }

    [Fact]
    public void Parent_span_gets_one_completion_event_per_child()
    {
        var childA = new TraceNode(
            new MethodSignature("Repo", "Save", []), new Returned(null), [], 0);
        var childB = new TraceNode(
            new MethodSignature("Cache", "Put", []), new Returned(null), [], 0);
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Svc", "Run", []),
                new Returned(null), [childA, childB], 0),
        ]);

        TraceActivityExporter.Export(tree);

        var parent = _activities.First(a => a.DisplayName == "Svc.Run");
        var names = parent.Events.Select(e => e.Name).ToList();
        Assert.Equal(2, names.Count);
        Assert.Contains("Repo.Save", names);
        Assert.Contains("Cache.Put", names);
    }

    [Fact]
    public void Child_completion_event_carries_outcome_attribute()
    {
        var ok = new TraceNode(
            new MethodSignature("Repo", "Save", []),
            new Returned("\"saved\""), [], 0);
        var boom = new TraceNode(
            new MethodSignature("Cache", "Put", []),
            new Threw(new InvalidOperationException("down")), [], 0);
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Svc", "Run", []),
                new Returned(null), [ok, boom], 0),
        ]);

        TraceActivityExporter.Export(tree);

        var parent = _activities.First(a => a.DisplayName == "Svc.Run");
        Assert.Equal(
            "\"saved\"",
            EventTag(parent, "Repo.Save", "narrative.outcome"));
        Assert.Equal(
            "error: down",
            EventTag(parent, "Cache.Put", "narrative.outcome"));
    }

    [Fact]
    public void Child_completion_event_carries_typed_param_attributes()
    {
        var child = new TraceNode(
            new MethodSignature("Repo", "Save", [
                new ParameterCapture("id", "42", false),
            ]),
            new Returned(null), [], 0);
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Svc", "Run", []),
                new Returned(null), [child], 0),
        ]);

        TraceActivityExporter.Export(tree);

        var parent = _activities.First(a => a.DisplayName == "Svc.Run");
        Assert.Equal(42L, EventTag(parent, "Repo.Save", "narrative.param.id"));
    }

    [Fact]
    public void Child_completion_event_timestamp_is_child_end_time()
    {
        var child = new TraceNode(
            new MethodSignature("Repo", "Save", []),
            new Returned(null), [], 5 * TimeSpan.TicksPerMillisecond,
            StartTimestamp: 3_000_000);
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Svc", "Run", []),
                new Returned(null), [child], 10 * TimeSpan.TicksPerMillisecond,
                StartTimestamp: 1_000_000),
        ]);

        TraceActivityExporter.Export(tree);

        var parent = _activities.First(a => a.DisplayName == "Svc.Run");
        var childSpan = _activities.First(a => a.DisplayName == "Repo.Save");
        var evt = parent.Events.First(e => e.Name == "Repo.Save");
        Assert.Equal(childSpan.StartTimeUtc.Add(childSpan.Duration),
            evt.Timestamp.UtcDateTime);
    }

    [Fact]
    public void Preserves_parent_child_structure()
    {
        var child = new TraceNode(
            new MethodSignature("Repo", "save", []),
            new Returned(null), [], 500);
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature(
                    "Svc", "place", []),
                new Returned(null), [child], 1000),
        ]);

        TraceActivityExporter.Export(tree);

        Assert.Equal(2, _activities.Count);
        var parent = _activities.First(
            a => a.DisplayName == "Svc.place");
        var c = _activities.First(
            a => a.DisplayName == "Repo.save");
        Assert.Equal(parent.Id, c.ParentId);
    }

    [Fact]
    public void Incomplete_outcome_sets_unset_status()
    {
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Svc", "Run", []),
                new Incomplete(), [], 0),
        ]);

        TraceActivityExporter.Export(tree);

        Assert.Single(_activities);
        var tags = _activities[0].Tags.ToDictionary(
            t => t.Key, t => t.Value);
        Assert.Equal("in-flight",
            tags["narrative.outcome"]);
        Assert.Equal(
            ActivityStatusCode.Unset,
            _activities[0].Status);
    }

    [Fact]
    public void Exports_string_parameter_as_string_tag()
    {
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature(
                    "Svc", "run",
                    [new ParameterCapture(
                        "name", "alice", false)]),
                new Returned(null), [], 100),
        ]);

        TraceActivityExporter.Export(tree);

        Assert.Equal("alice",
            _activities[0].GetTagItem("narrative.param.name"));
    }

    [Fact]
    public void Structured_long_parameter_overrides_string_inference()
    {
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature(
                    "Svc", "run",
                    [new ParameterCapture(
                        "count", "five", false,
                        new RenderedValue.LongVal(5))]),
                new Returned(null), [], 100),
        ]);

        TraceActivityExporter.Export(tree);

        Assert.Equal(5L,
            _activities[0].GetTagItem("narrative.param.count"));
    }

    [Fact]
    public void Structured_object_parameter_is_dot_flattened()
    {
        var obj = new RenderedValue.ObjectVal("Addr",
            new Dictionary<string, RenderedValue>
            {
                ["city"] = new RenderedValue.StringVal("Oslo"),
                ["zip"] = new RenderedValue.LongVal(1234),
            });
        var tree = SingleParamTree("addr", obj);

        TraceActivityExporter.Export(tree);

        Assert.Equal("Oslo",
            _activities[0].GetTagItem("narrative.param.addr.city"));
        Assert.Equal(1234L,
            _activities[0].GetTagItem("narrative.param.addr.zip"));
    }

    [Fact]
    public void Structured_object_flatten_sets_depth_three_scalar_but_not_deeper()
    {
        var deep = new RenderedValue.ObjectVal("A",
            new Dictionary<string, RenderedValue>
            {
                ["b"] = new RenderedValue.ObjectVal("B",
                    new Dictionary<string, RenderedValue>
                    {
                        ["c"] = new RenderedValue.ObjectVal("C",
                            new Dictionary<string, RenderedValue>
                            {
                                ["x"] = new RenderedValue.LongVal(7),
                                ["d"] = new RenderedValue.ObjectVal("D",
                                    new Dictionary<string, RenderedValue>
                                    {
                                        ["e"] = new RenderedValue.LongVal(9),
                                    }),
                            }),
                    }),
            });
        var tree = SingleParamTree("root", deep);

        TraceActivityExporter.Export(tree);

        Assert.Equal(7L,
            _activities[0].GetTagItem("narrative.param.root.b.c.x"));
        Assert.Null(
            _activities[0].GetTagItem("narrative.param.root.b.c.d.e"));
    }

    [Fact]
    public void Homogeneous_string_list_parameter_becomes_string_array_tag()
    {
        var list = new RenderedValue.ListVal([
            new RenderedValue.StringVal("a"),
            new RenderedValue.StringVal("b"),
        ]);
        var tree = SingleParamTree("tags", list);

        TraceActivityExporter.Export(tree);

        Assert.Equal(
            new[] { "a", "b" },
            _activities[0].GetTagItem("narrative.param.tags"));
    }

    [Fact]
    public void Heterogeneous_list_parameter_is_skipped()
    {
        var list = new RenderedValue.ListVal([
            new RenderedValue.StringVal("a"),
            new RenderedValue.LongVal(1),
        ]);
        var tree = SingleParamTree("mixed", list);

        TraceActivityExporter.Export(tree);

        Assert.Null(_activities[0].GetTagItem("narrative.param.mixed"));
    }

    [Fact]
    public void Structured_instant_parameter_is_emitted_as_epoch_millis_long()
    {
        var tree = SingleParamTree(
            "at", new RenderedValue.InstantVal(1_700_000_000_000));

        TraceActivityExporter.Export(tree);

        Assert.Equal(1_700_000_000_000L,
            _activities[0].GetTagItem("narrative.param.at"));
    }

    [Fact]
    public void Empty_structured_list_parameter_is_skipped()
    {
        var tree = SingleParamTree("empty", new RenderedValue.ListVal([]));

        TraceActivityExporter.Export(tree);

        Assert.Null(_activities[0].GetTagItem("narrative.param.empty"));
    }

    [Fact]
    public void Incomplete_child_completion_event_has_no_outcome_tag()
    {
        var child = new TraceNode(
            new MethodSignature("Repo", "Save", []),
            new Incomplete(), [], 0);
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Svc", "Run", []),
                new Returned(null), [child], 0),
        ]);

        TraceActivityExporter.Export(tree);

        var parent = _activities.First(a => a.DisplayName == "Svc.Run");
        var evt = parent.Events.First(e => e.Name == "Repo.Save");
        Assert.DoesNotContain(evt.Tags, t => t.Key == "narrative.outcome");
    }

    [Fact]
    public void Child_completion_event_omits_redacted_and_empty_params()
    {
        var child = new TraceNode(
            new MethodSignature("Auth", "Login", [
                new ParameterCapture("password", "[REDACTED]", true),
                new ParameterCapture("note", "", false),
                new ParameterCapture("user", "\"alice\"", false),
            ]),
            new Returned(null), [], 0);
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Svc", "Run", []),
                new Returned(null), [child], 0),
        ]);

        TraceActivityExporter.Export(tree);

        var parent = _activities.First(a => a.DisplayName == "Svc.Run");
        var evt = parent.Events.First(e => e.Name == "Auth.Login");
        Assert.DoesNotContain(evt.Tags, t => t.Key == "narrative.param.password");
        Assert.DoesNotContain(evt.Tags, t => t.Key == "narrative.param.note");
        Assert.Equal("alice", EventTag(parent, "Auth.Login", "narrative.param.user"));
    }

    [Fact]
    public void Boolean_string_parameter_is_emitted_as_bool_typed_tag()
    {
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature(
                    "Svc", "run",
                    [new ParameterCapture("active", "true", false)]),
                new Returned(null), [], 100),
        ]);

        TraceActivityExporter.Export(tree);

        var tag = Assert.IsType<bool>(
            _activities[0].GetTagItem("narrative.param.active"));
        Assert.True(tag);
    }

    [Fact]
    public void Decimal_string_parameter_is_emitted_as_double_typed_tag()
    {
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature(
                    "Svc", "run",
                    [new ParameterCapture("ratio", "3.14", false)]),
                new Returned(null), [], 100),
        ]);

        TraceActivityExporter.Export(tree);

        Assert.Equal(3.14,
            _activities[0].GetTagItem("narrative.param.ratio"));
    }

    [Fact]
    public void Quoted_string_parameter_is_unwrapped_to_string_tag()
    {
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature(
                    "Svc", "run",
                    [new ParameterCapture("name", "\"alice\"", false)]),
                new Returned(null), [], 100),
        ]);

        TraceActivityExporter.Export(tree);

        Assert.Equal("alice",
            _activities[0].GetTagItem("narrative.param.name"));
    }

    [Fact]
    public void Numeric_string_parameter_is_emitted_as_long_typed_tag()
    {
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature(
                    "Svc", "run",
                    [new ParameterCapture(
                        "id", "42", false)]),
                new Returned(null), [], 100),
        ]);

        TraceActivityExporter.Export(tree);

        Assert.Equal(42L,
            _activities[0].GetTagItem("narrative.param.id"));
    }

    [Fact]
    public void Exported_node_emits_duration_ms_tag_from_node_duration()
    {
        var fiveMs = 5 * TimeSpan.TicksPerMillisecond;
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Svc", "Run", []),
                new Returned(null), [], fiveMs),
        ]);

        TraceActivityExporter.Export(tree);

        Assert.Equal(5.0, _activities[0].GetTagItem("narrative.duration_ms"));
    }

    [Fact]
    public void Exported_activity_duration_is_anchored_to_node_duration()
    {
        var fiveMs = 5 * TimeSpan.TicksPerMillisecond;
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Svc", "Run", []),
                new Returned(null), [], fiveMs, StartTimestamp: 7_000_000),
        ]);

        TraceActivityExporter.Export(tree);

        Assert.Equal(TimeSpan.FromTicks(fiveMs), _activities[0].Duration);
    }

    [Fact]
    public void Sequential_node_has_no_concurrency_tags()
    {
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Svc", "Run", []),
                new Returned(null), [], 0),
        ]);

        TraceActivityExporter.Export(tree);

        var tags = _activities[0].Tags.ToDictionary(
            t => t.Key, t => t.Value);
        Assert.False(
            tags.ContainsKey(
                "narrative.concurrency.groupId"));
    }

    [Fact]
    public void Concurrent_node_has_groupId_tag()
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

        TraceActivityExporter.Export(tree);

        var tags = _activities[0].Tags.ToDictionary(
            t => t.Key, t => t.Value);
        Assert.Equal("fork-1",
            tags["narrative.concurrency.groupId"]);
    }

    [Fact]
    public void Concurrent_node_has_kind_tag()
    {
        var info = MakeForkInfo("fork-1");
        var tree = MakeSingleNodeTree(info);

        TraceActivityExporter.Export(tree);

        var tags = TagsDict();
        Assert.Equal("ForkJoin",
            tags["narrative.concurrency.kind"]);
    }

    [Fact]
    public void Concurrent_node_has_taskLabel_tag()
    {
        var info = MakeForkInfo("fork-1");
        var tree = MakeSingleNodeTree(info);

        TraceActivityExporter.Export(tree);

        var tags = TagsDict();
        Assert.Equal("Svc.Run",
            tags["narrative.concurrency.taskLabel"]);
    }

    [Fact]
    public void Concurrent_node_has_threadId_tag()
    {
        var info = MakeForkInfo("fork-1");
        var tree = MakeSingleNodeTree(info);

        TraceActivityExporter.Export(tree);

        var tags = TagsDict();
        Assert.Equal("4",
            tags["narrative.concurrency.threadId"]);
    }

    [Fact]
    public void Concurrent_node_omits_virtual_tag_by_decision()
    {
        var info = MakeForkInfo("fork-1");
        var tree = MakeSingleNodeTree(info);

        TraceActivityExporter.Export(tree);

        Assert.False(
            TagsDict().ContainsKey("narrative.concurrency.virtual"));
    }

    [Fact]
    public void Concurrent_node_has_threadName_tag()
    {
        var info = MakeForkInfo("fork-1");
        var tree = MakeSingleNodeTree(info);

        TraceActivityExporter.Export(tree);

        Assert.Equal("Worker", TagsDict()["narrative.concurrency.threadName"]);
    }

    [Fact]
    public void FireAndForget_node_has_correct_kind_tag()
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

        TraceActivityExporter.Export(tree);

        var tags = TagsDict();
        Assert.Equal("FireAndForget",
            tags["narrative.concurrency.kind"]);
    }

    [Fact]
    public void Fork_members_share_same_groupId_tag()
    {
        var info = MakeForkInfo("fork-1");
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

        TraceActivityExporter.Export(tree);

        Assert.Equal(2, _activities.Count);
        var g1 = _activities[0].Tags.First(
            t => t.Key ==
                "narrative.concurrency.groupId")
            .Value;
        var g2 = _activities[1].Tags.First(
            t => t.Key ==
                "narrative.concurrency.groupId")
            .Value;
        Assert.Equal(g1, g2);
    }

    [Fact]
    public void Activity_status_set_for_cancelled_tasks()
    {
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Svc", "Run", []),
                new Threw(
                    new OperationCanceledException()),
                [], 0),
        ]);

        TraceActivityExporter.Export(tree);

        Assert.Equal(
            ActivityStatusCode.Error,
            _activities[0].Status);
    }

    [Fact]
    public void Thrown_outcome_sets_error_status_with_exception_message_as_description()
    {
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Svc", "Run", []),
                new Threw(new InvalidOperationException("boom")), [], 0),
        ]);

        TraceActivityExporter.Export(tree);

        Assert.Equal(ActivityStatusCode.Error, _activities[0].Status);
        Assert.Equal("boom", _activities[0].StatusDescription);
    }

    [Fact]
    public void Returned_null_sets_no_narrative_outcome_tag()
    {
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Svc", "DoIt", []),
                new Returned(null), [], 0),
        ]);

        TraceActivityExporter.Export(tree);

        Assert.False(TagsDict().ContainsKey("narrative.outcome"));
    }

    [Fact]
    public void Thrown_outcome_sets_no_narrative_outcome_tag()
    {
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Svc", "Run", []),
                new Threw(new InvalidOperationException("boom")), [], 0),
        ]);

        TraceActivityExporter.Export(tree);

        Assert.False(TagsDict().ContainsKey("narrative.outcome"));
    }

    [Fact]
    public void Thrown_outcome_records_an_exception_event()
    {
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Svc", "Run", []),
                new Threw(new InvalidOperationException("boom")), [], 0),
        ]);

        TraceActivityExporter.Export(tree);

        var evt = Assert.Single(_activities[0].Events);
        Assert.Equal("exception", evt.Name);
        var tags = evt.Tags.ToDictionary(t => t.Key, t => t.Value);
        Assert.Equal("System.InvalidOperationException", tags["exception.type"]);
        Assert.Equal("boom", tags["exception.message"]);
        Assert.True(tags.ContainsKey("exception.stacktrace"));
    }

    [Fact]
    public void Redacted_and_empty_parameters_are_omitted_from_tags()
    {
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Auth", "Login",
                [
                    new ParameterCapture("password", "[REDACTED]", true),
                    new ParameterCapture("note", "", false),
                    new ParameterCapture("user", "alice", false),
                ]),
                new Returned(null), [], 0),
        ]);

        TraceActivityExporter.Export(tree);

        var tags = TagsDict();
        Assert.False(tags.ContainsKey("narrative.param.password"));
        Assert.False(tags.ContainsKey("narrative.param.note"));
        Assert.Equal("alice", tags["narrative.param.user"]);
    }

    [Fact]
    public void Span_with_context_gets_trace_identity_and_schema_tags()
    {
        TraceActivityExporter.Export(TreeWithContext());

        var tags = TagsDict();
        Assert.Equal(
            "0af7651916cd43dd8448eb211c80319c",
            tags["narrative.trace_id"]);
        Assert.Equal(
            new TraceId("0af7651916cd43dd8448eb211c80319c").HumanName,
            tags["narrative.trace_name"]);
        Assert.Equal("entry", tags["nt.entryType"]);
        Assert.Equal("1.2", tags["nt.schemaVersion"]);
        Assert.Equal("story-1", tags["nt.storyId"]);
        Assert.Equal("chap-1", tags["nt.chapterId"]);
    }

    [Fact]
    public void Root_span_gets_trace_level_attributes()
    {
        TraceActivityExporter.Export(TreeWithContext());

        var tags = TagsDict();
        Assert.Equal("OrderSvc", tags["narrative.service.name"]);
        Assert.Equal("test", tags["narrative.service.environment"]);
        Assert.Equal("GET", tags["narrative.http.method"]);
        Assert.Equal("/orders", tags["narrative.http.route"]);
    }

    [Fact]
    public void Child_span_omits_trace_level_attributes()
    {
        var child = new TraceNode(
            new MethodSignature("Repo", "Save", []),
            new Returned(null), [], 0,
            SpanContext: MakeContext());
        var root = new TraceNode(
            new MethodSignature("Svc", "Run", []),
            new Returned(null), [child], 0,
            SpanContext: MakeContext());

        TraceActivityExporter.Export(new TraceTree([root]));

        var childTags = _activities[0].Tags.ToDictionary(
            t => t.Key, t => t.Value);
        Assert.False(
            childTags.ContainsKey("narrative.service.name"));
        Assert.True(childTags.ContainsKey("narrative.trace_id"));
    }

    [Fact]
    public void Span_without_context_omits_identity_tags()
    {
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Svc", "Run", []),
                new Returned(null), [], 0),
        ]);

        TraceActivityExporter.Export(tree);

        Assert.False(TagsDict().ContainsKey("narrative.trace_id"));
    }

    private static SpanContext MakeContext()
    {
        return new SpanContext(
            new TraceId("0af7651916cd43dd8448eb211c80319c"),
            new SpanId("b7ad6b7169203331"),
            null,
            ServiceName: "OrderSvc",
            Environment: "test")
        {
            StoryId = "story-1",
            ChapterId = "chap-1",
            HttpMethod = "GET",
            HttpRoute = new HttpRoute("/orders"),
        };
    }

    private static TraceTree TreeWithContext()
    {
        return new TraceTree([
            new TraceNode(
                new MethodSignature("OrderService", "PlaceOrder", []),
                new Returned(null), [], 0,
                SpanContext: MakeContext()),
        ]);
    }

    private static TraceTree SingleParamTree(
        string paramName, RenderedValue structured)
    {
        return new TraceTree([
            new TraceNode(
                new MethodSignature("Svc", "run", [
                    new ParameterCapture(
                        paramName, "rendered", false, structured),
                ]),
                new Returned(null), [], 100),
        ]);
    }

    private static ConcurrencyInfo MakeForkInfo(
        string groupId)
    {
        return new ConcurrencyInfo(
            groupId, "Svc.Run", 4,
            "Worker", true,
            ConcurrencyKind.ForkJoin);
    }

    private static TraceTree MakeSingleNodeTree(
        ConcurrencyInfo info)
    {
        return new TraceTree([
            new TraceNode(
                new MethodSignature("Svc", "Run", []),
                new Returned(null), [], 0,
                Concurrency: info),
        ]);
    }

    private static object? EventTag(
        Activity parent, string eventName, string tagKey)
    {
        var evt = parent.Events.First(e => e.Name == eventName);
        return evt.Tags.First(t => t.Key == tagKey).Value;
    }

    private Dictionary<string, string?> TagsDict()
    {
        return _activities[0].Tags.ToDictionary(
            t => t.Key, t => t.Value);
    }
}
