// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.SkillsEvals;

/// <summary>
/// The one locator of the built CLI assembly — for a grader that invokes it directly and for the
/// installer delivery that runs this checkout's own <c>init</c> before the agent starts.
/// </summary>
public static class CliDll
{
    /// <summary>The CLI project's assembly name — the entry assembly is <c>dotnet-narrativetrace</c>, not its project name.</summary>
    private const string Assembly = "dotnet-narrativetrace.dll";

    private const string Project = "src/NarrativeTrace.Cli";

    /// <summary>
    /// The most recently written build under the CLI project's <c>bin/</c>, or <c>null</c> when the
    /// project has not been built. "Most recently written", never "whichever the filesystem lists
    /// first": Debug and Release builds sit side by side.
    /// </summary>
    public static string? Locate(string repoRoot)
    {
        var binDir = Path.Combine(repoRoot, "src", "NarrativeTrace.Cli", "bin");
        if (!Directory.Exists(binDir))
        {
            return null;
        }

        return Directory.EnumerateFiles(binDir, Assembly, SearchOption.AllDirectories)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
    }

    /// <summary>The built CLI assembly; an unbuilt project fails naming what to build.</summary>
    /// <exception cref="InvalidOperationException">the CLI project has not been built.</exception>
    public static string Require(string repoRoot) =>
        Locate(repoRoot)
        ?? throw new InvalidOperationException(
            $"the CLI is not built: run `dotnet build {Project}` first (looked under {Path.Combine(repoRoot, Project, "bin")})");
}
