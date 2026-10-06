// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Collections.ObjectModel;

namespace NarrativeTrace.Tooling.Init;

/// <summary>
/// Everything an install or an uninstall would do, decided before anything is written.
/// </summary>
/// <remarks>
/// <para>INTENT: the seam that makes approval structural. A plan can be rendered as a diff,
/// reviewed, and only then applied — and because it is complete, applying it needs no second look at
/// the project.</para>
/// <para>A plan holds at most ONE action per path. Two actions on one path would make the diff a lie
/// (the second would be computed from a state the first has not produced), so the planners never emit
/// one and construction refuses it.</para>
/// </remarks>
public sealed record InitPlan
{
    /// <param name="carrier">The coordinate everything in this plan is stamped with.</param>
    /// <param name="dryRun">Whether this plan is for showing only — a dry run exits 0 even when it refuses.</param>
    /// <param name="actions">The actions, in the order a person should read them.</param>
    /// <param name="warnings">
    /// Advisory lines the output prints beside the plan — the version guard's mismatch note (D4),
    /// never a refusal and never part of the exit code. Empty for most plans.
    /// </param>
    public InitPlan(string carrier, bool dryRun, IReadOnlyList<Action> actions, IReadOnlyList<string>? warnings = null)
    {
        if (string.IsNullOrWhiteSpace(carrier))
        {
            throw new ArgumentException("a plan names the carrier it came from");
        }

        if (actions is null)
        {
            throw new ArgumentException("a plan with no actions is an empty list, never null");
        }

        Carrier = carrier;
        DryRun = dryRun;
        Actions = new ReadOnlyCollection<Action>([.. actions]);
        Warnings = new ReadOnlyCollection<string>([.. warnings ?? []]);
        if (!DistinctPaths(Actions))
        {
            throw new ArgumentException("a plan holds at most one action per path");
        }
    }

    /// <summary>The coordinate everything in this plan is stamped with.</summary>
    public string Carrier { get; }

    /// <summary>Whether this plan is for showing only — a dry run exits 0 even when it refuses.</summary>
    public bool DryRun { get; }

    /// <summary>The actions, in the order a person should read them.</summary>
    public IReadOnlyList<Action> Actions { get; }

    /// <summary>Advisory lines the output prints beside the plan (the version-guard note); never affect the exit code.</summary>
    public IReadOnlyList<string> Warnings { get; }

    /// <summary>True when there is nothing to do — the shape a re-run of an up-to-date install produces.</summary>
    public bool IsEmpty => Actions.Count == 0;

    /// <summary>True when at least one action is a refusal.</summary>
    public bool HasRefusals => Actions.OfType<Refuse>().Any();

    /// <summary>Every refusal, in plan order.</summary>
    public IReadOnlyList<Refuse> Refusals() => Actions.OfType<Refuse>().ToList();

    /// <summary>
    /// The exit code an entry point returns: 1 when anything was refused, 0 otherwise — and always 0
    /// for a dry run, where a refusal is something shown rather than something that happened.
    /// </summary>
    public int ExitCode => !DryRun && HasRefusals ? 1 : 0;

    /// <summary>One action per path, every path project-relative, every refusal with a reason.</summary>
    internal bool Invariant() =>
        DistinctPaths(Actions) && Actions.All(action => !System.IO.Path.IsPathRooted(action.Path));

    private static bool DistinctPaths(IReadOnlyList<Action> actions) =>
        actions.Select(action => action.Path).Distinct(StringComparer.Ordinal).Count() == actions.Count;
}
