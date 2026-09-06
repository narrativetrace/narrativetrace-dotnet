// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Clarity;

/// <summary>
/// Scores a method name for clarity, mirroring the Java model. A multi-token
/// name blends verb quality, token specificity, abbreviation quality, token
/// count, and morphology.
/// </summary>
public static class MethodNameScorer
{
    private static readonly string[] VerbSuffixes =
        ["ate", "ize", "ise", "ify", "en"];

    /// <summary>Scores a method name for clarity.</summary>
    /// <param name="methodName">
    /// The bare method name, not a qualified one — a dotted name tokenizes
    /// poorly and scores low for the wrong reason.
    /// </param>
    /// <returns>
    /// A score from <c>0.0</c> to <c>1.0</c>. A name that yields no tokens
    /// (empty, or punctuation only) scores <c>0.0</c>.
    /// </returns>
    /// <remarks>
    /// Single-token names are judged on a different rubric from multi-token
    /// ones — a lone verb such as <c>Save</c> is not penalized for lacking the
    /// noun that <c>SaveOrder</c> supplies.
    /// </remarks>
    /// <param name="vocabulary">
    /// The project's committed glossary vocabulary, which extends every
    /// dictionary consulted here without overriding any of them; null scores
    /// with the built-in dictionaries alone.
    /// </param>
    public static double Score(
        string methodName, DomainVocabulary? vocabulary = null)
    {
        var tokens = IdentifierTokenizer.Tokenize(methodName);
        if (tokens.Count == 0)
        {
            return 0.0;
        }

        return tokens.Count == 1
            ? ScoreSingleToken(tokens[0], vocabulary)
            : ScoreMultiToken(tokens, vocabulary);
    }

    private static double ScoreSingleToken(
        string token, DomainVocabulary? vocabulary)
    {
        var category = VerbDictionary.Classify(token, vocabulary);
        var @base = category switch
        {
            VerbCategory.Generic => 0.10,
            VerbCategory.Boolean => 0.40,
            VerbCategory.Standard => 0.45,
            VerbCategory.Domain => 0.60,
            _ => MorphologyAnalyzer.Analyze(token, vocabulary)
                == PartOfSpeech.Verb ? 0.55 : 0.50,
        };

        if (AbbreviationDictionary.Classify(token, vocabulary) is { } tier)
        {
            @base *= AbbreviationDictionary.TierScore(tier);
        }

        return Clamp(@base);
    }

    private static double ScoreMultiToken(
        IReadOnlyList<string> tokens, DomainVocabulary? vocabulary)
    {
        return Clamp(
            (ScoreVerbQuality(tokens[0], vocabulary) * 0.45)
            + (ScoreTokenSpecificity(tokens, vocabulary) * 0.15)
            + (ScoreAbbreviations(tokens, vocabulary) * 0.10)
            + (ScoreTokenCount(tokens.Count) * 0.15)
            + (ScoreMorphology(tokens[0], vocabulary) * 0.15));
    }

    private static double ScoreVerbQuality(
        string firstToken, DomainVocabulary? vocabulary)
    {
        var category = VerbDictionary.Classify(firstToken, vocabulary);
        return category switch
        {
            VerbCategory.Domain => 1.0,
            VerbCategory.Boolean => 1.0,
            VerbCategory.Standard => 0.8,
            VerbCategory.Generic => 0.4,
            _ => MorphologyAnalyzer.Analyze(firstToken, vocabulary)
                == PartOfSpeech.Verb ? 0.6 : 0.4,
        };
    }

    private static double ScoreTokenSpecificity(
        IReadOnlyList<string> tokens, DomainVocabulary? vocabulary)
    {
        var sum = 0.0;
        for (var i = 1; i < tokens.Count; i++)
        {
            sum += GenericTokenDetector.Score(
                GenericTokenDetector.Classify(tokens[i], vocabulary));
        }

        return tokens.Count > 1 ? sum / (tokens.Count - 1) : 0.0;
    }

    private static double ScoreAbbreviations(
        IReadOnlyList<string> tokens, DomainVocabulary? vocabulary)
    {
        var sum = 0.0;
        for (var i = 0; i < tokens.Count; i++)
        {
            sum += AbbreviationDictionary.Classify(tokens[i], vocabulary)
                is { } tier
                ? AbbreviationDictionary.TierScore(tier)
                : 1.0;
        }

        return tokens.Count > 0 ? sum / tokens.Count : 1.0;
    }

    private static double ScoreTokenCount(int count)
    {
        if (count is >= 2 and <= 4)
        {
            return 1.0;
        }

        return count == 1 ? 0.6 : Math.Max(0.0, 0.7 - (0.15 * (count - 4)));
    }

    private static double ScoreMorphology(
        string firstToken, DomainVocabulary? vocabulary)
    {
        var category = VerbDictionary.Classify(firstToken, vocabulary);
        var inDictionary = category != VerbCategory.Unknown;
        if (category == VerbCategory.Generic)
        {
            return 0.3;
        }

        var hasSuffix = HasVerbSuffix(firstToken);
        if (inDictionary && hasSuffix)
        {
            return 1.0;
        }

        return inDictionary || hasSuffix ? 0.8 : 0.3;
    }

    private static bool HasVerbSuffix(string token)
    {
        if (token.Length < 4)
        {
            return false;
        }

        for (var i = 0; i < VerbSuffixes.Length; i++)
        {
            if (token.Length > VerbSuffixes[i].Length
                && token.EndsWith(VerbSuffixes[i], StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static double Clamp(double value)
    {
        return Math.Max(0.0, Math.Min(1.0, value));
    }
}
