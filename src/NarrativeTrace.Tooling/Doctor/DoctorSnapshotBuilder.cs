// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Runtime.InteropServices;
using System.Text.Json;
using NarrativeTrace.Tooling.Init;

namespace NarrativeTrace.Tooling.Doctor;

/// <summary>
/// The one impure module in the doctor subsystem: walks the real file
/// system and process environment to build a <see cref="DoctorSnapshot"/>.
/// Every check downstream is a pure function of the result.
/// </summary>
public static class DoctorSnapshotBuilder
{
    private static readonly string[] ExcludedDirs = ["bin", "obj", ".git", "node_modules"];
    private static readonly string[] EnvKeys =
    [
        "NARRATIVETRACE_OUTPUT", "NARRATIVETRACE_OUTPUT_DIR", "NARRATIVETRACE_APPROVED_DIR",
        "NARRATIVETRACE_LEVEL", "NARRATIVETRACE_FORMAT", "NARRATIVETRACE_APPROVAL",
    ];
    private const int MaxFiles = 5000;
    private static readonly string[] ManifestFilePatterns =
        ["*.csproj", "*.fsproj", "*.vbproj", "Directory.Build.props", "Directory.Build.targets"];

    /// <summary>
    /// Builds a snapshot of <paramref name="cwd"/> using <paramref name="readEnv"/> for variables, with
    /// no carrier to resolve agent skills from — the skills check reads this as "cannot tell".
    /// </summary>
    public static DoctorSnapshot Build(string cwd, Func<string, string?> readEnv) =>
        Build(cwd, readEnv, NoCarrierConfigured);

    /// <summary>
    /// Builds a snapshot of <paramref name="cwd"/> using <paramref name="readEnv"/> for variables and
    /// <paramref name="openCarrier"/> to resolve the skills carrier this run installs from — the same
    /// carrier resolution <c>init</c> uses (D9). A carrier that cannot be opened is absorbed here, the
    /// same best-effort policy every other field in this builder already follows.
    /// </summary>
    public static DoctorSnapshot Build(string cwd, Func<string, string?> readEnv, Func<Carrier> openCarrier)
    {
        var env = EnvKeys.ToDictionary(key => key, readEnv);
        var outputDir = ResolveDir(cwd, env, "NARRATIVETRACE_OUTPUT_DIR", "TestResults/narrativetrace");
        var projectState = TryReadProjectState(cwd);
        var carrier = TryOpenCarrier(openCarrier);
        return new DoctorSnapshot(
            cwd,
            RuntimeInformation.FrameworkDescription,
            env,
            ReadManifests(cwd),
            ReadFiles(cwd, "*.cs"),
            ReadFiles(outputDir, "*"),
            ReadApprovedDirs(cwd, env),
            ReadFiles(cwd, "appsettings*.json"),
            ReadInstalledPackages(cwd),
            projectState?.InstalledSkills ?? [],
            carrier?.Skills.Select(skill => skill.Name).ToList() ?? [],
            projectState?.ResolvedFamilyVersion)
        { RunSettingsFiles = ReadFiles(cwd, "*.runsettings") };
    }

    /// <summary>
    /// Reads the project's <see cref="ProjectState"/> through the installer's own strict reader,
    /// absorbing its refusal at this seam: the installer must not create over a file it could not
    /// read, but the doctor must not crash on a project it cannot read (best-effort, like every other
    /// field this builder reads).
    /// </summary>
    private static ProjectState? TryReadProjectState(string cwd)
    {
        if (!Directory.Exists(cwd))
        {
            return null;
        }

        try
        {
            return ProjectStateReader.Read(cwd);
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static Carrier? TryOpenCarrier(Func<Carrier> openCarrier)
    {
        try
        {
            return openCarrier();
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static Carrier NoCarrierConfigured() =>
        throw new ArgumentException("this snapshot was built with no carrier to resolve agent skills from");

    /// <summary>
    /// The approval-trace directory under the doctor's own folder and beside every project manifest:
    /// a test run resolves a relative directory against its test project, so that is where its
    /// baselines and review copies are. An absolute directory is one directory, read once.
    /// </summary>
    private static Dictionary<string, string> ReadApprovedDirs(string cwd, Dictionary<string, string?> env)
    {
        var roots = new HashSet<string>(StringComparer.Ordinal) { cwd };
        foreach (var manifest in ReadManifests(cwd).Keys)
        {
            roots.Add(Path.GetDirectoryName(manifest) ?? cwd);
        }

        var result = new Dictionary<string, string>();
        foreach (var dir in roots.Select(root => Path.GetFullPath(ResolveDir(root, env, "NARRATIVETRACE_APPROVED_DIR", "narratives"))).Distinct())
        {
            foreach (var (path, content) in ReadFiles(dir, "*"))
            {
                result[path] = content;
            }
        }

        return result;
    }

    private static string ResolveDir(
        string cwd, Dictionary<string, string?> env, string key, string relativeDefault)
    {
        var configured = env.TryGetValue(key, out var value) ? value : null;
        return Path.Combine(cwd, string.IsNullOrWhiteSpace(configured) ? relativeDefault : configured);
    }

    /// <summary>
    /// Every MSBuild manifest a framework marker or a package reference can live in: the project files
    /// of every language the SDK builds, and the <c>Directory.Build.*</c> files that add items to all of
    /// them. <c>Directory.Packages.props</c> is not read — a central version pin references nothing.
    /// </summary>
    private static Dictionary<string, string> ReadManifests(string cwd)
    {
        var result = new Dictionary<string, string>();
        foreach (var pattern in ManifestFilePatterns)
        {
            foreach (var (path, content) in ReadFiles(cwd, pattern))
            {
                result[path] = content;
            }
        }

        return result;
    }

    private static Dictionary<string, string> ReadFiles(string root, string pattern)
    {
        var result = new Dictionary<string, string>();
        if (Directory.Exists(root))
        {
            Walk(root, pattern, result);
        }

        return result;
    }

    private static void Walk(string dir, string pattern, Dictionary<string, string> result)
    {
        if (result.Count >= MaxFiles)
        {
            return;
        }

        try
        {
            AddMatchingFiles(dir, pattern, result);
            var included = Directory.EnumerateDirectories(dir)
                .Where(sub => !ExcludedDirs.Contains(Path.GetFileName(sub)))
                .ToList();
            foreach (var sub in included)
            {
                Walk(sub, pattern, result);
            }
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
        {
            // A directory this user cannot list is skipped, like a file it cannot read: the doctor
            // reports on what it can see and never crashes on a project's permissions.
        }
    }

    private static void AddMatchingFiles(string dir, string pattern, Dictionary<string, string> result)
    {
        foreach (var file in Directory.EnumerateFiles(dir, pattern))
        {
            if (result.Count >= MaxFiles)
            {
                return;
            }

            result[file] = ReadSafely(file);
        }
    }

    private static string ReadSafely(string file)
    {
        try
        {
            return File.ReadAllText(file);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return string.Empty;
        }
    }

    private static Dictionary<string, string> ReadInstalledPackages(string cwd)
    {
        var result = new Dictionary<string, string>();
        if (!Directory.Exists(cwd))
        {
            return result;
        }

        var everywhereReadable = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true };
        foreach (var assets in Directory.EnumerateFiles(cwd, "project.assets.json", everywhereReadable))
        {
            MergeLibraries(assets, result);
        }

        return result;
    }

    private static void MergeLibraries(string assetsPath, Dictionary<string, string> result)
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
                AddLibrary(library.Name, result);
            }
        }
        catch (JsonException)
        {
            // A malformed or partial restore output is not this check's problem to report.
        }
    }

    private static void AddLibrary(string idAndVersion, Dictionary<string, string> result)
    {
        var parts = idAndVersion.Split('/', 2);
        if (parts.Length == 2)
        {
            result[parts[0]] = parts[1];
        }
    }
}
