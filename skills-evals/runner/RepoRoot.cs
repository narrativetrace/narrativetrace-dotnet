// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.SkillsEvals;

/// <summary>
/// Resolves the repository root independent of the invoking shell's working directory — the marker
/// is <c>NarrativeTrace.sln</c>. The runner reads <c>skills-evals/</c> cases, fixtures and the
/// ledger relative to it, so it must resolve the same whether launched from the repo root, from
/// <c>skills-evals/runner/</c>, or from the built assembly's own directory.
/// </summary>
public static class RepoRoot
{
    private const string RootMarker = "NarrativeTrace.sln";

    /// <summary>The production entry point: resolves from the real process working directory.</summary>
    public static string Locate() => Locate(Directory.GetCurrentDirectory());

    /// <summary>Cwd-injectable for tests; walks up from <paramref name="startDir"/> then the assembly.</summary>
    public static string Locate(string startDir)
    {
        return WalkUp(startDir)
            ?? WalkUp(AppContext.BaseDirectory)
            ?? throw new InvalidOperationException(
                $"could not locate {RootMarker} above {startDir} or {AppContext.BaseDirectory}");
    }

    private static string? WalkUp(string? dir)
    {
        for (var current = dir; current is not null; current = Path.GetDirectoryName(current))
        {
            if (File.Exists(Path.Combine(current, RootMarker)))
            {
                return current;
            }
        }

        return null;
    }
}
