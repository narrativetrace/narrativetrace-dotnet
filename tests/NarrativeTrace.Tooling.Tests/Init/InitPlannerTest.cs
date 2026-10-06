// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Tooling.Init;
using Xunit;
using Action = NarrativeTrace.Tooling.Init.Action;

namespace NarrativeTrace.Tooling.Tests.Init;

/// <summary>
/// Every row of the existing-file policy, one case each, plus the near misses that decide whether a
/// row applies at all. The planner is pure, so none of this touches a disk: a snapshot is built by
/// hand and the plan is read back. Method names mirror the Java <c>InitPlannerTest</c>; this port's
/// carrier carries two fixed skills (doctor and add-tracing) and stamps the
/// <c>NarrativeTrace.Skills/&lt;version&gt;</c> coordinate (D6).
/// </summary>
public sealed class InitPlannerTest : IDisposable
{
    private const string Coordinate = "NarrativeTrace.Skills/1.2.3";
    private const string AgentsMd = "AGENTS.md";
    private const string ClaudeMd = "CLAUDE.md";
    private const string DoctorPage = ".agents/skills/narrativetrace-doctor/SKILL.md";

    private readonly string _root = Path.Combine(Path.GetTempPath(), "nt-initplanner-" + Guid.NewGuid().ToString("N"));
    private readonly Carrier _carrier;

    public InitPlannerTest()
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

    private InitPlan Plan(ProjectState state) => InitPlanner.Plan(state, _carrier, InitOptions.Defaults());

    private InitPlan Plan(ProjectState state, InitOptions options) => InitPlanner.Plan(state, _carrier, options);

    private static Action? ActionOn(InitPlan plan, string path) =>
        plan.Actions.FirstOrDefault(action => action.Path == path);

    private static string After(InitPlan plan, string path) =>
        ((FileEdit)ActionOn(plan, path)!).After;

    private string Block(ProjectState state) => AgentsMdBlock.Render(_carrier, state);

    private static ProjectState.Builder Project() => ProjectState.NewBuilder();

    private static InitOptions WriteExisting() => InitOptions.Defaults().WithWriteExisting(true);

    // --- AGENTS.md ------------------------------------------------------------------------------

    [Fact]
    public void CreatesAgentsMdWithTheBlockAndTheCreatedNoteWhenThereIsNone()
    {
        var state = Project().Build();

        var plan = Plan(state);

        Assert.IsType<CreateFile>(ActionOn(plan, AgentsMd));
        Assert.Equal(MarkedBlock.CreatedNote + "\n" + Block(state), After(plan, AgentsMd));
    }

    [Fact]
    public void RefusesAnExistingAgentsMdWithoutTheFlagAndNamesTheFlag()
    {
        var plan = Plan(Project().AgentsMd("# Agents\n").Build());

        var refusal = Assert.IsType<Refuse>(ActionOn(plan, AgentsMd));
        Assert.Contains("--write-existing", refusal.Reason, StringComparison.Ordinal);
        Assert.Equal(1, plan.ExitCode);
    }

    [Fact]
    public void AppendsToAnExistingAgentsMdWithTheFlag()
    {
        var state = Project().AgentsMd("# Agents\n").Build();

        var plan = Plan(state, WriteExisting());

        Assert.IsType<AppendBlock>(ActionOn(plan, AgentsMd));
        Assert.Equal("# Agents\n\n" + Block(state), After(plan, AgentsMd));
    }

    [Fact]
    public void ReplacesOurOwnBlockWithoutAnyFlag()
    {
        var old =
            "# Agents\n\n<!-- narrativetrace:start NarrativeTrace.Skills/0.0.1 -->\n"
            + "stale\n<!-- narrativetrace:end -->\n\ntail\n";
        var state = Project().AgentsMd(old).Build();

        var plan = Plan(state);

        Assert.IsType<ReplaceBlock>(ActionOn(plan, AgentsMd));
        Assert.Equal("# Agents\n\n" + Block(state) + "\ntail\n", After(plan, AgentsMd));
    }

    [Fact]
    public void PlansNothingForAnAgentsMdThatIsAlreadyCurrent()
    {
        var first = Project().Build();
        var written = MarkedBlock.CreatedNote + "\n" + Block(first);
        var state = Project().AgentsMd(written).Build();

        Assert.Null(ActionOn(Plan(state), AgentsMd));
    }

    [Fact]
    public void RefusesAnAgentsMdWithTwoBlocksAndNamesTheLines()
    {
        var twice =
            "<!-- narrativetrace:start -->\na\n<!-- narrativetrace:end -->\n"
            + "<!-- narrativetrace:start -->\nb\n<!-- narrativetrace:end -->\n";

        var plan = Plan(Project().AgentsMd(twice).Build(), WriteExisting());

        var refusal = Assert.IsType<Refuse>(ActionOn(plan, AgentsMd));
        Assert.Contains("line 1", refusal.Reason, StringComparison.Ordinal);
        Assert.Contains("line 4", refusal.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void RefusesAnAgentsMdWithAMarkerThatNeverCloses()
    {
        var plan = Plan(Project().AgentsMd("<!-- narrativetrace:start -->\nbody\n").Build(), WriteExisting());

        Assert.Contains("line 1", ((Refuse)ActionOn(plan, AgentsMd)!).Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void TheRestOfThePlanProceedsWhenAgentsMdIsRefused()
    {
        var twice =
            "<!-- narrativetrace:start -->\na\n<!-- narrativetrace:end -->\n"
            + "<!-- narrativetrace:start -->\nb\n<!-- narrativetrace:end -->\n";

        var plan = Plan(Project().AgentsMd(twice).Build());

        Assert.IsType<CreateFile>(ActionOn(plan, DoctorPage));
        Assert.True(plan.HasRefusals);
    }

    [Fact]
    public void DoesNotMistakeAMarkerInsideAFencedBlockForOurs()
    {
        var documented =
            "# Agents\n\n```\n<!-- narrativetrace:start -->\n<!-- narrativetrace:end -->\n```\n";
        var state = Project().AgentsMd(documented).Build();

        var plan = Plan(state, WriteExisting());

        Assert.IsType<AppendBlock>(ActionOn(plan, AgentsMd));
        Assert.Equal(documented + "\n" + Block(state), After(plan, AgentsMd));
    }

    [Fact]
    public void WritesTheBlockWithTheFilesOwnLineEndings()
    {
        var state = Project().AgentsMd("# Agents\r\n").Build();

        var after = After(Plan(state, WriteExisting()), AgentsMd);

        Assert.Equal("# Agents\r\n\r\n" + MarkedBlock.WithEol(Block(state), "\r\n"), after);
        Assert.DoesNotContain("\r\r", after, StringComparison.Ordinal);
    }

    [Fact]
    public void KeepsAByteOrderMarkAndAMissingFinalNewlineInMind()
    {
        var state = Project().AgentsMd("﻿# Agents").Build();

        Assert.Equal("﻿# Agents\n\n" + Block(state), After(Plan(state, WriteExisting()), AgentsMd));
    }

    [Fact]
    public void AppendsToAnEmptyButExistingAgentsMdWithTheFlag()
    {
        var state = Project().AgentsMd("").Build();

        var plan = Plan(state, WriteExisting());

        Assert.IsType<AppendBlock>(ActionOn(plan, AgentsMd));
        Assert.Equal(Block(state), After(plan, AgentsMd));
    }

    [Fact]
    public void RefusesToAppendToAnAgentsMdThatEndsInsideAnUnfinishedFence()
    {
        var state = Project().AgentsMd("# Agents\n\n```\nnot closed\n").Build();

        var plan = Plan(state, WriteExisting());

        Assert.Contains("unfinished fenced code block",
            ((Refuse)ActionOn(plan, AgentsMd)!).Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void RefusesToAddTheImportLineToAVendorFileThatEndsInsideAnUnfinishedFence()
    {
        var state = Project().ClaudeMd("# Project\n\n~~~\nnot closed\n").Build();

        var plan = Plan(state, WriteExisting());

        Assert.Contains("unfinished fenced code block",
            ((Refuse)ActionOn(plan, ClaudeMd)!).Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void StillReplacesOurOwnSectionInAFileWhoseFenceOpensAfterIt()
    {
        var text =
            "<!-- narrativetrace:start -->\nstale\n<!-- narrativetrace:end -->\n\n```\nnot closed\n";
        var state = Project().AgentsMd(text).Build();

        Assert.IsType<ReplaceBlock>(ActionOn(Plan(state), AgentsMd));
    }

    // --- CLAUDE.md ------------------------------------------------------------------------------

    [Fact]
    public void CreatesNoVendorContextFileWhenTheProjectHasNone()
    {
        Assert.Null(ActionOn(Plan(Project().Build()), ClaudeMd));
    }

    [Fact]
    public void AppendsTheImportLineToAnExistingVendorContextFileWithTheFlag()
    {
        var state = Project().ClaudeMd("# Project\n").Build();

        var plan = Plan(state, WriteExisting());

        Assert.IsType<AppendLine>(ActionOn(plan, ClaudeMd));
        Assert.Equal("# Project\n\n@AGENTS.md\n", After(plan, ClaudeMd));
    }

    [Fact]
    public void RefusesToTouchTheVendorContextFileWithoutTheFlag()
    {
        var plan = Plan(Project().ClaudeMd("# Project\n").Build());

        Assert.Contains("--write-existing", ((Refuse)ActionOn(plan, ClaudeMd)!).Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void LeavesAVendorContextFileThatAlreadyImportsAlone()
    {
        var state = Project().ClaudeMd("# Project\n\n@AGENTS.md\n").Build();

        Assert.Null(ActionOn(Plan(state, WriteExisting()), ClaudeMd));
    }

    [Fact]
    public void CountsAnImportLineWithTrailingSpacesAsAlreadyThere()
    {
        var state = Project().ClaudeMd("# Project\n\n@AGENTS.md   \n").Build();

        Assert.Null(ActionOn(Plan(state, WriteExisting()), ClaudeMd));
    }

    [Fact]
    public void DoesNotCountAnImportLineInsideACommentOrAFence()
    {
        var commented = Project().ClaudeMd("<!-- @AGENTS.md -->\n").Build();
        var fenced = Project().ClaudeMd("```\n@AGENTS.md\n```\n").Build();

        Assert.IsType<AppendLine>(ActionOn(Plan(commented, WriteExisting()), ClaudeMd));
        Assert.IsType<AppendLine>(ActionOn(Plan(fenced, WriteExisting()), ClaudeMd));
    }

    // --- skills ---------------------------------------------------------------------------------

    [Fact]
    public void CopiesEverySkillIntoTheOpenStandardPathWithItsProvenanceLine()
    {
        var plan = Plan(Project().Build());

        Assert.Equal(
            Provenance.Stamp(Carriers.PageBody("agents", Carriers.Doctor, Carriers.DoctorDescription), Coordinate),
            After(plan, DoctorPage));
        Assert.Equal(Coordinate, Provenance.CoordinateIn(After(plan, DoctorPage)));
    }

    [Fact]
    public void InstallsTheVendorFlavourWhereTheVendorIsDetected()
    {
        const string vendorPage = ".claude/skills/narrativetrace-doctor/SKILL.md";

        Assert.NotNull(ActionOn(Plan(Project().HasClaudeDirectory(true).Build()), vendorPage));
        Assert.NotNull(ActionOn(Plan(Project().ClaudeMd("# C\n").Build()), vendorPage));
        Assert.Null(ActionOn(Plan(Project().Build()), vendorPage));
    }

    [Fact]
    public void ObeysAnExplicitVendorChoiceOverDetection()
    {
        const string vendorPage = ".claude/skills/narrativetrace-doctor/SKILL.md";
        var on = InitOptions.Defaults().WithVendorClaude(Vendor.On);
        var off = InitOptions.Defaults().WithVendorClaude(Vendor.Off);

        Assert.NotNull(ActionOn(Plan(Project().Build(), on), vendorPage));
        Assert.Null(ActionOn(Plan(Project().HasClaudeDirectory(true).Build(), off), vendorPage));
    }

    [Fact]
    public void PlansNothingForASkillThatIsAlreadyExactlyRight()
    {
        var installed =
            Provenance.Stamp(Carriers.PageBody("agents", Carriers.Doctor, Carriers.DoctorDescription), Coordinate);
        var state = Project().AddInstalledSkill(Ours(Carriers.Doctor, Coordinate, installed)).Build();

        Assert.DoesNotContain(Plan(state).Actions, action => action.Path == DoctorPage);
    }

    [Fact]
    public void UpgradesASkillInstalledFromAnOlderCarrierWithoutAnyFlag()
    {
        const string oldCoordinate = "NarrativeTrace.Skills/0.0.1";
        var older = Provenance.Stamp("---\nname: doctor\n---\nold\n", oldCoordinate);
        var state = Project().AddInstalledSkill(Ours(Carriers.Doctor, oldCoordinate, older)).Build();

        var plan = Plan(state);

        Assert.IsType<ReplaceBlock>(ActionOn(plan, DoctorPage));
        Assert.Equal(Coordinate, Provenance.CoordinateIn(After(plan, DoctorPage)));
    }

    [Fact]
    public void RefusesASkillDirectorySomebodyElseOwnsAndLetsTheOthersProceed()
    {
        var state = Project()
            .AddInstalledSkill(new InstalledSkill(SkillFlavour.Agents, Carriers.Doctor, SkillPresence.Foreign, "", "theirs\n"))
            .Build();

        var plan = Plan(state);

        var refusal = Assert.IsType<Refuse>(ActionOn(plan, ".agents/skills/narrativetrace-doctor"));
        Assert.Contains("--force", refusal.Reason, StringComparison.Ordinal);
        Assert.NotNull(ActionOn(plan, AgentsMd));
    }

    [Fact]
    public void OverwritesAForeignSkillDirectoryWhenForced()
    {
        var state = Project()
            .AddInstalledSkill(new InstalledSkill(SkillFlavour.Agents, Carriers.Doctor, SkillPresence.Foreign, "", "theirs\n"))
            .Build();

        var plan = Plan(state, InitOptions.Defaults().WithForce(true));

        Assert.IsType<ReplaceBlock>(ActionOn(plan, DoctorPage));
    }

    [Fact]
    public void CreatesThePageWhenAForeignDirectoryHasNoneAndForceIsGiven()
    {
        var state = Project()
            .AddInstalledSkill(new InstalledSkill(SkillFlavour.Agents, Carriers.Doctor, SkillPresence.Foreign, "", ""))
            .Build();

        var plan = Plan(state, InitOptions.Defaults().WithForce(true));

        Assert.IsType<CreateFile>(ActionOn(plan, DoctorPage));
    }

    [Fact]
    public void RefusesAFileSittingWhereASkillDirectoryBelongsEvenWhenForced()
    {
        var state = Project()
            .AddInstalledSkill(new InstalledSkill(SkillFlavour.Agents, Carriers.Doctor, SkillPresence.NotADirectory, "", ""))
            .Build();

        var plan = Plan(state, InitOptions.Defaults().WithForce(true));

        Assert.Contains("not a directory",
            ((Refuse)ActionOn(plan, ".agents/skills/narrativetrace-doctor")!).Reason, StringComparison.Ordinal);
    }

    // --- scope, rule files and the plan itself --------------------------------------------------

    [Fact]
    public void PlansOnlyTheHalfItWasAskedFor()
    {
        var state = Project().Build();

        var skillsOnly = Plan(state, InitOptions.Defaults().WithScope(Scope.Skills));
        var sectionOnly = Plan(state, InitOptions.Defaults().WithScope(Scope.AgentsMd));

        Assert.NotNull(ActionOn(skillsOnly, DoctorPage));
        Assert.Null(ActionOn(skillsOnly, AgentsMd));
        Assert.Null(ActionOn(sectionOnly, DoctorPage));
        Assert.NotNull(ActionOn(sectionOnly, AgentsMd));
    }

    [Fact]
    public void KeepsAnExistingBlockInAVendorRuleFileUpToDate()
    {
        var rules = "# rules\n\n<!-- narrativetrace:start -->\nstale\n<!-- narrativetrace:end -->\n";
        var state = Project().MarkedRuleFile(".cursorrules", rules).Build();

        var plan = Plan(state);

        Assert.IsType<ReplaceBlock>(ActionOn(plan, ".cursorrules"));
        Assert.Contains(Block(state), After(plan, ".cursorrules"), StringComparison.Ordinal);
    }

    [Fact]
    public void RefusesAVendorRuleFileWithTwoBlocks()
    {
        var rules =
            "<!-- narrativetrace:start -->\na\n<!-- narrativetrace:end -->\n"
            + "<!-- narrativetrace:start -->\nb\n<!-- narrativetrace:end -->\n";
        var state = Project().MarkedRuleFile(".cursorrules", rules).Build();

        Assert.IsType<Refuse>(ActionOn(Plan(state), ".cursorrules"));
    }

    [Fact]
    public void StampsThePlanWithTheCarrierAndCarriesTheDryRunFlag()
    {
        var plan = Plan(Project().Build(), InitOptions.Defaults().WithDryRun(true));

        Assert.Equal(Coordinate, plan.Carrier);
        Assert.True(plan.DryRun);
        Assert.Equal(0, plan.ExitCode);
    }

    [Fact]
    public void NeverPlansTwoActionsOnOnePath()
    {
        var state = Project()
            .HasClaudeDirectory(true)
            .ClaudeMd("# C\n")
            .AgentsMd("# A\n")
            .MarkedRuleFile(".cursorrules", "<!-- narrativetrace:start -->\n<!-- narrativetrace:end -->\n")
            .Build();

        var paths = Plan(state, WriteExisting()).Actions.Select(action => action.Path).ToList();

        Assert.Equal(paths.Count, paths.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void RefusesToPlanWithoutASnapshotACarrierOrOptions()
    {
        Assert.Throws<ArgumentException>(() => InitPlanner.Plan(null!, _carrier, InitOptions.Defaults()));
        Assert.Throws<ArgumentException>(() => InitPlanner.Plan(Project().Build(), null!, InitOptions.Defaults()));
        Assert.Throws<ArgumentException>(() => InitPlanner.Plan(Project().Build(), _carrier, null!));
    }

    private static InstalledSkill Ours(string name, string coordinate, string body) =>
        new(SkillFlavour.Agents, name, SkillPresence.Ours, coordinate, body);
}
