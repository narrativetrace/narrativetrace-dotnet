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

    private void SeedFixtureFiles()
    {
        var fixtureDir = Path.Combine(_root, "examples", "NarrativeTrace.Examples.SixtySeconds");
        Directory.CreateDirectory(fixtureDir);
        File.WriteAllText(Path.Combine(fixtureDir, "Program.cs"), "// program\n");

        var withLoggerDir = Path.Combine(_root, "examples", "NarrativeTrace.Examples.SixtySeconds.WithLogger");
        Directory.CreateDirectory(withLoggerDir);
        File.WriteAllText(Path.Combine(withLoggerDir, "Program.cs"), "// with logger\n");
    }
}
