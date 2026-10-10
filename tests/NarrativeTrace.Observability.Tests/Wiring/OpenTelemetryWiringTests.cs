// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Diagnostics;
using Example.Orders;
using NarrativeTrace.Core;
using NarrativeTrace.Runtime;
using OpenTelemetry;
using Xunit;

namespace NarrativeTrace.Observability.Tests.Wiring;

/// <summary>
/// Runs alone: <see cref="TraceActivityExporter"/> emits through ONE static <c>"NarrativeTrace"</c>
/// source, so spans this proof exports would land in any listener another test class has open on it at
/// the same moment (it did — <c>TraceActivityExporterTests</c> counted a third span).
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class SharedActivitySourceCollection
{
    /// <summary>The collection's name.</summary>
    public const string Name = "the shared NarrativeTrace ActivitySource";
}

/// <summary>
/// Proves the framework table's OpenTelemetry snippet (<see cref="OpenTelemetryWiring"/>): a
/// captured call reaches the registered exporter as a span.
/// </summary>
[Collection(SharedActivitySourceCollection.Name)]
public sealed class OpenTelemetryWiringTests
{
    private sealed class RecordingExporter : BaseExporter<Activity>
    {
        public List<string> DisplayNames { get; } = [];

        public override ExportResult Export(in Batch<Activity> batch)
        {
            foreach (var activity in batch)
            {
                DisplayNames.Add(activity.DisplayName);
            }

            return ExportResult.Success;
        }
    }

    [Fact]
    public void A_captured_call_reaches_the_exporter_as_a_span()
    {
        var context = new SyncNarrativeContext(new NarrativeTraceConfig(TracingLevel.Detail));
        context.EnterMethod("IOrderService", "PlaceOrder", []);
        context.ExitMethodWithReturn("confirmed");
        using var exporter = new RecordingExporter();

        OpenTelemetryWiring.Export(context, exporter);

        Assert.Contains(exporter.DisplayNames, name => name.Contains("PlaceOrder", StringComparison.Ordinal));
    }
}
