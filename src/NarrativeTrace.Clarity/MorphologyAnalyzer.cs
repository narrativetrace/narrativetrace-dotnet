// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Clarity;

/// <summary>
/// Infers a token's part of speech, mirroring the Java clarity model: any
/// dictionary verb is a verb; otherwise short tokens are unknown and longer
/// tokens are matched by suffix in priority order verb → noun → adjective.
/// </summary>
public static class MorphologyAnalyzer
{
    private const int MinSuffixLength = 4;

    private static readonly string[] VerbSuffixes =
        ["ate", "ize", "ise", "ify", "en"];

    private static readonly string[] NounSuffixes =
        ["tion", "sion", "ment", "ness", "ity", "ance", "ence",
         "er", "or", "ary", "ery", "ory"];

    private static readonly string[] AdjectiveSuffixes =
        ["able", "ible", "ive", "ous", "al", "ic", "ed"];

    /// <summary>Infers a token's part of speech from its shape and suffix.</summary>
    /// <param name="token">One token from <see cref="IdentifierTokenizer.Tokenize"/>.</param>
    /// <returns>
    /// The inferred role, or <see cref="PartOfSpeech.Unknown"/> when the shape is
    /// not conclusive — which is common and is treated as absence of signal
    /// rather than as a fault in the name.
    /// </returns>
    /// <remarks>
    /// Suffix-driven and English-oriented, with no dictionary and no context, so
    /// it is a heuristic: a domain term that happens to end like a verb is
    /// classified as one.
    /// </remarks>
    /// <param name="vocabulary">
    /// The project's declared vocabulary, or null for the built-in dictionaries
    /// alone. Verb detection defers to <see cref="VerbDictionary"/>, so a
    /// project's declared verbs read as verbs here too.
    /// </param>
    public static PartOfSpeech Analyze(
        string token, DomainVocabulary? vocabulary = null)
    {
        var lower = token.ToLowerInvariant();

        if (VerbDictionary.Classify(lower, vocabulary) != VerbCategory.Unknown)
        {
            return PartOfSpeech.Verb;
        }

        return lower.Length < MinSuffixLength
            ? PartOfSpeech.Unknown
            : ClassifyBySuffix(lower);
    }

    private static PartOfSpeech ClassifyBySuffix(string lower)
    {
        if (MatchesAny(lower, VerbSuffixes))
        {
            return PartOfSpeech.Verb;
        }

        if (MatchesAny(lower, NounSuffixes))
        {
            return PartOfSpeech.Noun;
        }

        return MatchesAny(lower, AdjectiveSuffixes)
            ? PartOfSpeech.Adjective
            : PartOfSpeech.Unknown;
    }

    private static bool MatchesAny(string word, string[] suffixes)
    {
        for (var i = 0; i < suffixes.Length; i++)
        {
            if (word.Length > suffixes[i].Length
                && word.EndsWith(suffixes[i], StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
