// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace NarrativeTrace.Build;

/// <summary>
/// Backs the project selection behind the <c>Test</c> and <c>Coverage</c>
/// targets — which projects of the solution those sweeps run.
/// </summary>
/// <remarks>
/// Selection is by <em>name</em>, so a test project can drop out of the gate
/// without anything going red: it simply stops being run, and the rules it
/// enforces stop being enforced. That is not hypothetical — the suffix was
/// <c>".Tests"</c>, which <c>NarrativeTrace.ArchTests</c> (no dot) never
/// matched, so the architecture gate the documentation advertised had never
/// run. <see cref="Check"/> closes that hole: every project under
/// <c>tests/</c> must be selectable and must be in the solution, because the
/// sweeps enumerate the solution's projects.
/// </remarks>
internal static class TestProjectSelection
{
    /// <summary>
    /// The suffix a project name must carry to join the sweeps. Deliberately
    /// without a leading dot, so <c>ArchTests</c>-style names are included.
    /// </summary>
    public const string NameSuffix = "Tests";

    private const string TestsDirectory = "tests";
    private const string SolutionFile = "NarrativeTrace.sln";

    private static readonly string[] ProjectExtensions = [".csproj", ".fsproj"];

    /// <summary>Whether the <c>Test</c> and <c>Coverage</c> sweeps run this project.</summary>
    /// <param name="projectName">The project's name, without extension.</param>
    public static bool IsSelected(string projectName) =>
        projectName is not null
        && projectName.EndsWith(NameSuffix, StringComparison.Ordinal);

    /// <summary>Every project file directly under <c>tests/</c>, full paths.</summary>
    public static IReadOnlyList<string> TestProjectFiles(string repoRoot)
    {
        var tests = Path.Combine(repoRoot, TestsDirectory);
        if (!Directory.Exists(tests))
            return [];

        return Directory.EnumerateDirectories(tests)
            .SelectMany(directory => Directory.EnumerateFiles(directory))
            .Where(file => ProjectExtensions.Contains(
                Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))
            .OrderBy(file => file, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Reports every project under <c>tests/</c> that the sweeps would skip —
    /// because its name does not end in <see cref="NameSuffix"/>, or because
    /// it is absent from the solution the sweeps enumerate.
    /// </summary>
    /// <param name="repoRoot">The directory holding the solution.</param>
    /// <returns>One human-readable problem per skipped project; empty when all run.</returns>
    public static IReadOnlyList<string> Check(string repoRoot)
    {
        var solution = SolutionText(repoRoot);
        return TestProjectFiles(repoRoot)
            .Select(file => Problem(file, solution))
            .OfType<string>()
            .ToList();
    }

    private static string? Problem(string projectFile, string solution)
    {
        var name = Path.GetFileNameWithoutExtension(projectFile);
        if (!IsSelected(name))
            return $"{name}: name does not end in \"{NameSuffix}\", "
                + "so the Test and Coverage sweeps skip it";

        if (!solution.Contains(Path.GetFileName(projectFile), StringComparison.Ordinal))
            return $"{name}: not in {SolutionFile}, "
                + "so the Test and Coverage sweeps never see it";

        return null;
    }

    private static string SolutionText(string repoRoot)
    {
        var solution = Path.Combine(repoRoot, SolutionFile);
        return File.Exists(solution) ? File.ReadAllText(solution) : "";
    }
}
