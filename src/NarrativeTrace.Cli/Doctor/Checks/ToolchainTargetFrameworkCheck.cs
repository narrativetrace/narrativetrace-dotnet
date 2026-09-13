// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Text.RegularExpressions;

namespace NarrativeTrace.Cli.Doctor.Checks;

/// <summary>
/// <c>toolchain.target-framework</c>: a project referencing
/// <c>NarrativeTrace.Legacy</c> — the one package built for classic .NET
/// Framework — must actually target a <c>net4x</c> framework, or the
/// reference silently restores a package whose API this project can never
/// exercise the way its docs describe.
/// </summary>
public static class ToolchainTargetFrameworkCheck
{
    private const string Id = "toolchain.target-framework";

    private static readonly Regex LegacyReference =
        new(@"NarrativeTrace\.Legacy", RegexOptions.Compiled);

    private static readonly Regex TargetFramework =
        new(@"<TargetFramework[s]?>([^<]+)</TargetFramework[s]?>", RegexOptions.Compiled);

    /// <summary>Runs the check.</summary>
    public static DoctorFinding Run(DoctorSnapshot snapshot)
    {
        var legacyProject = snapshot.ProjectFiles
            .FirstOrDefault(f => LegacyReference.IsMatch(f.Value));
        if (legacyProject.Key is null)
        {
            return DoctorFinding.Pass(
                Id, "NarrativeTrace.Legacy is not referenced — nothing to check",
                DoctorDocUrls.InstallationPackages);
        }

        var tfm = TargetFramework.Match(legacyProject.Value) is { Success: true } match
            ? match.Groups[1].Value
            : string.Empty;
        return tfm.Contains("net4", StringComparison.OrdinalIgnoreCase)
            ? DoctorFinding.Pass(
                Id, $"{legacyProject.Key} targets {tfm}, consistent with NarrativeTrace.Legacy",
                DoctorDocUrls.InstallationPackages)
            : Fail(legacyProject.Key, tfm);
    }

    private static DoctorFinding Fail(string path, string tfm)
    {
        return DoctorFinding.Fail(
            Id,
            $"{path} references NarrativeTrace.Legacy but targets '{tfm}', not a net4x framework",
            "NarrativeTrace.Legacy is built for classic .NET Framework (net48) — target net48 " +
            "in this project, or drop the reference and use NarrativeTrace.Core/.Runtime instead.",
            DoctorDocUrls.InstallationPackages);
    }
}
