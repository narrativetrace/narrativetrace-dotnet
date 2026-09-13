// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using NarrativeTrace.Runtime;
using Xunit;

namespace NarrativeTrace.Core.Tests;

/// <summary>ADR-014-style rung, cross-process half: what an inbound <see cref="Traceparent"/> does to a trace.</summary>
public class TraceparentAdoptionTests
{
    private const string RemoteTraceIdValue = "4bf92f3577b34da6a3ce929d0e0e4736";
    private const string RemoteSpanIdValue = "00f067aa0ba902b7";

    private static Traceparent Remote() =>
        new(new TraceId(RemoteTraceIdValue), new SpanId(RemoteSpanIdValue), 1);

    [Fact]
    public void Adopted_trace_id_becomes_the_trace_id_of_every_span()
    {
        var context = new SyncNarrativeContext(new NarrativeTraceConfig());
        context.AdoptTraceparent(Remote());

        var outer = context.EnterMethod("OrderService", "PlaceOrder", []);
        var inner = context.EnterMethod("PaymentService", "Charge", []);
        context.ExitMethodWithReturn("\"paid\"", inner);
        context.ExitMethodWithReturn("\"placed\"", outer);

        var root = context.CaptureTrace().Roots[0];
        Assert.Equal(new TraceId(RemoteTraceIdValue), root.SpanContext!.TraceId);
        Assert.Equal(new TraceId(RemoteTraceIdValue), root.Children[0].SpanContext!.TraceId);
    }

    [Fact]
    public void Adopted_span_id_parents_the_root_and_only_the_root()
    {
        var context = new SyncNarrativeContext(new NarrativeTraceConfig());
        context.AdoptTraceparent(Remote());

        var outer = context.EnterMethod("OrderService", "PlaceOrder", []);
        var inner = context.EnterMethod("PaymentService", "Charge", []);
        context.ExitMethodWithReturn("\"paid\"", inner);
        context.ExitMethodWithReturn("\"placed\"", outer);

        var root = context.CaptureTrace().Roots[0];
        Assert.Equal(new SpanId(RemoteSpanIdValue), root.SpanContext!.ParentSpanId);
        Assert.Equal(
            root.SpanContext!.SpanId, root.Children[0].SpanContext!.ParentSpanId);
    }

    [Fact]
    public void Adopted_trace_id_is_visible_before_any_span_is_entered()
    {
        var context = new SyncNarrativeContext(new NarrativeTraceConfig());

        context.AdoptTraceparent(Remote());

        Assert.Equal(new TraceId(RemoteTraceIdValue), context.CurrentTraceId);
        Assert.Equal(new TraceId(RemoteTraceIdValue), context.EnsureTraceId());
    }

    [Fact]
    public void Adopting_nothing_leaves_the_trace_to_generate_its_own_id()
    {
        var context = new SyncNarrativeContext(new NarrativeTraceConfig());

        context.AdoptTraceparent(null);
        var handle = context.EnterMethod("OrderService", "PlaceOrder", []);
        context.ExitMethodWithReturn("\"placed\"", handle);

        var root = context.CaptureTrace().Roots[0];
        Assert.NotNull(root.SpanContext!.TraceId);
        Assert.NotEqual(new TraceId(RemoteTraceIdValue), root.SpanContext!.TraceId);
        Assert.Null(root.SpanContext!.ParentSpanId);
    }

    [Fact]
    public void Reset_drops_the_adopted_context()
    {
        var context = new SyncNarrativeContext(new NarrativeTraceConfig());
        context.AdoptTraceparent(Remote());

        context.Reset();

        Assert.NotEqual(new TraceId(RemoteTraceIdValue), context.EnsureTraceId());
    }

    /// <summary>
    /// Divergence from a strict "adopt once" contract, mirroring the underlying context's own
    /// last-write-wins semantics for every other mutable field (level, request context, ...): a
    /// second call replaces the first rather than being rejected.
    /// </summary>
    [Fact]
    public void Adopting_twice_uses_the_last_call()
    {
        var other = new Traceparent(SpanIdGenerator.GenerateTraceId(), SpanIdGenerator.GenerateSpanId(), 1);
        var context = new SyncNarrativeContext(new NarrativeTraceConfig());

        context.AdoptTraceparent(Remote());
        context.AdoptTraceparent(other);

        Assert.Equal(other.TraceId, context.CurrentTraceId);
    }

    [Fact]
    public void Config_seeded_traceparent_is_adopted_at_construction()
    {
        var config = new NarrativeTraceConfig(initialTraceparent: Remote());

        var context = new SyncNarrativeContext(config);
        var handle = context.EnterMethod("OrderService", "PlaceOrder", []);
        context.ExitMethodWithReturn("\"placed\"", handle);

        var root = context.CaptureTrace().Roots[0];
        Assert.Equal(new TraceId(RemoteTraceIdValue), root.SpanContext!.TraceId);
        Assert.Equal(new SpanId(RemoteSpanIdValue), root.SpanContext!.ParentSpanId);
    }

    [Fact]
    public void Async_context_delegates_adoption_to_the_active_scope()
    {
        var context = new AsyncNarrativeContext(new NarrativeTraceConfig());

        context.Run(() =>
        {
            context.AdoptTraceparent(Remote());
            var handle = context.EnterMethod("OrderService", "PlaceOrder", []);
            context.ExitMethodWithReturn("\"placed\"", handle);
        });

        var root = context.CaptureTrace().Roots[0];
        Assert.Equal(new TraceId(RemoteTraceIdValue), root.SpanContext!.TraceId);
    }

    [Fact]
    public void Async_context_throws_when_adopting_outside_a_scope()
    {
        var context = new AsyncNarrativeContext(new NarrativeTraceConfig());

        Assert.Throws<InvalidOperationException>(() => context.AdoptTraceparent(Remote()));
    }

    [Fact]
    public void Noop_context_adopts_nothing_without_throwing()
    {
        NoopContext.Instance.AdoptTraceparent(Remote());

        Assert.Null(NoopContext.Instance.CurrentTraceId);
    }
}
