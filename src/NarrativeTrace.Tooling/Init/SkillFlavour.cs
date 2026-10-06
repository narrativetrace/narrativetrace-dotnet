// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Tooling.Init;

/// <summary>
/// The two rendered forms of the same skill the carrier ships, and where each one is installed.
/// </summary>
/// <remarks>
/// INTENT: a skill's text differs between the open-standard layout and the vendor layout by two
/// frontmatter fields, so the carrier ships both rather than making an installer synthesise one from
/// the other — rendering belongs in the catalogue, not in the installer. <see cref="Agents"/> is
/// written into every project. <see cref="Claude"/> is written only where a project is detected as
/// that vendor's, or where the caller asked for it explicitly; the planner owns that decision.
/// </remarks>
public enum SkillFlavour
{
    /// <summary>The open-standard flavour, installed under <c>.agents/skills/</c>.</summary>
    Agents,

    /// <summary>The vendor flavour, installed under <c>.claude/skills/</c>.</summary>
    Claude,
}

/// <summary>Where each <see cref="SkillFlavour"/> installs.</summary>
public static class SkillFlavourExtensions
{
    /// <summary>The project-relative directory this flavour's skill directories live in.</summary>
    public static string InstallRoot(this SkillFlavour flavour) => flavour switch
    {
        SkillFlavour.Agents => ".agents/skills",
        SkillFlavour.Claude => ".claude/skills",
        _ => throw new ArgumentException("a flavour must be given to pick an install root"),
    };
}
