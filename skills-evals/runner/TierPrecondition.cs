// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Diagnostics;

namespace NarrativeTrace.SkillsEvals;

/// <summary>
/// Sporadic-lanes policy, rule 3: "Deterministic tiers first, always. A cheaper-lane run refuses to
/// start unless the skill's Tier A lints and Tier A2 replay are green at HEAD." For this port Tier A
/// is <c>SkillsLint</c> and Tier A2 is <c>SkillsReplay</c>, run together via <c>./build.sh</c>. The
/// <see cref="RunCommand"/> seam is injectable so a unit test can fake the invocation without a real
/// build.
/// </summary>
public static class TierPrecondition
{
    /// <summary>Runs <paramref name="command"/> in <paramref name="cwd"/>, throwing on a non-zero exit.</summary>
    public delegate void RunCommand(IReadOnlyList<string> command, string cwd);

    /// <summary>The exact, closed-vocabulary <c>./build.sh</c> invocation this precondition runs.</summary>
    public static IReadOnlyList<string> Command() => ["./build.sh", "SkillsLint", "SkillsReplay"];

    /// <summary>The real <see cref="RunCommand"/> seam: spawns the command, throwing on non-zero exit.</summary>
    public static void RunViaProcess(IReadOnlyList<string> command, string cwd)
    {
        var psi = new ProcessStartInfo { FileName = command[0], WorkingDirectory = cwd, UseShellExecute = false };
        foreach (var arg in command.Skip(1))
        {
            psi.ArgumentList.Add(arg);
        }

        using var process = Process.Start(psi)
            ?? throw new IOException($"failed to start: {string.Join(' ', command)}");
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new IOException($"{string.Join(' ', command)} exited {process.ExitCode}");
        }
    }

    /// <summary>Throws with a quota-preserving explanation when the deterministic tiers are red.</summary>
    public static void AssertDeterministicTiersGreen(string repoRoot, RunCommand run)
    {
        try
        {
            run(Command(), repoRoot);
        }
        catch (Exception e) when (e is not InvalidOperationException)
        {
            throw new InvalidOperationException(
                "Tier A lints / Tier A2 replay are not green at HEAD (./build.sh SkillsLint"
                + " SkillsReplay) — a sporadic-lane (codex/antigravity/cursor) trial refuses to start on top of"
                + " a known defect. Fix the skills tiers before spending quota here.",
                e);
        }
    }
}
