// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Tooling.Init;

/// <summary>
/// One skill directory found in a consumer project, and what the installer is allowed to do with it.
/// </summary>
/// <remarks>
/// INTENT: the planner's decision for a skill is a function of this record alone — is the directory
/// ours (overwrite, no flag), somebody else's (refuse unless forced), or not a directory at all
/// (refuse, always).
/// </remarks>
public sealed record InstalledSkill
{
    /// <summary>The page's file name inside a skill directory — the same in every flavour.</summary>
    public const string Page = "SKILL.md";

    /// <param name="flavour">Which install root it was found under.</param>
    /// <param name="name">The directory name, which is the skill name.</param>
    /// <param name="presence">What was found there.</param>
    /// <param name="coordinate">The carrier a previous install stamped it with, empty unless <see cref="SkillPresence.Ours"/>.</param>
    /// <param name="body">The current <c>SKILL.md</c> text, empty when there is none.</param>
    public InstalledSkill(
        SkillFlavour flavour, string name, SkillPresence presence, string coordinate, string body)
    {
        RequireValid(flavour, name, presence, coordinate, body);
        Flavour = flavour;
        Name = name;
        Presence = presence;
        Coordinate = coordinate;
        Body = body;
    }

    private static void RequireValid(
        SkillFlavour flavour, string name, SkillPresence presence, string coordinate, string body)
    {
        if (!Enum.IsDefined(flavour) || !Enum.IsDefined(presence))
        {
            throw new ArgumentException("an installed skill needs a flavour and a presence");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("an installed skill's name must not be blank");
        }

        if (coordinate is null || body is null)
        {
            throw new ArgumentException("use \"\" for an unknown coordinate or body, never null");
        }

        if (presence == SkillPresence.Ours && string.IsNullOrWhiteSpace(coordinate))
        {
            throw new ArgumentException("an installed skill of ours carries its coordinate");
        }
    }

    /// <summary>Which install root it was found under.</summary>
    public SkillFlavour Flavour { get; }

    /// <summary>The directory name, which is the skill name.</summary>
    public string Name { get; }

    /// <summary>What was found there.</summary>
    public SkillPresence Presence { get; }

    /// <summary>The carrier a previous install stamped it with, empty unless <see cref="SkillPresence.Ours"/>.</summary>
    public string Coordinate { get; }

    /// <summary>The current <c>SKILL.md</c> text, empty when there is none.</summary>
    public string Body { get; }

    /// <summary>The project-relative directory, e.g. <c>.agents/skills/narrativetrace-doctor</c>.</summary>
    public string Directory => Flavour.InstallRoot() + "/" + Name;

    /// <summary>The project-relative page, e.g. <c>.agents/skills/narrativetrace-doctor/SKILL.md</c>.</summary>
    public string PagePath => Directory + "/" + Page;
}

/// <summary>What sits at a skill's path in the consumer project.</summary>
public enum SkillPresence
{
    /// <summary>A directory whose <c>SKILL.md</c> carries our provenance line — an install of ours.</summary>
    Ours,

    /// <summary>A directory somebody else owns: no page, or a page without our provenance line.</summary>
    Foreign,

    /// <summary>Something that is not a directory at all sits at the path a skill needs.</summary>
    NotADirectory,
}
