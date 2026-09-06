// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using Microsoft.Extensions.Logging;
using NarrativeTrace.Core;
using NarrativeTrace.Runtime;

namespace NarrativeTrace.AspNetCore;

/// <summary>
/// Request-boundary exporter that logs each completed trace as a single
/// entry — <c>GET /api/orders [200] 42ms — {json}</c> — to the dedicated
/// <c>NarrativeTrace.Export</c> logger category. Use this as the default
/// production exporter when log aggregation is your storage.
/// </summary>
public sealed class LoggerTraceExporter : ITraceExporter
{
    private static readonly Action<
        ILogger, string, string, int, long, string, Exception?>
        LogTrace = LoggerMessage.Define<
            string, string, int, long, string>(
            LogLevel.Information,
            new EventId(1, "TraceExport"),
            "{Method} {Route} [{Status}] {Duration}ms — {Json}");

    private readonly ILogger _logger;

    /// <summary>
    /// Creates the exporter, opening a logger under
    /// <see cref="NarrativeTraceOptions.LoggerName"/> (defaulting to
    /// <c>NarrativeTrace.Export</c>).
    /// </summary>
    /// <param name="loggerFactory">Factory used to create the export logger.</param>
    /// <param name="options">Options supplying the logger category; when null
    /// the default category is used.</param>
    public LoggerTraceExporter(
        ILoggerFactory loggerFactory, NarrativeTraceOptions? options = null)
    {
        var category = options?.LoggerName ?? "NarrativeTrace.Export";
        _logger = loggerFactory.CreateLogger(category);
    }

    /// <summary>
    /// Logs the completed trace as a single information-level entry, tagged
    /// with the request method, route, status, and duration, and carrying the
    /// full trace as JSON. No-ops when the trace is empty or the logger's
    /// information level is disabled.
    /// </summary>
    /// <param name="tree">The captured trace tree for the request.</param>
    /// <param name="requestContext">Outcome facts (status code, duration)
    /// known once the request finished.</param>
    public void Export(TraceTree tree, RequestContext requestContext)
    {
        if (tree.IsEmpty || !_logger.IsEnabled(LogLevel.Information))
        {
            return;
        }

        var root = tree.Roots[0].SpanContext;
        // An HTTP request trace has no test-framework verdict, so the outcome
        // is derived from the trace itself — explicitly here rather than by an
        // exporter guessing on the producer's behalf.
        var json = JsonExporter.Export(
            tree,
            new TraceMetadata(
                "request",
                ScenarioResultExtensions.Of(
                    TraceNode.HasAnyError(tree.Roots))));
        using var scope = _logger.BeginScope(SchemaScope(root));
        LogTrace(
            _logger,
            root?.HttpMethod ?? "?",
            root?.HttpRoute?.Value ?? "?",
            requestContext.StatusCode,
            requestContext.DurationMs,
            json,
            null);
    }

    // Canonical schema Layer-3 fields (matching Slf4jTraceExporter's MDC).
    // Nested inside an existing scope, so any outer trace_id is preserved.
    private static Dictionary<string, object> SchemaScope(SpanContext? root)
    {
        var scope = new Dictionary<string, object>();
        if (root is null)
        {
            return scope;
        }

        scope["nt.entryType"] = "chapter";
        scope["nt.schemaVersion"] = CanonicalSchema.Version;
        Put(scope, "nt.storyId", root.StoryId);
        Put(scope, "nt.chapterId", root.ChapterId);
        scope["trace_id"] = root.TraceId.Value;
        scope["nt.traceName"] = root.TraceId.HumanName;
        return scope;
    }

    private static void Put(
        Dictionary<string, object> scope, string key, string? value)
    {
        if (value is not null)
        {
            scope[key] = value;
        }
    }
}
