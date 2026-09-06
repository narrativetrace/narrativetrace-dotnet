// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using Microsoft.Extensions.Logging;
using NarrativeTrace.Core;

namespace NarrativeTrace.Logging;

/// <summary>
/// Decorates an inner <see cref="INarrativeContext"/> and mirrors each context
/// call to an <see cref="ILogger"/>, the .NET twin of the Java
/// <c>LoggingNarrativeContext</c>. Every method delegates to the wrapped
/// context first, then emits a structured log record for the corresponding
/// span (enter, return, exception, graft) plus the concurrency lifecycle
/// events (fork/join/fire-and-forget).
/// </summary>
/// <remarks>
/// Wrap any context to add logging: <c>new LoggingNarrativeContext(inner,
/// logger)</c>. Log records are written synchronously at each call, one per
/// span transition, with span/trace/story/chapter ids and an optional
/// <see cref="ServiceIdentity"/> pushed into <c>ILogger.BeginScope</c>. Levels
/// and event ids come from <see cref="TraceLoggingOptions"/>. Depth is tracked
/// per async flow for readable indentation only. Compare with
/// <see cref="LoggingTraceEventListener"/>, which logs the drained event stream
/// rather than decorating the context directly.
/// </remarks>
public sealed class LoggingNarrativeContext
    : INarrativeContext, IConcurrencyLifecycle, IConcurrentChildFactory,
      ITraceLossSource
{
    private readonly INarrativeContext _inner;
    private readonly ServiceIdentity? _serviceIdentity;
    private readonly TraceLogEmitter _emitter;
    private readonly AsyncLocal<int> _depth = new();

    /// <summary>
    /// Wraps <paramref name="inner"/> and logs to <paramref name="logger"/>
    /// using <see cref="TraceLoggingOptions.Default"/> and no service identity.
    /// </summary>
    public LoggingNarrativeContext(
        INarrativeContext inner, ILogger logger)
        : this(inner, logger, TraceLoggingOptions.Default)
    {
    }

    /// <summary>
    /// Wraps <paramref name="inner"/> and logs to <paramref name="logger"/>
    /// with the given <paramref name="options"/> controlling per-event levels,
    /// stamping the optional <paramref name="serviceIdentity"/> into enter-log
    /// scope.
    /// </summary>
    public LoggingNarrativeContext(
        INarrativeContext inner, ILogger logger,
        TraceLoggingOptions options,
        ServiceIdentity? serviceIdentity = null)
    {
        _inner = inner;
        _serviceIdentity = serviceIdentity;
        _emitter = new TraceLogEmitter(logger, options);
    }

    /// <summary>Whether the wrapped context is actively capturing.</summary>
    public bool IsActive => _inner.IsActive;

    /// <inheritdoc/>
    /// <remarks>
    /// Forwards the inner context's accounting rather than answering zero: a
    /// decorator that swallowed the loss would be the layer that made it
    /// silent. <see cref="TraceLoss.None"/> when the inner context cannot lose
    /// anything.
    /// </remarks>
    public TraceLoss TraceLoss =>
        (_inner as ITraceLossSource)?.TraceLoss ?? TraceLoss.None;

    /// <summary>
    /// Whether the wrapped context retains parameter values.
    /// </summary>
    public bool CapturesParameterValues => _inner.CapturesParameterValues;

    /// <summary>The wrapped context's current trace id, if any.</summary>
    public TraceId? CurrentTraceId => _inner.CurrentTraceId;

    /// <summary>
    /// Returns the wrapped context's trace id, generating one if needed.
    /// </summary>
    public TraceId EnsureTraceId() => _inner.EnsureTraceId();

    /// <summary>
    /// Creates a concurrent child from the wrapped context when it supports
    /// <see cref="IConcurrentChildFactory"/>; otherwise returns the wrapped
    /// context itself. The child is not re-decorated with logging.
    /// </summary>
    public INarrativeContext CreateConcurrentChild() =>
        _inner is IConcurrentChildFactory factory
            ? factory.CreateConcurrentChild()
            : _inner;

    /// <summary>The wrapped context's story id, if any.</summary>
    public string? StoryId => _inner.StoryId;

    /// <summary>The wrapped context's chapter id, if any.</summary>
    public string? ChapterId => _inner.ChapterId;

    /// <summary>
    /// Enters a span on the wrapped context, then logs an enter record for it
    /// (span/parent/trace/story/chapter ids, class, method and depth).
    /// </summary>
    public SpanId EnterMethod(
        string className,
        string methodName,
        IReadOnlyList<ParameterCapture> parameters,
        MethodOptions? options = null)
    {
        var handle = _inner.EnterMethod(
            className, methodName, parameters, options);
        var parent = _inner.ParentOf(handle);
        LogEnter(className, methodName, handle, parent, parameters);
        return handle;
    }

    /// <summary>
    /// Exits the span on the wrapped context with a rendered return value, then
    /// logs a return record for it.
    /// </summary>
    public void ExitMethodWithReturn(
        string? renderedValue, SpanId? handle = null)
    {
        _inner.ExitMethodWithReturn(renderedValue, handle);
        _emitter.Return(BuildExitScope(handle), renderedValue);
    }

    /// <summary>
    /// Exits the span on the wrapped context with both the flat and structured
    /// return value, then logs a return record carrying the rendered value.
    /// </summary>
    public void ExitMethodWithReturn(
        string? renderedValue,
        RenderedValue? structuredValue,
        SpanId? handle = null)
    {
        _inner.ExitMethodWithReturn(
            renderedValue, structuredValue, handle);
        _emitter.Return(BuildExitScope(handle), renderedValue);
    }

    /// <summary>
    /// Exits the span on the wrapped context with an exception, then logs an
    /// exception record with the sanitized type and message.
    /// </summary>
    public void ExitMethodWithException(
        Exception? exception, SpanId? handle = null)
    {
        _inner.ExitMethodWithException(exception, handle);
        LogExitError(handle, exception);
    }

    /// <summary>
    /// Exits the span on the wrapped context with an exception and error
    /// narrative, then logs an exception record including the sanitized
    /// <paramref name="errorContext"/>.
    /// </summary>
    public void ExitMethodWithException(
        Exception? exception, string? errorContext,
        SpanId? handle = null)
    {
        _inner.ExitMethodWithException(
            exception, errorContext, handle);
        LogExitError(handle, exception, errorContext);
    }

    /// <summary>
    /// Detaches a frame on the wrapped context. No log record is emitted.
    /// </summary>
    public void DetachFrame(SpanId handle)
    {
        _inner.DetachFrame(handle);
    }

    /// <summary>Captures the completed trace tree from the wrapped context.</summary>
    public TraceTree CaptureTrace()
    {
        return _inner.CaptureTrace();
    }

    /// <summary>Resets the wrapped context.</summary>
    public void Reset()
    {
        _inner.Reset();
    }

    /// <summary>
    /// Captures a re-activatable snapshot from the wrapped context.
    /// </summary>
    public IContextSnapshot Snapshot()
    {
        return _inner.Snapshot();
    }

    /// <summary>Returns the parent span of <paramref name="handle"/>, if any.</summary>
    public SpanId? ParentOf(SpanId handle)
    {
        return _inner.ParentOf(handle);
    }

    /// <summary>
    /// Runs <paramref name="fn"/> scoped to <paramref name="handle"/> on the
    /// wrapped context.
    /// </summary>
    public T RunScoped<T>(SpanId handle, Func<T> fn)
    {
        return _inner.RunScoped(handle, fn);
    }

    /// <summary>
    /// Grafts a foreign subtree onto the wrapped context, then logs a graft
    /// record carrying the node's concurrency group, kind and task label.
    /// </summary>
    public void GraftChild(TraceNode node)
    {
        _inner.GraftChild(node);
        _emitter.Graft(
            BuildGraftScope(node),
            node.Signature.ClassName,
            node.Signature.MethodName);
    }

    /// <summary>
    /// Sets the request context on the wrapped context. No log record is
    /// emitted.
    /// </summary>
    public void SetRequestContext(
        string? httpMethod, HttpRoute? httpRoute, ClientIp? clientIp)
    {
        _inner.SetRequestContext(httpMethod, httpRoute, clientIp);
    }

    /// <summary>
    /// Sets the user context on the wrapped context. No log record is emitted.
    /// </summary>
    public void SetUserContext(
        EnduserId? enduserId, SessionId? sessionId, TenantId? tenantId)
    {
        _inner.SetUserContext(enduserId, sessionId, tenantId);
    }

    private void LogEnter(
        string className, string methodName,
        SpanId handle, SpanId? parent,
        IReadOnlyList<ParameterCapture> parameters)
    {
        var depth = _depth.Value + 1;
        _depth.Value = depth;
        _emitter.Enter(
            BuildEnterScope(className, methodName, handle, parent, depth),
            className, methodName, parameters);
    }

    /// <summary>
    /// The span identity and service keys every line carries, so an enter and
    /// its exit correlate to the same trace.
    /// </summary>
    /// <remarks>
    /// Shared by both scope builders deliberately: these keys drifted apart once
    /// already, leaving exit lines with no <c>nt.traceId</c> and therefore no way
    /// to correlate them by the field aggregators filter on.
    /// </remarks>
    private Dictionary<string, object> BuildSpanScope(
        SpanId? handle, SpanId? parent, int depth)
    {
        var traceId = _inner.CurrentTraceId;
        var scope = new Dictionary<string, object>
        {
            ["nt.spanId"] = handle?.Value ?? "",
            ["nt.parentSpanId"] = parent?.Value ?? "",
            ["nt.traceId"] = traceId?.Value ?? "",
            ["nt.traceName"] = traceId?.HumanName ?? "",
            ["nt.depth"] = depth,
        };
        AddServiceKeys(scope, _serviceIdentity);
        return scope;
    }

    private Dictionary<string, object> BuildEnterScope(
        string className, string methodName,
        SpanId handle, SpanId? parent, int depth)
    {
        var scope = BuildSpanScope(handle, parent, depth);
        scope["nt.storyId"] = _inner.StoryId ?? "";
        scope["nt.chapterId"] = _inner.ChapterId ?? "";
        scope["nt.class"] = className;
        scope["nt.method"] = methodName;
        scope["nt.entryType"] = "entry";
        scope["nt.schemaVersion"] = CanonicalSchema.Version;
        return scope;
    }

    internal static void AddServiceKeys(
        Dictionary<string, object> scope, ServiceIdentity? identity)
    {
        if (identity is null)
        {
            return;
        }

        if (identity.ServiceName is { } name)
        {
            scope["service.name"] = name;
        }

        if (identity.ServiceVersion is { } version)
        {
            scope["service.version"] = version;
        }

        if (identity.Environment is { } env)
        {
            scope["service.environment"] = env;
        }
    }

    private void LogExitError(
        SpanId? handle, Exception? error,
        string? errorContext = null)
    {
        var type = error?.GetType().Name ?? "Unknown";
        var message = error is null ? string.Empty : ExceptionMessage.Text(error);
        var context = errorContext is null
            ? null
            : ControlEscape.Sanitize(errorContext);
        _emitter.Exception(
            BuildExitScope(handle), type, message, context);
    }

    private Dictionary<string, object> BuildExitScope(
        SpanId? handle)
    {
        var depth = Math.Max(0, _depth.Value - 1);
        _depth.Value = depth;
        var parent = handle is { } h ? _inner.ParentOf(h) : null;
        return BuildSpanScope(handle, parent, depth);
    }

    /// <summary>
    /// Logs a fork-created lifecycle record for the given concurrency group.
    /// </summary>
    public void OnForkCreated(string groupId)
    {
        _emitter.ForkCreated(GroupScope(groupId), groupId);
    }

    /// <summary>
    /// Logs a join-complete lifecycle record for the given group, including
    /// member count and wall time in milliseconds.
    /// </summary>
    public void OnJoinComplete(
        string groupId, int memberCount,
        long wallTimeTicks)
    {
        var scope = GroupScope(groupId);
        scope["nt.memberCount"] = memberCount;
        scope["nt.wallTimeMs"] =
            wallTimeTicks / TimeSpan.TicksPerMillisecond;
        _emitter.JoinComplete(scope, groupId, memberCount);
    }

    /// <summary>
    /// Logs a fire-and-forget-launched lifecycle record for the given group.
    /// </summary>
    public void OnFireAndForgetLaunched(string groupId)
    {
        _emitter.FanfLaunched(GroupScope(groupId), groupId);
    }

    private static Dictionary<string, object> GroupScope(string groupId)
    {
        return new Dictionary<string, object>
        {
            ["nt.groupId"] = groupId,
        };
    }

    private static Dictionary<string, object>
        BuildGraftScope(TraceNode node)
    {
        var scope = new Dictionary<string, object>();
        if (node.Concurrency is null)
        {
            return scope;
        }

        scope["nt.groupId"] = node.Concurrency.GroupId;
        scope["nt.kind"] =
            node.Concurrency.Kind.ToString();
        scope["nt.taskLabel"] =
            node.Concurrency.TaskLabel;
        return scope;
    }
}
