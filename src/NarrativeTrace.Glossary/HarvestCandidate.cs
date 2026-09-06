// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Glossary;

/// <summary>
/// One aggregated observation of a normalized phrase at a code site during
/// harvesting.
/// </summary>
/// <remarks>
/// The bridge between traces and the glossary. The merger turns unseen
/// <c>(context, phrase)</c> pairs into new terms; alias hits become
/// vocabulary violations; everything else is usage-report material only.
/// </remarks>
public sealed record HarvestCandidate
{
    /// <param name="context">Bounded context resolved from the declaring class's namespace.</param>
    /// <param name="phrase">Normalized phrase produced by <see cref="TermNormalizer"/>.</param>
    /// <param name="kind">Grammatical shape of the phrase.</param>
    /// <param name="site">Observed code site, <c>Class.Method</c> or <c>Class</c>.</param>
    /// <param name="identifier">Raw identifier the phrase was normalized from.</param>
    /// <param name="occurrences">How many times this exact observation appeared; at least 1.</param>
    /// <exception cref="ArgumentException">Any text argument is blank, or occurrences is below 1.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> is not a defined value.</exception>
    public HarvestCandidate(
        string context, string phrase, TermKind kind,
        string site, string identifier, int occurrences)
    {
        GuardBlank(context, nameof(context));
        GuardBlank(phrase, nameof(phrase));
        kind.JsonName();
        GuardBlank(site, nameof(site));
        GuardBlank(identifier, nameof(identifier));
        if (occurrences < 1)
        {
            throw new ArgumentException(
                $"occurrences must be at least 1: {occurrences}", nameof(occurrences));
        }

        Context = context;
        Phrase = phrase;
        Kind = kind;
        Site = site;
        Identifier = identifier;
        Occurrences = occurrences;
    }

    /// <summary>Bounded context resolved from the declaring class's namespace.</summary>
    public string Context { get; }

    /// <summary>Normalized phrase produced by <see cref="TermNormalizer"/>.</summary>
    public string Phrase { get; }

    /// <summary>Grammatical shape of the phrase.</summary>
    public TermKind Kind { get; }

    /// <summary>Observed code site, <c>Class.Method</c> or <c>Class</c>.</summary>
    public string Site { get; }

    /// <summary>Raw identifier the phrase was normalized from.</summary>
    public string Identifier { get; }

    /// <summary>How many times this exact observation appeared; at least 1.</summary>
    public int Occurrences { get; }

    private static void GuardBlank(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{name} must not be blank", name);
        }
    }
}
