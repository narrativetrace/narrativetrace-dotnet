// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Tooling.Doctor;
using NarrativeTrace.Tooling.Feedback;
using NarrativeTrace.Tooling.Init;

namespace NarrativeTrace.Cli;

/// <summary>
/// <c>doctor --report</c>: leaves the doctor's JSON report where <c>feedback</c> reads it
/// (<see cref="FeedbackGatherer.DoctorReportPath"/>), so a problem report can carry the very
/// findings that prompted it.
/// </summary>
/// <remarks>
/// INTENT: the report is written only when the doctor actually produced one — a run that could not
/// start (exit 2) leaves no file, so a stale report is never mistaken for a fresh one. The file is
/// always JSON, whichever form the person asked to see.
/// </remarks>
internal static class DoctorReportFile
{
    /// <summary>Runs the doctor, prints it as asked, and writes the JSON report beside the project.</summary>
    internal static int Run(
        string dir, Func<string, string?> readEnv, Func<Carrier> openCarrier, bool json,
        TextWriter output, TextWriter error)
    {
        var captured = new StringWriter();
        var exit = DoctorCommand.Run(dir, readEnv, openCarrier, true, captured, error);
        if (exit == 2)
        {
            return exit;
        }

        Write(dir, captured.ToString());
        if (json)
        {
            output.Write(captured.ToString());
            return exit;
        }

        return DoctorCommand.Run(dir, readEnv, openCarrier, false, output, error);
    }

    private static void Write(string dir, string report)
    {
        var path = Path.Combine(dir, FeedbackGatherer.DoctorReportPath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, report);
    }
}
