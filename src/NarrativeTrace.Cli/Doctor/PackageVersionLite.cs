// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Globalization;
using System.Text.RegularExpressions;

namespace NarrativeTrace.Cli.Doctor;

/// <summary>
/// Minimal major.minor.patch comparison for NuGet version strings, ignoring
/// any prerelease/build suffix. Good enough for doctor checks that only need
/// to compare against a known release boundary (e.g. <c>0.1.4</c>); not a
/// general SemVer implementation.
/// </summary>
internal static class PackageVersionLite
{
    private static readonly Regex Leading =
        new(@"^(\d+)\.(\d+)\.(\d+)", RegexOptions.Compiled);

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="version"/> parses
    /// and is strictly less than <paramref name="boundary"/>. An unparseable
    /// version is treated as not-less-than, so a check degrades to "nothing
    /// to warn about" rather than a false alarm.
    /// </summary>
    public static bool IsBelow(string version, string boundary)
    {
        var actual = Leading.Match(version);
        var limit = Leading.Match(boundary);
        if (!actual.Success || !limit.Success)
        {
            return false;
        }

        return CompareTriple(actual).CompareTo(CompareTriple(limit)) < 0;
    }

    private static (int Major, int Minor, int Patch) CompareTriple(Match match)
    {
        return (
            int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture),
            int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture),
            int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture));
    }
}
