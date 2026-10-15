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
    private const string SeedField = "seedSkills";
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

    /// <summary>
    /// Whether the harness copies its own rendered pages into the scaffolded project so the skill
    /// under trial can trigger at all — the default. A case that asks what the INSTALL leaves behind
    /// (the fresh-session case) declares <c>"seedSkills": false</c>, because seeded pages would answer
    /// its question before the install ran.
    /// </summary>
    /// <exception cref="InvalidOperationException"><c>seedSkills</c> is declared as something other than a boolean.</exception>
    public static bool SeedsTheRenderedSkills(string caseDir)
    {
        var manifest = Path.Combine(caseDir, "case.json");
        if (!File.Exists(manifest))
        {
            return true;
        }

        using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(manifest));
        if (document.RootElement.ValueKind != System.Text.Json.JsonValueKind.Object
            || !document.RootElement.TryGetProperty(SeedField, out var declared))
        {
            return true;
        }

        return declared.ValueKind is System.Text.Json.JsonValueKind.True or System.Text.Json.JsonValueKind.False
            ? declared.GetBoolean()
            : throw new InvalidOperationException($"{manifest} declares \"{SeedField}\" as something other than true or false");
    }

    [GeneratedRegex("\"fixture\"\\s*:\\s*\"([^\"]+)\"")]
    private static partial Regex FixtureField();
}
