// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.IO.Compression;
using System.Text.RegularExpressions;
using NarrativeTrace.Skills;
using NarrativeTrace.Skills.Catalogue;
using NarrativeTrace.Skills.Render;

namespace NarrativeTrace.SkillsDevTool;

/// <summary>
/// The <c>skills pack-openai</c> verb: builds the OpenAI plugin directory's upload ZIP from the
/// current tree and asserts it. The version is the build's own <c>VersionPrefix</c> from
/// <c>Directory.Build.props</c>, never typed; the manifest is rendered WITH it, the pages are the
/// drift-pinned <c>.agents/skills</c> tree, and the assets are the committed placeholders. The entry
/// list comes from <see cref="OpenAiPackageLayout"/> and the finished ZIP is checked against it by
/// <see cref="OpenAiPackageCheck"/> (release rule 5: derived, then asserted — never a glob).
/// </summary>
/// <remarks>
/// The ZIP is a build output, never committed, and it is built by hand from a <c>v*</c> tag — every
/// upload is its own review. Entries carry a fixed time and mode so the same tree always packs to the
/// same bytes. This verb uploads nothing.
/// </remarks>
public static class SkillsPackOpenAiCommand
{
    private static readonly DateTimeOffset FixedEntryTime = new(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private const int RegularFileMode0644 = 0x81A4;
    private static readonly Regex VersionPrefix = new(
        @"<VersionPrefix>\s*(?<v>[^<]*?)\s*</VersionPrefix>", RegexOptions.None, TimeSpan.FromSeconds(1));
    private static readonly Regex XmlComment = new("<!--.*?-->", RegexOptions.Singleline, TimeSpan.FromSeconds(1));
    private static readonly Regex Semver = new(@"^[0-9]+\.[0-9]+\.[0-9]+\z", RegexOptions.None, TimeSpan.FromSeconds(1));

    /// <summary>Packs the ZIP into <paramref name="outDir"/>; returns 0 when it is built and passes every check, 1 otherwise.</summary>
    /// <param name="repoRoot">The repository root, holding <c>Directory.Build.props</c> and the rendered tree.</param>
    /// <param name="outDir">Where the ZIP is written (created if absent).</param>
    /// <param name="output">Receives the ZIP's path and its asserted entry list.</param>
    /// <param name="error">Receives every refusal and check violation.</param>
    public static int Run(string repoRoot, string outDir, TextWriter output, TextWriter error)
    {
        try
        {
            var version = ReadVersion(repoRoot);
            var zipPath = Path.Combine(outDir, $"{SkillCatalogue.OpenAiDirectory.Base.Name}-openai-plugin-{version}.zip");
            Directory.CreateDirectory(outDir);
            WriteZip(zipPath, Entries(repoRoot, version));
            return Report(zipPath, version, output, error);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException)
        {
            error.WriteLine($"  - {ex.Message}");
            return 1;
        }
    }

    private static string ReadVersion(string repoRoot)
    {
        var props = Path.Combine(repoRoot, "Directory.Build.props");
        var match = File.Exists(props) ? VersionPrefix.Match(XmlComment.Replace(File.ReadAllText(props), string.Empty)) : Match.Empty;
        if (!match.Success)
        {
            throw new InvalidOperationException($"{props} carries no <VersionPrefix> — the package version comes from the build");
        }

        var version = match.Groups["v"].Value;
        return Semver.IsMatch(version)
            ? version
            : throw new InvalidOperationException($"version '{version}' is not semantic (X.Y.Z, no suffix) — the directory takes an explicit semantic version");
    }

    private static SortedDictionary<string, byte[]> Entries(string repoRoot, string version)
    {
        var entries = new SortedDictionary<string, byte[]>(StringComparer.Ordinal)
        {
            [OpenAiPackageLayout.ManifestEntry] = System.Text.Encoding.UTF8.GetBytes(
                OpenAiPluginJsonRenderer.Render(SkillCatalogue.OpenAiDirectory, version)),
        };
        foreach (var skill in SkillCatalogue.Skills)
        {
            var name = skill.CanonicalName;
            entries[$"skills/{name}/SKILL.md"] = File.ReadAllBytes(Path.Combine(repoRoot, ".agents", "skills", name, "SKILL.md"));
        }

        var assets = Path.Combine(OpenAiPluginFile.PackageRootPath(repoRoot), "assets");
        foreach (var asset in new[] { OpenAiPackageLayout.LogoEntry, OpenAiPackageLayout.ComposerIconEntry })
        {
            entries[asset] = File.ReadAllBytes(Path.Combine(assets, Path.GetFileName(asset)));
        }

        return entries;
    }

    private static void WriteZip(string zipPath, SortedDictionary<string, byte[]> entries)
    {
        File.Delete(zipPath);
        using var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create);
        foreach (var (name, bytes) in entries)
        {
            var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
            entry.LastWriteTime = FixedEntryTime;
            entry.ExternalAttributes = RegularFileMode0644 << 16;
            using var stream = entry.Open();
            stream.Write(bytes);
        }
    }

    private static int Report(string zipPath, string version, TextWriter output, TextWriter error)
    {
        var expected = OpenAiPackageLayout.Entries(SkillCatalogue.Skills, SkillCatalogue.OpenAiDirectory);
        var violations = OpenAiPackageCheck.Violations(zipPath, expected, version);
        foreach (var violation in violations)
        {
            error.WriteLine($"  - {violation}");
        }

        if (violations.Count > 0)
        {
            File.Delete(zipPath); // a ZIP that failed its own check must not sit where it could be uploaded
        }

        output.WriteLine(violations.Count == 0
            ? $"skills pack-openai: {zipPath} matches the layout and passes every check{Environment.NewLine}{string.Join(Environment.NewLine, expected.Select(e => "  " + e))}"
            : $"skills pack-openai: {violations.Count} violation(s) in {zipPath}");
        return violations.Count == 0 ? 0 : 1;
    }
}
