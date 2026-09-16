// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Cli.Doctor.Checks;

/// <summary>
/// <c>trap.llms-before-you-start</c>: mirrors the quickstart's own "before
/// you start" version-skew notes — packages resolved before 0.1.5 default
/// <c>NARRATIVETRACE_OUTPUT</c> to <b>off</b> (0.1.5+ defaults it on), which
/// silently changes whether output is written at all.
/// </summary>
public static class TrapLlmsBeforeYouStartCheck
{
    private const string Id = "trap.llms-before-you-start";
    private const string DefaultsChangedAt = "0.1.5";

    /// <summary>Runs the check.</summary>
    public static DoctorFinding Run(DoctorSnapshot snapshot)
    {
        var preRelease = snapshot.InstalledPackages
            .Where(p => p.Key.StartsWith("NarrativeTrace.", StringComparison.Ordinal))
            .Where(p => PackageVersionLite.IsBelow(p.Value, DefaultsChangedAt))
            .ToList();
        return preRelease.Count == 0
            ? DoctorFinding.Pass(
                Id, "installed packages are 0.1.5+ (or none found) — defaults match current docs",
                DoctorDocUrls.LlmsBeforeYouStart)
            : Fail(preRelease);
    }

    private static DoctorFinding Fail(List<KeyValuePair<string, string>> preRelease)
    {
        var detail = string.Join(", ", preRelease.Select(p => $"{p.Key}@{p.Value}"));
        return DoctorFinding.Fail(
            Id,
            $"installed before 0.1.5: {detail} — NARRATIVETRACE_OUTPUT defaults OFF there, not on",
            $"Upgrade to {DefaultsChangedAt}+ for the current defaults, or read the quickstart's " +
            "\"before you start\" notes and set NARRATIVETRACE_OUTPUT explicitly.",
            DoctorDocUrls.LlmsBeforeYouStart);
    }
}
