// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.SkillsEvals;
using Xunit;

namespace NarrativeTrace.SkillsEvals.Tests;

public sealed class ModelIdsTests
{
    private static string[] ArgvFor(string platform, string model) =>
        ["--skill", "s", "--case", "c", "--platform", platform, "--model", model];

    [Theory]
    [InlineData("haiku", "claude-haiku-5-5")]
    [InlineData("sonnet", "claude-sonnet-5-5")]
    [InlineData("opus", "claude-opus-5-5")]
    public void The_parsed_model_is_the_full_id_for_an_alias(string alias, string id)
    {
        Assert.Equal(id, EvalRunnerArgs.Parse(ArgvFor("claude", alias)).Model);
    }

    [Fact]
    public void A_full_id_passes_through_unchanged()
    {
        Assert.Equal("claude-haiku-5-5", EvalRunnerArgs.Parse(ArgvFor("claude", "claude-haiku-5-5")).Model);
    }

    [Fact]
    public void The_agent_command_and_the_row_carry_the_same_id()
    {
        var args = EvalRunnerArgs.Parse(ArgvFor("claude", "haiku"));

        Assert.Contains("--model claude-haiku-5-5", args.Platform.PresetAgentCommand(args.Model, "s"), StringComparison.Ordinal);
    }

    [Fact]
    public void An_unknown_claude_alias_is_refused_naming_the_accepted_ids()
    {
        var error = Assert.Throws<ArgumentException>(() => EvalRunnerArgs.Parse(ArgvFor("claude", "hiaku")));

        Assert.Contains("claude-haiku-5-5", error.Message, StringComparison.Ordinal);
        Assert.Contains("claude-opus-5-5", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Auto_is_accepted_for_the_cursor_lane()
    {
        Assert.Equal("auto", EvalRunnerArgs.Parse(ArgvFor("cursor", "auto")).Model);
    }

    [Fact]
    public void Another_platforms_own_slug_passes_through()
    {
        Assert.Equal("gemini-3.8-flash-low", EvalRunnerArgs.Parse(ArgvFor("antigravity", "gemini-3.8-flash-low")).Model);
    }

    [Theory]
    [InlineData("haiku")]
    [InlineData("sonnet")]
    public void The_ledger_lint_rejects_a_row_carrying_an_alias(string alias)
    {
        var row = new RunLedgerRow("2026-10-18T00:00:00.0000000Z", "s", "c", Platform.Claude, alias, 1, true);

        Assert.False(ModelIds.IsRecordable(row.Platform, row.Model));
    }

    [Fact]
    public void The_ledger_lint_accepts_a_full_id_row()
    {
        Assert.True(ModelIds.IsRecordable(Platform.Claude, "claude-haiku-5-5"));
    }

    [Fact]
    public void Every_committed_ledger_row_carries_a_full_model_id()
    {
        var ledger = Path.Combine(RepoRoot.Locate(), "skills-evals", "ledger", "runs.jsonl");

        var offenders = RunLedgerRow.ParseJsonl(File.ReadAllText(ledger))
            .Where(r => !ModelIds.IsRecordable(r.Platform, r.Model))
            .Select(r => $"{r.Date} {r.Skill}/{r.CaseName}: {r.Model}")
            .ToList();

        Assert.Empty(offenders);
    }
}
