// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Cli.Doctor.Checks;

namespace NarrativeTrace.Cli.Doctor;

/// <summary>
/// The registered checks and the aggregate report they produce. The id is
/// each check's stable contract, not its position in this list.
/// </summary>
public static class DoctorChecks
{
    /// <summary>Every registered check, in a stable (but not load-bearing) order.</summary>
    public static readonly IReadOnlyList<DoctorCheck> All =
    [
        ToolchainTargetFrameworkCheck.Run,
        ToolchainPackageVersionsAlignedCheck.Run,
        ToolchainCompanionPackagesCheck.Run,
        ConfigOutputEnvCheck.Run,
        ConfigRegistrationPairCheck.Run,
        ConfigAppSettingsKeysCheck.Run,
        TrapSilentSinkCheck.Run,
        TrapProxyInterfaceCheck.Run,
        TrapRedactionProofCheck.Run,
        TrapApprovalTracesCheck.Run,
        TrapLlmsBeforeYouStartCheck.Run,
    ];

    /// <summary>Runs every registered check over a snapshot and derives the exit code.</summary>
    public static DoctorReport Run(DoctorSnapshot snapshot)
    {
        var findings = All.Select(check => check(snapshot)).ToList();
        var exitCode = findings.Any(f => !f.Passed) ? 1 : 0;
        return new DoctorReport(findings, exitCode);
    }
}
