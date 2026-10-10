// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using Microsoft.Extensions.Logging;
using NarrativeTrace.Core;
using NarrativeTrace.Logging;

namespace Example.Orders;

/// <summary>
/// The framework table's logger wiring, compiled here and exercised by <c>LoggerWiringTests</c>.
/// The doctor's <c>trap.silent-sink</c> fix prints the <c>wiring</c> region verbatim (carried by
/// <c>wiring-snippets.md</c>): the one call that sends a captured trace to the application's own
/// <c>ILogger</c>.
/// </summary>
public static class LoggerWiring
{
    /// <summary>Replays <paramref name="context"/>'s captured trace onto <paramref name="loggerFactory"/>.</summary>
    public static void Export(INarrativeContext context, ILoggerFactory loggerFactory)
    {
        // snippet:begin wiring
        TraceLogExporter.ExportToLogger(context.CaptureTrace(), loggerFactory.CreateLogger("NarrativeTrace"));
        // snippet:end wiring
    }
}
