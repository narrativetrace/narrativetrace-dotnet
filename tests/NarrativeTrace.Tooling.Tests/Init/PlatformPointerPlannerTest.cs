// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Tooling.Init;
using NarrativeTrace.Tooling.Platforms;
using Xunit;
using Action = NarrativeTrace.Tooling.Init.Action;

namespace NarrativeTrace.Tooling.Tests.Init;

/// <summary>
/// D2 — a platform's pointer is written only when the platform's marker is in the project, adopted
/// when it is already ours, refused when it is somebody's, and never written through a link. Method
/// names mirror the Java <c>PlatformPointerPlannerTest</c>.
/// </summary>
public sealed class PlatformPointerPlannerTest : IDisposable
{
    private const string ReplitMd = "replit.md";
    private const string CursorRulePath = ".cursor/rules/narrativetrace.mdc";

    private readonly string _root = Path.Combine(Path.GetTempPath(), "nt-pointers-" + Guid.NewGuid().ToString("N"));
    private readonly Carrier _carrier;

    public PlatformPointerPlannerTest()
    {
        Directory.CreateDirectory(_root);
        _carrier = Carriers.Open(Path.Combine(_root, "carrier"), "1.2.3");
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private InitPlan Plan(ProjectState state) => InitPlanner.Plan(state, _carrier, InitOptions.Defaults());

    private static ProjectState.Builder Project() => ProjectState.NewBuilder();

    private static Action? On(InitPlan plan, string path) => plan.Actions.FirstOrDefault(action => action.Path == path);

    private static string After(InitPlan plan, string path) => ((FileEdit)On(plan, path)!).After;

    private static string RuleFor(string coordinate) =>
        Tooling.Init.CursorRule.Render(((Pointer.Rule)PlatformTable.Row("cursor")!.Pointer).Text, coordinate);

    private string Section(ProjectState state) => AgentsMdBlock.Render(_carrier, state);

    // --- no marker, no pointer ------------------------------------------------------------------

    [Fact]
    public void WritesNoPointerForAProjectOnNoPlatform()
    {
        var plan = Plan(Project().Build());

        Assert.Null(On(plan, ReplitMd));
        Assert.Null(On(plan, CursorRulePath));
    }

    [Fact]
    public void WritesNoPointerForPlatformsThatNeedNone()
    {
        var state = Project().DetectedPlatform("antigravity").DetectedPlatform("github-copilot")
            .DetectedPlatform("claude-code").Build();

        var paths = Plan(state).Actions.Select(action => action.Path);

        Assert.DoesNotContain(ReplitMd, paths);
        Assert.DoesNotContain(CursorRulePath, paths);
    }

    // --- Replit ---------------------------------------------------------------------------------

    [Fact]
    public void CreatesReplitMdWithTheManagedSectionWhenOnlyTheDotReplitFileMarksTheProject()
    {
        var state = Project().DetectedPlatform("replit").Build();

        var plan = Plan(state);

        Assert.IsType<CreateFile>(On(plan, ReplitMd));
        Assert.Equal(MarkedBlock.CreatedNote + "\n" + Section(state), After(plan, ReplitMd));
    }

    [Fact]
    public void RefusesToAppendToAnExistingReplitMdWithoutPermission()
    {
        var state = Project().DetectedPlatform("replit").PointerFile(ReplitMd, "# My notes\n").Build();

        Assert.IsType<Refuse>(On(Plan(state), ReplitMd));
    }

    [Fact]
    public void AppendsTheSectionToAnExistingReplitMdWhenAllowed()
    {
        var state = Project().DetectedPlatform("replit").PointerFile(ReplitMd, "# My notes\n").Build();

        var plan = InitPlanner.Plan(state, _carrier, InitOptions.Defaults().WithWriteExisting(true));

        Assert.IsType<AppendBlock>(On(plan, ReplitMd));
        Assert.StartsWith("# My notes\n\n", After(plan, ReplitMd), StringComparison.Ordinal);
        Assert.EndsWith(Section(state), After(plan, ReplitMd), StringComparison.Ordinal);
    }

    [Fact]
    public void LeavesACurrentReplitMdAloneAndRefreshesAStaleOne()
    {
        var current = MarkedBlock.CreatedNote + "\n" + Section(Project().DetectedPlatform("replit").Build());
        var upToDate = Project().DetectedPlatform("replit").PointerFile(ReplitMd, current).Build();
        Assert.Null(On(Plan(upToDate), ReplitMd));

        var stale = current.Replace(_carrier.Coordinate, "NarrativeTrace.Skills/0.0.1", StringComparison.Ordinal);
        var behind = Project().DetectedPlatform("replit").PointerFile(ReplitMd, stale).Build();

        Assert.IsType<ReplaceBlock>(On(Plan(behind), ReplitMd));
        Assert.Equal(current, After(Plan(behind), ReplitMd));
    }

    // --- Cursor ---------------------------------------------------------------------------------

    [Fact]
    public void CreatesTheCursorRuleWhenTheCursorDirectoryIsThere()
    {
        var plan = Plan(Project().DetectedPlatform("cursor").Build());

        Assert.IsType<CreateFile>(On(plan, CursorRulePath));
        Assert.Equal(RuleFor(_carrier.Coordinate), After(plan, CursorRulePath));
    }

    [Fact]
    public void TheCursorRuleIsOneRuleAndNotACopyOfAnySkillPage()
    {
        var rule = RuleFor(_carrier.Coordinate);

        Assert.StartsWith("---\n", rule, StringComparison.Ordinal);
        Assert.Contains("alwaysApply: true", rule, StringComparison.Ordinal);
        Assert.Contains(".agents/skills", rule, StringComparison.Ordinal);
        Assert.DoesNotContain("narrativetrace-doctor", rule, StringComparison.Ordinal);
        Assert.True(MarkedBlock.ScanText(rule).HasExactlyOneRegion);
    }

    [Fact]
    public void LeavesACurrentCursorRuleAloneAndRefreshesAStaleOne()
    {
        var current = Project().DetectedPlatform("cursor")
            .PointerFile(CursorRulePath, RuleFor(_carrier.Coordinate)).Build();
        Assert.Null(On(Plan(current), CursorRulePath));

        var stale = Project().DetectedPlatform("cursor")
            .PointerFile(CursorRulePath, RuleFor("NarrativeTrace.Skills/0.0.1")).Build();

        Assert.IsType<ReplaceBlock>(On(Plan(stale), CursorRulePath));
        Assert.Equal(RuleFor(_carrier.Coordinate), After(Plan(stale), CursorRulePath));
    }

    [Fact]
    public void AdoptsTheUnstampedRuleThePluginShipsWithoutAskingForForce()
    {
        var state = Project().DetectedPlatform("cursor").PointerFile(CursorRulePath, RuleFor("")).Build();

        var plan = Plan(state);

        Assert.IsType<AdoptPage>(On(plan, CursorRulePath));
        Assert.Equal(RuleFor(_carrier.Coordinate), After(plan, CursorRulePath));
    }

    [Fact]
    public void RefusesARuleFileSomebodyElseWroteAtOurPath()
    {
        var state = Project().DetectedPlatform("cursor")
            .PointerFile(CursorRulePath, "---\nalwaysApply: true\n---\nmine\n").Build();

        var action = Assert.IsType<Refuse>(On(Plan(state), CursorRulePath));

        Assert.Contains("not written by narrativetrace", action.Reason, StringComparison.Ordinal);
    }

    // --- links, scope and the mixed case --------------------------------------------------------

    [Fact]
    public void RefusesAPointerWhosePathRunsThroughASymbolicLink()
    {
        var state = Project().DetectedPlatform("cursor").DetectedPlatform("replit")
            .PointerBehindLink(CursorRulePath).PointerBehindLink(ReplitMd).Build();

        var plan = Plan(state);

        Assert.IsType<Refuse>(On(plan, CursorRulePath));
        Assert.Contains("symbolic link", Assert.IsType<Refuse>(On(plan, ReplitMd)).Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void PlansBothPointersWhenTwoMarkersArePresentAtOnce()
    {
        var plan = Plan(Project().DetectedPlatform("cursor").DetectedPlatform("replit").Build());

        Assert.IsType<CreateFile>(On(plan, ReplitMd));
        Assert.IsType<CreateFile>(On(plan, CursorRulePath));
    }

    [Fact]
    public void ASkillsOnlyRunWritesNoPointer()
    {
        var state = Project().DetectedPlatform("cursor").DetectedPlatform("replit").Build();

        var plan = InitPlanner.Plan(state, _carrier, InitOptions.Defaults().WithScope(Scope.Skills));

        Assert.Null(On(plan, ReplitMd));
        Assert.Null(On(plan, CursorRulePath));
    }
}
