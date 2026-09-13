// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Cli.Doctor;

/// <summary>
/// The <c>doctor</c> verb: read-only diagnosis of a NarrativeTrace install.
/// Builds a snapshot, runs every registered check, and renders the report.
/// Zero network, mutates nothing.
/// </summary>
public static class DoctorCommand
{
    /// <summary>
    /// Runs the doctor verb.
    /// </summary>
    /// <param name="cwd">The directory to diagnose.</param>
    /// <param name="readEnv">Reads an environment variable by name.</param>
    /// <param name="json">Whether to render JSON instead of human-readable text.</param>
    /// <param name="output">Sink for the report.</param>
    /// <param name="error">Sink for a could-not-run message.</param>
    /// <returns>0 clean, 1 findings, 2 could not run.</returns>
    public static int Run(
        string cwd, Func<string, string?> readEnv, bool json, TextWriter output, TextWriter error)
    {
        if (!Directory.Exists(cwd))
        {
            error.WriteLine($"error: could not run: no such directory {cwd}");
            return 2;
        }

        var snapshot = DoctorSnapshotBuilder.Build(cwd, readEnv);
        if (snapshot.ProjectFiles.Count == 0)
        {
            error.WriteLine($"error: could not run: no .csproj found under {cwd}");
            return 2;
        }

        var report = DoctorChecks.Run(snapshot);
        output.WriteLine(json ? DoctorRenderer.RenderJson(report) : DoctorRenderer.RenderHuman(report));
        return report.ExitCode;
    }
}
