// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
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

    private void SeedFixtureFiles()
    {
        var fixtureDir = Path.Combine(_root, "examples", "NarrativeTrace.Examples.SixtySeconds");
        Directory.CreateDirectory(fixtureDir);
        File.WriteAllText(Path.Combine(fixtureDir, "Program.cs"), "// program\n");
        File.WriteAllText(Path.Combine(fixtureDir, "WithLogger.cs"), "// with logger\n");
    }
}
