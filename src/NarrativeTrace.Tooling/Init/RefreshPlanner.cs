// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Tooling.Init;

/// <summary>
/// Decides what a build-time refresh would rewrite: the pages and the managed section a previous
/// install left behind, brought up to the carrier the build now resolves.
/// </summary>
/// <remarks>
/// INTENT: a build may KEEP an install current; it may never start one. That is the whole of the
/// difference from <see cref="InitPlanner"/>, and it is why this planner exists rather than a flag —
/// "never create" has to be true by construction, not by a caller remembering an option. Pure, like the
/// other two planners.
/// </remarks>
public static class RefreshPlanner
{
    /// <summary>The plan a refresh against this carrier would apply — empty unless something is stale.</summary>
    public static InitPlan Plan(ProjectState state, Carrier carrier)
    {
        if (state is null || carrier is null)
        {
            throw new ArgumentException("planning a refresh needs a project state and a carrier");
        }

        return new InitPlan(carrier.Coordinate, false, Rewrites(state, carrier));
    }

    /// <summary>
    /// Whether this project carries an install of ours at all — one skill directory whose page has our
    /// provenance line.
    /// </summary>
    /// <remarks>
    /// This is the question a build asks BEFORE it reaches for a carrier. A project that never ran
    /// <c>init</c> must never resolve one, or every offline build of every project would warn about
    /// skills nobody installed. A directory somebody else owns is not an install of ours, however named.
    /// </remarks>
    public static bool IsInstalled(ProjectState state)
    {
        if (state is null)
        {
            throw new ArgumentException("asking what a project carries needs a project state");
        }

        return Ours(state).Any();
    }

    /// <summary>
    /// The install plan, kept down to the actions that REWRITE something already ours. Every other kind
    /// — a page to create, a section to append, an import line to add, a refusal — is dropped here, which
    /// is what keeps a build from starting an install nobody asked for.
    /// </summary>
    private static List<Action> Rewrites(ProjectState state, Carrier carrier)
    {
        if (!IsStale(state, carrier.Coordinate))
        {
            return [];
        }

        return InitPlanner.Plan(state, carrier, InitOptions.Defaults()).Actions
            .OfType<ReplaceBlock>()
            .Cast<Action>()
            .ToList();
    }

    /// <summary>
    /// Whether this project carries an install of ours from a DIFFERENT carrier. A project with no
    /// install of ours is never stale: a build refreshes what init put there and starts nothing.
    /// </summary>
    private static bool IsStale(ProjectState state, string coordinate) =>
        Ours(state).Any(skill => !string.Equals(skill.Coordinate, coordinate, StringComparison.Ordinal));

    private static IEnumerable<InstalledSkill> Ours(ProjectState state) =>
        state.InstalledSkills.Where(skill => skill.Presence == SkillPresence.Ours);
}
