// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Tooling.Init;
using Xunit;
using Action = NarrativeTrace.Tooling.Init.Action;

namespace NarrativeTrace.Tooling.Tests.Init;

/// <summary>
/// The refresh-on-build plan: the narrow, never-creating half of an install. Pure, like the other two
/// planners, so every rule here is a unit test with no disk and no build at all. Method names mirror
/// the Java <c>RefreshPlannerTest</c>; this port's carrier carries two fixed skills and stamps the
/// <c>NarrativeTrace.Skills/&lt;version&gt;</c> coordinate (D6). Where a Java case opened a second
/// carrier only to have "more skills than are installed", this port uses the default two-skill carrier.
/// </summary>
public sealed class RefreshPlannerTest : IDisposable
{
    private const string Old = "NarrativeTrace.Skills/0.0.1";
    private const string DoctorPage = ".agents/skills/narrativetrace-doctor/SKILL.md";

    private readonly string _root = Path.Combine(Path.GetTempPath(), "nt-refresh-" + Guid.NewGuid().ToString("N"));
    private readonly Carrier _carrier;

    public RefreshPlannerTest()
    {
        Directory.CreateDirectory(_root);
        _carrier = Carriers.Open(Path.Combine(_root, "carrier"), "1.2.3");
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static ProjectState.Builder Project() => ProjectState.NewBuilder();

    [Fact]
    public void PlansNothingForAProjectThatNeverRanInit()
    {
        var plan = RefreshPlanner.Plan(Project().Build(), _carrier);

        Assert.True(plan.IsEmpty);
    }

    [Fact]
    public void RewritesAStaleSkillPageInPlace()
    {
        var state = Project().AddInstalledSkill(Ours(Carriers.Doctor, Old)).Build();

        var plan = RefreshPlanner.Plan(state, _carrier);

        Assert.Contains(DoctorPage, plan.Actions.Select(action => action.Path));
        Assert.All(plan.Actions, action => Assert.IsType<ReplaceBlock>(action));
    }

    [Fact]
    public void PlansNothingWhenEveryInstalledSkillAlreadyCarriesTheCarriersCoordinate()
    {
        var state = Project().AddInstalledSkill(Ours(Carriers.Doctor, _carrier.Coordinate)).Build();

        Assert.True(RefreshPlanner.Plan(state, _carrier).IsEmpty);
    }

    [Fact]
    public void LeavesAPageAloneWhenItsStampMatchesHoweverItHasBeenEdited()
    {
        var edited = new InstalledSkill(
            SkillFlavour.Agents,
            Carriers.Doctor,
            SkillPresence.Ours,
            _carrier.Coordinate,
            Provenance.Stamp("---\nname: doctor\n---\n\nsomebody's own words\n", _carrier.Coordinate));

        var plan = RefreshPlanner.Plan(Project().AddInstalledSkill(edited).Build(), _carrier);

        Assert.True(plan.IsEmpty);
    }

    [Fact]
    public void NeverCreatesASkillTheProjectDoesNotHave()
    {
        var state = Project().AddInstalledSkill(Ours(Carriers.Doctor, Old)).Build();

        var plan = RefreshPlanner.Plan(state, _carrier);

        Assert.Equal([DoctorPage], plan.Actions.Select(action => action.Path).ToList());
    }

    [Fact]
    public void RefreshesTheStalePageAndLeavesASkillTheCarrierDoesNotCarry()
    {
        var state = Project()
            .AddInstalledSkill(Ours(Carriers.Doctor, Old))
            .AddInstalledSkill(Ours("harvester", _carrier.Coordinate))
            .Build();

        var plan = RefreshPlanner.Plan(state, _carrier);

        Assert.Equal([DoctorPage], plan.Actions.Select(action => action.Path).ToList());
    }

    [Fact]
    public void NeverAppendsASectionToAnAgentsMdThatCarriesNone()
    {
        var state = Project().AddInstalledSkill(Ours(Carriers.Doctor, Old)).AgentsMd("# Agents\n").Build();

        var plan = RefreshPlanner.Plan(state, _carrier);

        Assert.DoesNotContain(plan.Actions, action => action.Path == "AGENTS.md");
    }

    [Fact]
    public void RewritesOurOwnSectionBesideThePages()
    {
        var agents =
            "# Agents\n\n<!-- narrativetrace:start " + Old + " -->\nstale\n<!-- narrativetrace:end -->\n";
        var state = Project().AddInstalledSkill(Ours(Carriers.Doctor, Old)).AgentsMd(agents).Build();

        var plan = RefreshPlanner.Plan(state, _carrier);

        Assert.Contains("AGENTS.md", plan.Actions.Select(action => action.Path));
        Assert.Contains(_carrier.Coordinate, After(plan, "AGENTS.md"), StringComparison.Ordinal);
    }

    [Fact]
    public void NeverAddsTheImportLineToAClaudeMd()
    {
        var state = Project().AddInstalledSkill(Ours(Carriers.Doctor, Old)).ClaudeMd("# Claude\n").Build();

        var plan = RefreshPlanner.Plan(state, _carrier);

        Assert.DoesNotContain(plan.Actions, action => action.Path == "CLAUDE.md");
    }

    [Fact]
    public void NeverTouchesASkillDirectorySomebodyElseOwns()
    {
        var state = Project()
            .AddInstalledSkill(Ours(Carriers.Doctor, Old))
            .AddInstalledSkill(new InstalledSkill(SkillFlavour.Agents, "theirs", SkillPresence.Foreign, "", "# theirs\n"))
            .Build();

        var plan = RefreshPlanner.Plan(state, _carrier);

        Assert.Equal([DoctorPage], plan.Actions.Select(action => action.Path).ToList());
    }

    [Fact]
    public void NeverCarriesARefusal()
    {
        var state = Project()
            .AddInstalledSkill(Ours(Carriers.Doctor, Old))
            .AddInstalledSkill(new InstalledSkill(SkillFlavour.Agents, Carriers.AddTracing, SkillPresence.NotADirectory, "", ""))
            .AgentsMd(
                "# Agents\n\n<!-- narrativetrace:start -->\na\n<!-- narrativetrace:end -->\n"
                + "<!-- narrativetrace:start -->\nb\n<!-- narrativetrace:end -->\n")
            .Build();

        var plan = RefreshPlanner.Plan(state, _carrier);

        Assert.False(plan.HasRefusals);
        Assert.Equal(0, plan.ExitCode);
    }

    [Fact]
    public void KnowsWhetherAProjectCarriesAnInstallOfOursAtAll()
    {
        Assert.False(RefreshPlanner.IsInstalled(Project().Build()));
        Assert.True(RefreshPlanner.IsInstalled(Project().AddInstalledSkill(Ours(Carriers.Doctor, Old)).Build()));
    }

    [Fact]
    public void ASkillDirectorySomebodyElseOwnsIsNotAnInstallOfOurs()
    {
        var state = Project()
            .AddInstalledSkill(new InstalledSkill(SkillFlavour.Agents, "theirs", SkillPresence.Foreign, "", "# theirs\n"))
            .AddInstalledSkill(new InstalledSkill(SkillFlavour.Claude, "gone", SkillPresence.NotADirectory, "", ""))
            .Build();

        Assert.False(RefreshPlanner.IsInstalled(state));
    }

    [Fact]
    public void RefusesToAnswerWhetherNothingIsInstalled()
    {
        Assert.Throws<ArgumentException>(() => RefreshPlanner.IsInstalled(null!));
    }

    [Fact]
    public void RefusesToPlanWithoutAStateOrACarrier()
    {
        Assert.Throws<ArgumentException>(() => RefreshPlanner.Plan(null!, _carrier));
        Assert.Throws<ArgumentException>(() => RefreshPlanner.Plan(Project().Build(), null!));
    }

    private static string After(InitPlan plan, string path) =>
        ((FileEdit)plan.Actions.First(action => action.Path == path)).After;

    private static InstalledSkill Ours(string name, string coordinate) =>
        new(SkillFlavour.Agents, name, SkillPresence.Ours, coordinate,
            Provenance.Stamp(Carriers.PageBody("agents", name, "d"), coordinate));
}
