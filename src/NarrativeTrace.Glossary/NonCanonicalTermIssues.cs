// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Clarity;

namespace NarrativeTrace.Glossary;

/// <summary>
/// Maps vocabulary violations into <c>non-canonical-term</c> clarity issues.
/// </summary>
/// <remarks>
/// Violations ride the existing clarity reporting surface unchanged —
/// <c>clarity-results.json</c> and the clarity-check gate all consume
/// <see cref="ClarityIssue"/>s, so vocabulary governance costs no new report
/// format. Severity is Medium per the plan, and the violation's occurrence
/// count carries straight into the issue's impact score, so a widely used
/// deprecated synonym ranks above a one-off.
/// </remarks>
public static class NonCanonicalTermIssues
{
    /// <summary>Issue category label consumed by clarity reports and gates.</summary>
    public const string Category = "non-canonical-term";

    /// <summary>Converts violations to clarity issues, preserving order.</summary>
    /// <param name="violations">Aggregated violations of one run; must not be null.</param>
    /// <returns>
    /// One issue per violation, category <c>non-canonical-term</c>, element
    /// <c>&lt;context&gt;.&lt;site&gt;</c>, severity Medium.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="violations"/> is null.</exception>
    public static IReadOnlyList<ClarityIssue> From(
        IReadOnlyList<VocabularyViolation> violations)
    {
        if (violations is null)
        {
            throw new ArgumentNullException(nameof(violations));
        }

        return violations.Select(ToIssue).ToList();
    }

    private static ClarityIssue ToIssue(VocabularyViolation violation)
    {
        return new ClarityIssue(
            Category,
            $"{violation.Context}.{violation.Site}",
            Suggestion(violation),
            ClaritySeverity.Medium,
            violation.Occurrences);
    }

    private static string Suggestion(VocabularyViolation violation)
    {
        var baseSuggestion = $"use canonical term '{violation.CanonicalTerm}'";
        if (violation.SuggestedIdentifier is null)
        {
            return baseSuggestion;
        }

        return $"{baseSuggestion} → rename to {violation.SuggestedIdentifier}";
    }
}
