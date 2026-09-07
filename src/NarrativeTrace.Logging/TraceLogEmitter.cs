// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using Microsoft.Extensions.Logging;
using NarrativeTrace.Core;
using System.Text;

namespace NarrativeTrace.Logging;

/// <summary>
/// Owns the source-generated <see cref="LoggerMessage"/> delegates shared by
/// the <see cref="LoggingNarrativeContext"/> decorator and the
/// <see cref="LoggingTraceEventListener"/> event-stream adapter. Each emit
/// method wraps its structured message in the supplied <c>BeginScope</c>
/// dictionary and pops it immediately after the log call.
/// </summary>
internal sealed class TraceLogEmitter
{
    private readonly ILogger _logger;
    private Action<ILogger, string, string, string, Exception?> _logEnter = null!;
    private Action<ILogger, string?, Exception?> _logReturn = null!;
    private Action<ILogger, Exception?> _logCompleted = null!;
    private Action<ILogger, string, string, Exception?> _logException = null!;
    private Action<ILogger, string, string, string, Exception?>
        _logExceptionCtx = null!;
    private Action<ILogger, string, string, Exception?> _logGraft = null!;
    private Action<ILogger, string, Exception?> _logForkCreated = null!;
    private Action<ILogger, string, int, Exception?> _logJoinComplete = null!;
    private Action<ILogger, string, Exception?> _logFanfLaunched = null!;

    public TraceLogEmitter(ILogger logger, TraceLoggingOptions options)
    {
        _logger = logger;
        BuildEventDelegates(options);
        BuildLifecycleDelegates(options);
    }

    private void BuildEventDelegates(TraceLoggingOptions options)
    {
        // Message text mirrors the Java slf4j bridge byte-for-byte, glyphs
        // included, so a trace reads identically across runtimes. The placeholders
        // double as structured fields here, which SLF4J's positional {} do not —
        // that is why Params also surfaces as a field with no Java counterpart.
        _logEnter = LoggerMessage.Define<string, string, string>(
            options.EnterLevel,
            new EventId(1, "NarrativeEnter"),
            "\u2192 {Class}.{Method}({Params})");
        _logReturn = LoggerMessage.Define<string?>(
            options.ReturnLevel,
            new EventId(2, "NarrativeExitReturn"),
            "\u2190 returned: {Value}");
        _logCompleted = LoggerMessage.Define(
            options.ReturnLevel,
            new EventId(9, "NarrativeExitCompleted"),
            "\u2190 completed");
        _logException = LoggerMessage.Define<string, string>(
            options.ExceptionLevel,
            new EventId(3, "NarrativeExitException"),
            "!! {ExceptionType}: {ExceptionMessage}");
        _logExceptionCtx = LoggerMessage.Define<string, string, string>(
            options.ExceptionLevel,
            new EventId(8, "NarrativeExitExceptionCtx"),
            "!! {ExceptionType}: {ExceptionMessage} [{ErrorContext}]");
    }

    private void BuildLifecycleDelegates(TraceLoggingOptions options)
    {
        _logGraft = LoggerMessage.Define<string, string>(
            options.EnterLevel,
            new EventId(4, "NarrativeGraft"),
            "Graft {Class}.{Method}");
        _logForkCreated = LoggerMessage.Define<string>(
            options.EnterLevel,
            new EventId(5, "NarrativeForkCreated"),
            "\u2442 fork group created [groupId: {GroupId}]");
        _logJoinComplete = LoggerMessage.Define<string, int>(
            options.EnterLevel,
            new EventId(6, "NarrativeJoinComplete"),
            "\u2443 fork joined [groupId: {GroupId}, members: {MemberCount}]");
        _logFanfLaunched = LoggerMessage.Define<string>(
            options.EnterLevel,
            new EventId(7, "NarrativeFanfLaunched"),
            "\u2933 fire-and-forget launched [groupId: {GroupId}]");
    }

    /// <summary>
    /// Logs a method entry as <c>→ Class.Method(name: value, …)</c>, mirroring
    /// the Java bridge.
    /// </summary>
    /// <param name="scope">Span identity and service keys for the line.</param>
    /// <param name="className">Declaring type's simple name.</param>
    /// <param name="methodName">Method name.</param>
    /// <param name="parameters">
    /// Captured arguments, rendered inline. Redacted captures surface the
    /// redaction marker rather than their stored value — the same backstop every
    /// renderer applies. Below <see cref="TracingLevel.Detail"/> capture has
    /// already blanked the values, so names appear with empty values.
    /// </param>
    public void Enter(
        Dictionary<string, object> scope,
        string className, string methodName,
        IReadOnlyList<ParameterCapture> parameters)
    {
        using (_logger.BeginScope(scope))
        {
            _logEnter(
                _logger, className, methodName,
                FormatParameters(parameters), null);
        }
    }

    /// <summary>
    /// Renders a captured argument list as Java does: <c>name: value</c> pairs
    /// joined by <c>", "</c>, with redacted values replaced by the marker.
    /// </summary>
    internal static string FormatParameters(
        IReadOnlyList<ParameterCapture> parameters)
    {
        if (parameters.Count == 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        for (var i = 0; i < parameters.Count; i++)
        {
            if (i > 0)
            {
                sb.Append(", ");
            }

            var p = parameters[i];
            sb.Append(p.Name).Append(": ").Append(
                p.Redacted ? RedactionPolicy.Marker : p.RenderedValue);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Void-completion contract: a null rendered value means the method
    /// carried no value at all, which logs as a completion rather than a
    /// fabricated "null" return.
    /// </summary>
    public void Return(
        Dictionary<string, object> scope, string? renderedValue)
    {
        using (_logger.BeginScope(scope))
        {
            if (renderedValue is null)
            {
                _logCompleted(_logger, null);
            }
            else
            {
                _logReturn(_logger, renderedValue, null);
            }
        }
    }

    public void Exception(
        Dictionary<string, object> scope,
        string type, string message, string? errorContext)
    {
        using (_logger.BeginScope(scope))
        {
            if (errorContext is null)
            {
                _logException(_logger, type, message, null);
                return;
            }

            _logExceptionCtx(_logger, type, message, errorContext, null);
        }
    }

    public void Graft(
        Dictionary<string, object> scope,
        string className, string methodName)
    {
        using (_logger.BeginScope(scope))
        {
            _logGraft(_logger, className, methodName, null);
        }
    }

    public void ForkCreated(
        Dictionary<string, object> scope, string groupId)
    {
        using (_logger.BeginScope(scope))
        {
            _logForkCreated(_logger, groupId, null);
        }
    }

    public void JoinComplete(
        Dictionary<string, object> scope,
        string groupId, int memberCount)
    {
        using (_logger.BeginScope(scope))
        {
            _logJoinComplete(_logger, groupId, memberCount, null);
        }
    }

    public void FanfLaunched(
        Dictionary<string, object> scope, string groupId)
    {
        using (_logger.BeginScope(scope))
        {
            _logFanfLaunched(_logger, groupId, null);
        }
    }
}
