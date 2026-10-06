// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Tooling.Init;
using Xunit;
using Action = NarrativeTrace.Tooling.Init.Action;

namespace NarrativeTrace.Tooling.Tests.Init;

/// <summary>
/// Feature COMBINATIONS, from the adversarial pass that closes the milestone: a flag that changes one
/// decision while another decision still refuses, two skills where only one is somebody else's, a
/// scope that silences half the plan while the other half still has an opinion. Method names mirror the
/// Java <c>AdversarialInitTest</c>; this port's carrier carries two fixed skills (doctor and
/// add-tracing) and stamps the <c>NarrativeTrace.Skills/&lt;version&gt;</c> coordinate (D6).
/// </summary>
public sealed class AdversarialInitTest : IDisposable
{
    private const string Coordinate = "NarrativeTrace.Skills/1.2.3";
    private const string AgentsMd = "AGENTS.md";
    private const string ClaudeMd = "CLAUDE.md";
    private const string DoctorDirectory = ".agents/skills/narrativetrace-doctor";
    private const string DoctorPage = ".agents/skills/narrativetrace-doctor/SKILL.md";
    private const string VendorDoctorPage = ".claude/skills/narrativetrace-doctor/SKILL.md";

    private readonly string _root = Path.Combine(Path.GetTempPath(), "nt-advinit-" + Guid.NewGuid().ToString("N"));
    private readonly Carrier _carrier;

    public AdversarialInitTest()
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

    private InitPlan Plan(ProjectState state, InitOptions options) => InitPlanner.Plan(state, _carrier, options);

    private InitPlan Plan(ProjectState state) => Plan(state, InitOptions.Defaults());

    private static Action? ActionOn(InitPlan plan, string path) =>
        plan.Actions.FirstOrDefault(action => action.Path == path);

    private static string After(InitPlan plan, string path) => ((FileEdit)ActionOn(plan, path)!).After;

    private static ProjectState.Builder Project() => ProjectState.NewBuilder();

    // --- scope silences one half, the other half still decides ----------------------------------

    [Fact]
    public void ScopeSkillsLeavesBOTHContextFilesAloneEvenWhenEachWouldHaveHadAnOpinion()
    {
        var state = Project().AgentsMd("# Agents\n").ClaudeMd("# C\n").HasClaudeDirectory(true).Build();

        var plan = Plan(state, InitOptions.Defaults().WithScope(Scope.Skills));

        Assert.Null(ActionOn(plan, AgentsMd));
        Assert.Null(ActionOn(plan, ClaudeMd));
        Assert.NotNull(ActionOn(plan, DoctorPage));
        Assert.False(plan.HasRefusals);
    }

    [Fact]
    public void ScopeAgentsMdCoversTheImportLineTooAndRefusesBothWithoutTheFlag()
    {
        var state = Project().AgentsMd("# Agents\n").ClaudeMd("# C\n").HasClaudeDirectory(true).Build();

        var plan = Plan(state, InitOptions.Defaults().WithScope(Scope.AgentsMd));

        Assert.IsType<Refuse>(ActionOn(plan, AgentsMd));
        Assert.IsType<Refuse>(ActionOn(plan, ClaudeMd));
        Assert.Null(ActionOn(plan, DoctorPage));
        Assert.Equal(1, plan.ExitCode);
    }

    // --- one flag answers one question; the others still stand ----------------------------------

    [Fact]
    public void ForceOverwritesAForeignSkillWhileTheVendorContextFileIsStillRefused()
    {
        var state = Project().ClaudeMd("# C\n").AddInstalledSkill(Foreign(Carriers.Doctor)).Build();

        var plan = Plan(state, InitOptions.Defaults().WithForce(true));

        Assert.IsType<ReplaceBlock>(ActionOn(plan, DoctorPage));
        Assert.IsType<Refuse>(ActionOn(plan, ClaudeMd));
        Assert.Equal(1, plan.ExitCode);
    }

    [Fact]
    public void ForceReachesTheVendorFlavourOfTheSameForeignSkillToo()
    {
        var state = Project()
            .HasClaudeDirectory(true)
            .AddInstalledSkill(Foreign(Carriers.Doctor))
            .AddInstalledSkill(new InstalledSkill(SkillFlavour.Claude, Carriers.Doctor, SkillPresence.Foreign, "", "theirs\n"))
            .Build();

        var plan = Plan(state, InitOptions.Defaults().WithForce(true));

        Assert.IsType<ReplaceBlock>(ActionOn(plan, DoctorPage));
        Assert.IsType<ReplaceBlock>(ActionOn(plan, VendorDoctorPage));
    }

    /// <summary>One skill of ours, one somebody else's: the refusal is per skill, never per run.</summary>
    [Fact]
    public void RefusesOneSkillAndInstallsTheOtherInTheSamePlan()
    {
        var state = Project().AddInstalledSkill(Foreign(Carriers.Doctor)).Build();

        var plan = Plan(state);

        Assert.IsType<Refuse>(ActionOn(plan, DoctorDirectory));
        Assert.IsType<CreateFile>(ActionOn(plan, ".agents/skills/add-narrative-tracing/SKILL.md"));
        Assert.Equal(1, plan.ExitCode);
    }

    [Fact]
    public void RewritesOurOwnSkillPageWhenItHasDriftedFromTheCarrier()
    {
        var state = Project().AddInstalledSkill(Ours(Carriers.Doctor, Coordinate, "hand edited\n")).Build();

        var plan = Plan(state);

        Assert.IsType<ReplaceBlock>(ActionOn(plan, DoctorPage));
        Assert.Equal(
            Provenance.Stamp(Carriers.PageBody("agents", Carriers.Doctor, Carriers.DoctorDescription), Coordinate),
            After(plan, DoctorPage));
    }

    [Fact]
    public void AnUpgradeLeavesNoTraceOfTheCarrierItReplaced()
    {
        const string old = "NarrativeTrace.Skills/0.0.1";
        var state = Project()
            .AddInstalledSkill(Ours(Carriers.Doctor, old, Provenance.Stamp("---\nx\n---\nold\n", old)))
            .Build();

        var content = After(Plan(state), DoctorPage);

        Assert.Equal(Coordinate, Provenance.CoordinateIn(content));
        Assert.DoesNotContain(old, content, StringComparison.Ordinal);
    }

    // --- a dry run decides everything and writes nothing -----------------------------------------

    [Fact]
    public void ADryRunStillRefusesWhatARealRunWouldRefuseAndStillExitsZero()
    {
        var state = Project().AgentsMd("# Agents\n").ClaudeMd("# C\n").Build();

        var plan = Plan(state, InitOptions.Defaults().WithDryRun(true));

        Assert.Equal(2, plan.Refusals().Count);
        Assert.Equal(0, plan.ExitCode);
        Assert.NotNull(ActionOn(plan, DoctorPage));
    }

    // --- what a file's shape does to the block ---------------------------------------------------

    [Fact]
    public void AppendingToAWindowsFileLeavesNoUnixLineEndingBehind()
    {
        var state = Project().AgentsMd("# Agents\r\n").Build();

        var after = After(Plan(state, InitOptions.Defaults().WithWriteExisting(true)), AgentsMd);

        Assert.StartsWith("# Agents\r\n\r\n", after, StringComparison.Ordinal);
        Assert.DoesNotContain("\n\n", after, StringComparison.Ordinal);
        Assert.Contains("\r\n\r\n", after, StringComparison.Ordinal);
    }

    [Fact]
    public void ACreatedFileUsesUnixLineEndingsBecauseThereIsNoFileToFollow()
    {
        var after = After(Plan(Project().Build()), AgentsMd);

        Assert.EndsWith("\n", after, StringComparison.Ordinal);
        Assert.DoesNotContain("\r", after, StringComparison.Ordinal);
    }

    /// <summary>The created note marks a file the installer MADE — never one it merely wrote into.</summary>
    [Fact]
    public void OnlyACreatedFileCarriesTheCreatedNote()
    {
        var empty = Project().Build();
        var existing = Project().AgentsMd("").Build();

        var created = Plan(empty);
        var appended = Plan(existing, InitOptions.Defaults().WithWriteExisting(true));

        Assert.IsType<CreateFile>(ActionOn(created, AgentsMd));
        Assert.Contains(MarkedBlock.CreatedNote, After(created, AgentsMd), StringComparison.Ordinal);
        Assert.IsType<AppendBlock>(ActionOn(appended, AgentsMd));
        var appendedText = After(appended, AgentsMd);
        Assert.DoesNotContain(MarkedBlock.CreatedNote, appendedText, StringComparison.Ordinal);
        Assert.False(appendedText.StartsWith('\n'));
    }

    [Fact]
    public void RefusesAVendorRuleFileWithTwoSectionsAndSaysToLeaveOne()
    {
        var twice =
            "<!-- narrativetrace:start -->\na\n<!-- narrativetrace:end -->\n"
            + "<!-- narrativetrace:start -->\nb\n<!-- narrativetrace:end -->\n";
        var state = Project().MarkedRuleFile(".cursorrules", twice).Build();

        var refusal = Assert.IsType<Refuse>(ActionOn(Plan(state), ".cursorrules"));

        Assert.Contains("leave exactly one", refusal.Reason, StringComparison.Ordinal);
        Assert.Contains("line 1", refusal.Reason, StringComparison.Ordinal);
        Assert.Contains("line 4", refusal.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void AVendorChoiceOfOffSurvivesEveryOtherFlag()
    {
        var state = Project().HasClaudeDirectory(true).ClaudeMd("# C\n").Build();
        var everythingElse =
            InitOptions.Defaults().WithVendorClaude(Vendor.Off).WithForce(true).WithWriteExisting(true);

        var plan = Plan(state, everythingElse);

        Assert.Null(ActionOn(plan, VendorDoctorPage));
        Assert.NotNull(ActionOn(plan, ClaudeMd));
    }

    private static InstalledSkill Ours(string name, string coordinate, string body) =>
        new(SkillFlavour.Agents, name, SkillPresence.Ours, coordinate, body);

    private static InstalledSkill Foreign(string name) =>
        new(SkillFlavour.Agents, name, SkillPresence.Foreign, "", "theirs\n");
}
