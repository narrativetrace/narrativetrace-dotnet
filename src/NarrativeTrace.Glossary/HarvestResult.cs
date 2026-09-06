// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Glossary;

/// <summary>Everything one harvest pass observed, in deterministic order.</summary>
/// <remarks>
/// A pure value handed to <see cref="GlossaryMerger"/>; contains observations
/// only — no merge decisions. Candidates are sorted by
/// <c>(context, phrase, kind, site)</c> so downstream output is reproducible
/// run-over-run.
/// </remarks>
public sealed record HarvestResult
{
    /// <param name="candidates">Aggregated observations; must not be null.</param>
    /// <exception cref="ArgumentNullException"><paramref name="candidates"/> is null.</exception>
    public HarvestResult(IReadOnlyList<HarvestCandidate> candidates)
    {
        if (candidates is null)
        {
            throw new ArgumentNullException(nameof(candidates));
        }

        Candidates = candidates.ToArray();
    }

    /// <summary>Aggregated observations; never null.</summary>
    public IReadOnlyList<HarvestCandidate> Candidates { get; }
}
