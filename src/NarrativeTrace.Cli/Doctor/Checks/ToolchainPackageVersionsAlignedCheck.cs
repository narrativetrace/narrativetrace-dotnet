// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Cli.Doctor.Checks;

/// <summary>
/// <c>toolchain.package-versions-aligned</c>: every installed
/// <c>NarrativeTrace.*</c> package should resolve to the same version.
/// These packages ship together and version-gate behavior against each
/// other (see the "before you start" notes) — a drifted install is a real
/// source of confusing, version-dependent bugs.
/// </summary>
public static class ToolchainPackageVersionsAlignedCheck
{
    private const string Id = "toolchain.package-versions-aligned";

    /// <summary>Runs the check.</summary>
    public static DoctorFinding Run(DoctorSnapshot snapshot)
    {
        var narrativeTrace = snapshot.InstalledPackages
            .Where(p => p.Key.StartsWith("NarrativeTrace.", StringComparison.Ordinal))
            .ToList();
        var versions = narrativeTrace.Select(p => p.Value).Distinct().ToList();
        if (narrativeTrace.Count < 2)
        {
            return DoctorFinding.Pass(
                Id, "fewer than two NarrativeTrace packages installed — nothing to check",
                DoctorDocUrls.InstallationPackages);
        }

        return versions.Count == 1
            ? DoctorFinding.Pass(
                Id, $"all {narrativeTrace.Count} installed NarrativeTrace packages are on {versions[0]}",
                DoctorDocUrls.InstallationPackages)
            : Fail(narrativeTrace, versions);
    }

    private static DoctorFinding Fail(
        List<KeyValuePair<string, string>> narrativeTrace, List<string> versions)
    {
        var detail = string.Join(", ", narrativeTrace.Select(p => $"{p.Key}@{p.Value}"));
        return DoctorFinding.Fail(
            Id,
            $"installed NarrativeTrace packages are on {versions.Count} different versions: {detail}",
            "Pin every NarrativeTrace.* package reference to the same version — a mixed install " +
            "can pair one package's newer defaults with another's older ones.",
            DoctorDocUrls.InstallationPackages);
    }
}
