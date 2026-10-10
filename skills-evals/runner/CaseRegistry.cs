// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Text.RegularExpressions;

namespace NarrativeTrace.SkillsEvals;

/// <summary>
/// Reads a case's <c>case.json</c> for the <c>registry</c> field that names which registry
/// delivered its pages. Sibling of <see cref="CaseFixture"/>: both read the one manifest, neither
/// is a general JSON parser, because the field is a single token out of a closed vocabulary.
/// </summary>
public static partial class CaseRegistry
{
    /// <summary>
    /// The <see cref="RegistryPreStep"/> <paramref name="caseDir"/> declares, or <c>null</c> for an
    /// ordinary case (no manifest, or a manifest with no <c>registry</c> field). An id outside the
    /// closed vocabulary throws — a case file is data, and a value the harness does not know is a
    /// mistake, never a pre-step quietly skipped.
    /// </summary>
    public static RegistryPreStep? RegistryFor(string caseDir)
    {
        var manifest = Path.Combine(caseDir, "case.json");
        if (!File.Exists(manifest))
        {
            return null;
        }

        var match = RegistryField().Match(File.ReadAllText(manifest));
        if (!match.Success)
        {
            return null;
        }

        var id = match.Groups[1].Value;
        return RegistryPreSteps.Parse(id)
            ?? throw new InvalidOperationException(
                $"{manifest} names an unknown registry \"{id}\" — the vocabulary is claude-marketplace, npx-skills");
    }

    [GeneratedRegex("\"registry\"\\s*:\\s*\"([^\"]+)\"")]
    private static partial Regex RegistryField();
}
