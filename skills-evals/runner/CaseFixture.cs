// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Text.RegularExpressions;

namespace NarrativeTrace.SkillsEvals;

/// <summary>
/// A case directory may declare which fixture to scaffold via <c>case.json</c>; a case without one
/// defaults to the skill's own canonical fixture, <c>examples/NarrativeTrace.Examples.SixtySeconds</c>
/// — this runtime's analogue of the reference runners' default.
/// </summary>
public static partial class CaseFixture
{
    private const string DefaultFixture = "examples/NarrativeTrace.Examples.SixtySeconds";

    /// <summary>Repo-root-relative fixture path <paramref name="caseDir"/> names, or the default.</summary>
    public static string FixtureFor(string caseDir)
    {
        var manifest = Path.Combine(caseDir, "case.json");
        if (!File.Exists(manifest))
        {
            return DefaultFixture;
        }

        var match = FixtureField().Match(File.ReadAllText(manifest));
        if (!match.Success)
        {
            throw new InvalidOperationException($"{manifest} has no \"fixture\" field");
        }

        return match.Groups[1].Value;
    }

    [GeneratedRegex("\"fixture\"\\s*:\\s*\"([^\"]+)\"")]
    private static partial Regex FixtureField();
}
