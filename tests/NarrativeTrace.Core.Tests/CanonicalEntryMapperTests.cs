// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using NarrativeTrace.Runtime;
using Xunit;

namespace NarrativeTrace.Core.Tests;

public class CanonicalEntryMapperTests
{
    private const string TraceHex =
        "0af7651916cd43dd8448eb211c80319c";
    private const string SpanHex = "b7ad6b7169203331";

    private static readonly CanonicalEntryMapper Mapper =
        new(_ => "2026-07-05T00:00:00.000Z");

    private static SpanContext Context(
        string? spanName = "OrderService.PlaceOrder",
        SpanId? parent = null) =>
        new(
            new TraceId(TraceHex),
            new SpanId(SpanHex),
            parent,
            ServiceName: "OrderSvc",
            Environment: "test")
        {
            SpanName = spanName,
            StoryId = "story-1",
            ChapterId = "chap-1",
        };

    [Fact]
    public void Enter_event_maps_to_method_enter()
    {
        var sig = new MethodSignature(
            "OrderService", "PlaceOrder",
            [new ParameterCapture("customerId", "C1", false)]);
        var evt = new EnterEvent(Context(), 123L, sig);

        var entry = Mapper.FromEvent(evt);

        Assert.Equal("2026-07-05T00:00:00.000Z", entry.Timestamp);
        Assert.Equal("method_enter", entry.NtEventType);
        Assert.Equal("trace", entry.Level);
        Assert.Equal("OrderSvc", entry.Service);
        Assert.Equal("test", entry.Environment);
        Assert.Equal(TraceHex, entry.TraceId);
        Assert.Equal(SpanHex, entry.SpanId);
        Assert.Null(entry.ParentSpanId);
        Assert.Equal("OrderService", entry.CodeNamespace);
        Assert.Equal("PlaceOrder", entry.CodeFunction);
        Assert.Equal("story-1", entry.NtStoryId);
        Assert.Equal("chap-1", entry.NtChapterId);
        Assert.Equal("entry", entry.NtEntryType);
        Assert.Equal("1.2", entry.NtSchemaVersion);
    }

    [Fact]
    public void Enter_event_formats_arrow_message_with_parameters()
    {
        var sig = new MethodSignature(
            "OrderService", "PlaceOrder",
            [new ParameterCapture("customerId", "C1", false)]);

        var entry = Mapper.FromEvent(new EnterEvent(Context(), 1L, sig));

        Assert.Equal(
            "→ OrderService.PlaceOrder(customerId: C1)", entry.Message);
    }

    [Fact]
    public void Enter_event_redacts_sensitive_parameter_value()
    {
        var sig = new MethodSignature(
            "Auth", "Login",
            [new ParameterCapture("password", "hunter2", true)]);

        var entry = Mapper.FromEvent(new EnterEvent(Context(), 1L, sig));

        Assert.Equal("[REDACTED]", entry.NtParameters![0].RenderedValue);
        Assert.True(entry.NtParameters[0].Redacted);
    }

    [Fact]
    public void Enter_with_no_parameters_has_null_nt_parameters()
    {
        var sig = new MethodSignature("Svc", "Run", []);

        var entry = Mapper.FromEvent(new EnterEvent(Context(), 1L, sig));

        Assert.Null(entry.NtParameters);
    }

    [Fact]
    public void Enter_message_separates_multiple_parameters()
    {
        var sig = new MethodSignature(
            "Svc", "Do",
            [
                new ParameterCapture("a", "1", false),
                new ParameterCapture("b", "2", false),
            ]);

        var entry = Mapper.FromEvent(new EnterEvent(Context(), 1L, sig));

        Assert.Equal("→ Svc.Do(a: 1, b: 2)", entry.Message);
    }

    [Fact]
    public void Enter_message_shows_redacted_placeholder()
    {
        var sig = new MethodSignature(
            "Auth", "Login",
            [new ParameterCapture("password", "hunter2", true)]);

        var entry = Mapper.FromEvent(new EnterEvent(Context(), 1L, sig));

        Assert.Equal("→ Auth.Login(password: [REDACTED])", entry.Message);
    }

    [Fact]
    public void Enter_preserves_non_redacted_parameter_value()
    {
        var sig = new MethodSignature(
            "Svc", "Do",
            [new ParameterCapture("id", "C1", false)]);

        var entry = Mapper.FromEvent(new EnterEvent(Context(), 1L, sig));

        Assert.Equal("C1", entry.NtParameters![0].RenderedValue);
        Assert.False(entry.NtParameters[0].Redacted);
    }

    [Fact]
    public void Exit_exception_message_includes_type_and_message()
    {
        var evt = new ExitEvent(
            Context(), 5L,
            new Threw(new InvalidOperationException("declined")));

        var entry = Mapper.FromEvent(evt);

        Assert.Equal(
            "!! InvalidOperationException: declined", entry.Message);
    }

    [Fact]
    public void Exit_without_span_name_uses_method_placeholder()
    {
        var evt = new ExitEvent(
            Context(spanName: null), 5L, new Returned(null));

        var entry = Mapper.FromEvent(evt);

        Assert.Equal("← method returned", entry.Message);
        Assert.Equal("", entry.CodeNamespace);
        Assert.Equal("", entry.CodeFunction);
    }

    [Fact]
    public void Exit_span_name_with_leading_dot_keeps_dot_in_function()
    {
        var evt = new ExitEvent(
            Context(spanName: ".Ctor"), 5L, new Returned(null));

        var entry = Mapper.FromEvent(evt);

        Assert.Equal("", entry.CodeNamespace);
        Assert.Equal(".Ctor", entry.CodeFunction);
    }

    [Fact]
    public void Unsupported_event_message_names_the_problem()
    {
        var node = new TraceNode(
            new MethodSignature("Svc", "Run", []),
            new Returned(null), [], 0);
        var graft = new GraftEvent(null, 1L, node);

        var ex = Assert.Throws<ArgumentException>(
            () => Mapper.FromEvent(graft));
        Assert.Contains("event type", ex.Message);
    }

    [Theory]
    [MemberData(nameof(ConcurrencyEvents))]
    public void Concurrency_events_map_to_their_canonical_entry(
        TraceEvent concurrencyEvent, string expectedEventType)
    {
        // Was TESTFX-9, which pinned the gap: these events used to throw, so
        // the canonical stream silently omitted fork/join/async_dispatch that
        // every other port emits. They now map.
        var entry = Mapper.FromEvent(concurrencyEvent);

        Assert.Equal(expectedEventType, entry.NtEventType);
        Assert.Equal("g", entry.NtForkId);
    }

    public static TheoryData<TraceEvent, string> ConcurrencyEvents() => new()
    {
        { new ForkCreatedEvent("g", 1L), "fork" },
        { new MergeEvent("g", 2, 5L, 1L), "join" },
        { new FireAndForgetEvent("g", 1L), "async_dispatch" },
    };

    [Fact]
    public void Enter_event_with_parent_carries_parent_span_id()
    {
        var parent = new SpanId("00f067aa0ba902b7");
        var sig = new MethodSignature("Svc", "Run", []);
        var evt = new EnterEvent(Context(parent: parent), 1L, sig);

        var entry = Mapper.FromEvent(evt);

        Assert.Equal("00f067aa0ba902b7", entry.ParentSpanId);
    }

    [Fact]
    public void Exit_event_with_return_maps_to_success()
    {
        var evt = new ExitEvent(Context(), 5L, new Returned("\"OK\""));

        var entry = Mapper.FromEvent(evt);

        Assert.Equal("method_exit", entry.NtEventType);
        Assert.Equal("trace", entry.Level);
        Assert.Equal("success", entry.NtOutcome);
        Assert.Equal("\"OK\"", entry.NtReturnValue);
        Assert.Equal("OrderService", entry.CodeNamespace);
        Assert.Equal("PlaceOrder", entry.CodeFunction);
    }

    [Fact]
    public void Exit_event_with_exception_maps_to_failure()
    {
        var evt = new ExitEvent(
            Context(), 5L,
            new Threw(new InvalidOperationException("declined")));

        var entry = Mapper.FromEvent(evt);

        Assert.Equal("error", entry.Level);
        Assert.Equal("failure", entry.NtOutcome);
        Assert.Equal("InvalidOperationException", entry.ExceptionType);
        Assert.Equal("declined", entry.ExceptionMessage);
        Assert.StartsWith("!! InvalidOperationException", entry.Message);
    }

    [Fact]
    public void Exit_event_incomplete_maps_to_incomplete()
    {
        var evt = new ExitEvent(Context(), 5L, new Incomplete());

        var entry = Mapper.FromEvent(evt);

        Assert.Equal("incomplete", entry.NtOutcome);
        Assert.Null(entry.NtReturnValue);
    }

    [Fact]
    public void Enter_event_sets_human_readable_trace_name()
    {
        var sig = new MethodSignature("Svc", "Run", []);

        var entry = Mapper.FromEvent(new EnterEvent(Context(), 1L, sig));

        Assert.Equal(
            new TraceId(TraceHex).HumanName, entry.NtTraceName);
        Assert.NotNull(entry.NtTraceName);
    }

    [Fact]
    public void Exit_returned_with_value_formats_arrow_message()
    {
        var evt = new ExitEvent(Context(), 5L, new Returned("\"OK\""));

        var entry = Mapper.FromEvent(evt);

        Assert.Equal(
            "← OrderService.PlaceOrder returned \"OK\"", entry.Message);
    }

    [Fact]
    public void Exit_returned_without_value_formats_bare_message()
    {
        var evt = new ExitEvent(Context(), 5L, new Returned(null));

        var entry = Mapper.FromEvent(evt);

        Assert.Equal(
            "← OrderService.PlaceOrder returned", entry.Message);
    }

    [Fact]
    public void Exit_incomplete_formats_incomplete_message()
    {
        var evt = new ExitEvent(Context(), 5L, new Incomplete());

        var entry = Mapper.FromEvent(evt);

        Assert.Equal(
            "← OrderService.PlaceOrder incomplete", entry.Message);
    }

    [Fact]
    public void Exit_span_name_without_dot_maps_to_empty_namespace()
    {
        var evt = new ExitEvent(
            Context(spanName: "Bare"), 5L, new Returned(null));

        var entry = Mapper.FromEvent(evt);

        Assert.Equal("", entry.CodeNamespace);
        Assert.Equal("Bare", entry.CodeFunction);
    }

    [Fact]
    public void Unsupported_event_throws()
    {
        var node = new TraceNode(
            new MethodSignature("Svc", "Run", []),
            new Returned(null), [], 0);
        var graft = new GraftEvent(null, 1L, node);

        Assert.Throws<ArgumentException>(
            () => Mapper.FromEvent(graft));
    }

    [Fact]
    public void Fork_created_maps_to_a_fork_entry_carrying_the_group_id()
    {
        var entry = Mapper.FromEvent(new ForkCreatedEvent("fork-7", 1L));

        Assert.Equal("fork", entry.NtEventType);
        Assert.Equal("fork-7", entry.NtForkId);
        Assert.Equal("fork [fork-7]", entry.Message);
        Assert.Equal("trace", entry.Level);
        Assert.Equal("2026-07-05T00:00:00.000Z", entry.Timestamp);
    }

    [Fact]
    public void Merge_maps_to_a_join_entry()
    {
        var entry = Mapper.FromEvent(new MergeEvent("fork-7", 3, 250L, 2L));

        Assert.Equal("join", entry.NtEventType);
        Assert.Equal("fork-7", entry.NtForkId);
        Assert.Equal("join [fork-7]", entry.Message);
    }

    [Fact]
    public void Fire_and_forget_maps_to_an_async_dispatch_entry()
    {
        var entry = Mapper.FromEvent(new FireAndForgetEvent("faf-2", 3L));

        Assert.Equal("async_dispatch", entry.NtEventType);
        Assert.Equal("faf-2", entry.NtForkId);
        Assert.Equal("async_dispatch [faf-2]", entry.Message);
    }

    [Theory]
    [MemberData(nameof(ConcurrencyEvents))]
    public void Concurrency_entries_deliberately_fail_the_full_entry_schema(
        TraceEvent concurrencyEvent, string expectedEventType)
    {
        Assert.NotEmpty(expectedEventType);

        // Fork/join/async_dispatch are lifecycle markers that belong to a group
        // rather than a span, so they carry no trace_id/span_id/code.* — the
        // entry schema requires all four. This documents the divergence rather
        // than asserting conformance, matching the Java edition's
        // forkEventDoesNotConformToEntrySchema. Emitting them into a stream
        // validated against entry.schema.json is therefore not supported.
        var json = CanonicalEntrySerializer.ToJson(
            Mapper.FromEvent(concurrencyEvent));

        var results = SchemaValidator.Validate("entry.schema.json", json);

        Assert.False(results.IsValid);
    }

    [Fact]
    public void Concurrency_entries_carry_no_span_or_code_identity()
    {
        var entry = Mapper.FromEvent(new ForkCreatedEvent("fork-7", 1L));

        Assert.Null(entry.TraceId);
        Assert.Null(entry.SpanId);
        Assert.Null(entry.ParentSpanId);
        Assert.Null(entry.CodeNamespace);
        Assert.Null(entry.CodeFunction);
        Assert.Null(entry.NtOutcome);
        Assert.Null(entry.NtParameters);
        Assert.Null(entry.DurationMs);
    }

    [Fact]
    public void Mapped_enter_entry_validates_against_entry_schema()
    {
        var sig = new MethodSignature(
            "OrderService", "PlaceOrder",
            [new ParameterCapture("customerId", "C1", false)]);
        var entry = Mapper.FromEvent(new EnterEvent(Context(), 1L, sig));

        var json = CanonicalEntrySerializer.ToJson(entry);

        SchemaValidator.AssertValid("entry.schema.json", json);
    }

    [Fact]
    public void Mapped_exit_entry_validates_against_entry_schema()
    {
        var evt = new ExitEvent(
            Context(), 5L,
            new Threw(new InvalidOperationException("declined")));
        var entry = Mapper.FromEvent(evt);

        var json = CanonicalEntrySerializer.ToJson(entry);

        SchemaValidator.AssertValid("entry.schema.json", json);
    }

    // ── service fallback ────────────────────────────────────────────────────

    private static SpanContext ContextWithService(string? serviceName) =>
        new(
            new TraceId(TraceHex),
            new SpanId(SpanHex),
            null,
            ServiceName: serviceName,
            Environment: "test");

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Service_falls_back_when_the_host_pins_no_name(string? serviceName)
    {
        var sig = new MethodSignature("OrderService", "PlaceOrder", []);
        var evt = new EnterEvent(ContextWithService(serviceName), 123L, sig);

        var entry = Mapper.FromEvent(evt);

        Assert.Equal("unknown_service:dotnet", entry.Service);
    }

    [Fact]
    public void A_pinned_service_name_survives_the_fallback()
    {
        var sig = new MethodSignature("OrderService", "PlaceOrder", []);

        var entry = Mapper.FromEvent(new EnterEvent(Context(), 123L, sig));

        Assert.Equal("OrderSvc", entry.Service);
    }

    [Theory]
    [MemberData(nameof(ConcurrencyEvents))]
    public void Concurrency_entries_carry_the_service_fallback(
        TraceEvent concurrencyEvent, string expectedEventType)
    {
        Assert.NotEmpty(expectedEventType);

        var entry = Mapper.FromEvent(concurrencyEvent);

        Assert.Equal(CanonicalEntryMapper.UnknownService, entry.Service);
    }

    [Fact]
    public void Exit_entries_carry_the_service_fallback_when_no_name_is_pinned()
    {
        var evt = new ExitEvent(
            ContextWithService(null), 123L, new Returned("42"));

        var entry = Mapper.FromEvent(evt);

        Assert.Equal("unknown_service:dotnet", entry.Service);
    }
}
