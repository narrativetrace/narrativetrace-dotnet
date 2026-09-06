// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Text;

namespace NarrativeTrace.Glossary;

/// <summary>Formats the vocabulary line of the suite console summary.</summary>
/// <remarks>
/// Same role as core's <c>ConsoleSummaryReporter</c> — keep test integrations
/// free of formatting detail while producing a stable, human-readable footer
/// block:
/// <code>
/// Vocabulary: 3 new terms harvested, 2 deprecated synonyms in use
///   accountWithOverdraft → use overdraftAccount (billing: "overdraft account")
/// </code>
/// </remarks>
public static class VocabularySummaryFormatter
{
    /// <summary>Formats the vocabulary summary for one harvest run.</summary>
    /// <param name="newTermCount">Number of terms this run added to the glossary; must be non-negative.</param>
    /// <param name="violations">Deprecated-synonym uses, one detail line each; must not be null.</param>
    /// <returns>The summary block without a trailing newline.</returns>
    /// <exception cref="ArgumentException"><paramref name="newTermCount"/> is negative.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="violations"/> is null.</exception>
    public static string FormatSummary(
        int newTermCount, IReadOnlyList<VocabularyViolation> violations)
    {
        if (newTermCount < 0)
        {
            throw new ArgumentException(
                $"newTermCount must be non-negative: {newTermCount}",
                nameof(newTermCount));
        }

        if (violations is null)
        {
            throw new ArgumentNullException(nameof(violations));
        }

        var output = new StringBuilder();
        output.Append("Vocabulary: ").Append(newTermCount)
            .Append(newTermCount == 1 ? " new term harvested" : " new terms harvested");
        AppendViolations(output, violations);
        return output.ToString();
    }

    private static void AppendViolations(
        StringBuilder output, IReadOnlyList<VocabularyViolation> violations)
    {
        if (violations.Count == 0)
        {
            return;
        }

        output.Append(", ").Append(violations.Count)
            .Append(violations.Count == 1
                ? " deprecated synonym in use"
                : " deprecated synonyms in use");
        foreach (var violation in violations)
        {
            output.Append("\n  ").Append(DetailLine(violation));
        }
    }

    private static string DetailLine(VocabularyViolation violation)
    {
        if (violation.SuggestedIdentifier is not null)
        {
            return $"{violation.Identifier} → use {violation.SuggestedIdentifier} "
                + $"({violation.Context}: \"{violation.CanonicalTerm}\")";
        }

        return $"{violation.Identifier} → use canonical term "
            + $"\"{violation.CanonicalTerm}\" ({violation.Context})";
    }
}
