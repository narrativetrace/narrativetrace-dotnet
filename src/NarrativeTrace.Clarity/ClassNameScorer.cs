// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Clarity;

/// <summary>
/// Scores a class name for clarity, mirroring the Java model. A multi-token
/// name blends role-suffix quality, prefix specificity, abbreviation quality,
/// token count, and morphology of the suffix.
/// </summary>
public static class ClassNameScorer
{
    /// <summary>Scores a class name for clarity.</summary>
    /// <param name="className">
    /// The simple class name. Pass the final segment, not a namespace-qualified
    /// name.
    /// </param>
    /// <returns>A score from <c>0.0</c> to <c>1.0</c>; <c>0.0</c> when the name yields no tokens.</returns>
    /// <remarks>
    /// Judged on the noun rubric plus its role suffix, so a
    /// <see cref="RoleSuffixCategory.Generic"/> ending such as <c>Manager</c>
    /// costs the name even when the rest of it is specific.
    /// </remarks>
    /// <param name="vocabulary">
    /// The project's committed glossary vocabulary, which extends every
    /// dictionary consulted here without overriding any of them; null scores
    /// with the built-in dictionaries alone.
    /// </param>
    public static double Score(
        string className, DomainVocabulary? vocabulary = null)
    {
        var tokens = IdentifierTokenizer.Tokenize(className);
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
        return RoleSuffixDictionary.Classify(token) switch
        {
            RoleSuffixCategory.Generic => 0.05,
            RoleSuffixCategory.DesignPattern => 0.10,
            RoleSuffixCategory.Functional => 0.10,
            _ => MorphologyAnalyzer.Analyze(token, vocabulary)
                == PartOfSpeech.Adjective ? 0.85 : 0.75,
        };
    }

    private static double ScoreMultiToken(
        IReadOnlyList<string> tokens, DomainVocabulary? vocabulary)
    {
        var last = tokens[tokens.Count - 1];
        return Clamp(
            (ScoreRoleSuffix(last) * 0.50)
            + (ScorePrefixQuality(tokens, vocabulary) * 0.20)
            + (ScoreAbbreviations(tokens, vocabulary) * 0.10)
            + (ScoreTokenCount(tokens.Count) * 0.10)
            + (ScoreMorphology(last, vocabulary) * 0.10));
    }

    private static double ScoreRoleSuffix(string suffix)
    {
        return RoleSuffixDictionary.Classify(suffix) switch
        {
            RoleSuffixCategory.DesignPattern => 1.0,
            RoleSuffixCategory.Functional => 1.0,
            RoleSuffixCategory.Generic => 0.3,
            _ => 0.8,
        };
    }

    private static double ScorePrefixQuality(
        IReadOnlyList<string> tokens, DomainVocabulary? vocabulary)
    {
        var count = tokens.Count - 1;
        if (count == 0)
        {
            return 0.0;
        }

        var sum = 0.0;
        for (var i = 0; i < count; i++)
        {
            sum += PrefixTierScore(
                GenericTokenDetector.Classify(tokens[i], vocabulary));
        }

        return sum / count;
    }

    private static double PrefixTierScore(TokenTier tier)
    {
        return tier switch
        {
            TokenTier.Meaningless => 0.0,
            TokenTier.Vague => 0.2,
            TokenTier.TypedGeneric => 0.7,
            _ => 1.0,
        };
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
        if (count is 2 or 3)
        {
            return 1.0;
        }

        return count == 1 ? 0.5 : Math.Max(0.0, 0.9 - (0.1 * (count - 3)));
    }

    private static double ScoreMorphology(
        string suffix, DomainVocabulary? vocabulary)
    {
        return MorphologyAnalyzer.Analyze(suffix, vocabulary) switch
        {
            PartOfSpeech.Noun => 1.0,
            PartOfSpeech.Adjective => 0.9,
            PartOfSpeech.Verb => 0.3,
            _ => 0.7,
        };
    }

    private static double Clamp(double value)
    {
        return Math.Max(0.0, Math.Min(1.0, value));
    }
}
