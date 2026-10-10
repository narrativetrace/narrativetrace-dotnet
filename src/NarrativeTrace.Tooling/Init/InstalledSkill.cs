// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Tooling.Init;

/// <summary>
/// One skill directory found in a consumer project, and what the installer is allowed to do with it.
/// </summary>
/// <remarks>
/// INTENT: the planner's decision for a skill is a function of this record alone — is the directory
/// ours (overwrite, no flag), somebody else's (refuse unless forced), a symbolic link (replace the
/// link, or refuse — never write through it), or not a directory at all (refuse, always).
/// </remarks>
public sealed record InstalledSkill
{
    /// <summary>The page's file name inside a skill directory — the same in every flavour.</summary>
    public const string Page = "SKILL.md";

    /// <param name="flavour">Which install root it was found under.</param>
    /// <param name="name">The directory name, which is the skill name.</param>
    /// <param name="presence">What was found there.</param>
    /// <param name="coordinate">The carrier a previous install stamped it with, empty unless <see cref="SkillPresence.Ours"/>.</param>
    /// <param name="body">
    /// The current <c>SKILL.md</c> text, empty when there is none; for a link, the page it reaches
    /// INSIDE the project, and empty when it reaches none.
    /// </param>
    /// <param name="link">
    /// What the symbolic link points at, exactly as the filesystem reports it — empty unless the
    /// presence is one of the two linked ones.
    /// </param>
    public InstalledSkill(
        SkillFlavour flavour, string name, SkillPresence presence, string coordinate, string body, string link)
    {
        RequireValid(flavour, name, presence, coordinate, body, link);
        Flavour = flavour;
        Name = name;
        Presence = presence;
        Coordinate = coordinate;
        Body = body;
        Link = link;
    }

    /// <summary>
    /// A skill nothing links to — the shape every install of ours, every foreign directory and every
    /// file-in-the-way has.
    /// </summary>
    public InstalledSkill(
        SkillFlavour flavour, string name, SkillPresence presence, string coordinate, string body)
        : this(flavour, name, presence, coordinate, body, "")
    {
    }

    private static void RequireValid(
        SkillFlavour flavour, string name, SkillPresence presence, string coordinate, string body, string link)
    {
        if (!Enum.IsDefined(flavour) || !Enum.IsDefined(presence))
        {
            throw new ArgumentException("an installed skill needs a flavour and a presence");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("an installed skill's name must not be blank");
        }

        if (coordinate is null || body is null || link is null)
        {
            throw new ArgumentException("use \"\" for an unknown coordinate, body or link, never null");
        }

        RequireConsistent(presence, coordinate, link);
    }

    /// <summary>The two cross-field rules: ours carries a coordinate, and a link is a linked presence.</summary>
    private static void RequireConsistent(SkillPresence presence, string coordinate, string link)
    {
        if (presence == SkillPresence.Ours && string.IsNullOrWhiteSpace(coordinate))
        {
            throw new ArgumentException("an installed skill of ours carries its coordinate");
        }

        if (IsLinked(presence) == string.IsNullOrWhiteSpace(link))
        {
            throw new ArgumentException(
                "a linked presence names what the link points at, and only a linked one does");
        }
    }

    /// <summary>Whether this presence is one of the two the installer must not write through.</summary>
    private static bool IsLinked(SkillPresence presence) =>
        presence is SkillPresence.LinkedDirectory or SkillPresence.LinkedPage;

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

    /// <summary>
    /// What the symbolic link points at, exactly as the filesystem reports it — empty unless the
    /// presence is one of the two linked ones.
    /// </summary>
    public string Link { get; }

    /// <summary>The project-relative directory, e.g. <c>.agents/skills/narrativetrace-doctor</c>.</summary>
    public string Directory => Flavour.InstallRoot() + "/" + Name;

    /// <summary>The project-relative page, e.g. <c>.agents/skills/narrativetrace-doctor/SKILL.md</c>.</summary>
    public string PagePath => Directory + "/" + Page;

    /// <summary>
    /// Where the symbolic link itself sits: the skill's directory, or its page.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// When nothing here is a link, which is a caller reading a field that has no meaning rather than
    /// a project in a strange state.
    /// </exception>
    public string LinkedAt()
    {
        if (!IsLinked(Presence))
        {
            throw new InvalidOperationException("nothing links to " + Directory);
        }

        return Presence == SkillPresence.LinkedPage ? PagePath : Directory;
    }
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

    /// <summary>The skill's directory is a symbolic link; what a registry leaves at the vendor path.</summary>
    LinkedDirectory,

    /// <summary>The directory is real, and its <c>SKILL.md</c> is a symbolic link.</summary>
    LinkedPage,
}
