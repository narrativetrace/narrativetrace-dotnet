// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using Xunit;

namespace NarrativeTrace.Cli.Tests;

public sealed class SkillsLintCommandTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "nt-skills-lint-" + Guid.NewGuid().ToString("N"));

    private readonly StringWriter _out = new();
    private readonly StringWriter _err = new();

    public SkillsLintCommandTests()
    {
        Directory.CreateDirectory(_root);
        SeedFixtureFiles();
        SeedFeatureGuide();
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void Reports_drift_when_no_pages_have_been_rendered_yet()
    {
        var exit = SkillsLintCommand.Run(_root, _out, _err);

        Assert.Equal(1, exit);
        Assert.Contains("stale", _err.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Passes_once_the_pages_are_freshly_rendered()
    {
        SkillsRenderCommand.Run(_root, _out, _err);

        var exit = SkillsLintCommand.Run(_root, _out, _err);

        Assert.Equal(0, exit);
    }

    [Fact]
    public void Fails_again_once_a_rendered_page_is_hand_edited()
    {
        SkillsRenderCommand.Run(_root, _out, _err);
        var path = Path.Combine(_root, ".claude", "skills", "doctor", "SKILL.md");
        File.AppendAllText(path, "\nhand-edited\n");

        var exit = SkillsLintCommand.Run(_root, _out, _err);

        Assert.Equal(1, exit);
    }

    private void SeedFixtureFiles()
    {
        var fixtureDir = Path.Combine(_root, "examples", "NarrativeTrace.Examples.SixtySeconds");
        Directory.CreateDirectory(fixtureDir);
        File.WriteAllText(Path.Combine(fixtureDir, "Program.cs"), "// program\n");
        File.WriteAllText(Path.Combine(fixtureDir, "WithLogger.cs"), "// with logger\n");
    }

    private void SeedFeatureGuide()
    {
        var docsDir = Path.Combine(_root, "documentation");
        Directory.CreateDirectory(docsDir);
        File.WriteAllText(
            Path.Combine(docsDir, "feature-guide.md"),
            "`EventAggregator` — Pro\nPlanned (Pro, gated)\n");
    }
}
