// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using Microsoft.AspNetCore.Http;
using NarrativeTrace.AspNetCore;
using NarrativeTrace.Core;
using Xunit;

namespace NarrativeTrace.AspNetCore.Tests;

public class FailSafeObservabilityTests
{
    private sealed class ThrowingExporter : ITraceExporter
    {
        public void Export(TraceTree tree, RequestContext requestContext)
            => throw new InvalidOperationException("exporter boom");
    }

    private sealed class ThrowingProvider : IRequestContextProvider
    {
        public UserContext? ResolveUserContext(HttpContext context)
            => throw new InvalidOperationException("provider boom");
    }

    private static DefaultHttpContext Request(string method, string path)
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Method = method;
        ctx.Request.Path = path;
        return ctx;
    }

    private static Task Traced(HttpContext ctx)
    {
        var nc = ctx.GetNarrativeContext();
        nc.EnterMethod("Svc", "Run", []);
        nc.ExitMethodWithReturn(null);
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Throwing_exporter_does_not_fail_a_successful_request()
    {
        var middleware = new NarrativeTraceMiddleware(
            new NarrativeTraceConfig(), new ThrowingExporter());
        var ran = false;

        await middleware.InvokeAsync(Request("GET", "/api/orders"), ctx =>
        {
            ran = true;
            return Traced(ctx);
        });

        Assert.True(ran);
    }

    [Fact]
    public async Task Handler_exception_survives_a_throwing_exporter()
    {
        var middleware = new NarrativeTraceMiddleware(
            new NarrativeTraceConfig(), new ThrowingExporter());

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => middleware.InvokeAsync(Request("GET", "/boom"), ctx =>
            {
                ctx.GetNarrativeContext().EnterMethod("Svc", "Fail", []);
                throw new ArgumentException("handler-X");
            }));

        Assert.Equal("handler-X", ex.Message);
    }

    [Fact]
    public async Task Throwing_provider_does_not_fail_the_request_and_next_runs()
    {
        var middleware = new NarrativeTraceMiddleware(
            new NarrativeTraceConfig(), exporter: null,
            userProvider: new ThrowingProvider());
        var ran = false;

        await middleware.InvokeAsync(Request("GET", "/api/orders"), _ =>
        {
            ran = true;
            return Task.CompletedTask;
        });

        Assert.True(ran);
    }
}
