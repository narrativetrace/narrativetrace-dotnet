// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Tooling.Init;
using Xunit;
using Action = NarrativeTrace.Tooling.Init.Action;

namespace NarrativeTrace.Tooling.Tests.Init;

/// <summary>An uninstall removes exactly the pointers the installer wrote, and no more.</summary>
public sealed class PlatformPointerUninstallTest
{
    private const string ReplitMd = "replit.md";
    private const string CursorRulePath = ".cursor/rules/narrativetrace.mdc";
    private const string RuleText = "Read the skills.";
    private const string Coordinate = "NarrativeTrace.Skills/1.2.3";

    private static readonly string Section =
        "<!-- narrativetrace:start " + Coordinate + " -->\nsection\n<!-- narrativetrace:end -->\n";

    private static InitPlan Plan(ProjectState state) => UninstallPlanner.Plan(state, InitOptions.Defaults());

    private static Action? On(InitPlan plan, string path) => plan.Actions.FirstOrDefault(action => action.Path == path);

    private static string After(InitPlan plan, string path) => ((FileEdit)On(plan, path)!).After;

    private static string Rule() => CursorRule.Render(RuleText, Coordinate);

    [Fact]
    public void DeletesACursorRuleThatIsNothingButOurSection()
    {
        var state = ProjectState.NewBuilder().PointerFile(CursorRulePath, Rule()).Build();

        Assert.IsType<DeleteFile>(On(Plan(state), CursorRulePath));
    }

    [Fact]
    public void KeepsAnythingElseInACursorRuleTheUserEdited()
    {
        var edited = Rule() + "\nMy own rule below.\n";
        var state = ProjectState.NewBuilder().PointerFile(CursorRulePath, edited).Build();

        var plan = Plan(state);

        Assert.IsType<ReplaceBlock>(On(plan, CursorRulePath));
        Assert.Contains("My own rule below.", After(plan, CursorRulePath), StringComparison.Ordinal);
        Assert.DoesNotContain("narrativetrace:", After(plan, CursorRulePath), StringComparison.Ordinal);
    }

    [Fact]
    public void LeavesACursorRuleWithNoSectionOfOursUntouched()
    {
        var state = ProjectState.NewBuilder().PointerFile(CursorRulePath, "---\nalwaysApply: true\n---\nmine\n").Build();

        Assert.Null(On(Plan(state), CursorRulePath));
    }

    [Fact]
    public void RefusesARuleWithAnUnclosedSectionRatherThanGuessing()
    {
        var state = ProjectState.NewBuilder()
            .PointerFile(CursorRulePath, "---\n---\n<!-- narrativetrace:start x -->\nnever closed\n").Build();

        Assert.IsType<Refuse>(On(Plan(state), CursorRulePath));
    }

    [Fact]
    public void DeletesAReplitMdTheInstallerCreatedAndKeepsOneItAppendedTo()
    {
        var created = ProjectState.NewBuilder().PointerFile(ReplitMd, MarkedBlock.CreatedNote + "\n" + Section).Build();
        Assert.IsType<DeleteFile>(On(Plan(created), ReplitMd));

        var appended = ProjectState.NewBuilder().PointerFile(ReplitMd, "# Notes\n\n" + Section).Build();
        var plan = Plan(appended);

        Assert.IsType<ReplaceBlock>(On(plan, ReplitMd));
        Assert.Equal("# Notes\n", After(plan, ReplitMd));
    }

    [Fact]
    public void RemovesBothPointersWhenBothArePresent()
    {
        var state = ProjectState.NewBuilder()
            .PointerFile(ReplitMd, MarkedBlock.CreatedNote + "\n" + Section)
            .PointerFile(CursorRulePath, Rule()).Build();

        var plan = Plan(state);

        Assert.IsType<DeleteFile>(On(plan, ReplitMd));
        Assert.IsType<DeleteFile>(On(plan, CursorRulePath));
    }

    [Fact]
    public void ASkillsOnlyUninstallLeavesThePointersAlone()
    {
        var state = ProjectState.NewBuilder().PointerFile(CursorRulePath, Rule()).Build();

        var plan = UninstallPlanner.Plan(state, InitOptions.Defaults().WithScope(Scope.Skills));

        Assert.Null(On(plan, CursorRulePath));
    }

    [Fact]
    public void ARuleSavedWithAByteOrderMarkIsDeletedWhenNothingButTheFrontmatterRemains()
    {
        var state = ProjectState.NewBuilder().PointerFile(CursorRulePath, "﻿" + Rule()).Build();

        Assert.IsType<DeleteFile>(On(Plan(state), CursorRulePath));
    }

    [Fact]
    public void ACrlfRuleTheInstallerMadeIsDeleted()
    {
        var state = ProjectState.NewBuilder().PointerFile(CursorRulePath, Rule().Replace("\n", "\r\n", StringComparison.Ordinal)).Build();

        Assert.IsType<DeleteFile>(On(Plan(state), CursorRulePath));
    }

    [Fact]
    public void ACreatedReplitMdSavedWithAByteOrderMarkIsDeletedWhenNothingButTheSectionRemains()
    {
        var state = ProjectState.NewBuilder().PointerFile(ReplitMd, "﻿" + MarkedBlock.CreatedNote + "\n" + Section).Build();

        Assert.IsType<DeleteFile>(On(Plan(state), ReplitMd));
    }

    [Fact]
    public void ASectionOnLineOneAfterAByteOrderMarkIsRemovedWithoutTakingTheMark()
    {
        var state = ProjectState.NewBuilder().PointerFile(ReplitMd, "﻿" + Section + "\nnotes\n").Build();

        var plan = Plan(state);

        Assert.StartsWith("﻿", After(plan, ReplitMd), StringComparison.Ordinal);
        Assert.Contains("notes", After(plan, ReplitMd), StringComparison.Ordinal);
    }
}
