// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.SkillsEvals;
using Xunit;

namespace NarrativeTrace.SkillsEvals.Tests;

/// <summary>
/// How the case a runner is driving got its skill pages — by a registry, or by the harness itself.
/// One value answers all three questions that follow: whether the harness still copies the rendered
/// pages in, what runs before the agent starts, and which configuration directory every command of
/// the trial uses.
/// </summary>
public sealed class RegistryDeliveryTests
{
    private const string RepoRoot = "/repo";
    private const string Work = "/work";

    [Fact]
    public void The_ordinary_case_delivers_nothing_and_runs_nothing_before_the_agent()
    {
        var delivery = RegistryDelivery.None();

        Assert.False(delivery.DeliversTheSkills());
        Assert.Empty(delivery.Commands(RepoRoot));
        Assert.Empty(delivery.Environment());
    }

    [Fact]
    public void A_registry_case_delivers_its_pages_and_keeps_its_own_staged_tree_and_config()
    {
        var delivery = RegistryDelivery.Through(RegistryPreStep.NpxSkills, Work);

        Assert.True(delivery.DeliversTheSkills());
        Assert.Equal(Path.Combine(Work, "staged"), delivery.StagedSnapshot());
        Assert.Equal(IsolatedAgentConfig.Env(Work), delivery.Environment());
    }

    [Fact]
    public void A_registry_case_runs_its_delivery_commands_against_its_own_staged_tree()
    {
        var delivery = RegistryDelivery.Through(RegistryPreStep.ClaudeMarketplace, Work);

        Assert.Equal(
            RegistryPreStep.ClaudeMarketplace.Commands(RepoRoot, delivery.StagedSnapshot()),
            delivery.Commands(RepoRoot));
    }

    [Fact]
    public void A_delivery_that_delivers_nothing_stages_nothing()
    {
        Assert.Throws<InvalidOperationException>(() => RegistryDelivery.None().StagedSnapshot());
    }
}
