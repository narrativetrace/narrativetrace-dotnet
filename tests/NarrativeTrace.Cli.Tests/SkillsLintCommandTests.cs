// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.SkillsDevTool;
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
        var path = Path.Combine(_root, ".claude", "skills", "narrativetrace-doctor", "SKILL.md");
        File.AppendAllText(path, "\nhand-edited\n");

        var exit = SkillsLintCommand.Run(_root, _out, _err);

        Assert.Equal(1, exit);
    }

    [Fact]
    public void Fails_again_once_a_rendered_codex_page_is_hand_edited()
    {
        SkillsRenderCommand.Run(_root, _out, _err);
        var path = Path.Combine(_root, ".agents", "skills", "narrativetrace-doctor", "SKILL.md");
        File.AppendAllText(path, "\nhand-edited\n");

        var exit = SkillsLintCommand.Run(_root, _out, _err);

        Assert.Equal(1, exit);
    }

    [Fact]
    public void Fails_when_a_carrier_claude_page_is_hand_edited()
    {
        SkillsRenderCommand.Run(_root, _out, _err);
        var path = Path.Combine(_root, "src", "NarrativeTrace.Skills", "skills", "claude", "narrativetrace-doctor", "SKILL.md");
        File.AppendAllText(path, "\nhand-edited\n");

        var exit = SkillsLintCommand.Run(_root, _out, _err);

        Assert.Equal(1, exit);
    }

    [Fact]
    public void Fails_when_a_carrier_agents_page_is_hand_edited()
    {
        SkillsRenderCommand.Run(_root, _out, _err);
        var path = Path.Combine(_root, "src", "NarrativeTrace.Skills", "skills", "agents", "narrativetrace-doctor", "SKILL.md");
        File.AppendAllText(path, "\nhand-edited\n");

        var exit = SkillsLintCommand.Run(_root, _out, _err);

        Assert.Equal(1, exit);
    }

    [Fact]
    public void Fails_when_the_carrier_catalogue_json_is_hand_edited()
    {
        SkillsRenderCommand.Run(_root, _out, _err);
        var path = Path.Combine(_root, "src", "NarrativeTrace.Skills", "skills", "catalogue.json");
        File.WriteAllText(path, "{}");

        var exit = SkillsLintCommand.Run(_root, _out, _err);

        Assert.Equal(1, exit);
    }

    [Fact]
    public void Fails_when_the_marketplace_file_is_hand_edited()
    {
        SkillsRenderCommand.Run(_root, _out, _err);
        var path = Path.Combine(_root, ".claude-plugin", "marketplace.json");
        File.WriteAllText(path, "{}");

        var exit = SkillsLintCommand.Run(_root, _out, _err);

        Assert.Equal(1, exit);
        Assert.Contains("marketplace.json", _err.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(".cursor-plugin/plugin.json")]
    [InlineData(".cursor-plugin/marketplace.json")]
    [InlineData(".cursor/rules/narrativetrace.mdc")]
    public void Fails_when_a_cursor_file_is_hand_edited_and_names_it(string relative)
    {
        SkillsRenderCommand.Run(_root, _out, _err);
        File.WriteAllText(Path.Combine(_root, relative.Replace('/', Path.DirectorySeparatorChar)), "{}");

        var exit = SkillsLintCommand.Run(_root, _out, _err);

        Assert.Equal(1, exit);
        Assert.Contains(relative + " is stale", _err.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(".cursor-plugin/plugin.json")]
    [InlineData(".cursor/rules/narrativetrace.mdc")]
    public void Fails_when_a_cursor_file_is_missing(string relative)
    {
        SkillsRenderCommand.Run(_root, _out, _err);
        File.Delete(Path.Combine(_root, relative.Replace('/', Path.DirectorySeparatorChar)));

        Assert.Equal(1, SkillsLintCommand.Run(_root, _out, _err));
    }

    [Fact]
    public void Fails_when_a_stray_file_sits_in_the_carrier()
    {
        SkillsRenderCommand.Run(_root, _out, _err);
        var strayPath = Path.Combine(_root, "src", "NarrativeTrace.Skills", "skills", "stray.txt");
        File.WriteAllText(strayPath, "not part of the carrier\n");

        var exit = SkillsLintCommand.Run(_root, _out, _err);

        Assert.Equal(1, exit);
        Assert.Contains("stray", _err.ToString(), StringComparison.Ordinal);
    }

    private void SeedFixtureFiles() => SkillListingSeed.Seed(_root);

    private void SeedFeatureGuide()
    {
        var docsDir = Path.Combine(_root, "documentation");
        Directory.CreateDirectory(docsDir);
        File.WriteAllText(
            Path.Combine(docsDir, "feature-guide.md"),
            "`EventAggregator` — Pro\nPlanned (Pro, gated)\n");
    }
}
