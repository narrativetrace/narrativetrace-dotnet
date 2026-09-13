// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Globalization;

namespace NarrativeTrace.ContractProbe;

/// <summary>
/// Version compare, deliberately duplicated (not shared) from <c>build/ContractLintSupport.cs</c>'s
/// <c>ContractDecisionSupport.IsApplicable</c>: this project consumes only registry artifacts and
/// can never depend on the root build project, which is not published.
/// </summary>
internal static class Versions
{
    /// <summary>True while <paramref name="since"/> is NOT strictly later than <paramref name="installedVersion"/>.</summary>
    public static bool IsApplicable(string since, string installedVersion)
    {
        var a = Parts(since);
        var b = Parts(installedVersion);
        var length = Math.Max(a.Length, b.Length);
        for (var i = 0; i < length; i++)
        {
            var x = i < a.Length ? a[i] : 0;
            var y = i < b.Length ? b[i] : 0;
            if (x != y)
                return x < y;
        }

        return true;
    }

    private static int[] Parts(string version) =>
        version.Split('.').Select(part => int.Parse(part, CultureInfo.InvariantCulture)).ToArray();
}
