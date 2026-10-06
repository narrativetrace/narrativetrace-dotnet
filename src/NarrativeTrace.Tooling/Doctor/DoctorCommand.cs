// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Tooling.Init;

namespace NarrativeTrace.Tooling.Doctor;

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
    /// <param name="openCarrier">
    /// Resolves the skills carrier <c>config.skills-installed</c> reads its catalogue from — the same
    /// carrier resolution <c>init</c> uses (D9). This layer has no sensible default of its own: the
    /// bundled carrier lives in the CLI launcher's own embedded resources, an assembly this library
    /// must never reference, so the caller always supplies it.
    /// </param>
    /// <param name="json">Whether to render JSON instead of human-readable text.</param>
    /// <param name="output">Sink for the report.</param>
    /// <param name="error">Sink for a could-not-run message.</param>
    /// <returns>0 clean, 1 findings, 2 could not run.</returns>
    public static int Run(
        string cwd, Func<string, string?> readEnv, Func<Carrier> openCarrier, bool json,
        TextWriter output, TextWriter error)
    {
        if (!Directory.Exists(cwd))
        {
            error.WriteLine($"error: could not run: no such directory {cwd}");
            return 2;
        }

        var snapshot = DoctorSnapshotBuilder.Build(cwd, readEnv, openCarrier);
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
