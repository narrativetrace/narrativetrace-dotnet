// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Diagnostics;
using NarrativeTrace.Core;
using NarrativeTrace.Observability;
using Xunit;

namespace NarrativeTrace.Observability.Tests;

public sealed class OtelTraceEventListenerTests : IDisposable
{
    private readonly ActivitySource _source =
        new("NarrativeTrace.OtelListenerTests");

    private readonly ActivityListener _listener;
    private readonly List<Activity> _stopped = [];
    private readonly OtelTraceEventListener _sut;

    public OtelTraceEventListenerTests()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = s => s == _source,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllData,
            ActivityStopped = a => _stopped.Add(a),
        };
        ActivitySource.AddActivityListener(_listener);
        _sut = new OtelTraceEventListener(_source);
    }

    public void Dispose()
    {
        _listener.Dispose();
        _source.Dispose();
    }

    [Fact]
    public void Enter_then_exit_produces_one_span_named_class_dot_method()
    {
        var sc = RootContext();

        _sut.OnEvent(new EnterEvent(
            sc, 1_000_000, new MethodSignature("OrderService", "PlaceOrder", [])));
        _sut.OnEvent(new ExitEvent(
            sc, 6_000_000, new Returned("\"ok\"")));

        Assert.Single(_stopped);
        Assert.Equal("OrderService.PlaceOrder", _stopped[0].DisplayName);
    }

    [Fact]
    public void Interleaved_enters_and_exits_each_produce_their_own_span()
    {
        var a = RootContext();
        var b = RootContext();

        _sut.OnEvent(new EnterEvent(a, 1_000_000, new MethodSignature("A", "Work", [])));
        _sut.OnEvent(new EnterEvent(b, 1_000_100, new MethodSignature("B", "Work", [])));
        _sut.OnEvent(new ExitEvent(a, 5_000_000, new Returned("\"a\"")));
        _sut.OnEvent(new ExitEvent(b, 6_000_000, new Returned("\"b\"")));

        Assert.Equal(2, _stopped.Count);
        Assert.Contains(_stopped, s => s.DisplayName == "A.Work");
        Assert.Contains(_stopped, s => s.DisplayName == "B.Work");
    }

    [Fact]
    public void Nested_calls_link_child_span_to_parent()
    {
        var parent = RootContext();
        var child = ChildContext(parent);

        _sut.OnEvent(new EnterEvent(
            parent, 1_000_000, new MethodSignature("OrderService", "PlaceOrder", [])));
        _sut.OnEvent(new EnterEvent(
            child, 1_000_100, new MethodSignature("PaymentService", "Charge", [])));
        _sut.OnEvent(new ExitEvent(child, 2_000_000, new Returned("\"paid\"")));
        _sut.OnEvent(new ExitEvent(parent, 5_000_000, new Returned("\"ok\"")));

        Assert.Equal(2, _stopped.Count);
        var outer = _stopped.First(s => s.DisplayName == "OrderService.PlaceOrder");
        var inner = _stopped.First(s => s.DisplayName == "PaymentService.Charge");
        Assert.Equal(outer.SpanId, inner.ParentSpanId);
    }

    [Fact]
    public void Interleaved_independent_roots_have_no_parent()
    {
        var a = RootContext();
        var b = RootContext();

        _sut.OnEvent(new EnterEvent(a, 1_000_000, new MethodSignature("A", "Work", [])));
        _sut.OnEvent(new EnterEvent(b, 1_000_100, new MethodSignature("B", "Work", [])));
        _sut.OnEvent(new ExitEvent(a, 5_000_000, new Returned("\"a\"")));
        _sut.OnEvent(new ExitEvent(b, 6_000_000, new Returned("\"b\"")));

        var spanA = _stopped.First(s => s.DisplayName == "A.Work");
        var spanB = _stopped.First(s => s.DisplayName == "B.Work");
        Assert.Equal("0000000000000000", spanA.ParentSpanId.ToString());
        Assert.Equal("0000000000000000", spanB.ParentSpanId.ToString());
    }

    [Fact]
    public void Span_has_class_and_method_attributes()
    {
        EmitEnterExit(RootContext(), "Svc", "Run", "\"ok\"");

        var tags = TagsOf("Svc.Run");
        Assert.Equal("Svc", tags["narrative.class"]);
        Assert.Equal("Run", tags["narrative.method"]);
    }

    [Fact]
    public void Returned_outcome_is_written_as_tag()
    {
        EmitEnterExit(RootContext(), "Svc", "Calc", "42");

        Assert.Equal("42", TagsOf("Svc.Calc")["narrative.outcome"]);
    }

    [Fact]
    public void Void_return_writes_no_outcome_tag()
    {
        var sc = RootContext();
        _sut.OnEvent(new EnterEvent(sc, 1_000_000, new MethodSignature("Svc", "DoIt", [])));
        _sut.OnEvent(new ExitEvent(sc, 2_000_000, new Returned(null)));

        Assert.False(TagsOf("Svc.DoIt").ContainsKey("narrative.outcome"));
    }

    [Fact]
    public void Thrown_outcome_sets_error_status_with_message_and_no_outcome_tag()
    {
        var sc = RootContext();
        _sut.OnEvent(new EnterEvent(sc, 1_000_000, new MethodSignature("Svc", "Fail", [])));
        _sut.OnEvent(new ExitEvent(
            sc, 2_000_000, new Threw(new InvalidOperationException("boom"))));

        var span = _stopped.First(s => s.DisplayName == "Svc.Fail");
        Assert.Equal(ActivityStatusCode.Error, span.Status);
        Assert.Equal("boom", span.StatusDescription);
        Assert.DoesNotContain(span.Tags, t => t.Key == "narrative.outcome");
    }

    [Fact]
    public void Span_writes_parameter_tags_and_omits_redacted_and_empty()
    {
        var sc = RootContext();
        var sig = new MethodSignature("Auth", "Login", [
            new ParameterCapture("user", "alice", false),
            new ParameterCapture("password", "[REDACTED]", true),
            new ParameterCapture("note", "", false),
        ]);

        _sut.OnEvent(new EnterEvent(sc, 1_000_000, sig));
        _sut.OnEvent(new ExitEvent(sc, 2_000_000, new Returned("\"ok\"")));

        var tags = TagsOf("Auth.Login");
        Assert.Equal("alice", tags["narrative.param.user"]);
        Assert.False(tags.ContainsKey("narrative.param.password"));
        Assert.False(tags.ContainsKey("narrative.param.note"));
    }

    [Fact]
    public void Span_duration_reflects_enter_to_exit_timestamp_delta()
    {
        var sc = RootContext();
        var tenthOfSecond = Stopwatch.Frequency / 10;

        _sut.OnEvent(new EnterEvent(sc, 1_000_000, new MethodSignature("Svc", "Run", [])));
        _sut.OnEvent(new ExitEvent(
            sc, 1_000_000 + tenthOfSecond, new Returned("\"ok\"")));

        var span = _stopped.First(s => s.DisplayName == "Svc.Run");
        Assert.InRange(
            span.Duration,
            TimeSpan.FromMilliseconds(90),
            TimeSpan.FromMilliseconds(110));
    }

    [Fact]
    public void Root_span_gets_identity_and_trace_level_tags()
    {
        var sc = RichRootContext();

        EmitEnterExit(sc, "Svc", "Handle", "\"ok\"");

        var tags = TagsOf("Svc.Handle");
        Assert.Equal(sc.TraceId.Value, tags["narrative.trace_id"]);
        Assert.Equal("entry", tags["nt.entryType"]);
        Assert.Equal("1.2", tags["nt.schemaVersion"]);
        Assert.Equal("order-svc", tags["narrative.service.name"]);
        Assert.Equal("1.0.0", tags["narrative.service.version"]);
        Assert.Equal("test", tags["narrative.service.environment"]);
        Assert.Equal("POST", tags["narrative.http.method"]);
        Assert.Equal("/api/orders", tags["narrative.http.route"]);
        Assert.Equal("10.0.0.1", tags["narrative.client_ip"]);
        Assert.Equal("user-42", tags["narrative.enduser.id"]);
        Assert.Equal("sess-9", tags["narrative.session.id"]);
        Assert.Equal("tenant-a", tags["narrative.tenant.id"]);
    }

    [Fact]
    public void Child_span_omits_trace_level_tags_but_keeps_identity()
    {
        var parent = RichRootContext();
        var child = ChildContext(parent);

        _sut.OnEvent(new EnterEvent(parent, 1_000_000, new MethodSignature("Svc", "Handle", [])));
        _sut.OnEvent(new EnterEvent(child, 1_000_100, new MethodSignature("Repo", "Find", [])));
        _sut.OnEvent(new ExitEvent(child, 1_500_000, new Returned("\"ok\"")));
        _sut.OnEvent(new ExitEvent(parent, 2_000_000, new Returned("\"done\"")));

        var childTags = TagsOf("Repo.Find");
        Assert.False(childTags.ContainsKey("narrative.service.name"));
        Assert.False(childTags.ContainsKey("narrative.http.method"));
        Assert.True(childTags.ContainsKey("narrative.trace_id"));
    }

    [Fact]
    public void Non_enter_exit_events_produce_no_span()
    {
        var node = new TraceNode(
            new MethodSignature("X", "Y", []), new Returned(null), [], 0);

        _sut.OnEvent(new GraftEvent(null, 1_000_000, node));

        Assert.Empty(_stopped);
    }

    [Fact]
    public void Exit_without_matching_enter_creates_orphan_error_span()
    {
        var sc = RichRootContext();

        _sut.OnEvent(new ExitEvent(sc, 1_000_000, new Returned("\"ok\"")));

        var orphan = Assert.Single(_stopped);
        Assert.Equal(ActivityStatusCode.Error, orphan.Status);
        Assert.Contains("enter event lost", orphan.StatusDescription);
        var tags = orphan.Tags.ToDictionary(t => t.Key, t => t.Value);
        Assert.Equal("order-svc", tags["narrative.service.name"]);
        Assert.Equal("POST", tags["narrative.http.method"]);
        Assert.Equal("\"ok\"", tags["narrative.outcome"]);
        // Enter time is unknown, so the orphan span is stamped instantaneous at
        // the exit moment rather than running until it is stopped "now".
        Assert.True(
            orphan.Duration < TimeSpan.FromSeconds(1),
            $"orphan duration should be ~0, was {orphan.Duration}");
    }

    [Fact]
    public void No_registered_listener_produces_no_span_and_does_not_throw()
    {
        using var quietSource = new ActivitySource("NarrativeTrace.Unlistened");
        var listener = new OtelTraceEventListener(quietSource);
        var sc = RootContext();

        var enter = new EnterEvent(sc, 1_000_000, new MethodSignature("Svc", "Run", []));
        var exit = new ExitEvent(sc, 2_000_000, new Returned("\"ok\""));
        var orphanExit = new ExitEvent(RootContext(), 3_000_000, new Returned("\"x\""));

        listener.OnEvent(enter);
        listener.OnEvent(exit);
        listener.OnEvent(orphanExit);

        Assert.Empty(_stopped);
    }

    [Fact]
    public void Over_capacity_enter_evicts_oldest_active_span_as_orphan()
    {
        var bounded = new OtelTraceEventListener(
            _source, maxActiveSpans: 1, ttl: TimeSpan.FromHours(1));

        bounded.OnEvent(new EnterEvent(
            RootContext(), 1_000, new MethodSignature("Old", "Stale", [])));
        bounded.OnEvent(new EnterEvent(
            RootContext(), 2_000, new MethodSignature("New", "Fresh", [])));

        var evicted = Assert.Single(_stopped);
        Assert.Equal("Old.Stale", evicted.DisplayName);
        Assert.Equal(ActivityStatusCode.Error, evicted.Status);
        Assert.Contains("orphan", evicted.StatusDescription);
    }

    [Fact]
    public void Expired_active_span_is_evicted_as_orphan_on_next_enter()
    {
        var shortTtl = new OtelTraceEventListener(
            _source, maxActiveSpans: 1024, ttl: TimeSpan.FromTicks(1));

        shortTtl.OnEvent(new EnterEvent(
            RootContext(), 1_000_000, new MethodSignature("Svc", "Slow", [])));
        shortTtl.OnEvent(new EnterEvent(
            RootContext(), 2_000_000, new MethodSignature("Svc", "Next", [])));

        var orphan = _stopped.FirstOrDefault(s => s.DisplayName == "Svc.Slow");
        Assert.NotNull(orphan);
        Assert.Equal(ActivityStatusCode.Error, orphan.Status);
    }

    [Fact]
    public void Parent_exiting_before_child_does_not_crash_and_emits_both()
    {
        var parent = RootContext();
        var child = ChildContext(parent);

        _sut.OnEvent(new EnterEvent(parent, 1_000_000, new MethodSignature("Root", "Handle", [])));
        _sut.OnEvent(new EnterEvent(child, 1_000_100, new MethodSignature("Svc", "Process", [])));
        _sut.OnEvent(new ExitEvent(parent, 1_500_000, new Returned("\"done\"")));
        _sut.OnEvent(new ExitEvent(child, 2_000_000, new Returned("\"ok\"")));

        Assert.Equal(2, _stopped.Count);
    }

    [Fact]
    public void Live_parent_span_gets_child_completion_event_on_child_exit()
    {
        var parent = RootContext();
        var child = ChildContext(parent);

        _sut.OnEvent(new EnterEvent(
            parent, 1_000_000, new MethodSignature("Root", "Handle", [])));
        _sut.OnEvent(new EnterEvent(
            child, 1_000_100, new MethodSignature("Svc", "Process", [
                new ParameterCapture("id", "7", false),
            ])));
        _sut.OnEvent(new ExitEvent(child, 2_000_000, new Returned("\"ok\"")));
        _sut.OnEvent(new ExitEvent(parent, 5_000_000, new Returned("\"done\"")));

        var parentSpan = _stopped.First(s => s.DisplayName == "Root.Handle");
        var evt = Assert.Single(parentSpan.Events);
        Assert.Equal("Svc.Process", evt.Name);
        var tags = evt.Tags.ToDictionary(t => t.Key, t => t.Value);
        Assert.Equal(7L, tags["narrative.param.id"]);
        Assert.Equal("\"ok\"", tags["narrative.outcome"]);
    }

    [Fact]
    public void On_event_is_usable_as_a_pipeline_subscriber_delegate()
    {
        Action<TraceEvent> subscriber = _sut.OnEvent;
        var sc = RootContext();

        subscriber(new EnterEvent(sc, 1_000_000, new MethodSignature("Svc", "Run", [])));
        subscriber(new ExitEvent(sc, 2_000_000, new Returned("\"ok\"")));

        Assert.Single(_stopped);
    }

    private void EmitEnterExit(
        SpanContext sc, string className, string methodName, string returnValue)
    {
        _sut.OnEvent(new EnterEvent(
            sc, 1_000_000, new MethodSignature(className, methodName, [])));
        _sut.OnEvent(new ExitEvent(sc, 2_000_000, new Returned(returnValue)));
    }

    private Dictionary<string, string?> TagsOf(string displayName)
    {
        return _stopped.First(s => s.DisplayName == displayName)
            .Tags.ToDictionary(t => t.Key, t => t.Value);
    }

    private static SpanContext RootContext()
    {
        return new SpanContext(
            SpanIdGenerator.GenerateTraceId(),
            SpanIdGenerator.GenerateSpanId(),
            null);
    }

    private static SpanContext ChildContext(SpanContext parent)
    {
        return new SpanContext(
            parent.TraceId, SpanIdGenerator.GenerateSpanId(), parent.SpanId);
    }

    private static SpanContext RichRootContext()
    {
        return new SpanContext(
            SpanIdGenerator.GenerateTraceId(),
            SpanIdGenerator.GenerateSpanId(),
            null,
            ServiceName: "order-svc",
            ServiceVersion: "1.0.0",
            Environment: "test")
        {
            HttpMethod = "POST",
            HttpRoute = new HttpRoute("/api/orders"),
            ClientIp = new ClientIp("10.0.0.1"),
            EnduserId = new EnduserId("user-42"),
            SessionId = new SessionId("sess-9"),
            TenantId = new TenantId("tenant-a"),
        };
    }
}
