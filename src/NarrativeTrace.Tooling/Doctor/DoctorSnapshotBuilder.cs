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
        var approvedDir = ResolveDir(cwd, env, "NARRATIVETRACE_APPROVED_DIR", "narratives");
        var projectState = TryReadProjectState(cwd);
        var carrier = TryOpenCarrier(openCarrier);
        return new DoctorSnapshot(
            cwd,
            RuntimeInformation.FrameworkDescription,
            env,
            ReadFiles(cwd, "*.csproj"),
            ReadFiles(cwd, "*.cs"),
            ReadFiles(outputDir, "*"),
            ReadFiles(approvedDir, "*"),
            ReadFiles(cwd, "appsettings*.json"),
            ReadInstalledPackages(cwd),
            projectState?.InstalledSkills ?? [],
            carrier?.Skills.Select(skill => skill.Name).ToList() ?? [],
            projectState?.ResolvedFamilyVersion);
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

    private static string ResolveDir(
        string cwd, Dictionary<string, string?> env, string key, string relativeDefault)
    {
        var configured = env.TryGetValue(key, out var value) ? value : null;
        return Path.Combine(cwd, string.IsNullOrWhiteSpace(configured) ? relativeDefault : configured);
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

        AddMatchingFiles(dir, pattern, result);
        var included = Directory.EnumerateDirectories(dir)
            .Where(sub => !ExcludedDirs.Contains(Path.GetFileName(sub)));
        foreach (var sub in included)
        {
            Walk(sub, pattern, result);
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
        catch (IOException)
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

        foreach (var assets in Directory.EnumerateFiles(cwd, "project.assets.json", SearchOption.AllDirectories))
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
