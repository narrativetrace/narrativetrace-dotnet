// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Core;

/// <summary>
/// Request outcome metadata known only after a request completes.
/// </summary>
public sealed record RequestContext(int StatusCode, long DurationMs);

/// <summary>
/// Request-boundary export hook. Framework integrations call this after a
/// request cycle completes and a trace tree is ready to hand off to logging,
/// storage, or telemetry. Exporters consume completed traces; they do not
/// participate in capture.
/// </summary>
public interface ITraceExporter
{
    /// <summary>
    /// Exports a captured trace with request outcome metadata. Integrations
    /// typically skip export for empty trees.
    /// </summary>
    void Export(TraceTree tree, RequestContext requestContext);
}
