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
    private const string VendorPage = ".claude/skills/narrativetrace-doctor/SKILL.md";
    private const string LinkTarget = "../../.agents/skills/narrativetrace-doctor";

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
    public void InstallsTheVendorFlavourOnEveryProjectBecauseClaudeCodeReadsNothingElse()
    {
        const string vendorPage = ".claude/skills/narrativetrace-doctor/SKILL.md";

        Assert.NotNull(ActionOn(Plan(Project().Build()), vendorPage));
        Assert.NotNull(ActionOn(Plan(Project().HasClaudeDirectory(true).Build()), vendorPage));
        Assert.NotNull(ActionOn(Plan(Project().ClaudeMd("# C\n").Build()), vendorPage));
    }

    [Fact]
    public void ObeysAnExplicitVendorChoice()
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

    // --- adoption of an identical page ----------------------------------------------------------

    [Fact]
    public void AdoptsAForeignPageThatIsIdenticalToTheCarriersOwnWithoutAnyFlag()
    {
        var state = Project().AddInstalledSkill(Theirs(IdenticalPage())).Build();

        var plan = Plan(state);

        Assert.IsType<AdoptPage>(ActionOn(plan, DoctorPage));
        Assert.Equal(Provenance.Stamp(IdenticalPage(), Coordinate), After(plan, DoctorPage));
        Assert.Equal(0, plan.ExitCode);
    }

    /// <summary>The vendor flavour is adopted on the same terms: a rule that holds on one path only is none.</summary>
    [Fact]
    public void AdoptsAnIdenticalVendorPageToo()
    {
        const string vendorPage = ".claude/skills/narrativetrace-doctor/SKILL.md";
        var rendered = Carriers.PageBody("claude", Carriers.Doctor, Carriers.DoctorDescription);
        var state = Project()
            .HasClaudeDirectory(true)
            .AddInstalledSkill(new InstalledSkill(SkillFlavour.Claude, Carriers.Doctor, SkillPresence.Foreign, "", rendered))
            .Build();

        Assert.IsType<AdoptPage>(ActionOn(Plan(state), vendorPage));
    }

    /// <summary>
    /// The OTHER flavour's page at this flavour's path is a near miss, not a match: it is what a
    /// registry leaves when it installs one flavour and links the other to it, and adopting it would
    /// leave the project with two copies of the open-standard page and no vendor page at all.
    /// </summary>
    [Fact]
    public void RefusesAPageThatIsTheOtherFlavoursRendering()
    {
        var state = Project()
            .HasClaudeDirectory(true)
            .AddInstalledSkill(new InstalledSkill(
                SkillFlavour.Claude, Carriers.Doctor, SkillPresence.Foreign, "",
                Carriers.PageBody("agents", Carriers.Doctor, Carriers.DoctorDescription)))
            .Build();

        Assert.IsType<Refuse>(ActionOn(Plan(state), ".claude/skills/narrativetrace-doctor"));
    }

    /// <summary>Adoption is about a name the catalogue owns; a skill of theirs stays theirs, identical or not.</summary>
    [Fact]
    public void LeavesAnIdenticalPageUnderASkillNameTheCatalogueNeverNamedAlone()
    {
        var state = Project()
            .AddInstalledSkill(new InstalledSkill(
                SkillFlavour.Agents, "deploy-to-staging", SkillPresence.Foreign, "", IdenticalPage()))
            .Build();

        Assert.DoesNotContain(
            Plan(state).Actions,
            action => action.Path.StartsWith(".agents/skills/deploy-to-staging", StringComparison.Ordinal));
    }

    [Fact]
    public void NeedsNoForceToAdoptAndAdoptsWhenForcedAnyway()
    {
        var state = Project().AddInstalledSkill(Theirs(IdenticalPage())).Build();

        var forced = Plan(state, InitOptions.Defaults().WithForce(true));

        Assert.IsType<AdoptPage>(ActionOn(forced, DoctorPage));
    }

    /// <summary>Once adopted, the page is ours: the next run of the same carrier plans nothing at all.</summary>
    [Fact]
    public void PlansNothingForAPageItAlreadyAdopted()
    {
        var adopted = Project()
            .AddInstalledSkill(Ours(Carriers.Doctor, Coordinate, Provenance.Stamp(IdenticalPage(), Coordinate)))
            .Build();

        Assert.Null(ActionOn(Plan(adopted), DoctorPage));
    }

    // --- symbolic links -------------------------------------------------------------------------

    /// <summary>
    /// The registry hazard, at the planner: the vendor directory is a LINK to the open-standard one,
    /// so writing the vendor page through it would overwrite the page just adopted. The link is
    /// replaced by a real directory instead, and the page it pointed at is not in the plan at all.
    /// </summary>
    [Fact]
    public void ReplacesALinkOverAPageItAdoptsWithARealDirectory()
    {
        var state = Project()
            .HasClaudeDirectory(true)
            .AddInstalledSkill(Linked(SkillPresence.LinkedDirectory, IdenticalPage()))
            .Build();

        var plan = Plan(state);

        var action = Assert.IsType<ReplaceLink>(ActionOn(plan, VendorPage));
        Assert.Equal(".claude/skills/narrativetrace-doctor", action.Link);
        Assert.Equal(LinkTarget, action.Target);
        Assert.Equal(
            Provenance.Stamp(Carriers.PageBody("claude", Carriers.Doctor, Carriers.DoctorDescription), Coordinate),
            action.After);
        Assert.Equal(0, plan.ExitCode);
    }

    [Fact]
    public void ReplacesALinkOverAPageOfItsOwn()
    {
        var ours = Provenance.Stamp(IdenticalPage(), "NarrativeTrace.Skills/0.0.1");
        var state = Project()
            .HasClaudeDirectory(true)
            .AddInstalledSkill(Linked(SkillPresence.LinkedDirectory, ours))
            .Build();

        Assert.IsType<ReplaceLink>(ActionOn(Plan(state), VendorPage));
    }

    /// <summary>
    /// A link whose page matches the VENDOR flavour's rendering is adoptable too — IsOursOrAdoptable
    /// checks both flavours, because a link could reach either. Exercises the Claude branch of that
    /// decision, the one the registry case (which links to the agents page) never reaches.
    /// </summary>
    [Fact]
    public void ReplacesALinkWhosePageMatchesTheVendorFlavour()
    {
        var vendorBody = Carriers.PageBody("claude", Carriers.Doctor, Carriers.DoctorDescription);
        var state = Project()
            .HasClaudeDirectory(true)
            .AddInstalledSkill(Linked(SkillPresence.LinkedDirectory, vendorBody))
            .Build();

        Assert.IsType<ReplaceLink>(ActionOn(Plan(state), VendorPage));
    }

    /// <summary>A linked PAGE inside a real directory is the same decision, one level down.</summary>
    [Fact]
    public void ReplacesALinkedPageWithARealFile()
    {
        var state = Project()
            .HasClaudeDirectory(true)
            .AddInstalledSkill(Linked(SkillPresence.LinkedPage, IdenticalPage()))
            .Build();

        var action = Assert.IsType<ReplaceLink>(ActionOn(Plan(state), VendorPage));
        Assert.Equal(VendorPage, action.Link);
    }

    [Fact]
    public void RefusesALinkOverAPageNobodyCanPlaceAndNamesTheTarget()
    {
        var state = Project()
            .HasClaudeDirectory(true)
            .AddInstalledSkill(Linked(SkillPresence.LinkedDirectory, "somebody else's page\n"))
            .Build();

        var refusal = Assert.IsType<Refuse>(ActionOn(Plan(state), ".claude/skills/narrativetrace-doctor"));
        Assert.Contains("symbolic link", refusal.Reason, StringComparison.Ordinal);
        Assert.Contains(LinkTarget, refusal.Reason, StringComparison.Ordinal);
        Assert.Contains("remove the link", refusal.Reason, StringComparison.Ordinal);
    }

    /// <summary>A dangling link, and one out of the project, both reach no page: refused, never followed.</summary>
    [Fact]
    public void RefusesALinkThatReachesNoPage()
    {
        var state = Project()
            .HasClaudeDirectory(true)
            .AddInstalledSkill(Linked(SkillPresence.LinkedDirectory, ""))
            .Build();

        var refusal = Assert.IsType<Refuse>(ActionOn(Plan(state), ".claude/skills/narrativetrace-doctor"));
        Assert.Contains("no page", refusal.Reason, StringComparison.Ordinal);
        Assert.Contains(LinkTarget, refusal.Reason, StringComparison.Ordinal);
    }

    /// <summary>
    /// The refusal for a linked PAGE names the page, not the directory. One level down from
    /// <see cref="RefusesALinkOverAPageNobodyCanPlaceAndNamesTheTarget"/>, and a different path in the
    /// plan — which is what the "never two actions on one path" property reads, and what a person has
    /// to be told to remove.
    /// </summary>
    [Fact]
    public void RefusesALinkedPageOverSomebodyElsesPageAtThePageItself()
    {
        var state = Project()
            .HasClaudeDirectory(true)
            .AddInstalledSkill(Linked(SkillPresence.LinkedPage, "somebody else's page\n"))
            .Build();

        var refusal = Assert.IsType<Refuse>(ActionOn(Plan(state), VendorPage));
        Assert.Contains("symbolic link", refusal.Reason, StringComparison.Ordinal);
        Assert.Contains("--force covers content", refusal.Reason, StringComparison.Ordinal);
        Assert.Equal(1, Plan(state).ExitCode);
    }

    /// <summary><c>--force</c> covers foreign CONTENT, never structure — and a link is structure.</summary>
    [Fact]
    public void ForceDoesNotCoverALink()
    {
        var state = Project()
            .HasClaudeDirectory(true)
            .AddInstalledSkill(Linked(SkillPresence.LinkedDirectory, "somebody else's page\n"))
            .Build();

        var forced = Plan(state, InitOptions.Defaults().WithForce(true));

        Assert.IsType<Refuse>(ActionOn(forced, ".claude/skills/narrativetrace-doctor"));
        Assert.Equal(1, forced.ExitCode);
    }

    /// <summary>One refused link never costs the other flavour, or the other skill, its own install.</summary>
    [Fact]
    public void ARefusedLinkLeavesTheRestOfThePlanAlone()
    {
        var state = Project()
            .HasClaudeDirectory(true)
            .AddInstalledSkill(Linked(SkillPresence.LinkedDirectory, "theirs\n"))
            .Build();

        var plan = Plan(state);

        Assert.IsType<CreateFile>(ActionOn(plan, DoctorPage));
        Assert.NotNull(ActionOn(plan, AgentsMd));
    }

    /// <summary>
    /// A linked install root is one decision, not one per skill: the refusal names the root, every
    /// skill of that flavour is left unplanned, and the other flavour is installed as usual.
    /// </summary>
    [Fact]
    public void RefusesAWholeFlavourWhoseInstallRootIsALinkAndInstallsTheOther()
    {
        var state = Project()
            .HasClaudeDirectory(true)
            .LinkedInstallRoot(SkillFlavour.Claude, "/elsewhere")
            .Build();

        var plan = Plan(state);

        var refusal = Assert.IsType<Refuse>(ActionOn(plan, ".claude/skills"));
        Assert.Contains("symbolic link", refusal.Reason, StringComparison.Ordinal);
        Assert.Contains("/elsewhere", refusal.Reason, StringComparison.Ordinal);
        Assert.Contains("remove the link", refusal.Reason, StringComparison.Ordinal);
        Assert.Null(ActionOn(plan, VendorPage));
        Assert.IsType<CreateFile>(ActionOn(plan, DoctorPage));
        Assert.Equal(1, plan.ExitCode);
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

    /// <summary>The carrier's own AGENTS page, byte for byte — what a registry install leaves behind.</summary>
    private static string IdenticalPage() =>
        Carriers.PageBody("agents", Carriers.Doctor, Carriers.DoctorDescription);

    private static InstalledSkill Theirs(string body) =>
        new(SkillFlavour.Agents, Carriers.Doctor, SkillPresence.Foreign, "", body);

    /// <summary>A skill whose vendor path is what `npx skills add` leaves: a link to the agents directory.</summary>
    private static InstalledSkill Linked(SkillPresence presence, string reached) =>
        new(SkillFlavour.Claude, Carriers.Doctor, presence, "", reached, LinkTarget);

    private static InstalledSkill Ours(string name, string coordinate, string body) =>
        new(SkillFlavour.Agents, name, SkillPresence.Ours, coordinate, body);
}
