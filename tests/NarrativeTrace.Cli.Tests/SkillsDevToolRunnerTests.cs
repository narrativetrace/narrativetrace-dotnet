// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.SkillsDevTool;
using Xunit;

namespace NarrativeTrace.Cli.Tests;

/// <summary>
/// Dispatch tests for the standalone skills dev tool's thin argument parser (Phase 3 Q3). The
/// render/lint/replay behaviour itself is covered by <see cref="SkillsRenderCommandTests"/>,
/// <see cref="SkillsLintCommandTests"/> and <see cref="SkillsReplayCommandTests"/>; these only
/// pin that each verb reaches the right command and that an unknown/absent verb is a usage error.
/// </summary>
public sealed class SkillsDevToolRunnerTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "nt-skills-devtool-" + Guid.NewGuid().ToString("N"));

    private readonly StringWriter _out = new();
    private readonly StringWriter _err = new();

    public SkillsDevToolRunnerTests()
    {
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void No_verb_reports_usage()
    {
        var exit = SkillsDevToolRunner.Run([], _out, _err);

        Assert.Equal(2, exit);
        Assert.Contains("render", _err.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void An_unknown_verb_reports_usage()
    {
        var exit = SkillsDevToolRunner.Run(["frobnicate"], _out, _err);

        Assert.Equal(2, exit);
        Assert.Contains("render", _err.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Render_verb_dispatches_to_the_render_command()
    {
        SeedFixtureFiles();

        var exit = SkillsDevToolRunner.Run(["render", "--dir", _root], _out, _err);

        Assert.Equal(0, exit);
        Assert.True(File.Exists(Path.Combine(_root, ".claude", "skills", "narrativetrace-doctor", "SKILL.md")));
    }

    [Fact]
    public void Lint_verb_dispatches_to_the_lint_command()
    {
        var exit = SkillsDevToolRunner.Run(["lint", "--dir", _root], _out, _err);

        Assert.Equal(1, exit);
    }

    [Fact]
    public void Replay_verb_dispatches_to_the_replay_command()
    {
        var exit = SkillsDevToolRunner.Run(["replay", "--dir", _root], _out, _err);

        Assert.Equal(1, exit);
        Assert.Contains("problem", _out.ToString(), StringComparison.Ordinal);
    }

    private void SeedFixtureFiles() => SkillListingSeed.Seed(_root);
}
