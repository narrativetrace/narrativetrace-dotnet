// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.SkillsDevTool;
using Xunit;

namespace NarrativeTrace.Cli.Tests;

public sealed class SkillsRenderCommandTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "nt-skills-render-" + Guid.NewGuid().ToString("N"));

    private readonly StringWriter _out = new();
    private readonly StringWriter _err = new();

    public SkillsRenderCommandTests()
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
    public void Renders_a_skill_page_for_every_catalogue_entry()
    {
        SeedFixtureFiles();

        var exit = SkillsRenderCommand.Run(_root, _out, _err);

        Assert.Equal(0, exit);
        Assert.True(File.Exists(Path.Combine(_root, ".claude", "skills", "narrativetrace-doctor", "SKILL.md")));
        Assert.True(File.Exists(Path.Combine(_root, ".claude", "skills", "add-narrative-tracing", "SKILL.md")));
    }

    /// <summary>
    /// The framework table's two documentation blocks render with the skill pages, from the same
    /// command — one render step, never a second one somebody forgets.
    /// </summary>
    [Fact]
    public void Renders_the_framework_table_blocks_into_llms_full_and_llms_txt_where_their_markers_are()
    {
        SeedFixtureFiles();
        var guides = Path.Combine(_root, "documentation", "guides");
        Directory.CreateDirectory(guides);
        File.WriteAllText(Path.Combine(guides, "llms-full.md"), "# Ref\n<!-- framework-table:begin -->\nstale\n<!-- framework-table:end -->\n");
        File.WriteAllText(Path.Combine(guides, "llms.txt"), "# Txt\n<!-- covered-frameworks:begin -->\nstale\n<!-- covered-frameworks:end -->\n");

        SkillsRenderCommand.Run(_root, _out, _err);

        Assert.Contains("| ASP.NET Core |", File.ReadAllText(Path.Combine(guides, "llms-full.md")), StringComparison.Ordinal);
        Assert.Contains("Covered frameworks", File.ReadAllText(Path.Combine(guides, "llms.txt")), StringComparison.Ordinal);
    }

    [Fact]
    public void A_repository_without_the_two_documentation_pages_renders_the_skills_and_nothing_else()
    {
        SeedFixtureFiles();

        Assert.Equal(0, SkillsRenderCommand.Run(_root, _out, _err));
        Assert.False(Directory.Exists(Path.Combine(_root, "documentation", "guides")));
    }

    [Fact]
    public void Renders_the_same_skill_pages_under_codexs_agents_skills_layout()
    {
        SeedFixtureFiles();

        var exit = SkillsRenderCommand.Run(_root, _out, _err);

        Assert.Equal(0, exit);
        Assert.True(File.Exists(Path.Combine(_root, ".agents", "skills", "narrativetrace-doctor", "SKILL.md")));
        Assert.True(File.Exists(Path.Combine(_root, ".agents", "skills", "add-narrative-tracing", "SKILL.md")));
    }

    [Fact]
    public void Splices_the_managed_section_into_agents_md()
    {
        SeedFixtureFiles();
        File.WriteAllText(Path.Combine(_root, "AGENTS.md"), "# AGENTS\n\nExisting content.\n");

        SkillsRenderCommand.Run(_root, _out, _err);

        var content = File.ReadAllText(Path.Combine(_root, "AGENTS.md"));
        Assert.Contains("Existing content.", content, StringComparison.Ordinal);
        Assert.Contains("narrativetrace-doctor", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Does_not_rewrite_a_byte_identical_file()
    {
        SeedFixtureFiles();
        SkillsRenderCommand.Run(_root, _out, _err);
        var path = Path.Combine(_root, ".claude", "skills", "narrativetrace-doctor", "SKILL.md");
        var before = File.GetLastWriteTimeUtc(path);

        SkillsRenderCommand.Run(_root, _out, _err);

        Assert.Equal(before, File.GetLastWriteTimeUtc(path));
    }

    [Fact]
    public void Writes_the_carrier_tree_the_published_package_and_the_cli_both_read()
    {
        SeedFixtureFiles();

        var exit = SkillsRenderCommand.Run(_root, _out, _err);

        Assert.Equal(0, exit);
        var carrier = Path.Combine(_root, "src", "NarrativeTrace.Skills", "skills");
        Assert.True(File.Exists(Path.Combine(carrier, "agents", "narrativetrace-doctor", "SKILL.md")));
        Assert.True(File.Exists(Path.Combine(carrier, "claude", "narrativetrace-doctor", "SKILL.md")));
        Assert.True(File.Exists(Path.Combine(carrier, "agents", "add-narrative-tracing", "SKILL.md")));
        Assert.True(File.Exists(Path.Combine(carrier, "claude", "add-narrative-tracing", "SKILL.md")));
        Assert.True(File.Exists(Path.Combine(carrier, "catalogue.json")));
    }

    [Fact]
    public void Carrier_pages_match_the_repo_layout_pages_byte_for_byte()
    {
        SeedFixtureFiles();

        SkillsRenderCommand.Run(_root, _out, _err);

        var carrier = Path.Combine(_root, "src", "NarrativeTrace.Skills", "skills");
        Assert.Equal(
            File.ReadAllText(Path.Combine(_root, ".agents", "skills", "narrativetrace-doctor", "SKILL.md")),
            File.ReadAllText(Path.Combine(carrier, "agents", "narrativetrace-doctor", "SKILL.md")));
        Assert.Equal(
            File.ReadAllText(Path.Combine(_root, ".claude", "skills", "narrativetrace-doctor", "SKILL.md")),
            File.ReadAllText(Path.Combine(carrier, "claude", "narrativetrace-doctor", "SKILL.md")));
    }

    [Fact]
    public void Rendering_twice_does_not_rewrite_a_byte_identical_catalogue_json()
    {
        SeedFixtureFiles();
        SkillsRenderCommand.Run(_root, _out, _err);
        var catalogueJsonPath = Path.Combine(_root, "src", "NarrativeTrace.Skills", "skills", "catalogue.json");
        var before = File.GetLastWriteTimeUtc(catalogueJsonPath);

        SkillsRenderCommand.Run(_root, _out, _err);

        Assert.Equal(before, File.GetLastWriteTimeUtc(catalogueJsonPath));
    }

    [Fact]
    public void Renders_the_claude_plugin_marketplace_file_at_the_repo_root()
    {
        SeedFixtureFiles();

        var exit = SkillsRenderCommand.Run(_root, _out, _err);

        Assert.Equal(0, exit);
        var path = Path.Combine(_root, ".claude-plugin", "marketplace.json");
        Assert.True(File.Exists(path));
        Assert.Contains("\"source\": \"./.claude\"", File.ReadAllText(path), StringComparison.Ordinal);
    }

    [Fact]
    public void Does_not_rewrite_a_byte_identical_marketplace_file()
    {
        SeedFixtureFiles();
        SkillsRenderCommand.Run(_root, _out, _err);
        var path = Path.Combine(_root, ".claude-plugin", "marketplace.json");
        var before = File.GetLastWriteTimeUtc(path);

        SkillsRenderCommand.Run(_root, _out, _err);

        Assert.Equal(before, File.GetLastWriteTimeUtc(path));
    }

    [Fact]
    public void Renders_the_openai_plugin_manifest_into_the_catalogue_package_without_a_version()
    {
        SeedFixtureFiles();

        var exit = SkillsRenderCommand.Run(_root, _out, _err);

        Assert.Equal(0, exit);
        var path = Path.Combine(_root, "src", "NarrativeTrace.Skills.Catalogue", "openai-plugin", "plugin.json");
        var json = File.ReadAllText(path);
        Assert.Contains("\"name\": \"narrativetrace-dotnet\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"version\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Does_not_rewrite_a_byte_identical_openai_plugin_manifest()
    {
        SeedFixtureFiles();
        SkillsRenderCommand.Run(_root, _out, _err);
        var path = Path.Combine(_root, "src", "NarrativeTrace.Skills.Catalogue", "openai-plugin", "plugin.json");
        var before = File.GetLastWriteTimeUtc(path);

        SkillsRenderCommand.Run(_root, _out, _err);

        Assert.Equal(before, File.GetLastWriteTimeUtc(path));
    }

    [Fact]
    public void Renders_the_cursor_plugin_manifests_and_the_one_rule_at_the_repo_root()
    {
        SeedFixtureFiles();

        var exit = SkillsRenderCommand.Run(_root, _out, _err);

        Assert.Equal(0, exit);
        Assert.Contains("\"skills\": \"./.agents/skills/\"", File.ReadAllText(Path.Combine(_root, ".cursor-plugin", "plugin.json")), StringComparison.Ordinal);
        Assert.Contains("\"source\": \".\"", File.ReadAllText(Path.Combine(_root, ".cursor-plugin", "marketplace.json")), StringComparison.Ordinal);
        var rule = File.ReadAllText(Path.Combine(_root, ".cursor", "rules", "narrativetrace.mdc"));
        Assert.StartsWith("---\n", rule, StringComparison.Ordinal);
        Assert.Contains("<!-- narrativetrace:start -->", rule, StringComparison.Ordinal);
    }

    [Fact]
    public void Does_not_rewrite_a_byte_identical_cursor_rule()
    {
        SeedFixtureFiles();
        SkillsRenderCommand.Run(_root, _out, _err);
        var path = Path.Combine(_root, ".cursor", "rules", "narrativetrace.mdc");
        var before = File.GetLastWriteTimeUtc(path);

        SkillsRenderCommand.Run(_root, _out, _err);

        Assert.Equal(before, File.GetLastWriteTimeUtc(path));
    }

    [Fact]
    public void Renders_the_platform_table_into_the_use_with_your_agent_page_where_its_markers_are()
    {
        SeedFixtureFiles();
        var page = Path.Combine(_root, "documentation", "use-with-your-agent.md");
        Directory.CreateDirectory(Path.GetDirectoryName(page)!);
        File.WriteAllText(page, "# Page\n<!-- platform-table:begin -->\nstale\n<!-- platform-table:end -->\n");

        SkillsRenderCommand.Run(_root, _out, _err);

        Assert.Contains("| Cursor |", File.ReadAllText(page), StringComparison.Ordinal);
        Assert.DoesNotContain("stale", File.ReadAllText(page), StringComparison.Ordinal);
    }

    private void SeedFixtureFiles() => SkillListingSeed.Seed(_root);
}
