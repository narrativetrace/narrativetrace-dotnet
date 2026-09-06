// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Glossary;

/// <summary>
/// One use of a deprecated synonym in code, reported by a harvest run.
/// </summary>
/// <remarks>
/// The run-artifact form of "always use the canonical term" — feeds the
/// console summary, <c>glossary-usage.json</c>, and the
/// <c>non-canonical-term</c> clarity issue. Never written into the committed
/// glossary.
/// </remarks>
public sealed record VocabularyViolation
{
    /// <param name="context">Bounded context in which the alias is deprecated.</param>
    /// <param name="alias">Normalized deprecated phrase observed.</param>
    /// <param name="canonicalTerm">Normalized canonical term to use instead.</param>
    /// <param name="site">Code site of the offending identifier, <c>Class.Method</c> or <c>Class</c>.</param>
    /// <param name="identifier">Raw offending identifier.</param>
    /// <param name="suggestedIdentifier">
    /// Mechanical rename to the canonical term, or null when no contiguous
    /// alias window exists in the identifier.
    /// </param>
    /// <param name="occurrences">Observed uses at this site; at least 1.</param>
    /// <exception cref="ArgumentException">Any required text is blank, or occurrences is below 1.</exception>
    public VocabularyViolation(
        string context, string alias, string canonicalTerm, string site,
        string identifier, string? suggestedIdentifier, int occurrences)
    {
        GuardBlank(context, nameof(context));
        GuardBlank(alias, nameof(alias));
        GuardBlank(canonicalTerm, nameof(canonicalTerm));
        GuardBlank(site, nameof(site));
        GuardBlank(identifier, nameof(identifier));
        if (occurrences < 1)
        {
            throw new ArgumentException(
                $"occurrences must be at least 1: {occurrences}", nameof(occurrences));
        }

        Context = context;
        Alias = alias;
        CanonicalTerm = canonicalTerm;
        Site = site;
        Identifier = identifier;
        SuggestedIdentifier = suggestedIdentifier;
        Occurrences = occurrences;
    }

    /// <summary>Bounded context in which the alias is deprecated.</summary>
    public string Context { get; }

    /// <summary>Normalized deprecated phrase observed.</summary>
    public string Alias { get; }

    /// <summary>Normalized canonical term to use instead.</summary>
    public string CanonicalTerm { get; }

    /// <summary>Code site of the offending identifier, <c>Class.Method</c> or <c>Class</c>.</summary>
    public string Site { get; }

    /// <summary>Raw offending identifier.</summary>
    public string Identifier { get; }

    /// <summary>Mechanical rename to the canonical term, or null when none exists.</summary>
    public string? SuggestedIdentifier { get; }

    /// <summary>Observed uses at this site; at least 1.</summary>
    public int Occurrences { get; }

    private static void GuardBlank(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{name} must not be blank", name);
        }
    }
}
