// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Skills;
using NarrativeTrace.Skills.Catalogue;

namespace NarrativeTrace.Cli;

/// <summary>
/// The <c>skills replay</c> verb — Tier A2 (skill-harness design §4.2): mechanically replays every
/// catalogue skill's own <c>commands</c>/<c>verify</c> steps against the sixty-seconds fixture, no
/// LLM. Mirrors the canonical TypeScript source's <c>packages/skills/__tests__/replay.test.ts</c>
/// (adapted the way the Java port's own <c>SkillReplayer</c> is: a closed registry of safe, in-repo
/// executors, since a literal replay of some commands — installing THIS repo's own tool from the
/// registry, adding a package this fixture already references by <c>ProjectReference</c> — would
/// either hit the network for no reason or mutate a checked-in fixture other gates depend on
/// verbatim; see <see cref="SkillReplayRegistry"/>'s own remarks).
/// </summary>
public static class SkillsReplayCommand
{
    /// <summary>Replays every catalogue skill against its fixture under <paramref name="repoRoot"/>.</summary>
    public static int Run(string repoRoot, TextWriter output, TextWriter error)
    {
        var problems = new List<string>();
        var ranAnything = false;
        foreach (var skill in SkillCatalogue.Skills)
        {
            ranAnything |= ReplaySkill(skill, repoRoot, problems);
        }

        if (!ranAnything)
        {
            problems.Add("replay ran nothing mechanical across every skill — this is a silent no-op, not a pass");
        }

        return Report(problems, output, error);
    }

    /// <summary>Replays one skill, appending any problem to <paramref name="problems"/>; returns whether anything ran.</summary>
    private static bool ReplaySkill(Skill skill, string repoRoot, List<string> problems)
    {
        var fixtureDir = Path.Combine(repoRoot, skill.Fixture);
        IReadOnlyList<StepReplayResult> results;
        try
        {
            results = SkillReplayEngine.Replay(
                skill,
                repoRoot,
                (command, root) => SkillReplayRegistry.RunCommand(command, root, fixtureDir),
                (verify, root) => SkillReplayRegistry.TryVerify(verify, root, fixtureDir));
        }
        catch (Exception ex)
        {
            // Never crash the CLI over a replay failure — a missing fixture, an unregistered
            // command, a process that could not start, or a timed-out subprocess are all reported
            // the same way SkillsLintCommand reports any other problem, never an unhandled
            // exception out of a verb that promises an exit code.
            problems.Add($"{skill.CanonicalName}: {ex.Message}");
            return false;
        }

        problems.AddRange(results.Where(r => r.Ran && !r.Ok)
            .Select(r => $"{skill.CanonicalName}: \"{r.Title}\" did not replay cleanly"));
        return results.Any(r => r.Ran);
    }

    private static int Report(List<string> problems, TextWriter output, TextWriter error)
    {
        foreach (var problem in problems)
        {
            error.WriteLine($"  - {problem}");
        }

        output.WriteLine(
            problems.Count == 0
                ? $"skills replay: {SkillCatalogue.Skills.Count} skill(s), 0 problems"
                : $"skills replay: {problems.Count} problem(s)");
        return problems.Count == 0 ? 0 : 1;
    }
}
