// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Tooling.Init;
using Xunit;
using Action = NarrativeTrace.Tooling.Init.Action;

namespace NarrativeTrace.Tooling.Tests.Init;

/// <summary>
/// Uninstall removes EXACTLY what the installer wrote and nothing beside it. Every case here is a
/// "leave it alone" as much as it is a "remove it": a foreign directory, a line that is not the line
/// we added, a file a person has since written in. Method names mirror the Java
/// <c>UninstallPlannerTest</c>; this port carries the <c>NarrativeTrace.Skills/&lt;version&gt;</c>
/// coordinate form (D6).
/// </summary>
public sealed class UninstallPlannerTest
{
    private const string AgentsMd = "AGENTS.md";
    private const string ClaudeMd = "CLAUDE.md";
    private const string Coordinate = "NarrativeTrace.Skills/1.2.3";

    private static readonly string Block =
        "<!-- narrativetrace:start " + Coordinate + " -->\n## NarrativeTrace\n<!-- narrativetrace:end -->\n";

    private static InitPlan Plan(ProjectState state) => UninstallPlanner.Plan(state, InitOptions.Defaults());

    private static Action? ActionOn(InitPlan plan, string path) =>
        plan.Actions.FirstOrDefault(action => action.Path == path);

    private static string After(InitPlan plan, string path) => ((FileEdit)ActionOn(plan, path)!).After;

    private static InstalledSkill Ours(string coordinate) =>
        new(SkillFlavour.Agents, Carriers.Doctor, SkillPresence.Ours, coordinate, "page\n");

    // --- skills ---------------------------------------------------------------------------------

    [Fact]
    public void RemovesASkillPageAndThenItsDirectory()
    {
        var plan = Plan(ProjectState.NewBuilder().AddInstalledSkill(Ours(Coordinate)).Build());

        Assert.IsType<DeleteFile>(ActionOn(plan, ".agents/skills/narrativetrace-doctor/SKILL.md"));
        var directory = ActionOn(plan, ".agents/skills/narrativetrace-doctor");
        Assert.IsType<DeleteDirectory>(directory);
        Assert.Equal(1, plan.Actions.ToList().IndexOf(directory!));
    }

    [Fact]
    public void RemovesASkillInstalledFromAnyCarrierVersion()
    {
        var plan = Plan(ProjectState.NewBuilder().AddInstalledSkill(Ours("NarrativeTrace.Skills/0.0.1")).Build());

        Assert.Equal(2, plan.Actions.Count);
    }

    [Fact]
    public void LeavesASkillDirectorySomebodyElseOwnsCompletelyAlone()
    {
        var state = ProjectState.NewBuilder()
            .AddInstalledSkill(new InstalledSkill(SkillFlavour.Agents, "theirs", SkillPresence.Foreign, "", "page\n"))
            .AddInstalledSkill(new InstalledSkill(SkillFlavour.Claude, "a-file", SkillPresence.NotADirectory, "", ""))
            .Build();

        Assert.Empty(Plan(state).Actions);
        Assert.Equal("NarrativeTrace.Skills/unknown", Plan(state).Carrier);
    }

    /// <summary>
    /// A link is never something the installer created, so an uninstall leaves it — link and target
    /// both. Following one would delete a page at the other end that may not even be in this project,
    /// and a page reached through a link reads as OURS whenever it carries the provenance line.
    /// </summary>
    [Fact]
    public void LeavesASymbolicLinkAndWhateverItPointsAtAlone()
    {
        var state = ProjectState.NewBuilder()
            .AddInstalledSkill(new InstalledSkill(
                SkillFlavour.Claude, Carriers.Doctor, SkillPresence.LinkedDirectory, "",
                "page\n" + Provenance.Line(Coordinate) + "\n", "../../.agents/skills/narrativetrace-doctor"))
            .AddInstalledSkill(new InstalledSkill(
                SkillFlavour.Agents, Carriers.Doctor, SkillPresence.LinkedPage, "",
                "page\n", "../elsewhere/SKILL.md"))
            .Build();

        Assert.Empty(Plan(state).Actions);
    }

    // --- the managed section --------------------------------------------------------------------

    [Fact]
    public void RemovesTheSectionAndLeavesTheRestByteForByte()
    {
        var text = "# Agents\n\n" + Block + "\ntail\n";
        var state = ProjectState.NewBuilder().AgentsMd(text).Build();

        var plan = Plan(state);

        Assert.IsType<ReplaceBlock>(ActionOn(plan, AgentsMd));
        Assert.Equal("# Agents\n\ntail\n", After(plan, AgentsMd));
    }

    [Fact]
    public void DeletesAFileTheInstallerCreatedOnceNothingOfItIsLeft()
    {
        var created = MarkedBlock.CreatedNote + "\n" + Block;

        var plan = Plan(ProjectState.NewBuilder().AgentsMd(created).Build());

        Assert.IsType<DeleteFile>(ActionOn(plan, AgentsMd));
        Assert.Equal("", After(plan, AgentsMd));
    }

    [Fact]
    public void KeepsAFileTheInstallerCreatedOnceSomebodyElseHasWrittenInIt()
    {
        var created = MarkedBlock.CreatedNote + "\n" + Block + "\nmy own notes\n";

        var plan = Plan(ProjectState.NewBuilder().AgentsMd(created).Build());

        Assert.IsType<ReplaceBlock>(ActionOn(plan, AgentsMd));
        Assert.Equal("\nmy own notes\n", After(plan, AgentsMd));
    }

    [Fact]
    public void NeverDeletesAFileTheInstallerOnlyAppendedTo()
    {
        var plan = Plan(ProjectState.NewBuilder().AgentsMd(Block).Build());

        Assert.IsType<ReplaceBlock>(ActionOn(plan, AgentsMd));
        Assert.Equal("", After(plan, AgentsMd));
    }

    [Fact]
    public void LeavesAFileWithNoSectionOfOursAlone()
    {
        Assert.Empty(Plan(ProjectState.NewBuilder().AgentsMd("# Agents\n").Build()).Actions);
    }

    [Fact]
    public void RefusesAFileWithTwoSections()
    {
        var plan = Plan(ProjectState.NewBuilder().AgentsMd(Block + "\n" + Block).Build());

        Assert.IsType<Refuse>(ActionOn(plan, AgentsMd));
        Assert.Equal(1, plan.ExitCode);
    }

    [Fact]
    public void RefusesAFileWhoseMarkersDoNotPairUp()
    {
        var plan = Plan(ProjectState.NewBuilder().AgentsMd("<!-- narrativetrace:start -->\nx\n").Build());

        Assert.IsType<Refuse>(ActionOn(plan, AgentsMd));
    }

    // --- the import line ------------------------------------------------------------------------

    [Fact]
    public void RemovesTheExactImportLineItAdded()
    {
        var state = ProjectState.NewBuilder().ClaudeMd("# Project\n\n@AGENTS.md\n").Build();

        var plan = Plan(state);

        Assert.Equal("# Project\n", After(plan, ClaudeMd));
    }

    [Fact]
    public void LeavesALineThatIsNotTheExactLineItAdded()
    {
        var spaces = ProjectState.NewBuilder().ClaudeMd("# P\n\n@AGENTS.md  \n").Build();
        var fenced = ProjectState.NewBuilder().ClaudeMd("```\n@AGENTS.md\n```\n").Build();
        var quoted = ProjectState.NewBuilder().ClaudeMd("see `@AGENTS.md`\n").Build();

        Assert.Empty(Plan(spaces).Actions);
        Assert.Empty(Plan(fenced).Actions);
        Assert.Empty(Plan(quoted).Actions);
    }

    [Fact]
    public void DoesNothingWhenThereIsNoVendorContextFile()
    {
        Assert.Empty(Plan(ProjectState.NewBuilder().Build()).Actions);
    }

    // --- rule files, scope and the plan itself ---------------------------------------------------

    [Fact]
    public void RemovesOurSectionFromAVendorRuleFileButNeverTheFile()
    {
        var rules = "# rules\n\n" + Block;
        var state = ProjectState.NewBuilder().MarkedRuleFile(".cursorrules", rules).Build();

        var plan = Plan(state);

        Assert.IsType<ReplaceBlock>(ActionOn(plan, ".cursorrules"));
        Assert.Equal("# rules\n", After(plan, ".cursorrules"));
    }

    [Fact]
    public void RemovesOnlyTheHalfItWasAskedFor()
    {
        var state = ProjectState.NewBuilder().AddInstalledSkill(Ours(Coordinate)).AgentsMd(Block).Build();

        var skillsOnly = UninstallPlanner.Plan(state, InitOptions.Defaults().WithScope(Scope.Skills));
        var sectionOnly = UninstallPlanner.Plan(state, InitOptions.Defaults().WithScope(Scope.AgentsMd));

        Assert.Null(ActionOn(skillsOnly, AgentsMd));
        Assert.Equal(2, skillsOnly.Actions.Count);
        Assert.NotNull(ActionOn(sectionOnly, AgentsMd));
        Assert.Single(sectionOnly.Actions);
    }

    [Fact]
    public void NamesTheCarrierTheProjectWasInstalledFrom()
    {
        var fromSection = ProjectState.NewBuilder().AgentsMd(Block).Build();
        var fromSkill = ProjectState.NewBuilder().AddInstalledSkill(Ours(Coordinate)).Build();
        var fromNothing = ProjectState.NewBuilder().Build();

        Assert.Equal(Coordinate, Plan(fromSection).Carrier);
        Assert.Equal(Coordinate, Plan(fromSkill).Carrier);
        Assert.EndsWith("/unknown", Plan(fromNothing).Carrier, StringComparison.Ordinal);
    }

    [Fact]
    public void ReportsTheUnknownCarrierWhenTheSectionCarriesNoStamp()
    {
        var unstamped = "<!-- narrativetrace:start -->\nhand written\n<!-- narrativetrace:end -->\n";

        var plan = Plan(ProjectState.NewBuilder().AgentsMd(unstamped).Build());

        Assert.Equal("NarrativeTrace.Skills/unknown", plan.Carrier);
        Assert.IsType<ReplaceBlock>(ActionOn(plan, AgentsMd));
    }

    [Fact]
    public void AnUninstallOfNothingIsAnEmptyPlanThatExitsZero()
    {
        var plan = Plan(ProjectState.NewBuilder().Build());

        Assert.True(plan.IsEmpty);
        Assert.Equal(0, plan.ExitCode);
    }

    [Fact]
    public void CarriesTheDryRunFlag()
    {
        var plan = UninstallPlanner.Plan(
            ProjectState.NewBuilder().AgentsMd(Block + "\n" + Block).Build(),
            InitOptions.Defaults().WithDryRun(true));

        Assert.True(plan.DryRun);
        Assert.Equal(0, plan.ExitCode);
    }

    [Fact]
    public void RefusesToPlanWithoutASnapshotOrOptions()
    {
        Assert.Throws<ArgumentException>(() => UninstallPlanner.Plan(null!, InitOptions.Defaults()));
        Assert.Throws<ArgumentException>(() => UninstallPlanner.Plan(ProjectState.NewBuilder().Build(), null!));
    }
}
