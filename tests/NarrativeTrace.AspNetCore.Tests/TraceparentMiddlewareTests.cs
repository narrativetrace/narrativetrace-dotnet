// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using Microsoft.AspNetCore.Http;
using NarrativeTrace.AspNetCore;
using NarrativeTrace.Core;
using Xunit;

namespace NarrativeTrace.AspNetCore.Tests;

/// <summary>Cross-process trace continuity: what an inbound <c>traceparent</c> header does to a request.</summary>
public class TraceparentMiddlewareTests
{
    private const string RemoteTraceIdValue = "4bf92f3577b34da6a3ce929d0e0e4736";
    private const string RemoteSpanIdValue = "00f067aa0ba902b7";
    private const string Header = "00-" + RemoteTraceIdValue + "-" + RemoteSpanIdValue + "-01";

    private sealed class CapturingExporter : ITraceExporter
    {
        public TraceTree? Tree { get; private set; }

        public void Export(TraceTree tree, RequestContext requestContext) => Tree = tree;
    }

    private static DefaultHttpContext Request(string? traceparentHeader = null)
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Method = "GET";
        ctx.Request.Path = "/orders";
        if (traceparentHeader is not null)
        {
            ctx.Request.Headers[Traceparent.HeaderName] = traceparentHeader;
        }

        return ctx;
    }

    [Fact]
    public async Task Continues_the_callers_trace_when_the_header_is_present()
    {
        var exporter = new CapturingExporter();
        var middleware = new NarrativeTraceMiddleware(new NarrativeTraceConfig(), exporter);
        var http = Request(Header);

        var tree = await RunOnceAsync(middleware, exporter, http);

        var root = tree.Roots[0];
        Assert.Equal(new TraceId(RemoteTraceIdValue), root.SpanContext!.TraceId);
        Assert.Equal(new SpanId(RemoteSpanIdValue), root.SpanContext!.ParentSpanId);
    }

    [Fact]
    public async Task Matches_the_header_name_case_insensitively()
    {
        var exporter = new CapturingExporter();
        var middleware = new NarrativeTraceMiddleware(new NarrativeTraceConfig(), exporter);
        var http = new DefaultHttpContext();
        http.Request.Method = "GET";
        http.Request.Path = "/orders";
        http.Request.Headers["TraceParent"] = Header;

        var tree = await RunOnceAsync(middleware, exporter, http);

        Assert.Equal(new TraceId(RemoteTraceIdValue), tree.Roots[0].SpanContext!.TraceId);
    }

    [Fact]
    public async Task Starts_a_fresh_trace_when_no_header_arrives()
    {
        var exporter = new CapturingExporter();
        var middleware = new NarrativeTraceMiddleware(new NarrativeTraceConfig(), exporter);
        var http = Request();

        var tree = await RunOnceAsync(middleware, exporter, http);

        var root = tree.Roots[0];
        Assert.NotEqual(new TraceId(RemoteTraceIdValue), root.SpanContext!.TraceId);
        Assert.Null(root.SpanContext!.ParentSpanId);
    }

    [Fact]
    public async Task Starts_a_fresh_trace_and_serves_the_request_when_the_header_is_malformed()
    {
        var exporter = new CapturingExporter();
        var middleware = new NarrativeTraceMiddleware(new NarrativeTraceConfig(), exporter);
        var http = Request("00-nonsense-01");
        var chainRan = false;

        await middleware.InvokeAsync(http, ctx =>
        {
            chainRan = true;
            var nc = ctx.GetNarrativeContext();
            nc.EnterMethod("OrderService", "List", []);
            nc.ExitMethodWithReturn("\"ok\"");
            return Task.CompletedTask;
        });

        Assert.True(chainRan);
        var root = exporter.Tree!.Roots[0];
        Assert.NotEqual(new TraceId(RemoteTraceIdValue), root.SpanContext!.TraceId);
        Assert.Null(root.SpanContext!.ParentSpanId);
    }

    [Fact]
    public async Task Refuses_an_all_zero_trace_id_and_starts_fresh()
    {
        var exporter = new CapturingExporter();
        var middleware = new NarrativeTraceMiddleware(new NarrativeTraceConfig(), exporter);
        var http = Request("00-" + new string('0', 32) + "-" + RemoteSpanIdValue + "-01");

        var tree = await RunOnceAsync(middleware, exporter, http);

        Assert.NotEqual(new string('0', 32), tree.Roots[0].SpanContext!.TraceId.Value);
    }

    private static async Task<TraceTree> RunOnceAsync(
        NarrativeTraceMiddleware middleware, CapturingExporter exporter, DefaultHttpContext http)
    {
        await middleware.InvokeAsync(http, ctx =>
        {
            var nc = ctx.GetNarrativeContext();
            nc.EnterMethod("OrderService", "List", []);
            nc.ExitMethodWithReturn("\"ok\"");
            return Task.CompletedTask;
        });

        return exporter.Tree!;
    }
}
