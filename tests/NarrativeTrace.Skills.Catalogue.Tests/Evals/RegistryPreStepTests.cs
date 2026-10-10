// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Skills.Catalogue;
using NarrativeTrace.SkillsEvals;
using Xunit;

namespace NarrativeTrace.SkillsEvals.Tests;

/// <summary>
/// The closed vocabulary of registry deliveries a Tier B case may declare: each constant owns the
/// exact argv a documented registry line amounts to, so the case replays what a reader runs. A
/// case file is data, and data that may name any executable is a shell this harness does not have.
/// </summary>
public sealed class RegistryPreStepTests
{
    private const string RepoRoot = "/repo";
    private const string Staged = "/work/staged";

    [Theory]
    [InlineData("claude-marketplace", RegistryPreStep.ClaudeMarketplace)]
    [InlineData("npx-skills", RegistryPreStep.NpxSkills)]
    public void A_known_id_parses_to_its_delivery(string id, RegistryPreStep expected)
    {
        Assert.Equal(expected, RegistryPreSteps.Parse(id));
        Assert.Equal(id, expected.Id());
    }

    [Theory]
    [InlineData("gemini-skills")]
    [InlineData("")]
    [InlineData("npx_skills")]
    public void An_unknown_id_parses_to_null(string id)
    {
        Assert.Null(RegistryPreSteps.Parse(id));
    }

    [Fact]
    public void Every_delivery_stages_the_snapshot_before_it_runs_a_registry_command()
    {
        foreach (var step in new[] { RegistryPreStep.ClaudeMarketplace, RegistryPreStep.NpxSkills })
        {
            var commands = step.Commands(RepoRoot, Staged);
            Assert.Equal(StagedSnapshot.StagingCommands(RepoRoot, Staged)[0], commands[0]);
            Assert.Equal(StagedSnapshot.StagingCommands(RepoRoot, Staged)[1], commands[1]);
        }
    }

    [Fact]
    public void The_marketplace_delivery_adds_the_staged_tree_then_installs_this_repos_plugin()
    {
        var plugin = SkillCatalogue.Marketplace.Name;
        var id = plugin + "@" + plugin;
        var commands = RegistryPreStep.ClaudeMarketplace.Commands(RepoRoot, Staged);

        Assert.Equal(new[] { "claude", "plugin", "marketplace", "add", Staged }, commands[2]);
        Assert.Equal(new[] { "claude", "plugin", "install", id }, commands[3]);
        Assert.Equal(new[] { "claude", "plugin", "details", id }, commands[4]);
    }

    [Fact]
    public void The_npx_delivery_runs_the_one_documented_line_against_the_staged_tree()
    {
        var commands = RegistryPreStep.NpxSkills.Commands(RepoRoot, Staged);

        Assert.Equal(new[] { "npx", "--yes", "skills", "add", Staged, "-y" }, commands[2]);
        Assert.Equal(3, commands.Count);
    }
}
