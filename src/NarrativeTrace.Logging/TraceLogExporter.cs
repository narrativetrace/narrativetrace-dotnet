// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using Microsoft.Extensions.Logging;
using NarrativeTrace.Core;

namespace NarrativeTrace.Logging;

/// <summary>
/// One-shot exporter that writes a captured <see cref="TraceTree"/> to an
/// <see cref="ILogger"/> as an indented call tree, one log record per node.
/// </summary>
/// <remarks>
/// Unlike <see cref="LoggingNarrativeContext"/> and
/// <see cref="LoggingTraceEventListener"/>, which log incrementally as spans
/// occur, this replays a completed tree after the fact — call
/// <c>TraceLogExporter.ExportToLogger(context.CaptureTrace(), logger)</c>. Each
/// node is rendered at <see cref="LogLevel.Information"/> as
/// <c>Class.Method(params) -&gt; outcome</c>, indented two spaces per depth
/// level, with children following their parent depth-first. Redacted
/// parameters render as <c>[REDACTED]</c>.
/// <para>
/// <b>Ordering with the stock console logger.</b> <c>Microsoft.Extensions
/// .Logging.Console</c> writes through a background queue by default, so the
/// lines this method logs can appear after — or interleaved oddly with —
/// output the caller writes synchronously to the console around this call.
/// That is a platform behavior of the console provider, not something this
/// method does: it calls straight through to the supplied <c>logger</c>
/// synchronously, in tree order, on the calling thread. The fix that
/// actually works is on the caller's side — dispose the <c>ILoggerFactory</c>
/// (or the console provider) before relying on the order, e.g.
/// <c>using var loggerFactory = LoggerFactory.Create(...)</c>: disposal
/// blocks until the provider's background writer thread has drained
/// everything queued. See the Configuration Guide's logging-bridge section
/// and the "Send it to your logger" step of the sixty-seconds tutorial for a
/// worked, verified example.
/// </para>
/// </remarks>
public static class TraceLogExporter
{
    private static readonly Action<
        ILogger, string, string, string, string,
        string, Exception?>
        LogTraceEntry = LoggerMessage.Define<
            string, string, string, string, string>(
            LogLevel.Information,
            new EventId(1, "TraceEntry"),
            "{Indent}{Class}.{Method}({Params})" +
            " -> {Outcome}");

    /// <summary>
    /// Writes every node of <paramref name="tree"/> to
    /// <paramref name="logger"/> as an indented, depth-first call tree.
    /// </summary>
    public static void ExportToLogger(
        TraceTree tree, ILogger logger)
    {
        // TraceNode.Children is a type, not a guarantee of acyclicity -
        // bound once, here, so ExportNodes' recursion below can never
        // overflow the stack or loop forever on a hand-built or replayed
        // cycle. Cheap on ordinary input: TreeWalk.Bound returns Roots
        // unchanged once it confirms there is nothing to bound.
        ExportNodes(TreeWalk.Bound(tree.Roots), logger, 0);
    }

    private static void ExportNodes(
        IReadOnlyList<TraceNode> nodes,
        ILogger logger, int depth)
    {
        for (var i = 0; i < nodes.Count; i++)
        {
            ExportNode(nodes[i], logger, depth);
        }
    }

    private static void ExportNode(
        TraceNode node, ILogger logger, int depth)
    {
        var indent = new string(' ', depth * 2);
        var sig = node.Signature;
        var paramStr = FormatParams(sig.Parameters);

        LogTraceEntry(
            logger, indent, sig.ClassName,
            sig.MethodName, paramStr,
            FormatOutcome(node.Outcome), null);

        ExportNodes(node.Children, logger, depth + 1);
    }

    private static string FormatParams(
        IReadOnlyList<ParameterCapture> parameters)
    {
        if (parameters.Count == 0)
        {
            return string.Empty;
        }

        var parts = new string[parameters.Count];
        for (var i = 0; i < parameters.Count; i++)
        {
            var p = parameters[i];
            parts[i] = p.Redacted
                ? $"{p.Name}: [REDACTED]"
                : $"{p.Name}: {p.RenderedValue}";
        }

        return string.Join(", ", parts);
    }

    private static string FormatOutcome(
        TraceOutcome outcome)
    {
        return outcome switch
        {
            Returned r => r.RenderedValue ?? "void",
            Threw t =>
                $"threw {t.Error?.GetType().Name}",
            Incomplete => "incomplete",
            _ => "unknown",
        };
    }
}
