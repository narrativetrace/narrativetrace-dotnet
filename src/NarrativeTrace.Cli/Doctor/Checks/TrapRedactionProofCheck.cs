// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Cli.Doctor.Checks;

/// <summary>
/// <c>trap.redaction-proof</c>: redaction is deny-by-default and silent — a
/// project can rely on it working without ever proving it in a test. This
/// checks for a test file that actually asserts the redaction marker.
/// </summary>
public static class TrapRedactionProofCheck
{
    private const string Id = "trap.redaction-proof";
    private const string Marker = "[REDACTED]";

    /// <summary>Runs the check.</summary>
    public static DoctorFinding Run(DoctorSnapshot snapshot)
    {
        var proven = snapshot.SourceFiles.Any(f =>
            f.Key.Contains("Test", StringComparison.Ordinal) && f.Value.Contains(Marker, StringComparison.Ordinal));
        return proven
            ? DoctorFinding.Pass(
                Id, "a test asserts [REDACTED] for a deny-listed parameter name",
                DoctorDocUrls.PrivacyRedaction)
            : DoctorFinding.Fail(
                Id,
                "no test asserts [REDACTED] — redaction is unproven",
                "Render a call with a deny-listed parameter name (e.g. \"password\", \"token\") in " +
                "a test and assert the output contains \"[REDACTED]\" — and that a neighboring, " +
                "non-sensitive value is still present, so an over-broad redaction also fails.",
                DoctorDocUrls.PrivacyRedaction);
    }
}
