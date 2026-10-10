// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Tooling.Init;
using Xunit;

namespace NarrativeTrace.Tooling.Tests.Init;

/// <summary>
/// The snapshot a test builds by hand — the reason every planner case is a unit test with no disk at
/// all. These cases cover the defaults, the lookup and the contract guards. Method names mirror the
/// Java <c>ProjectStateTest</c>; the <c>gradleProject</c> field has no .NET counterpart.
/// </summary>
public class ProjectStateTest
{
    [Fact]
    public void AnUntouchedProjectIsTheDefault()
    {
        var state = ProjectState.NewBuilder().Build();

        Assert.Null(state.AgentsMd);
        Assert.Null(state.ClaudeMd);
        Assert.False(state.HasClaudeDirectory);
        Assert.Empty(state.InstalledSkills);
        Assert.Empty(state.MarkedRuleFiles);
        Assert.Equal("TestResults/narrativetrace", state.OutputDirectory);
        Assert.Null(state.ResolvedFamilyVersion);
        Assert.True(state.Invariant());
    }

    [Fact]
    public void KeepsEverythingItWasGiven()
    {
        var state = ProjectState.NewBuilder()
            .AgentsMd("# A\n")
            .ClaudeMd("# C\n")
            .HasClaudeDirectory(true)
            .OutputDirectory("out")
            .ResolvedFamilyVersion("0.1.5")
            .MarkedRuleFile(".cursorrules", "rules")
            .AddInstalledSkill(Ours("doctor"))
            .Build();

        Assert.Equal("# A\n", state.AgentsMd);
        Assert.Equal("# C\n", state.ClaudeMd);
        Assert.True(state.HasClaudeDirectory);
        Assert.Equal("out", state.OutputDirectory);
        Assert.Equal("0.1.5", state.ResolvedFamilyVersion);
        Assert.Equal("rules", state.MarkedRuleFiles[".cursorrules"]);
        Assert.NotNull(state.InstalledSkill(SkillFlavour.Agents, "doctor"));
        Assert.Null(state.InstalledSkill(SkillFlavour.Claude, "doctor"));
    }

    [Fact]
    public void KeepsItsCollectionsImmutable()
    {
        var state = ProjectState.NewBuilder().AddInstalledSkill(Ours("doctor")).Build();

        Assert.Throws<NotSupportedException>(() => ((IList<InstalledSkill>)state.InstalledSkills).Clear());
        Assert.Throws<NotSupportedException>(
            () => ((IDictionary<string, string>)state.MarkedRuleFiles).Clear());
    }

    [Fact]
    public void RefusesToDescribeTheSamePathTwice()
    {
        var builder = ProjectState.NewBuilder()
            .AddInstalledSkill(Ours("doctor"))
            .AddInstalledSkill(Ours("doctor"));

        Assert.Throws<ArgumentException>(builder.Build);
    }

    [Fact]
    public void RefusesAnInstalledSkillThatContradictsItself()
    {
        Assert.Contains("carries its coordinate",
            Assert.Throws<ArgumentException>(
                () => new InstalledSkill(SkillFlavour.Agents, "d", SkillPresence.Ours, "", "body")).Message,
            StringComparison.Ordinal);
        Assert.Contains("flavour and a presence",
            Assert.Throws<ArgumentException>(
                () => new InstalledSkill((SkillFlavour)999, "d", SkillPresence.Foreign, "", "")).Message,
            StringComparison.Ordinal);
        Assert.Contains("name must not be blank",
            Assert.Throws<ArgumentException>(
                () => new InstalledSkill(SkillFlavour.Agents, " ", SkillPresence.Foreign, "", "")).Message,
            StringComparison.Ordinal);
        Assert.Contains("never null",
            Assert.Throws<ArgumentException>(
                () => new InstalledSkill(SkillFlavour.Agents, "d", SkillPresence.Foreign, null!, "")).Message,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// A snapshot that lists a skill under a flavour whose install root is a link contradicts itself:
    /// whatever was found there was found THROUGH the link, so it describes somebody else's tree.
    /// </summary>
    [Fact]
    public void RefusesToListASkillUnderALinkedInstallRoot()
    {
        var builder = ProjectState.NewBuilder()
            .LinkedInstallRoot(SkillFlavour.Agents, "/elsewhere")
            .AddInstalledSkill(Ours("doctor"));

        Assert.Throws<ArgumentException>(builder.Build);
    }

    [Fact]
    public void RefusesAnInstalledSkillWhoseLinkContradictsItsPresence()
    {
        Assert.Contains("linked presence",
            Assert.Throws<ArgumentException>(
                () => new InstalledSkill(SkillFlavour.Agents, "d", SkillPresence.Foreign, "", "body", "../elsewhere")).Message,
            StringComparison.Ordinal);
        Assert.Contains("linked presence",
            Assert.Throws<ArgumentException>(
                () => new InstalledSkill(SkillFlavour.Agents, "d", SkillPresence.LinkedDirectory, "", "body", " ")).Message,
            StringComparison.Ordinal);
        Assert.Contains("never null",
            Assert.Throws<ArgumentException>(
                () => new InstalledSkill(SkillFlavour.Agents, "d", SkillPresence.Foreign, "", "b", null!)).Message,
            StringComparison.Ordinal);
    }

    /// <summary>Asking where a link sits when nothing is a link is a caller's mistake, not a project's.</summary>
    [Fact]
    public void RefusesToNameALinkThatIsNotThere()
    {
        Assert.Contains(".agents/skills/doctor",
            Assert.Throws<InvalidOperationException>(() => Ours("doctor").LinkedAt()).Message,
            StringComparison.Ordinal);
    }

    private static InstalledSkill Ours(string name) =>
        new(SkillFlavour.Agents, name, SkillPresence.Ours, "NarrativeTrace.Skills/1.0.0", "body");
}
