// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Core;

/// <summary>How the scenario's structure relates to its last green artifact.</summary>
public enum ScenarioDeltaKind
{
    /// <summary>No baseline exists yet.</summary>
    New,

    /// <summary>Byte-identical to the baseline.</summary>
    Unchanged,

    /// <summary>Differs from the baseline.</summary>
    Changed,
}

/// <summary>
/// One scenario's structural status against its last green <c>.nt</c> artifact.
/// </summary>
/// <remarks>
/// INTENT: The unit the trace writer reports upward after each test — the
/// suite footer aggregates these into the post-run delta line, and the
/// failure surface prints the diff of the failing scenario. Produced beside
/// the artifact write so baseline reading happens exactly once.
/// </remarks>
/// <param name="Scenario">The humanized scenario name (the <c>scenario:</c> header value).</param>
/// <param name="Kind"><see cref="ScenarioDeltaKind.New"/>, <see cref="ScenarioDeltaKind.Unchanged"/> or <see cref="ScenarioDeltaKind.Changed"/>.</param>
/// <param name="Summary">Compact change summary (<c>+4 calls X.y</c>); empty unless <see cref="ScenarioDeltaKind.Changed"/>.</param>
/// <param name="Diff">Readable line diff against the baseline; empty unless <see cref="ScenarioDeltaKind.Changed"/>.</param>
public sealed record ScenarioDelta(string Scenario, ScenarioDeltaKind Kind, string Summary, string Diff)
{
    /// <summary>
    /// Classifies the current artifact against the baseline; a null baseline
    /// means no last green artifact exists yet — the scenario is
    /// <see cref="ScenarioDeltaKind.New"/>.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="scenario"/> or <paramref name="current"/> is null.</exception>
    public static ScenarioDelta Of(string scenario, string? baseline, string current)
    {
        if (scenario is null)
        {
            throw new ArgumentNullException(nameof(scenario));
        }

        if (current is null)
        {
            throw new ArgumentNullException(nameof(current));
        }

        if (baseline is null)
        {
            return new ScenarioDelta(scenario, ScenarioDeltaKind.New, string.Empty, string.Empty);
        }

        var structural = StructuralDelta.Between(baseline, current);
        return structural.Unchanged
            ? new ScenarioDelta(scenario, ScenarioDeltaKind.Unchanged, string.Empty, string.Empty)
            : new ScenarioDelta(scenario, ScenarioDeltaKind.Changed, structural.Summary(), structural.Diff());
    }
}
