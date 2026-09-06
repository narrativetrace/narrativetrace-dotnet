// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using Xunit;

namespace NarrativeTrace.Core.Tests;

public class SpanContextTierTests
{
    [Fact]
    public void Carries_request_and_user_tier_fields()
    {
        var span = new SpanContext(
            new TraceId("4bf92f3577b34da6a3ce929d0e0e4736"),
            SpanIdGenerator.GenerateSpanId(), null)
        {
            HttpMethod = "GET",
            HttpRoute = new HttpRoute("/api/orders"),
            ClientIp = new ClientIp("203.0.113.5"),
            EnduserId = new EnduserId("user-42"),
            SessionId = new SessionId("sess-9"),
            TenantId = new TenantId("acme"),
            SpanName = "OrderService.PlaceOrder",
            TraceFlags = 1,
            TraceState = "vendor=x",
        };

        Assert.Equal("GET", span.HttpMethod);
        Assert.Equal("/api/orders", span.HttpRoute!.Value);
        Assert.Equal("acme", span.TenantId!.Value);
        Assert.Equal(1, span.TraceFlags);
    }

    [Fact]
    public void New_tier_fields_default_to_null_or_zero()
    {
        var span = new SpanContext(
            new TraceId("4bf92f3577b34da6a3ce929d0e0e4736"),
            SpanIdGenerator.GenerateSpanId(), null);

        Assert.Null(span.HttpRoute);
        Assert.Null(span.TenantId);
        Assert.Equal(0, span.TraceFlags);
    }
}

public class SpanContextTests
{
    [Fact]
    public void Create_assembles_record_with_typed_fields()
    {
        var traceId = new TraceId("4bf92f3577b34da6a3ce929d0e0e4736");
        var spanId = new SpanId("00f067aa0ba902b7");
        var parentSpanId = new SpanId("b9c7c989f97918e1");
        var identity = new ServiceIdentity("order-service", "2.3.1", "production");

        var ctx = SpanContext.Create(traceId, spanId, parentSpanId, identity);

        Assert.Equal(traceId, ctx.TraceId);
        Assert.Equal(spanId, ctx.SpanId);
        Assert.Equal(parentSpanId, ctx.ParentSpanId);
        Assert.Equal("order-service", ctx.ServiceName);
        Assert.Equal("2.3.1", ctx.ServiceVersion);
        Assert.Equal("production", ctx.Environment);
    }

    [Fact]
    public void Create_with_null_parent_and_no_identity()
    {
        var traceId = new TraceId("4bf92f3577b34da6a3ce929d0e0e4736");
        var spanId = new SpanId("00f067aa0ba902b7");

        var ctx = SpanContext.Create(traceId, spanId, null, null);

        Assert.Equal(traceId, ctx.TraceId);
        Assert.Equal(spanId, ctx.SpanId);
        Assert.Null(ctx.ParentSpanId);
        Assert.Null(ctx.ServiceName);
        Assert.Null(ctx.ServiceVersion);
        Assert.Null(ctx.Environment);
    }

    [Fact]
    public void SpanContext_supports_value_equality()
    {
        var traceId = new TraceId("4bf92f3577b34da6a3ce929d0e0e4736");
        var spanId = new SpanId("00f067aa0ba902b7");

        var a = SpanContext.Create(traceId, spanId, null, null);
        var b = SpanContext.Create(traceId, spanId, null, null);

        Assert.Equal(a, b);
    }
}
