// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace Build.Tests;

/// <summary>
/// Locates the repository root from the test assembly's output directory.
/// The build-script checks that read real repository files — the demo wiring
/// table, the demo colorizer — all need the same anchor, so it lives here
/// rather than once per test class.
/// </summary>
internal static class RepositoryPath
{
    /// <summary>The directory holding <c>NarrativeTrace.sln</c>.</summary>
    /// <exception cref="InvalidOperationException">
    /// No ancestor of the test output directory holds the solution.
    /// </exception>
    internal static string Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "NarrativeTrace.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException(
            "NarrativeTrace.sln not found above " + AppContext.BaseDirectory);
    }
}
