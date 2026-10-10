// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Diagnostics;
using NarrativeTrace.Core;
using NarrativeTrace.Observability;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace Example.Orders;

/// <summary>
/// The framework table's OpenTelemetry wiring, compiled here and exercised by
/// <c>OpenTelemetryWiringTests</c>. The doctor's <c>config.otel-source</c> fix prints the
/// <c>wiring</c> region verbatim (carried by <c>wiring-snippets.md</c>).
/// </summary>
public static class OpenTelemetryWiring
{
    /// <summary>
    /// Replays <paramref name="context"/>'s captured trace as spans through a tracer provider that
    /// listens to NarrativeTrace's source and hands them to <paramref name="exporter"/>.
    /// </summary>
    public static void Export(INarrativeContext context, BaseExporter<Activity> exporter)
    {
        // snippet:begin wiring
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource("NarrativeTrace") // the ActivitySource NarrativeTrace emits its spans under
            .AddProcessor(new SimpleActivityExportProcessor(exporter)) // your exporter
            .Build();

        TraceActivityExporter.Export(context.CaptureTrace()); // one span per traced call
        // snippet:end wiring
    }
}
