// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
#nullable enable
using System.IO;

namespace NarrativeTrace.Build;

/// <summary>
/// Makes a security-scanner target's outcome honest about whether the scanner ran.
/// </summary>
/// <remarks>
/// A 2026-09-08 audit (mirroring a java-side finding) ran <c>SecretsScan</c> and the
/// <c>Semgrep</c>/<c>OsvScan</c> targets without their binaries on <c>PATH</c> and got a clean
/// build — the exact failure class that bit this repository's own first release, where a secrets
/// scanner had gracefully skipped for the project's entire life (release retrospective rule 2: a
/// graceful-skip tool must prove it has ever run). Graceful degradation for developer ergonomics
/// stays, but it can no longer look like a clean scan:
/// <list type="bullet">
/// <item>absence of the binary is a <b>WARN</b> plus a recorded <c>skipped</c> status — never a
/// silent pass;</item>
/// <item>where security assurance is the point (CI, or an explicit required flag), absence
/// <b>fails</b> the target;</item>
/// <item>every scan target records <c>ran-clean</c> / <c>skipped: …</c> under
/// <c>artifacts/security/scan-status/</c>, so "ran clean" and "never ran" are distinguishable
/// after the fact, by humans and by jobs alike.</item>
/// </list>
/// </remarks>
internal static class ScannerGateSupport
{
    /// <summary>What a scan target must do when its binary is absent: fail, or warn with <see cref="Message"/>.</summary>
    public sealed record MissingBinaryDecision(bool Fail, string Message);

    /// <summary>The decision for a missing <paramref name="tool"/> binary, given whether scanners are <paramref name="required"/> here.</summary>
    public static MissingBinaryDecision OnMissingBinary(string tool, bool required, string installHint) =>
        required
            ? new MissingBinaryDecision(
                Fail: true,
                Message: $"{tool} is not on PATH and security scanners are required in this context "
                    + $"(CI, or an explicit required flag). Install: {installHint}")
            : new MissingBinaryDecision(
                Fail: false,
                Message: $"{tool} not found on PATH — scan SKIPPED. A skipped scan is NOT a clean "
                    + $"scan: nothing was checked. Install: {installHint}");

    /// <summary>Records that <paramref name="tool"/>'s scan was skipped for <paramref name="reason"/>; readable back via <see cref="Status"/>.</summary>
    public static void RecordSkipped(string reportsDir, string tool, string reason) =>
        Write(reportsDir, tool, $"skipped: {reason}");

    /// <summary>Records that <paramref name="tool"/> actually ran and reported nothing; readable back via <see cref="Status"/>.</summary>
    public static void RecordRanClean(string reportsDir, string tool) =>
        Write(reportsDir, tool, "ran-clean");

    /// <summary>
    /// The recorded outcome of <paramref name="tool"/>'s most recent scan: <c>ran-clean</c>,
    /// <c>skipped: …</c>, or <c>never-ran</c> when no scan target has executed at all — three
    /// states, so silence cannot masquerade as coverage.
    /// </summary>
    public static string Status(string reportsDir, string tool)
    {
        var file = StatusFile(reportsDir, tool);
        return File.Exists(file) ? File.ReadAllText(file).Trim() : "never-ran";
    }

    private static void Write(string reportsDir, string tool, string status)
    {
        Directory.CreateDirectory(reportsDir);
        File.WriteAllText(StatusFile(reportsDir, tool), status + "\n");
    }

    private static string StatusFile(string reportsDir, string tool) =>
        Path.Combine(reportsDir, $"{tool}.status");
}
