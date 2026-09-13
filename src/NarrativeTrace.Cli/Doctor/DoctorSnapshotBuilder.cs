// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Runtime.InteropServices;
using System.Text.Json;

namespace NarrativeTrace.Cli.Doctor;

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

    /// <summary>Builds a snapshot of <paramref name="cwd"/> using <paramref name="readEnv"/> for variables.</summary>
    public static DoctorSnapshot Build(string cwd, Func<string, string?> readEnv)
    {
        var env = EnvKeys.ToDictionary(key => key, readEnv);
        var outputDir = ResolveDir(cwd, env, "NARRATIVETRACE_OUTPUT_DIR", "TestResults/narrativetrace");
        var approvedDir = ResolveDir(cwd, env, "NARRATIVETRACE_APPROVED_DIR", "narratives");
        return new DoctorSnapshot(
            cwd,
            RuntimeInformation.FrameworkDescription,
            env,
            ReadFiles(cwd, "*.csproj"),
            ReadFiles(cwd, "*.cs"),
            ReadFiles(outputDir, "*"),
            ReadFiles(approvedDir, "*"),
            ReadFiles(cwd, "appsettings*.json"),
            ReadInstalledPackages(cwd));
    }

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
