// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.IO.Compression;
using NarrativeTrace.Skills.Catalogue;
using NarrativeTrace.Skills.Render;
using NarrativeTrace.SkillsDevTool;
using Xunit;

namespace NarrativeTrace.Cli.Tests;

public sealed class SkillsPackOpenAiCommandTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "nt-pack-openai-" + Guid.NewGuid().ToString("N"));
    private readonly StringWriter _out = new();
    private readonly StringWriter _err = new();

    public SkillsPackOpenAiCommandTests()
    {
        Directory.CreateDirectory(_root);
        SeedRepository("0.4.0");
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private string OutDir => Path.Combine(_root, "out");

    private string ZipPath(string version = "0.4.0") =>
        Path.Combine(OutDir, $"narrativetrace-dotnet-openai-plugin-{version}.zip");

    private static string Props(string version) =>
        $"<Project>\n  <PropertyGroup>\n    <VersionPrefix>{version}</VersionPrefix>\n  </PropertyGroup>\n</Project>\n";

    private void SeedRepository(string version)
    {
        File.WriteAllText(Path.Combine(_root, "Directory.Build.props"), Props(version));
        SkillListingSeed.Seed(_root);
        SkillsRenderCommand.Run(_root, new StringWriter(), new StringWriter());
        var assets = Path.Combine(_root, "src", "NarrativeTrace.Skills.Catalogue", "openai-plugin", "assets");
        Directory.CreateDirectory(assets);
        var real = Path.Combine(RepoRoot.Find(), "src", "NarrativeTrace.Skills.Catalogue", "openai-plugin", "assets");
        foreach (var name in new[] { "logo.png", "composer-icon.png" })
        {
            File.Copy(Path.Combine(real, name), Path.Combine(assets, name), overwrite: true);
        }
    }

    private static List<string> EntryNames(string zip)
    {
        using var archive = ZipFile.OpenRead(zip);
        return [.. archive.Entries.Select(e => e.FullName)];
    }

    [Fact]
    public void Packs_a_zip_whose_entries_equal_the_layout_derived_from_the_catalogue()
    {
        var exit = SkillsPackOpenAiCommand.Run(_root, OutDir, _out, _err);

        Assert.Equal(0, exit);
        Assert.Equal(
            OpenAiPackageLayout.Entries(SkillCatalogue.Skills, SkillCatalogue.OpenAiDirectory),
            EntryNames(ZipPath()));
        Assert.Contains("assets/logo.png", _out.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void The_packaged_manifest_carries_the_build_version_and_each_page_is_the_committed_agents_page()
    {
        SkillsPackOpenAiCommand.Run(_root, OutDir, _out, _err);

        using var archive = ZipFile.OpenRead(ZipPath());
        using var manifest = new StreamReader(archive.GetEntry("plugin.json")!.Open());
        Assert.Contains("\"version\": \"0.4.0\"", manifest.ReadToEnd(), StringComparison.Ordinal);
        foreach (var skill in SkillCatalogue.Skills)
        {
            using var page = new StreamReader(archive.GetEntry($"skills/{skill.CanonicalName}/SKILL.md")!.Open());
            var onDisk = File.ReadAllText(Path.Combine(_root, ".agents", "skills", skill.CanonicalName, "SKILL.md"));
            Assert.Equal(onDisk, page.ReadToEnd());
        }
    }

    [Fact]
    public void Packing_twice_yields_byte_identical_zips_with_fixed_entry_times()
    {
        SkillsPackOpenAiCommand.Run(_root, OutDir, _out, _err);
        var first = File.ReadAllBytes(ZipPath());
        File.SetLastWriteTimeUtc(Path.Combine(_root, ".agents", "skills", "narrativetrace-doctor", "SKILL.md"), DateTime.UtcNow.AddDays(1));

        SkillsPackOpenAiCommand.Run(_root, OutDir, _out, _err);

        Assert.Equal(first, File.ReadAllBytes(ZipPath()));
        using var archive = ZipFile.OpenRead(ZipPath());
        Assert.All(archive.Entries, e => Assert.Equal(new DateTime(1980, 1, 1, 0, 0, 0), e.LastWriteTime.DateTime));
    }

    [Theory]
    [InlineData("0.4.0-preview.1")]
    [InlineData("0.4")]
    [InlineData("")]
    public void Refuses_a_version_that_is_not_plain_semver_and_writes_no_zip(string version)
    {
        File.WriteAllText(Path.Combine(_root, "Directory.Build.props"), Props(version));

        var exit = SkillsPackOpenAiCommand.Run(_root, OutDir, _out, _err);

        Assert.Equal(1, exit);
        Assert.Contains("semantic", _err.ToString(), StringComparison.Ordinal);
        Assert.False(Directory.Exists(OutDir) && Directory.EnumerateFiles(OutDir).Any());
    }

    [Fact]
    public void Refuses_a_build_without_a_version_prefix_and_a_repository_without_the_props_file()
    {
        File.WriteAllText(Path.Combine(_root, "Directory.Build.props"), "<Project />\n");
        Assert.Equal(1, SkillsPackOpenAiCommand.Run(_root, OutDir, _out, _err));
        Assert.Contains("VersionPrefix", _err.ToString(), StringComparison.Ordinal);

        File.Delete(Path.Combine(_root, "Directory.Build.props"));
        Assert.Equal(1, SkillsPackOpenAiCommand.Run(_root, OutDir, _out, new StringWriter()));
    }

    [Fact]
    public void Fails_naming_the_file_when_a_rendered_page_or_an_asset_is_missing()
    {
        File.Delete(Path.Combine(_root, ".agents", "skills", "narrativetrace-doctor", "SKILL.md"));
        Assert.Equal(1, SkillsPackOpenAiCommand.Run(_root, OutDir, _out, _err));
        Assert.Contains("narrativetrace-doctor", _err.ToString(), StringComparison.Ordinal);

        SkillsRenderCommand.Run(_root, new StringWriter(), new StringWriter());
        File.Delete(Path.Combine(_root, "src", "NarrativeTrace.Skills.Catalogue", "openai-plugin", "assets", "logo.png"));
        var err = new StringWriter();
        Assert.Equal(1, SkillsPackOpenAiCommand.Run(_root, OutDir, _out, err));
        Assert.Contains("logo.png", err.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Fails_the_check_when_an_asset_is_not_a_square_png_and_deletes_the_failed_zip()
    {
        var logo = Path.Combine(_root, "src", "NarrativeTrace.Skills.Catalogue", "openai-plugin", "assets", "logo.png");
        File.WriteAllBytes(logo, "not a png at all, only text bytes."u8.ToArray());

        var exit = SkillsPackOpenAiCommand.Run(_root, OutDir, _out, _err);

        Assert.Equal(1, exit);
        Assert.Contains("not a readable PNG", _err.ToString(), StringComparison.Ordinal);
        Assert.False(File.Exists(ZipPath()));
    }

    [Fact]
    public void Reads_the_version_prefix_the_build_uses_and_not_one_inside_an_xml_comment()
    {
        File.WriteAllText(
            Path.Combine(_root, "Directory.Build.props"),
            "<Project>\n  <!-- <VersionPrefix>9.9.9</VersionPrefix> -->\n  <PropertyGroup>\n    <VersionPrefix>0.4.0</VersionPrefix>\n  </PropertyGroup>\n</Project>\n");

        var exit = SkillsPackOpenAiCommand.Run(_root, OutDir, _out, _err);

        Assert.Equal(0, exit);
        Assert.True(File.Exists(ZipPath()));
    }

    [Fact]
    public void An_out_path_that_is_a_file_fails_cleanly_instead_of_throwing()
    {
        var blocked = Path.Combine(_root, "blocked");
        File.WriteAllText(blocked, "a file where the output directory should be");

        Assert.Equal(1, SkillsPackOpenAiCommand.Run(_root, blocked, _out, _err));
        Assert.NotEmpty(_err.ToString());
    }

    [Fact]
    public void The_pack_openai_verb_dispatches_with_dir_and_out_options()
    {
        var exit = SkillsDevToolRunner.Run(["pack-openai", "--dir", _root, "--out", OutDir], _out, _err);

        Assert.Equal(0, exit);
        Assert.True(File.Exists(ZipPath()));
    }

    [Fact]
    public void The_usage_text_lists_the_pack_openai_verb()
    {
        SkillsDevToolRunner.Run([], _out, _err);

        Assert.Contains("pack-openai", _err.ToString(), StringComparison.Ordinal);
    }
}
