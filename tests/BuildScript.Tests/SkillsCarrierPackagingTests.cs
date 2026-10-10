// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Diagnostics;
using System.IO.Compression;
using System.Text.RegularExpressions;
using Xunit;

namespace Build.Tests;

/// <summary>
/// Proves the carrier is inside the REAL built <c>.nupkg</c>s, not just in the source tree or an
/// in-memory reflection check (Phase 3 milestone 1's packaging test, Java's
/// <c>SkillsCarrierJarTest</c> shape). Opens the archives <c>dotnet pack</c> actually produces
/// rather than re-reading <c>src/NarrativeTrace.Skills/skills/**</c> back — a fixture nupkg
/// assembled by the test would only prove the test can build a nupkg.
/// </summary>
/// <remarks>
/// Traited <c>SpawnsBuild</c> (like <see cref="NukeBuildTests"/>) and run via
/// <c>./build.sh BuildScriptTests</c>, never <c>Test</c>/<c>Coverage</c> — <c>dotnet pack</c>
/// rebuilds the CLI's whole dependency graph, too slow for a per-test constructor; the packing
/// happens ONCE per test-class run via <see cref="SkillsCarrierPackagingFixture"/>.
/// </remarks>
[Trait("Category", "SpawnsBuild")]
public sealed class SkillsCarrierPackagingTests : IClassFixture<SkillsCarrierPackagingFixture>
{
    /// <summary>
    /// The rendered skill pages this checkout ships, read from the two layouts at the repository
    /// root — DERIVED, never listed: a hard-coded list of two skills went stale the day a third
    /// shipped, and stayed green only because nobody ran this suite (Java's cross-port item 9).
    /// </summary>
    private static string[] ExpectedSkillNames() =>
        [.. Directory.EnumerateDirectories(Path.Combine(RepositoryPath.Root(), ".agents", "skills"))
            .Where(dir => File.Exists(Path.Combine(dir, "SKILL.md")))
            .Select(Path.GetFileName)
            .OfType<string>()];

    private static string[] CarrierEntries() =>
        [
            .. ExpectedSkillNames().SelectMany(name => new[] { $"skills/agents/{name}/SKILL.md", $"skills/claude/{name}/SKILL.md" }),
            "skills/catalogue.json",
        ];

    private readonly SkillsCarrierPackagingFixture _fixture;

    public SkillsCarrierPackagingTests(SkillsCarrierPackagingFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void The_skills_package_carries_exactly_the_carrier_entries_and_nothing_else()
    {
        var carrierEntries = _fixture.SkillsEntries.Where(e => e.StartsWith("skills/", StringComparison.Ordinal));

        Assert.Equal(
            CarrierEntries().OrderBy(e => e, StringComparer.Ordinal),
            carrierEntries.OrderBy(e => e, StringComparer.Ordinal));
    }

    [Fact]
    public void The_skills_package_carries_no_compiled_assembly()
    {
        Assert.DoesNotContain(_fixture.SkillsEntries, e => e.EndsWith(".dll", StringComparison.Ordinal));
    }

    [Fact]
    public void The_skills_package_licence_expression_is_apache()
    {
        var nuspec = _fixture.ReadSkillsEntry("NarrativeTrace.Skills.nuspec");

        Assert.Contains("<license type=\"expression\">Apache-2.0</license>", nuspec, StringComparison.Ordinal);
    }

    [Fact]
    public void The_catalogue_names_every_skill_exactly_once_and_points_at_entries_that_exist()
    {
        var catalogue = _fixture.ReadSkillsEntry("skills/catalogue.json");
        var names = Regex.Matches(catalogue, "\"name\": \"([^\"]+)\"").Select(m => m.Groups[1].Value);

        Assert.Equal(
            ExpectedSkillNames().OrderBy(n => n, StringComparer.Ordinal),
            names.OrderBy(n => n, StringComparer.Ordinal));
        foreach (Match match in Regex.Matches(catalogue, "\"(?:agents|claude)\": \"([^\"]+)\""))
        {
            Assert.Contains("skills/" + match.Groups[1].Value, _fixture.SkillsEntries);
        }
    }

    [Fact]
    public void The_catalogue_carries_no_version_literal()
    {
        var catalogue = _fixture.ReadSkillsEntry("skills/catalogue.json");

        Assert.DoesNotMatch(@"\d+\.\d+\.\d+", catalogue);
    }

    [Fact]
    public void The_cli_tool_package_carries_the_cli_assembly_but_no_catalogue_assembly()
    {
        Assert.Contains(_fixture.CliEntries, e => e.EndsWith("dotnet-narrativetrace.dll", StringComparison.Ordinal));
        Assert.DoesNotContain(
            _fixture.CliEntries, e => e.Contains("NarrativeTrace.Skills.Catalogue", StringComparison.Ordinal));
    }

    [Fact]
    public void The_cli_and_skills_packages_carry_the_same_carrier_pages_byte_for_byte()
    {
        foreach (var entry in CarrierEntries())
        {
            var fromSkills = _fixture.ReadSkillsEntry(entry);
            var embedded = _fixture.CliEmbeddedCarrierPage(entry);
            Assert.Equal(fromSkills, embedded, StringComparer.Ordinal);
        }
    }
}

/// <summary>Packs <c>NarrativeTrace.Skills</c> and <c>NarrativeTrace.Cli</c> once and opens both nupkgs.</summary>
public sealed class SkillsCarrierPackagingFixture : IDisposable
{
    private static readonly string RepoRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "../../../../../"));

    private readonly string _outputDir = Path.Combine(
        Path.GetTempPath(), "nt-carrier-pack-" + Guid.NewGuid().ToString("N"));

    private readonly string _skillsNupkg;
    private readonly string _cliNupkg;

    public SkillsCarrierPackagingFixture()
    {
        Directory.CreateDirectory(_outputDir);
        Pack("src/NarrativeTrace.Skills/NarrativeTrace.Skills.csproj");
        Pack("src/NarrativeTrace.Cli/NarrativeTrace.Cli.csproj");
        _skillsNupkg = SingleNupkg("NarrativeTrace.Skills.*.nupkg");
        _cliNupkg = SingleNupkg("NarrativeTrace.Cli.*.nupkg");
        SkillsEntries = EntryNames(_skillsNupkg);
        CliEntries = EntryNames(_cliNupkg);
    }

    public IReadOnlyList<string> SkillsEntries { get; }

    public IReadOnlyList<string> CliEntries { get; }

    public string ReadSkillsEntry(string entryName) => ReadEntry(_skillsNupkg, entryName);

    /// <summary>
    /// Reads a carrier page back out of the CLI's own tool assembly, extracted straight from the
    /// packed nupkg — proves the bytes the PACKAGE ships, not the loose build-output dll.
    /// </summary>
    public string CliEmbeddedCarrierPage(string carrierRelativePath)
    {
        var logicalPath = carrierRelativePath["skills/".Length..];
        using var zip = ZipFile.OpenRead(_cliNupkg);
        var entry = zip.Entries.Single(e => e.FullName.EndsWith("dotnet-narrativetrace.dll", StringComparison.Ordinal));
        using var dllStream = entry.Open();
        using var memory = new MemoryStream();
        dllStream.CopyTo(memory);
        return ReadEmbeddedCarrierPage(memory.ToArray(), "NarrativeTrace.Skills/" + logicalPath);
    }

    public void Dispose()
    {
        if (Directory.Exists(_outputDir))
        {
            Directory.Delete(_outputDir, recursive: true);
        }
    }

    private static string ReadEmbeddedCarrierPage(byte[] assemblyBytes, string logicalName)
    {
        var context = new System.Runtime.Loader.AssemblyLoadContext("carrier-probe", isCollectible: true);
        try
        {
            using var stream = new MemoryStream(assemblyBytes);
            var assembly = context.LoadFromStream(stream);
            using var resource = assembly.GetManifestResourceStream(logicalName)
                ?? throw new InvalidOperationException($"{logicalName} not found in the packed CLI assembly");
            using var reader = new StreamReader(resource);
            return reader.ReadToEnd();
        }
        finally
        {
            context.Unload();
        }
    }

    private void Pack(string relativeProject)
    {
        var project = Path.Combine(RepoRoot, relativeProject.Replace('/', Path.DirectorySeparatorChar));
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"pack \"{project}\" -c Release -o \"{_outputDir}\" --nologo",
            WorkingDirectory = RepoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        using var process = Process.Start(psi) ?? throw new InvalidOperationException("failed to start dotnet pack");
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        if (!process.WaitForExit((int)TimeSpan.FromMinutes(5).TotalMilliseconds))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"dotnet pack {relativeProject} timed out");
        }

        Assert.True(process.ExitCode == 0, $"dotnet pack {relativeProject} exited {process.ExitCode}:\n{stdout}\n{stderr}");
    }

    private string SingleNupkg(string pattern)
    {
        var matches = Directory.GetFiles(_outputDir, pattern)
            .Where(f => !f.EndsWith(".snupkg", StringComparison.Ordinal))
            .ToList();
        return Assert.Single(matches);
    }

    private static List<string> EntryNames(string nupkgPath)
    {
        using var zip = ZipFile.OpenRead(nupkgPath);
        return zip.Entries.Select(e => e.FullName).Where(n => !n.EndsWith('/')).ToList();
    }

    private static string ReadEntry(string nupkgPath, string entryName)
    {
        using var zip = ZipFile.OpenRead(nupkgPath);
        var entry = zip.GetEntry(entryName) ?? throw new InvalidOperationException($"{entryName} not found in {nupkgPath}");
        using var stream = entry.Open();
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
