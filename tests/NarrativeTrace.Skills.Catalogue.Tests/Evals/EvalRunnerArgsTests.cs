// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.SkillsEvals;
using Xunit;

namespace NarrativeTrace.SkillsEvals.Tests;

public sealed class EvalRunnerArgsTests
{
    private static readonly string[] Minimal =
        ["--skill", "add-narrative-tracing", "--case", "init-prompt-empty-project",
         "--platform", "claude", "--model", "haiku"];

    [Fact]
    public void Parses_the_required_flags_and_defaults_trials_to_one()
    {
        var args = EvalRunnerArgs.Parse(Minimal);

        Assert.Equal("add-narrative-tracing", args.Skill);
        Assert.Equal("init-prompt-empty-project", args.CaseName);
        Assert.Equal(Platform.Claude, args.Platform);
        Assert.Equal("haiku", args.Model);
        Assert.Null(args.AgentCommand);
        Assert.Equal(1, args.Trials);
    }

    [Fact]
    public void Reads_the_trials_count_and_the_agent_command_override()
    {
        var args = EvalRunnerArgs.Parse(
            [.. Minimal, "--trials", "3", "--agent-command", "claude -p \"{prompt}\""]);

        Assert.Equal(3, args.Trials);
        Assert.Equal("claude -p \"{prompt}\"", args.AgentCommand);
    }

    [Theory]
    [InlineData("--skill")]
    [InlineData("--case")]
    [InlineData("--platform")]
    [InlineData("--model")]
    public void A_missing_required_flag_is_a_usage_error(string drop)
    {
        var kept = new List<string>();
        for (var i = 0; i < Minimal.Length; i += 2)
        {
            if (Minimal[i] != drop)
            {
                kept.Add(Minimal[i]);
                kept.Add(Minimal[i + 1]);
            }
        }

        Assert.Throws<ArgumentException>(() => EvalRunnerArgs.Parse([.. kept]));
    }

    [Fact]
    public void An_unknown_platform_is_a_usage_error()
    {
        Assert.Throws<ArgumentException>(() => EvalRunnerArgs.Parse(
            ["--skill", "s", "--case", "c", "--platform", "bard", "--model", "m"]));
    }

    [Fact]
    public void Trials_below_one_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => EvalRunnerArgs.Parse([.. Minimal, "--trials", "0"]));
    }
}
