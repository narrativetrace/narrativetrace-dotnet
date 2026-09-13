// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Cli.Doctor.Checks;

/// <summary>
/// <c>trap.silent-sink</c>: a project can wire up tracing — a proxy, DI
/// auto-wrap, or the ASP.NET Core middleware — and never attach anything
/// that reads the result. Every traced call narrates to nowhere.
/// </summary>
public static class TrapSilentSinkCheck
{
    private const string Id = "trap.silent-sink";

    private static readonly string[] WiringSignals =
        ["NarrativeTraceProxy.Create", "AddNarrativeTracing(", "AddNarrativeTrace("];

    private static readonly string[] SinkSignals =
    [
        "CaptureTrace(", "TraceLogExporter", "IndentedTextRenderer", "MarkdownRenderer",
        "ProseRenderer", "ITraceExporter", "BufferedEventConsumer", "NarrativeTrace.Observability",
        "NarrativeTrace.Testing",
    ];

    /// <summary>Runs the check.</summary>
    public static DoctorFinding Run(DoctorSnapshot snapshot)
    {
        if (!AnySignal(snapshot, WiringSignals))
        {
            return DoctorFinding.Pass(
                Id, "tracing is not wired up in this project — nothing to check",
                DoctorDocUrls.InstallationConfigureOutput);
        }

        return AnySignal(snapshot, SinkSignals) ? Pass() : Fail();
    }

    private static bool AnySignal(DoctorSnapshot snapshot, string[] signals)
    {
        return snapshot.SourceFiles.Values.Any(content => signals.Any(content.Contains));
    }

    private static DoctorFinding Pass()
    {
        return DoctorFinding.Pass(
            Id, "tracing is wired up and a consumer/renderer/exporter is attached",
            DoctorDocUrls.InstallationConfigureOutput);
    }

    private static DoctorFinding Fail()
    {
        return DoctorFinding.Fail(
            Id,
            "tracing is wired up but no consumer, renderer, or exporter was found",
            "Attach a sink: call CaptureTrace() and render/write the tree, export via " +
            "TraceLogExporter or an ITraceExporter, or observe through NarrativeTrace.Observability " +
            "— otherwise every traced call narrates to nowhere.",
            DoctorDocUrls.InstallationConfigureOutput);
    }
}
