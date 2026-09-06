// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Clarity;

/// <summary>
/// Scores a parameter name for clarity, mirroring the Java model. A
/// multi-token name blends average genericity, abbreviation quality (on a
/// parameter-specific scale), and a domain bonus; a single meaningless token
/// collapses the score.
/// </summary>
public static class ParameterNameScorer
{
    /// <summary>Scores a parameter name for clarity.</summary>
    /// <param name="parameterName">The declared parameter name.</param>
    /// <returns>A score from <c>0.0</c> to <c>1.0</c>; <c>0.0</c> when the name yields no tokens.</returns>
    /// <remarks>
    /// Judged as a noun — a parameter names a thing, so it is not penalized for
    /// lacking a verb. This is also the rubric used for property names.
    /// </remarks>
    /// <param name="vocabulary">
    /// The project's committed glossary vocabulary, which extends both
    /// dictionaries consulted here without overriding either; null scores with
    /// the built-in dictionaries alone.
    /// </param>
    public static double Score(
        string parameterName, DomainVocabulary? vocabulary = null)
    {
        var tokens = IdentifierTokenizer.Tokenize(parameterName);
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
        return GenericTokenDetector.Classify(token, vocabulary) switch
        {
            TokenTier.Meaningless => 0.0,
            TokenTier.Vague => 0.10,
            TokenTier.TypedGeneric => 0.50,
            _ => AbbreviationDictionary.Classify(token, vocabulary) is not null
                ? 0.40 : 0.80,
        };
    }

    private static double ScoreMultiToken(
        IReadOnlyList<string> tokens, DomainVocabulary? vocabulary)
    {
        if (HasMeaningless(tokens, vocabulary))
        {
            return 0.15;
        }

        return Clamp(
            (AverageGenericity(tokens, vocabulary) * 0.45)
            + (ScoreAbbreviations(tokens, vocabulary) * 0.25)
            + (DomainBonus(tokens, vocabulary) * 0.30));
    }

    private static bool HasMeaningless(
        IReadOnlyList<string> tokens, DomainVocabulary? vocabulary)
    {
        for (var i = 0; i < tokens.Count; i++)
        {
            if (GenericTokenDetector.Classify(tokens[i], vocabulary)
                == TokenTier.Meaningless)
            {
                return true;
            }
        }

        return false;
    }

    private static double AverageGenericity(
        IReadOnlyList<string> tokens, DomainVocabulary? vocabulary)
    {
        var sum = 0.0;
        for (var i = 0; i < tokens.Count; i++)
        {
            var tier = GenericTokenDetector.Classify(tokens[i], vocabulary);
            sum += tier == TokenTier.TypedGeneric
                ? 0.9
                : GenericTokenDetector.Score(tier);
        }

        return sum / tokens.Count;
    }

    private static double ScoreAbbreviations(
        IReadOnlyList<string> tokens, DomainVocabulary? vocabulary)
    {
        var sum = 0.0;
        for (var i = 0; i < tokens.Count; i++)
        {
            sum += AbbreviationDictionary.Classify(tokens[i], vocabulary)
                is { } tier
                ? ParamAbbrevScore(tier)
                : 1.0;
        }

        return sum / tokens.Count;
    }

    private static double ParamAbbrevScore(AbbreviationTier tier)
    {
        return tier switch
        {
            AbbreviationTier.Universal => 1.0,
            AbbreviationTier.WellKnown => 0.8,
            _ => 0.5,
        };
    }

    private static double DomainBonus(
        IReadOnlyList<string> tokens, DomainVocabulary? vocabulary)
    {
        for (var i = 0; i < tokens.Count; i++)
        {
            if (GenericTokenDetector.Classify(tokens[i], vocabulary)
                == TokenTier.NotGeneric)
            {
                return 1.0;
            }
        }

        return 0.7;
    }

    private static double Clamp(double value)
    {
        return Math.Max(0.0, Math.Min(1.0, value));
    }
}
