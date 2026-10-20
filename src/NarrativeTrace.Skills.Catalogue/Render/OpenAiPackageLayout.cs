// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Skills.Render;

/// <summary>
/// The OpenAI plugin package's file set, sorted: <c>plugin.json</c>, one
/// <c>skills/&lt;name&gt;/SKILL.md</c> per catalogue entry, and the two listing assets. The pack step
/// asserts the built ZIP's entries EQUAL this list — derived from the catalogue, never a glob (release
/// rule 5) — so a skill added to the catalogue without a packaging change fails the gate instead of
/// shipping an incomplete ZIP.
/// </summary>
public static class OpenAiPackageLayout
{
    /// <summary>The ZIP-entry path of the plugin's logo, relative to the package root.</summary>
    public const string LogoEntry = "assets/logo.png";

    /// <summary>The ZIP-entry path of the plugin's composer icon, relative to the package root.</summary>
    public const string ComposerIconEntry = "assets/composer-icon.png";

    /// <summary>The ZIP-entry path of the manifest, relative to the package root.</summary>
    public const string ManifestEntry = "plugin.json";

    /// <summary>The sorted entry paths a package for <paramref name="skills"/> must contain.</summary>
    /// <param name="skills">The skills the package ships.</param>
    /// <param name="listing">The directory listing the package is drawn from; required, so a package never lacks one.</param>
    public static IReadOnlyList<string> Entries(IReadOnlyList<Skill> skills, OpenAiListing listing)
    {
        ArgumentNullException.ThrowIfNull(skills);
        ArgumentNullException.ThrowIfNull(listing);
        var entries = new List<string> { ManifestEntry, LogoEntry, ComposerIconEntry };
        entries.AddRange(skills.Select(skill => $"skills/{skill.CanonicalName}/SKILL.md"));
        entries.Sort(StringComparer.Ordinal);
        return entries;
    }
}
