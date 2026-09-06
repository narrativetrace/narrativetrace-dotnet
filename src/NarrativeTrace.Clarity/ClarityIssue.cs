// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Clarity;

/// <summary>One actionable naming issue discovered during clarity analysis.</summary>
/// <remarks>
/// Structured enough that Markdown reports, <c>clarity-results.json</c>, and
/// the CI gate all consume the same value. Identity is
/// <c>(category, element)</c>: the analyzer collapses repeats of the same
/// finding into one issue carrying an occurrence count, and ranks the report
/// by <see cref="ImpactScore"/>.
/// </remarks>
public sealed record ClarityIssue
{
    /// <param name="category">Issue kind, e.g. <c>method-name</c>; never blank.</param>
    /// <param name="element">What the issue is about, e.g. <c>OrderService.doIt</c>; never blank.</param>
    /// <param name="suggestion">Concrete advice for fixing it; never blank.</param>
    /// <param name="severity">How badly the identifier scored; defaults to Medium.</param>
    /// <param name="occurrences">How many times this exact finding appeared; at least 1.</param>
    /// <exception cref="ArgumentException">Any text argument is blank, or occurrences is below 1.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="severity"/> is not a defined value.</exception>
    public ClarityIssue(
        string category,
        string element,
        string suggestion,
        ClaritySeverity severity = ClaritySeverity.Medium,
        int occurrences = 1)
    {
        GuardBlank(category, nameof(category));
        GuardBlank(element, nameof(element));
        GuardBlank(suggestion, nameof(suggestion));
        severity.Weight();
        GuardOccurrences(occurrences);
        Category = category;
        Element = element;
        Suggestion = suggestion;
        Severity = severity;
        Occurrences = occurrences;
    }

    /// <summary>Issue kind, e.g. <c>method-name</c>, <c>collocation</c>.</summary>
    public string Category { get; }

    /// <summary>The offending identifier or code site.</summary>
    public string Element { get; }

    /// <summary>Concrete advice for fixing it.</summary>
    public string Suggestion { get; }

    /// <summary>How badly the identifier scored.</summary>
    public ClaritySeverity Severity { get; }

    /// <summary>How many times this exact finding appeared; at least 1.</summary>
    public int Occurrences { get; }

    /// <summary>
    /// Ranking score: severity weight × occurrences.
    /// </summary>
    /// <remarks>
    /// Derived rather than stored, so it can never disagree with the fields it
    /// summarizes. A widespread medium issue therefore outranks a one-off
    /// high-severity one, which is the intent — reports lead with what costs
    /// the most to keep.
    /// </remarks>
    public double ImpactScore => Severity.Weight() * (double)Occurrences;

    /// <summary>Returns the same issue seen <paramref name="occurrences"/> times.</summary>
    /// <exception cref="ArgumentException"><paramref name="occurrences"/> is below 1.</exception>
    public ClarityIssue WithOccurrences(int occurrences)
    {
        GuardOccurrences(occurrences);
        return new ClarityIssue(Category, Element, Suggestion, Severity, occurrences);
    }

    private static void GuardBlank(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{name} must not be blank", name);
        }
    }

    private static void GuardOccurrences(int occurrences)
    {
        if (occurrences < 1)
        {
            throw new ArgumentException(
                $"occurrences must be at least 1: {occurrences}", nameof(occurrences));
        }
    }
}
