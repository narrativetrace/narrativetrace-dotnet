// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Clarity;

/// <summary>
/// Penalizes structural complexity, mirroring the Java model: each parameter
/// beyond 4 costs 0.10 and each call-depth level beyond 5 costs 0.05, summed
/// and subtracted from a perfect 1.0.
/// </summary>
public static class StructuralScorer
{
    /// <summary>Scores a trace's shape — nesting depth and parameter-list width.</summary>
    /// <param name="maxParams">The widest parameter list observed in the trace.</param>
    /// <param name="maxDepth">The deepest call nesting observed, counting the root as 1.</param>
    /// <returns>A score from <c>0.0</c> to <c>1.0</c>, falling as either measure grows.</returns>
    /// <remarks>
    /// The one clarity dimension renaming cannot improve: it reports structure,
    /// so raising it means changing the code's shape. Driven by the observed
    /// maxima, not by averages, so a single very wide signature moves the whole
    /// score.
    /// </remarks>
    public static double Score(int maxParams, int maxDepth)
    {
        var penalty = 0.0;
        if (maxParams > 4)
        {
            penalty += 0.10 * (maxParams - 4);
        }

        if (maxDepth > 5)
        {
            penalty += 0.05 * (maxDepth - 5);
        }

        return Math.Max(0.0, 1.0 - penalty);
    }
}
