// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using Microsoft.Extensions.Logging;
using NarrativeTrace.Core;

namespace NarrativeTrace.Logging;

/// <summary>
/// Live event-stream bridge mirroring every drained <see cref="TraceEvent"/> to
/// logs, the .NET twin of the Java <c>Slf4jTraceEventListener</c>. Complements
/// the <see cref="LoggingNarrativeContext"/> decorator: where the decorator logs
/// synchronously at each context call, this adapter subscribes to the event
/// pipeline so consumers can attach a logging sink to the raw stream.
/// </summary>
/// <remarks>
/// Register with a pipeline consumer via
/// <c>consumer.Subscribe(listener.OnEvent)</c>. Scope fields are read from each
/// event's self-describing <see cref="SpanContext"/> — including service
/// identity, which the decorator can only obtain by injection. Depth tracking is
/// local to the draining flow, for readable log indentation only.
/// </remarks>
public sealed class LoggingTraceEventListener
{
    private readonly TraceLogEmitter _emitter;
    private readonly AsyncLocal<int> _depth = new();

    /// <summary>
    /// Creates a listener that logs to <paramref name="logger"/> using
    /// <see cref="TraceLoggingOptions.Default"/>.
    /// </summary>
    public LoggingTraceEventListener(ILogger logger)
        : this(logger, TraceLoggingOptions.Default)
    {
    }

    /// <summary>
    /// Creates a listener that logs to <paramref name="logger"/> with the given
    /// <paramref name="options"/> controlling per-event levels.
    /// </summary>
    public LoggingTraceEventListener(
        ILogger logger, TraceLoggingOptions options)
    {
        _emitter = new TraceLogEmitter(logger, options);
    }

    /// <summary>Mirrors one trace event to the log stream.</summary>
    public void OnEvent(TraceEvent traceEvent)
    {
        switch (traceEvent)
        {
            case EnterEvent enter:
                HandleEnter(enter);
                break;
            case ExitEvent exit:
                HandleExit(exit);
                break;
            default:
                HandleLifecycle(traceEvent);
                break;
        }
    }

    private void HandleLifecycle(TraceEvent traceEvent)
    {
        switch (traceEvent)
        {
            case ForkCreatedEvent fork:
                _emitter.ForkCreated(
                    GroupScope(fork.GroupId), fork.GroupId);
                break;
            case MergeEvent merge:
                _emitter.JoinComplete(
                    GroupScope(merge.GroupId),
                    merge.GroupId, merge.MemberCount);
                break;
            case FireAndForgetEvent fanf:
                _emitter.FanfLaunched(
                    GroupScope(fanf.GroupId), fanf.GroupId);
                break;
        }
    }

    private void HandleEnter(EnterEvent enter)
    {
        var depth = _depth.Value + 1;
        _depth.Value = depth;
        var sig = enter.Signature;
        _emitter.Enter(
            BuildEnterScope(enter.SpanContext, sig, depth),
            sig.ClassName, sig.MethodName, sig.Parameters);
    }

    private void HandleExit(ExitEvent exit)
    {
        var scope = BuildExitScope(exit.SpanContext);
        if (exit.Outcome is Threw threw)
        {
            EmitException(scope, threw.Error, exit.ErrorContext);
            return;
        }

        var value = (exit.Outcome as Returned)?.RenderedValue;
        _emitter.Return(scope, value);
    }

    private void EmitException(
        Dictionary<string, object> scope,
        Exception? error, string? errorContext)
    {
        var type = error?.GetType().Name ?? "Unknown";
        var message = error is null ? string.Empty : ExceptionMessage.Text(error);
        var context = errorContext is null
            ? null
            : ControlEscape.Sanitize(errorContext);
        _emitter.Exception(scope, type, message, context);
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
    private static Dictionary<string, object> BuildSpanScope(
        SpanContext sc, int depth)
    {
        var scope = new Dictionary<string, object>
        {
            ["nt.spanId"] = sc.SpanId.Value,
            ["nt.parentSpanId"] = sc.ParentSpanId?.Value ?? "",
            ["nt.traceId"] = sc.TraceId.Value,
            ["nt.traceName"] = sc.TraceId.HumanName,
            ["nt.depth"] = depth,
        };
        LoggingNarrativeContext.AddServiceKeys(scope, ServiceOf(sc));
        return scope;
    }

    private static Dictionary<string, object> BuildEnterScope(
        SpanContext sc, MethodSignature sig, int depth)
    {
        var scope = BuildSpanScope(sc, depth);
        scope["nt.class"] = sig.ClassName;
        scope["nt.method"] = sig.MethodName;
        scope["nt.entryType"] = "entry";
        scope["nt.schemaVersion"] = CanonicalSchema.Version;
        return scope;
    }

    private Dictionary<string, object> BuildExitScope(SpanContext sc)
    {
        var depth = Math.Max(0, _depth.Value - 1);
        _depth.Value = depth;
        return BuildSpanScope(sc, depth);
    }

    private static ServiceIdentity? ServiceOf(SpanContext sc)
    {
        if (sc.ServiceName is null
            && sc.ServiceVersion is null
            && sc.Environment is null)
        {
            return null;
        }

        return new ServiceIdentity(
            sc.ServiceName, sc.ServiceVersion, sc.Environment);
    }

    private static Dictionary<string, object> GroupScope(string groupId)
    {
        return new Dictionary<string, object>
        {
            ["nt.groupId"] = groupId,
        };
    }
}
