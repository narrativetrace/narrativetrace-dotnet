// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NarrativeTrace.Core;
using NarrativeTrace.Runtime;

namespace NarrativeTrace.AspNetCore;

/// <summary>
/// Per-request tracing lifecycle: creates a scoped context, stamps request
/// and user tiers, runs the pipeline, then captures and exports the trace —
/// exceptions still produce a trace with the response status.
/// </summary>
public sealed class NarrativeTraceMiddleware : IMiddleware
{
    internal const string ContextKey = "NarrativeTrace.Context";
    private const string RequestCategory = "NarrativeTrace.Request";

    private readonly NarrativeTraceConfig _config;
    private readonly ITraceExporter? _exporter;
    private readonly IRequestContextProvider? _userProvider;
    private readonly NarrativeTraceOptions _options;
    private readonly ILogger? _logger;

    /// <summary>
    /// Creates a middleware with a default <see cref="NarrativeTraceConfig"/>
    /// and no exporter, user provider, options, or logging — the minimal
    /// parameterless form for hosts that resolve dependencies elsewhere.
    /// </summary>
    public NarrativeTraceMiddleware()
        : this(new NarrativeTraceConfig())
    {
    }

    /// <summary>
    /// Creates a middleware with explicit collaborators. Typically resolved
    /// from DI by <see cref="ServiceCollectionExtensions.AddNarrativeTrace(Microsoft.Extensions.DependencyInjection.IServiceCollection, System.Action{NarrativeTraceOptions})"/>,
    /// but can be constructed directly in tests.
    /// </summary>
    /// <param name="config">Tracing configuration used for any fallback
    /// per-request context the middleware creates.</param>
    /// <param name="exporter">Sink that receives the captured trace at request
    /// end; when null, no export occurs.</param>
    /// <param name="userProvider">Optional provider that stamps user-tier
    /// context (identity) during request stamping.</param>
    /// <param name="options">Options controlling excluded paths and exporter
    /// naming; defaults to a fresh <see cref="NarrativeTraceOptions"/>.</param>
    /// <param name="loggerFactory">Optional factory used to open a per-request
    /// logging scope correlating logs written during the pipeline.</param>
    public NarrativeTraceMiddleware(
        NarrativeTraceConfig config,
        ITraceExporter? exporter = null,
        IRequestContextProvider? userProvider = null,
        NarrativeTraceOptions? options = null,
        ILoggerFactory? loggerFactory = null)
    {
        _config = config;
        _exporter = exporter;
        _userProvider = userProvider;
        _options = options ?? new NarrativeTraceOptions();
        _logger = loggerFactory?.CreateLogger(RequestCategory);
    }

    /// <summary>
    /// Runs the per-request tracing lifecycle around <paramref name="next"/>.
    /// </summary>
    /// <remarks>
    /// Excluded paths short-circuit to <paramref name="next"/> with no context
    /// and no trace. Otherwise the middleware resolves a per-request context,
    /// stores it in <see cref="HttpContext.Items"/> under the key read by
    /// <see cref="HttpContextExtensions.GetNarrativeContext"/>, stamps the
    /// request (and user) tiers, runs the rest of the pipeline, and captures
    /// and exports the trace in a <c>finally</c> block — so a request that
    /// throws still produces a trace carrying the response status and elapsed
    /// milliseconds.
    /// </remarks>
    /// <param name="context">The current HTTP context.</param>
    /// <param name="next">The remaining request pipeline.</param>
    public async Task InvokeAsync(
        HttpContext context, RequestDelegate next)
    {
        if (IsExcluded(context.Request.Path))
        {
            await next(context);
            return;
        }

        var traceContext = ResolveContext(context);
        context.Items[ContextKey] = traceContext;
        var user = StampRequestContext(context, traceContext);
        await RunTraced(context, next, traceContext, user);
    }

    // Prefer the request-scoped context registered by AddNarrativeTrace so
    // proxied services (AddNarrativeTracing) and the middleware share one
    // per-request context. Fall back to a fresh context when DI is absent
    // (e.g. the middleware constructed directly in a unit test).
    private INarrativeContext ResolveContext(HttpContext context)
    {
        return context.RequestServices?
                .GetService(typeof(INarrativeContext)) as INarrativeContext
            ?? new SyncNarrativeContext(_config);
    }

    private async Task RunTraced(
        HttpContext context, RequestDelegate next,
        INarrativeContext traceContext, UserContext? user)
    {
        using var scope = BeginRequestScope(context, traceContext, user);
        var stopwatch = Stopwatch.StartNew();
        int? errorStatus = null;
        try
        {
            await next(context);
        }
        catch (Exception)
        {
            errorStatus = ErrorStatusOf(context);
            throw;
        }
        finally
        {
            stopwatch.Stop();
            Export(
                context, traceContext,
                stopwatch.ElapsedMilliseconds, errorStatus);
        }
    }

    // Correlates every log written during `next` with this request
    // (Java populates the same fields into MDC in stampContext).
    private IDisposable? BeginRequestScope(
        HttpContext context, INarrativeContext traceContext, UserContext? user)
    {
        if (_logger is null)
        {
            return null;
        }

        return _logger.BeginScope(ScopeFields(context, traceContext, user));
    }

    /// <summary>
    /// The correlation fields stamped on every log line of this request.
    /// </summary>
    /// <remarks>
    /// Mirrors the Java servlet filter's MDC set: method and route always,
    /// trace id and human trace name when the context can supply them, and
    /// client IP when the connection exposes one. Optional fields are omitted
    /// rather than logged blank — an empty value reads as "there was none"
    /// instead of "we could not determine it".
    /// </remarks>
    private static Dictionary<string, object> ScopeFields(
        HttpContext context, INarrativeContext traceContext, UserContext? user)
    {
        var scope = new Dictionary<string, object>
        {
            ["httpMethod"] = context.Request.Method,
            ["httpRoute"] = context.Request.Path.ToString(),
        };
        var traceId = CorrelationTraceId(traceContext);
        if (traceId is not null)
        {
            scope["traceId"] = traceId.Value.Value;
            scope["traceName"] = traceId.Value.HumanName;
        }

        var clientIp = ClientIpOf(context);
        if (clientIp is not null)
        {
            scope["clientIp"] = clientIp.Value;
        }

        AddUserFields(scope, user);
        return scope;
    }

    /// <summary>
    /// Adds the resolved identity, present fields only.
    /// </summary>
    /// <remarks>
    /// Same keys as the Java filter's MDC (<c>enduserId</c>,
    /// <c>sessionId</c>, <c>tenantId</c>). Anonymous traffic is normal, so an
    /// absent value means the key is absent, never blank.
    /// </remarks>
    private static void AddUserFields(
        Dictionary<string, object> scope, UserContext? user)
    {
        if (user is null)
        {
            return;
        }

        if (user.EnduserId is not null)
        {
            scope["enduserId"] = user.EnduserId;
        }

        if (user.SessionId is not null)
        {
            scope["sessionId"] = user.SessionId;
        }

        if (user.TenantId is not null)
        {
            scope["tenantId"] = user.TenantId;
        }
    }

    /// <summary>
    /// The trace id to correlate this request's logs with, or null when the
    /// context cannot supply one yet.
    /// </summary>
    /// <remarks>
    /// Resolved through <see cref="INarrativeContext"/> rather than a concrete
    /// type, so decorated registrations correlate too — they previously did
    /// not, despite implementing the interface. Scope-bound contexts
    /// (<c>AsyncNarrativeContext</c>) have no id until traced work begins,
    /// which happens after this scope opens; they throw rather than answer, so
    /// correlation degrades to absent instead of failing the request. Log
    /// correlation is best-effort and must never be the reason a request 500s.
    /// </remarks>
    private static TraceId? CorrelationTraceId(INarrativeContext traceContext)
    {
        try
        {
            return traceContext.EnsureTraceId();
        }
        catch (InvalidOperationException)
        {
            return traceContext.CurrentTraceId;
        }
    }

    // On an unhandled exception the response status is often still the default
    // 200; map it to 500 unless an error status was already set (Micronaut's
    // explicit error->500 mapping).
    private static int ErrorStatusOf(HttpContext context)
    {
        var status = context.Response.StatusCode;
        return status >= 400 ? status : 500;
    }

    /// <summary>
    /// Stamps the request tier onto the trace and resolves the user tier.
    /// </summary>
    /// <returns>
    /// The resolved identity for the caller to correlate logs with, or null
    /// when no provider is registered, none was resolved, or resolution threw.
    /// </returns>
    private UserContext? StampRequestContext(
        HttpContext context, INarrativeContext traceContext)
    {
        // Observability failure must never become a request failure.
        try
        {
            traceContext.SetRequestContext(
                context.Request.Method,
                new HttpRoute(context.Request.Path.ToString()),
                ClientIpOf(context));
            var user = _userProvider?.ResolveUserContext(context);
            ApplyUserContext(traceContext, user);
            return user;
        }
        catch (Exception)
        {
            // Swallow: stamping faults are isolated from the request.
            return null;
        }
    }

    /// <summary>Stamps the resolved identity onto spans created from here on.</summary>
    private static void ApplyUserContext(
        INarrativeContext traceContext, UserContext? user)
    {
        if (user is null)
        {
            return;
        }

        traceContext.SetUserContext(
            user.EnduserId is null ? null : new EnduserId(user.EnduserId),
            user.SessionId is null ? null : new SessionId(user.SessionId),
            user.TenantId is null ? null : new TenantId(user.TenantId));
    }

    private static ClientIp? ClientIpOf(HttpContext context)
    {
        var ip = context.Connection.RemoteIpAddress?.ToString();
        return ip is null ? null : new ClientIp(ip);
    }

    private void Export(
        HttpContext context, INarrativeContext traceContext,
        long durationMs, int? errorStatus = null)
    {
        if (_exporter is null)
        {
            return;
        }

        // Observability failure must never become a request failure.
        try
        {
            var tree = traceContext.CaptureTrace();
            if (tree.IsEmpty)
            {
                return;
            }

            _exporter.Export(
                tree,
                new RequestContext(
                    errorStatus ?? context.Response.StatusCode,
                    durationMs));
        }
        catch (Exception)
        {
            // Swallow: exporter faults are isolated from the request.
        }
    }

    private bool IsExcluded(PathString path)
    {
        // S3267: LINQ allocates a closure on every request through the pipeline.
#pragma warning disable S3267
        foreach (var excluded in _options.ExcludedPaths)
        {
            if (path.StartsWithSegments(
                    excluded, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
#pragma warning restore S3267

        return false;
    }
}
