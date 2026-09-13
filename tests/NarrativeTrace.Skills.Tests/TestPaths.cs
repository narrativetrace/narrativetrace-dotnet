// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Skills.Tests;

/// <summary>Locates the repository root from the test assembly's output directory.</summary>
internal static class TestPaths
{
    /// <summary>The absolute path to the repository root (the directory containing <c>NarrativeTrace.sln</c>).</summary>
    public static string RepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "NarrativeTrace.sln")))
        {
            dir = Path.GetDirectoryName(dir);
        }

        return dir ?? throw new InvalidOperationException(
            $"could not find NarrativeTrace.sln above {AppContext.BaseDirectory}");
    }
}
