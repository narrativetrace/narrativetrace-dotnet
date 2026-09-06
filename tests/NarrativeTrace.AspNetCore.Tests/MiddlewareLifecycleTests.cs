// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using Microsoft.AspNetCore.Http;
using NarrativeTrace.AspNetCore;
using NarrativeTrace.Core;
using Xunit;

namespace NarrativeTrace.AspNetCore.Tests;

public class MiddlewareLifecycleTests
{
    private sealed class CapturingExporter : ITraceExporter
    {
        public TraceTree? Tree { get; private set; }
        public RequestContext? ExportedRequest { get; private set; }

        public void Export(TraceTree tree, RequestContext requestContext)
        {
            Tree = tree;
            ExportedRequest = requestContext;
        }
    }

    private static DefaultHttpContext Request(
        string method, string path, int status = 200)
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Method = method;
        ctx.Request.Path = path;
        ctx.Response.StatusCode = status;
        return ctx;
    }

    [Fact]
    public async Task Exports_captured_trace_with_request_metadata()
    {
        var exporter = new CapturingExporter();
        var middleware = new NarrativeTraceMiddleware(
            new NarrativeTraceConfig(), exporter);
        var http = Request("GET", "/api/orders");

        await middleware.InvokeAsync(http, ctx =>
        {
            var nc = ctx.GetNarrativeContext();
            nc.EnterMethod("OrderService", "PlaceOrder", []);
            nc.ExitMethodWithReturn(null);
            return Task.CompletedTask;
        });

        Assert.NotNull(exporter.Tree);
        Assert.Single(exporter.Tree!.Roots);
        Assert.Equal(200, exporter.ExportedRequest!.StatusCode);
    }

    [Fact]
    public async Task Stamps_request_context_onto_spans()
    {
        var exporter = new CapturingExporter();
        var middleware = new NarrativeTraceMiddleware(
            new NarrativeTraceConfig(), exporter);
        var http = Request("POST", "/api/orders");

        await middleware.InvokeAsync(http, ctx =>
        {
            var nc = ctx.GetNarrativeContext();
            nc.EnterMethod("Svc", "Run", []);
            nc.ExitMethodWithReturn(null);
            return Task.CompletedTask;
        });

        var span = exporter.Tree!.Roots[0].SpanContext!;
        Assert.Equal("POST", span.HttpMethod);
        Assert.Equal("/api/orders", span.HttpRoute!.Value);
    }

    [Fact]
    public async Task Excluded_path_produces_no_export()
    {
        var exporter = new CapturingExporter();
        var options = new NarrativeTraceOptions();
        options.ExcludedPaths.Add("/health");
        var middleware = new NarrativeTraceMiddleware(
            new NarrativeTraceConfig(), exporter,
            userProvider: null, options);
        var http = Request("GET", "/health");

        await middleware.InvokeAsync(http, _ => Task.CompletedTask);

        Assert.Null(exporter.Tree);
    }

    [Fact]
    public async Task Empty_trace_is_not_handed_to_the_exporter()
    {
        var exporter = new CapturingExporter();
        var middleware = new NarrativeTraceMiddleware(
            new NarrativeTraceConfig(), exporter);
        var http = Request("GET", "/api/orders");

        await middleware.InvokeAsync(http, _ => Task.CompletedTask);

        Assert.Null(exporter.Tree);
    }

    [Fact]
    public async Task Exports_with_failure_status_when_handler_throws()
    {
        var exporter = new CapturingExporter();
        var middleware = new NarrativeTraceMiddleware(
            new NarrativeTraceConfig(), exporter);
        var http = Request("GET", "/boom", status: 500);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => middleware.InvokeAsync(http, ctx =>
            {
                ctx.GetNarrativeContext()
                    .EnterMethod("Svc", "Fail", []);
                throw new InvalidOperationException("boom");
            }));

        Assert.NotNull(exporter.Tree);
        Assert.Equal(500, exporter.ExportedRequest!.StatusCode);
    }

    [Fact]
    public async Task Handler_exception_with_default_status_exports_500()
    {
        var exporter = new CapturingExporter();
        var middleware = new NarrativeTraceMiddleware(
            new NarrativeTraceConfig(), exporter);
        var http = Request("GET", "/boom");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => middleware.InvokeAsync(http, ctx =>
            {
                ctx.GetNarrativeContext().EnterMethod("Svc", "Fail", []);
                throw new InvalidOperationException("boom");
            }));

        Assert.Equal(500, exporter.ExportedRequest!.StatusCode);
    }
}
