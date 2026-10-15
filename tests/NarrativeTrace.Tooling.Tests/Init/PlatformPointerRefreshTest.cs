// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Tooling.Init;
using NarrativeTrace.Tooling.Platforms;
using Xunit;
using Action = NarrativeTrace.Tooling.Init.Action;

namespace NarrativeTrace.Tooling.Tests.Init;

/// <summary>
/// A build-time refresh keeps what an install left behind current and starts nothing — and a platform
/// pointer is no exception: a stale stamped rule is rewritten, an absent one is never created.
/// </summary>
public sealed class PlatformPointerRefreshTest : IDisposable
{
    private const string RulePath = ".cursor/rules/narrativetrace.mdc";
    private const string Older = "NarrativeTrace.Skills/0.0.1";

    private readonly string _root = Path.Combine(Path.GetTempPath(), "nt-pointer-refresh-" + Guid.NewGuid().ToString("N"));
    private readonly Carrier _carrier;

    public PlatformPointerRefreshTest()
    {
        Directory.CreateDirectory(_root);
        _carrier = Carriers.Open(Path.Combine(_root, "carrier"), "1.2.3");
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private static string RuleFor(string coordinate) =>
        CursorRule.Render(((Pointer.Rule)PlatformTable.Row("cursor")!.Pointer).Text, coordinate);

    private static ProjectState.Builder StaleInstall() => ProjectState.NewBuilder()
        .AddInstalledSkill(new InstalledSkill(SkillFlavour.Agents, Carriers.Doctor, SkillPresence.Ours, Older, "old page"));

    private static Action? On(InitPlan plan, string path) => plan.Actions.FirstOrDefault(action => action.Path == path);

    [Fact]
    public void A_stale_install_refreshes_a_stale_cursor_rule_with_it()
    {
        var state = StaleInstall().DetectedPlatform("cursor").PointerFile(RulePath, RuleFor(Older)).Build();

        var plan = RefreshPlanner.Plan(state, _carrier);

        var rewrite = Assert.IsType<ReplaceBlock>(On(plan, RulePath));
        Assert.Equal(RuleFor(_carrier.Coordinate), rewrite.After);
    }

    [Fact]
    public void A_refresh_never_creates_a_pointer_a_project_never_had()
    {
        var state = StaleInstall().DetectedPlatform("cursor").DetectedPlatform("replit").Build();

        var plan = RefreshPlanner.Plan(state, _carrier);

        Assert.Null(On(plan, RulePath));
        Assert.Null(On(plan, "replit.md"));
    }

    [Fact]
    public void A_refresh_never_adopts_or_refuses_a_rule_that_is_not_ours()
    {
        var state = StaleInstall().DetectedPlatform("cursor").PointerFile(RulePath, RuleFor("")).Build();

        Assert.Null(On(RefreshPlanner.Plan(state, _carrier), RulePath));
    }
}
