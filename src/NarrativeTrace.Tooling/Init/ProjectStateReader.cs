// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Text;
using System.Text.Json;

namespace NarrativeTrace.Tooling.Init;

/// <summary>
/// Builds a <see cref="ProjectState"/> from a real project directory: read-only, zero network, a
/// bounded listing of each install root.
/// </summary>
/// <remarks>
/// <para>INTENT: the installer's single reader. Every file the planner reasons about is read here,
/// once, so nothing downstream touches the project until the executor writes.</para>
/// <para>Unlike the doctor's snapshot, this reader is NOT best-effort. A file that exists but cannot
/// be read is a hard failure, because "unreadable" and "absent" lead to opposite plans — absent means
/// create, and creating over a file we could not read would destroy it. Files are read as raw bytes
/// and decoded as UTF-8 so a leading byte-order mark and the exact bytes survive round-trip
/// (<see cref="File.ReadAllText(string)"/> would strip the BOM).</para>
/// <para>This port has no Gradle-style build file to read a trace output directory from, so the
/// snapshot carries the default; the doctor reads the real directory from the environment at run
/// time. The resolved family version (the version guard's input, D4) is read here from the
/// project's restore output — the same <c>project.assets.json</c> the doctor's package checks read
/// — so the entry point gets a complete state from one reader.</para>
/// </remarks>
public static class ProjectStateReader
{
    /// <summary>Vendor rule files whose existing managed block is kept up to date, never created.</summary>
    private static readonly string[] RuleFiles = [".cursorrules", ".github/copilot-instructions.md"];

    /// <summary>A listing cap, so a pathological tree degrades to a partial read rather than a hang.</summary>
    private const int DefaultMaxSkillDirectories = 200;

    /// <summary>Reads a project directory.</summary>
    public static ProjectState Read(string projectDirectory) =>
        Read(projectDirectory, DefaultMaxSkillDirectories);

    internal static ProjectState Read(string projectDirectory, int maxSkillDirectories)
    {
        if (projectDirectory is null)
        {
            throw new ArgumentException("a project directory must be given");
        }

        if (!Directory.Exists(projectDirectory))
        {
            throw new ArgumentException(projectDirectory + " is not a directory");
        }

        var builder = ProjectState.NewBuilder()
            .HasClaudeDirectory(Directory.Exists(Path.Combine(projectDirectory, ".claude")))
            .ResolvedFamilyVersion(ResolveFamilyVersion(projectDirectory));
        Set(TextOf(Path.Combine(projectDirectory, "AGENTS.md")), builder.AgentsMd);
        Set(TextOf(Path.Combine(projectDirectory, "CLAUDE.md")), builder.ClaudeMd);
        ReadRuleFiles(projectDirectory, builder);
        foreach (var flavour in new[] { SkillFlavour.Agents, SkillFlavour.Claude })
        {
            ReadInstallRoot(projectDirectory, flavour, maxSkillDirectories, builder);
        }

        return builder.Build();
    }

    private static void Set(string? value, Func<string, ProjectState.Builder> setter)
    {
        if (value is not null)
        {
            setter(value);
        }
    }

    private static void ReadRuleFiles(string projectDirectory, ProjectState.Builder builder)
    {
        foreach (var relative in RuleFiles)
        {
            var content = TextOf(Path.Combine(projectDirectory, relative.Replace('/', Path.DirectorySeparatorChar)));
            if (content is not null && CarriesMarkers(content))
            {
                builder.MarkedRuleFile(relative, content);
            }
        }
    }

    private static bool CarriesMarkers(string content)
    {
        var scan = MarkedBlock.ScanText(content);
        return scan.Regions.Count > 0 || scan.Problems.Count > 0;
    }

    private static void ReadInstallRoot(
        string projectDirectory, SkillFlavour flavour, int max, ProjectState.Builder builder)
    {
        var root = Path.Combine(
            projectDirectory, flavour.InstallRoot().Replace('/', Path.DirectorySeparatorChar));
        if (!Directory.Exists(root))
        {
            return;
        }

        foreach (var child in Children(root, max))
        {
            builder.AddInstalledSkill(ReadInstalledSkill(flavour, child));
        }
    }

    private static IEnumerable<string> Children(string root, int max) =>
        Directory.EnumerateFileSystemEntries(root)
            .OrderBy(Path.GetFileName, StringComparer.Ordinal)
            .Take(max);

    private static InstalledSkill ReadInstalledSkill(SkillFlavour flavour, string directory)
    {
        var name = Path.GetFileName(directory);
        if (!Directory.Exists(directory))
        {
            return new InstalledSkill(flavour, name, SkillPresence.NotADirectory, "", "");
        }

        var page = TextOf(Path.Combine(directory, InstalledSkill.Page)) ?? "";
        var coordinate = Provenance.CoordinateIn(page);
        return coordinate is not null
            ? new InstalledSkill(flavour, name, SkillPresence.Ours, coordinate, page)
            : new InstalledSkill(flavour, name, SkillPresence.Foreign, "", page);
    }

    /// <summary>
    /// The single NarrativeTrace family version this project restored, or <see langword="null"/> when
    /// none can be singled out — no restore output, no <c>NarrativeTrace.*</c> package, or a drifted
    /// install whose packages disagree (the doctor's alignment check reports that; the version guard
    /// stays silent rather than warn against a version it cannot pick, D4).
    /// </summary>
    private static string? ResolveFamilyVersion(string projectDirectory)
    {
        var versions = FamilyVersions(projectDirectory);
        return versions.Count == 1 ? versions.Single() : null;
    }

    private static HashSet<string> FamilyVersions(string projectDirectory)
    {
        var versions = new HashSet<string>(StringComparer.Ordinal);
        foreach (var assets in Directory.EnumerateFiles(
            projectDirectory, "project.assets.json", SearchOption.AllDirectories))
        {
            AddFamilyVersions(assets, versions);
        }

        return versions;
    }

    private static void AddFamilyVersions(string assetsPath, HashSet<string> versions)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(assetsPath));
            if (!document.RootElement.TryGetProperty("libraries", out var libraries))
            {
                return;
            }

            foreach (var library in libraries.EnumerateObject())
            {
                AddIfFamily(library.Name, versions);
            }
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            // A malformed, partial, or unreadable restore output is not the version guard's to report:
            // it leaves the guard unable to tell, which is silence (D4), never a crashed install.
        }
    }

    /// <summary>Splits a <c>Id/Version</c> library key and records the version when the Id is ours.</summary>
    private static void AddIfFamily(string idAndVersion, HashSet<string> versions)
    {
        var parts = idAndVersion.Split('/', 2);
        if (parts.Length == 2 && parts[0].StartsWith("NarrativeTrace.", StringComparison.Ordinal))
        {
            versions.Add(parts[1]);
        }
    }

    /// <summary>The file's text (BOM and bytes preserved), or <see langword="null"/> when it does not exist.</summary>
    private static string? TextOf(string file) =>
        File.Exists(file) ? Encoding.UTF8.GetString(File.ReadAllBytes(file)) : null;
}
