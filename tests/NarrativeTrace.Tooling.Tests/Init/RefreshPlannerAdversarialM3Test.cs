// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Tooling.Init;
using Xunit;
using Action = NarrativeTrace.Tooling.Init.Action;

namespace NarrativeTrace.Tooling.Tests.Init;

/// <summary>
/// Feature-combination coverage for <see cref="RefreshPlanner"/> beyond <c>RefreshPlannerTest</c>:
/// vendor rule files, both skill flavours going stale together, and a foreign directory under one
/// flavour sitting beside a stale directory of ours under another. Method names mirror the Java
/// <c>RefreshPlannerAdversarialM3Test</c>; coordinates use the port's
/// <c>NarrativeTrace.Skills/&lt;version&gt;</c> form (D6).
/// </summary>
public sealed class RefreshPlannerAdversarialM3Test : IDisposable
{
    private const string Old = "NarrativeTrace.Skills/0.0.1";
    private const string DoctorPage = ".agents/skills/narrativetrace-doctor/SKILL.md";
    private const string VendorDoctorPage = ".claude/skills/narrativetrace-doctor/SKILL.md";

    private readonly string _root = Path.Combine(Path.GetTempPath(), "nt-refreshadv-" + Guid.NewGuid().ToString("N"));
    private readonly Carrier _carrier;

    public RefreshPlannerAdversarialM3Test()
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

    /// <summary>A vendor rule file is never created by a build — only kept current when it is already ours.</summary>
    [Fact]
    public void RewritesAStaleMarkedBlockInAVendorRuleFileWithNoAgentsMdOrClaudeMdPresent()
    {
        const string rulePath = ".cursor/rules/narrativetrace.mdc";
        var stale =
            "# Cursor rules\n\n<!-- narrativetrace:start " + Old + " -->\nstale\n<!-- narrativetrace:end -->\n";
        var state = Project()
            .AddInstalledSkill(Ours(Carriers.Doctor, Old))
            .MarkedRuleFile(rulePath, stale)
            .Build();

        var plan = RefreshPlanner.Plan(state, _carrier);

        Assert.Contains(rulePath, plan.Actions.Select(action => action.Path));
        Assert.Contains(_carrier.Coordinate, After(plan, rulePath), StringComparison.Ordinal);
    }

    [Fact]
    public void RewritesBothFlavoursWhenTheClaudeDirectoryTurnsVendorDetectionOn()
    {
        var state = Project()
            .HasClaudeDirectory(true)
            .AddInstalledSkill(Ours(Carriers.Doctor, Old))
            .AddInstalledSkill(OursClaude(Carriers.Doctor, Old))
            .Build();

        var plan = RefreshPlanner.Plan(state, _carrier);

        var paths = plan.Actions.Select(action => action.Path).ToHashSet(StringComparer.Ordinal);
        Assert.Equal(2, plan.Actions.Count);
        Assert.Contains(DoctorPage, paths);
        Assert.Contains(VendorDoctorPage, paths);
    }

    [Fact]
    public void NeverTouchesAForeignSkillUnderTheVendorFlavourWhileRewritingTheStaleOpenStandardOne()
    {
        var state = Project()
            .HasClaudeDirectory(true)
            .AddInstalledSkill(Ours(Carriers.Doctor, Old))
            .AddInstalledSkill(new InstalledSkill(SkillFlavour.Claude, Carriers.Doctor, SkillPresence.Foreign, "", "# theirs\n"))
            .Build();

        var plan = RefreshPlanner.Plan(state, _carrier);

        Assert.Equal([DoctorPage], plan.Actions.Select(action => action.Path).ToList());
        Assert.False(plan.HasRefusals);
    }

    [Fact]
    public void NeverCarriesARefusalFromAVendorRuleFileWithTwoMarkedSections()
    {
        var twoRegions =
            "<!-- narrativetrace:start -->\na\n<!-- narrativetrace:end -->\n"
            + "<!-- narrativetrace:start -->\nb\n<!-- narrativetrace:end -->\n";
        var state = Project()
            .AddInstalledSkill(Ours(Carriers.Doctor, Old))
            .MarkedRuleFile("RULES.md", twoRegions)
            .Build();

        var plan = RefreshPlanner.Plan(state, _carrier);

        Assert.DoesNotContain(plan.Actions, action => action.Path == "RULES.md");
        Assert.False(plan.HasRefusals);
    }

    private static string After(InitPlan plan, string path) =>
        ((FileEdit)plan.Actions.First(action => action.Path == path)).After;

    private static InstalledSkill Ours(string name, string coordinate) =>
        new(SkillFlavour.Agents, name, SkillPresence.Ours, coordinate,
            Provenance.Stamp(Carriers.PageBody("agents", name, "d"), coordinate));

    private static InstalledSkill OursClaude(string name, string coordinate) =>
        new(SkillFlavour.Claude, name, SkillPresence.Ours, coordinate,
            Provenance.Stamp(Carriers.PageBody("claude", name, "d"), coordinate));
}
