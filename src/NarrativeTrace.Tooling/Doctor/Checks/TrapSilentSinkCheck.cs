// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Text.RegularExpressions;
using NarrativeTrace.Tooling.Frameworks;

namespace NarrativeTrace.Tooling.Doctor.Checks;

/// <summary>
/// <c>trap.silent-sink</c>: a project can wire up tracing — a proxy or DI
/// auto-wrap — and never attach anything that reads the result. Every traced
/// call narrates to nowhere.
/// </summary>
/// <remarks>
/// The framework table's <c>default-logger</c> row folds into this check: the
/// fix carries that row's package and its snippet (the one call that sends a
/// captured trace to the application's own logger), and that snippet is a
/// sink here — the fix the doctor prints is the fix it accepts. The ASP.NET
/// Core registration is a sink too: <c>AddNarrativeTrace</c> registers
/// <c>LoggerTraceExporter</c> as the default exporter.
/// </remarks>
public static class TrapSilentSinkCheck
{
    private const string Id = "trap.silent-sink";

    private static readonly Regex[] WiringSignals =
        [Signal(@"\bNarrativeTraceProxy\s*\.\s*Create\b"), Call("AddNarrativeTracing"), Call("AddNarrativeTrace")];

    private static readonly Regex[] SinkSignals =
    [
        Call("CaptureTrace"), Signal(@"\bTraceLogExporter\b"), Signal(@"\bIndentedTextRenderer\b"),
        Signal(@"\bMarkdownRenderer\b"), Signal(@"\bProseRenderer\b"), Signal(@"\bITraceExporter\b"),
        Signal(@"\bBufferedEventConsumer\b"), Signal(@"\bNarrativeTrace\.Observability\b"),
        Signal(@"\bNarrativeTrace\.Testing"), Call("AddNarrativeLogging"), Call("AddNarrativeTrace"),
    ];

    private static readonly FrameworkRow DefaultLogger =
        FrameworkTable.Row("default-logger") ?? throw new InvalidOperationException("the table has no default-logger row");

    /// <summary>Runs the check.</summary>
    public static DoctorFinding Run(DoctorSnapshot snapshot)
    {
        if (!AnySignal(snapshot, WiringSignals))
        {
            return DoctorFinding.Pass(
                Id, "tracing is not wired up in this project — nothing to check",
                DoctorDocUrls.InstallationConfigureOutput);
        }

        return AnySignal(snapshot, SinkSignals) ? Pass() : Fail(snapshot.ProjectResolvedFamilyVersion);
    }

    /// <summary>Whether any comment-free source shows any of the signals — commented-out code shows nothing.</summary>
    private static bool AnySignal(DoctorSnapshot snapshot, Regex[] signals)
    {
        return snapshot.SourceTexts().Any(content => signals.Any(signal => signal.IsMatch(content)));
    }

    private static Regex Call(string method) => Signal($@"\b{method}\s*\(");

    private static Regex Signal(string pattern) => new(pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static DoctorFinding Pass()
    {
        return DoctorFinding.Pass(
            Id, "tracing is wired up and a consumer/renderer/exporter is attached",
            DoctorDocUrls.InstallationConfigureOutput);
    }

    private static DoctorFinding Fail(string? familyVersion)
    {
        return DoctorFinding.Fail(
            Id,
            "tracing is wired up but no consumer, renderer, or exporter was found",
            "Attach a sink: call CaptureTrace() and render/write the tree, export via " +
            "TraceLogExporter or an ITraceExporter, or observe through NarrativeTrace.Observability " +
            "— otherwise every traced call narrates to nowhere. To send it to the application's own " +
            $"logger, add {DefaultLogger.Package.PackageId} (`{DefaultLogger.Package.AddCommand(familyVersion)}`) and " +
            $"{DefaultLogger.Wiring.Description}, adapted to this project's own names:\n" +
            WiringSnippets.Text(DefaultLogger.Id),
            DoctorDocUrls.InstallationConfigureOutput);
    }
}
