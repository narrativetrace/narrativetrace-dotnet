// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using Microsoft.AspNetCore.Http;
using NarrativeTrace.AspNetCore;
using NarrativeTrace.Core;
using Xunit;

namespace NarrativeTrace.AspNetCore.Tests;

public class MiddlewareLogScopeTests
{
    private sealed class CapturingExporter : ITraceExporter
    {
        public TraceTree? Tree { get; private set; }

        public void Export(TraceTree tree, RequestContext requestContext)
            => Tree = tree;
    }

    private static DefaultHttpContext Request(string method, string path)
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Method = method;
        ctx.Request.Path = path;
        return ctx;
    }

    [Fact]
    public async Task Request_scope_carries_trace_correlation_fields()
    {
        var logger = new ScopeCapturingLogger();
        var exporter = new CapturingExporter();
        var middleware = new NarrativeTraceMiddleware(
            new NarrativeTraceConfig(), exporter,
            userProvider: null, options: null,
            loggerFactory: new SingleLoggerFactory(logger));

        await middleware.InvokeAsync(
            Request("POST", "/api/orders"), ctx =>
            {
                var nc = ctx.GetNarrativeContext();
                nc.EnterMethod("Svc", "Run", []);
                nc.ExitMethodWithReturn(null);
                return Task.CompletedTask;
            });

        var scope = logger.LastScopeAsMap();
        Assert.Equal("POST", scope["httpMethod"]);
        Assert.Equal("/api/orders", scope["httpRoute"]);
        Assert.Equal(
            exporter.Tree!.Roots[0].SpanContext!.TraceId.Value,
            scope["traceId"]);
    }

    [Fact]
    public async Task Request_scope_carries_the_trace_name_and_client_ip()
    {
        var logger = new ScopeCapturingLogger();
        var exporter = new CapturingExporter();
        var middleware = new NarrativeTraceMiddleware(
            new NarrativeTraceConfig(), exporter,
            userProvider: null, options: null,
            loggerFactory: new SingleLoggerFactory(logger));
        var http = Request("POST", "/api/orders");
        http.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("203.0.113.7");

        await middleware.InvokeAsync(http, ctx =>
        {
            var nc = ctx.GetNarrativeContext();
            nc.EnterMethod("Svc", "Run", []);
            nc.ExitMethodWithReturn(null);
            return Task.CompletedTask;
        });

        var scope = logger.LastScopeAsMap();
        Assert.Equal(
            exporter.Tree!.Roots[0].SpanContext!.TraceId.HumanName,
            scope["traceName"]);
        Assert.Equal("203.0.113.7", scope["clientIp"]);
    }

    [Fact]
    public async Task Resolved_user_identity_reaches_both_the_span_and_the_log_scope()
    {
        var logger = new ScopeCapturingLogger();
        var exporter = new CapturingExporter();
        var middleware = new NarrativeTraceMiddleware(
            new NarrativeTraceConfig(), exporter,
            new StubProvider(new UserContext("u-1", "s-2", "t-3")),
            options: null,
            loggerFactory: new SingleLoggerFactory(logger));

        await middleware.InvokeAsync(Request("GET", "/api/orders"), ctx =>
        {
            var nc = ctx.GetNarrativeContext();
            nc.EnterMethod("Svc", "Run", []);
            nc.ExitMethodWithReturn(null);
            return Task.CompletedTask;
        });

        var scope = logger.LastScopeAsMap();
        Assert.Equal("u-1", scope["enduserId"]);
        Assert.Equal("s-2", scope["sessionId"]);
        Assert.Equal("t-3", scope["tenantId"]);
        var span = exporter.Tree!.Roots[0].SpanContext!;
        Assert.Equal("u-1", span.EnduserId?.Value);
        Assert.Equal("s-2", span.SessionId?.Value);
        Assert.Equal("t-3", span.TenantId?.Value);
    }

    [Fact]
    public async Task Only_the_identity_fields_the_provider_knows_are_logged()
    {
        var logger = new ScopeCapturingLogger();
        var middleware = new NarrativeTraceMiddleware(
            new NarrativeTraceConfig(), new CapturingExporter(),
            new StubProvider(new UserContext(TenantId: "t-3")),
            options: null,
            loggerFactory: new SingleLoggerFactory(logger));

        await middleware.InvokeAsync(Request("GET", "/api/orders"), _ => Task.CompletedTask);

        var scope = logger.LastScopeAsMap();
        Assert.Equal("t-3", scope["tenantId"]);
        Assert.DoesNotContain("enduserId", scope.Keys);
        Assert.DoesNotContain("sessionId", scope.Keys);
    }

    [Fact]
    public async Task Anonymous_traffic_logs_no_identity_fields()
    {
        var logger = new ScopeCapturingLogger();
        var middleware = new NarrativeTraceMiddleware(
            new NarrativeTraceConfig(), new CapturingExporter(),
            new StubProvider(null), options: null,
            loggerFactory: new SingleLoggerFactory(logger));

        await middleware.InvokeAsync(Request("GET", "/api/orders"), _ => Task.CompletedTask);

        var scope = logger.LastScopeAsMap();
        Assert.DoesNotContain("enduserId", scope.Keys);
        Assert.DoesNotContain("tenantId", scope.Keys);
    }

    private sealed class StubProvider : IRequestContextProvider
    {
        private readonly UserContext? _user;

        public StubProvider(UserContext? user) => _user = user;

        public UserContext? ResolveUserContext(HttpContext context) => _user;
    }

    [Fact]
    public async Task An_unknown_client_ip_is_omitted_rather_than_logged_empty()
    {
        var logger = new ScopeCapturingLogger();
        var middleware = new NarrativeTraceMiddleware(
            new NarrativeTraceConfig(), new CapturingExporter(),
            userProvider: null, options: null,
            loggerFactory: new SingleLoggerFactory(logger));

        await middleware.InvokeAsync(Request("GET", "/health"), _ => Task.CompletedTask);

        Assert.DoesNotContain("clientIp", logger.LastScopeAsMap().Keys);
    }

    /// <summary>
    /// Correlation resolves through <see cref="INarrativeContext"/>, so a
    /// registration that is not the concrete synchronous type still correlates
    /// — it previously did not.
    /// </summary>
    [Fact]
    public async Task Request_scope_correlates_a_context_registered_by_interface()
    {
        var logger = new ScopeCapturingLogger();
        var inner = new NarrativeTrace.Runtime.SyncNarrativeContext(
            new NarrativeTraceConfig());
        var middleware = new NarrativeTraceMiddleware(
            new NarrativeTraceConfig(), new CapturingExporter(),
            userProvider: null, options: null,
            loggerFactory: new SingleLoggerFactory(logger));
        var http = Request("GET", "/api/health");
        http.RequestServices = new SingleServiceProvider(inner);

        await middleware.InvokeAsync(http, _ => Task.CompletedTask);

        Assert.Equal(
            inner.EnsureTraceId().Value, logger.LastScopeAsMap()["traceId"]);
    }

    /// <summary>
    /// A scope-bound context has no trace id until traced work starts, which is
    /// after the log scope opens. Correlation must degrade to absent — never
    /// throw, because that would fail the request over a logging concern.
    /// </summary>
    [Fact]
    public async Task A_scope_bound_context_degrades_instead_of_failing_the_request()
    {
        var logger = new ScopeCapturingLogger();
        var asyncContext = new NarrativeTrace.Runtime.AsyncNarrativeContext(
            new NarrativeTraceConfig());
        var middleware = new NarrativeTraceMiddleware(
            new NarrativeTraceConfig(), new CapturingExporter(),
            userProvider: null, options: null,
            loggerFactory: new SingleLoggerFactory(logger));
        var http = Request("GET", "/api/health");
        http.RequestServices = new SingleServiceProvider(asyncContext);
        var reachedTheApp = false;

        await middleware.InvokeAsync(http, _ =>
        {
            reachedTheApp = true;
            return Task.CompletedTask;
        });

        Assert.True(reachedTheApp);
        Assert.DoesNotContain("traceId", logger.LastScopeAsMap().Keys);
    }

    private sealed class SingleServiceProvider : IServiceProvider
    {
        private readonly INarrativeContext _context;

        public SingleServiceProvider(INarrativeContext context) => _context = context;

        public object? GetService(Type serviceType)
            => serviceType == typeof(INarrativeContext) ? _context : null;
    }
}
