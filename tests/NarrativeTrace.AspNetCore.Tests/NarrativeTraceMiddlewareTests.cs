// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using Microsoft.AspNetCore.Http;
using NarrativeTrace.AspNetCore;
using NarrativeTrace.Core;
using Xunit;

namespace NarrativeTrace.AspNetCore.Tests;

public class NarrativeTraceMiddlewareTests
{
    [Fact]
    public async Task Creates_context_per_request()
    {
        var middleware = new NarrativeTraceMiddleware();
        var httpContext = new DefaultHttpContext();

        await middleware.InvokeAsync(
            httpContext,
            _ => Task.CompletedTask);

        var ctx = httpContext.GetNarrativeContext();
        Assert.NotNull(ctx);
    }

    [Fact]
    public async Task Context_traces_within_request()
    {
        var middleware = new NarrativeTraceMiddleware();
        var httpContext = new DefaultHttpContext();

        await middleware.InvokeAsync(
            httpContext,
            ctx =>
            {
                var nc = ctx.GetNarrativeContext();
                nc.EnterMethod("Ctrl", "index", []);
                nc.ExitMethodWithReturn(null);
                return Task.CompletedTask;
            });

        var trace = httpContext.GetNarrativeContext()
            .CaptureTrace();
        Assert.Single(trace.Roots);
    }

    [Fact]
    public void Returns_noop_when_no_middleware()
    {
        var httpContext = new DefaultHttpContext();

        var ctx = httpContext.GetNarrativeContext();

        Assert.False(ctx.IsActive);
    }
}
