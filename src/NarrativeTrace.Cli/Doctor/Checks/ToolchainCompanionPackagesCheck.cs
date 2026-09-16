// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Cli.Doctor.Checks;

/// <summary>
/// <c>toolchain.companion-packages</c>: before 0.1.5, <c>NarrativeTrace.Proxy</c>
/// does not pull in <c>.Runtime</c>/<c>.Core</c> transitively — a project on an
/// older resolved version needs all three added by hand (see the quickstart's
/// "before you start" notes).
/// </summary>
public static class ToolchainCompanionPackagesCheck
{
    private const string Id = "toolchain.companion-packages";
    private const string TransitiveSince = "0.1.5";
    private static readonly string[] RequiredCompanions = ["NarrativeTrace.Core", "NarrativeTrace.Runtime"];

    /// <summary>Runs the check.</summary>
    public static DoctorFinding Run(DoctorSnapshot snapshot)
    {
        if (!snapshot.InstalledPackages.TryGetValue("NarrativeTrace.Proxy", out var proxyVersion))
        {
            return DoctorFinding.Pass(
                Id, "NarrativeTrace.Proxy is not installed — nothing to check",
                DoctorDocUrls.InstallationPackages);
        }

        if (!PackageVersionLite.IsBelow(proxyVersion, TransitiveSince))
        {
            return DoctorFinding.Pass(
                Id, $"NarrativeTrace.Proxy@{proxyVersion} pulls in .Runtime and .Core transitively",
                DoctorDocUrls.InstallationPackages);
        }

        return CheckCompanions(snapshot, proxyVersion);
    }

    private static DoctorFinding CheckCompanions(DoctorSnapshot snapshot, string proxyVersion)
    {
        var missing = RequiredCompanions
            .Where(id => !snapshot.InstalledPackages.ContainsKey(id))
            .ToList();
        return missing.Count == 0
            ? DoctorFinding.Pass(
                Id, $"NarrativeTrace.Proxy@{proxyVersion} is pre-0.1.5, but Core and Runtime are " +
                    "both installed by hand",
                DoctorDocUrls.InstallationPackages)
            : Fail(proxyVersion, missing);
    }

    private static DoctorFinding Fail(string proxyVersion, List<string> missing)
    {
        return DoctorFinding.Fail(
            Id,
            $"NarrativeTrace.Proxy@{proxyVersion} is pre-0.1.5 and does not pull in " +
            $"{string.Join(", ", missing)} transitively",
            $"Add {string.Join(" and ", missing)} as direct package references, or upgrade " +
            $"NarrativeTrace.Proxy to {TransitiveSince}+.",
            DoctorDocUrls.InstallationPackages);
    }
}
