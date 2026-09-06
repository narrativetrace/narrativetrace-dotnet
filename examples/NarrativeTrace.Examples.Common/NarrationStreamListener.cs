// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using Microsoft.Extensions.Logging;
using NarrativeTrace.Core;

namespace NarrativeTrace.Examples.Common;

/// <summary>
/// Turns the live event stream into the <c>→ ← !!</c> log lines the demo
/// launcher colorizes — one <see cref="ILogger"/> message per event, the
/// same shapes the Java <c>Slf4jTraceEventListener</c> emits, so
/// <c>demo/colorize.awk</c> serves both platforms unchanged.
/// </summary>
/// <remarks>
/// Subscribe it as the synchronous path of a <c>DualPathPipeline</c>:
/// <c>new DualPathPipeline(listener.OnEvent)</c>. Entries and returns log at
/// <see cref="LogLevel.Trace"/>, exceptions at <see cref="LogLevel.Warning"/>.
/// </remarks>
public sealed class NarrationStreamListener(ILogger logger)
{
    private static readonly Action<ILogger, string, string, string, Exception?> Entered =
        LoggerMessage.Define<string, string, string>(
            LogLevel.Trace, new EventId(1, "Enter"), "→ {Class}.{Method}({Parameters})");

    private static readonly Action<ILogger, string, Exception?> ReturnedValue =
        LoggerMessage.Define<string>(LogLevel.Trace, new EventId(2, "Return"), "← returned: {Value}");

    private static readonly Action<ILogger, Exception?> Completed =
        LoggerMessage.Define(LogLevel.Trace, new EventId(3, "Completed"), "← completed");

    private static readonly Action<ILogger, string, string, Exception?> Threw =
        LoggerMessage.Define<string, string>(
            LogLevel.Warning, new EventId(4, "Exception"), "!! {Type}: {Message}");

    private static readonly Action<ILogger, string, string, string, Exception?> ThrewWithContext =
        LoggerMessage.Define<string, string, string>(
            LogLevel.Warning, new EventId(5, "ExceptionContext"), "!! {Type}: {Message} [{ErrorContext}]");

    private static readonly Action<ILogger, string, Exception?> ForkCreated =
        LoggerMessage.Define<string>(
            LogLevel.Trace, new EventId(6, "ForkCreated"), "⑂ fork group created [groupId: {GroupId}]");

    private static readonly Action<ILogger, string, int, Exception?> ForkJoined =
        LoggerMessage.Define<string, int>(
            LogLevel.Trace, new EventId(7, "ForkJoined"), "⑃ fork joined [groupId: {GroupId}, members: {Members}]");

    private static readonly Action<ILogger, string, Exception?> FireAndForgetLaunched =
        LoggerMessage.Define<string>(
            LogLevel.Trace, new EventId(8, "FireAndForget"), "⤳ fire-and-forget launched [groupId: {GroupId}]");

    /// <summary>Logs one trace event.</summary>
    public void OnEvent(TraceEvent traceEvent)
    {
        ArgumentNullException.ThrowIfNull(traceEvent);
        switch (traceEvent)
        {
            case EnterEvent enter:
                Entered(logger, enter.Signature.ClassName, enter.Signature.MethodName, Parameters(enter.Signature), null);
                break;
            case ExitEvent exit:
                LogExit(exit);
                break;
            case ForkCreatedEvent fork:
                ForkCreated(logger, fork.GroupId, null);
                break;
            case MergeEvent merge:
                ForkJoined(logger, merge.GroupId, merge.MemberCount, null);
                break;
            case FireAndForgetEvent launched:
                FireAndForgetLaunched(logger, launched.GroupId, null);
                break;
        }
    }

    private static string Parameters(MethodSignature sig)
    {
        return string.Join(", ", sig.Parameters.Select(
            p => $"{p.Name}: {(p.Redacted ? RedactionPolicy.Marker : p.RenderedValue)}"));
    }

    private void LogExit(ExitEvent exit)
    {
        switch (exit.Outcome)
        {
            case Returned { RenderedValue: { } value }:
                ReturnedValue(logger, value, null);
                break;
            case Returned:
                Completed(logger, null);
                break;
            case Core.Threw threw:
                LogException(threw.Error, exit.ErrorContext);
                break;
        }
    }

    private void LogException(Exception? error, string? errorContext)
    {
        var type = error?.GetType().Name ?? "Unknown";
        var message = ControlEscape.Sanitize(error?.Message ?? "");
        if (errorContext is null)
        {
            Threw(logger, type, message, null);
            return;
        }

        ThrewWithContext(logger, type, message, ControlEscape.Sanitize(errorContext), null);
    }
}
