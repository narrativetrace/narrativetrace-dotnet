// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Tooling.Init;
using Xunit;

namespace NarrativeTrace.Tooling.Tests.Consumer;

/// <summary>
/// The guards that keep an <see cref="InstalledSkill"/> from describing a project state that cannot
/// exist. Every planner decision is a function of this record alone, so a record that LIES — a skill
/// of ours with no coordinate, a linked presence naming no link, a link on a presence that is not one
/// — makes the planner's switch unanswerable rather than merely wrong. These are the contract tested
/// as the contract: each guard, with each input that should trip it. From OUTSIDE the Init namespace
/// on purpose, the way an entry point uses it. Mirrors the Java <c>InstalledSkillContractTest</c>.
/// </summary>
public sealed class InstalledSkillContractTest
{
    private const string Coordinate = "NarrativeTrace.Skills/1.0.0";
    private const string Target = "../../.agents/skills/doctor";
    private const string LinkRule = "a linked presence names what the link points at";

    // --- what must be present ---------------------------------------------------------------------

    [Fact]
    public void RefusesASkillWithNoFlavourOrNoPresence()
    {
        Assert.Contains("flavour and a presence",
            Assert.Throws<ArgumentException>(
                () => Skill((SkillFlavour)999, SkillPresence.Ours, Coordinate, "body\n", "")).Message,
            StringComparison.Ordinal);
        Assert.Contains("flavour and a presence",
            Assert.Throws<ArgumentException>(
                () => Skill(SkillFlavour.Agents, (SkillPresence)999, Coordinate, "body\n", "")).Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void RefusesANameThatIsNullOrBlank()
    {
        Assert.Throws<ArgumentException>(() => Named(null!));
        Assert.Throws<ArgumentException>(() => Named(""));
        Assert.Throws<ArgumentException>(() => Named("   "));
    }

    /// <summary><c>""</c> is the documented "unknown", which is why null is the mistake worth catching.</summary>
    [Fact]
    public void RefusesANullCoordinateBodyOrLinkRatherThanReadingItAsUnknown()
    {
        Assert.Contains("never null",
            Assert.Throws<ArgumentException>(
                () => Skill(SkillFlavour.Agents, SkillPresence.Foreign, null!, "theirs\n", "")).Message,
            StringComparison.Ordinal);
        Assert.Contains("never null",
            Assert.Throws<ArgumentException>(
                () => Skill(SkillFlavour.Agents, SkillPresence.Foreign, "", null!, "")).Message,
            StringComparison.Ordinal);
        Assert.Contains("never null",
            Assert.Throws<ArgumentException>(
                () => Skill(SkillFlavour.Agents, SkillPresence.Foreign, "", "theirs\n", null!)).Message,
            StringComparison.Ordinal);
    }

    // --- Ours carries its coordinate --------------------------------------------------------------

    [Fact]
    public void RefusesASkillOfOursThatNamesNoCarrier()
    {
        Assert.Contains("carries its coordinate",
            Assert.Throws<ArgumentException>(
                () => Skill(SkillFlavour.Agents, SkillPresence.Ours, "", "body\n", "")).Message,
            StringComparison.Ordinal);
        Assert.Contains("carries its coordinate",
            Assert.Throws<ArgumentException>(
                () => Skill(SkillFlavour.Agents, SkillPresence.Ours, "   ", "body\n", "")).Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void AcceptsAnEmptyCoordinateForEveryPresenceButOurs()
    {
        Assert.Equal("", Skill(SkillFlavour.Agents, SkillPresence.Foreign, "", "theirs\n", "").Coordinate);
        Assert.Equal("", Skill(SkillFlavour.Agents, SkillPresence.NotADirectory, "", "", "").Coordinate);
    }

    // --- a link and a linked presence are one fact ------------------------------------------------

    [Fact]
    public void RefusesALinkedPresenceThatNamesNoTarget()
    {
        Assert.Contains(LinkRule,
            Assert.Throws<ArgumentException>(
                () => Skill(SkillFlavour.Claude, SkillPresence.LinkedDirectory, "", "b\n", "")).Message,
            StringComparison.Ordinal);
        Assert.Contains(LinkRule,
            Assert.Throws<ArgumentException>(
                () => Skill(SkillFlavour.Claude, SkillPresence.LinkedPage, "", "b\n", "   ")).Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void RefusesATargetOnAPresenceThatIsNotALink()
    {
        Assert.Contains(LinkRule,
            Assert.Throws<ArgumentException>(
                () => Skill(SkillFlavour.Agents, SkillPresence.Ours, Coordinate, "b\n", Target)).Message,
            StringComparison.Ordinal);
        Assert.Contains(LinkRule,
            Assert.Throws<ArgumentException>(
                () => Skill(SkillFlavour.Agents, SkillPresence.Foreign, "", "theirs\n", Target)).Message,
            StringComparison.Ordinal);
    }

    /// <summary>The short constructor is the unlinked shape, so it has to produce an unlinked record.</summary>
    [Fact]
    public void TheShortConstructorNamesNoLink()
    {
        var skill = new InstalledSkill(SkillFlavour.Agents, "doctor", SkillPresence.Foreign, "", "theirs\n");

        Assert.Equal("", skill.Link);
        Assert.Throws<InvalidOperationException>(() => skill.LinkedAt());
    }

    // --- where the link sits ----------------------------------------------------------------------

    /// <summary>
    /// The two linked presences differ in exactly one thing: whether the link is the directory or the
    /// page inside it. That path is what the executor DELETES, so reading it off the wrong presence
    /// deletes the wrong thing.
    /// </summary>
    [Fact]
    public void ALinkedDirectoryLinksAtTheDirectoryAndALinkedPageAtThePage()
    {
        Assert.Equal(".claude/skills/doctor", Linked(SkillPresence.LinkedDirectory).LinkedAt());
        Assert.Equal(".claude/skills/doctor/SKILL.md", Linked(SkillPresence.LinkedPage).LinkedAt());
    }

    [Fact]
    public void NamesItsDirectoryAndItsPageUnderItsOwnFlavoursInstallRoot()
    {
        var vendor = new InstalledSkill(SkillFlavour.Claude, "doctor", SkillPresence.Ours, Coordinate, "body\n");

        Assert.Equal(".claude/skills/doctor", vendor.Directory);
        Assert.Equal(".claude/skills/doctor/SKILL.md", vendor.PagePath);
    }

    // --- fixtures ---------------------------------------------------------------------------------

    private static InstalledSkill Skill(
        SkillFlavour flavour, SkillPresence presence, string coordinate, string body, string link) =>
        new(flavour, "doctor", presence, coordinate, body, link);

    private static InstalledSkill Named(string name) =>
        new(SkillFlavour.Agents, name, SkillPresence.Foreign, "", "theirs\n", "");

    private static InstalledSkill Linked(SkillPresence presence) =>
        new(SkillFlavour.Claude, "doctor", presence, "", "body\n", Target);
}
