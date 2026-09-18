// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Clarity;

/// <summary>
/// Classifies a name token by genericity, mirroring the Java
/// <c>GenericTokenDetector</c>. First match wins; a single letter a–z is
/// always meaningless. Domain-ness is not judged here — it comes from the
/// verb dictionary and role-suffix dictionary.
/// </summary>
public static class GenericTokenDetector
{
    /// <summary>Classifies how much meaning a single token carries.</summary>
    /// <param name="token">One token from <see cref="IdentifierTokenizer.Tokenize"/>.</param>
    /// <returns>The token's genericity tier.</returns>
    /// <remarks>
    /// Judges the token alone, with no view of the identifier it came from — so
    /// <c>data</c> is <see cref="TokenTier.Meaningless"/> even inside an
    /// otherwise excellent name. The composite scorers are what weigh that back
    /// into context.
    /// </remarks>
    /// <param name="vocabulary">
    /// The project's declared vocabulary, or null for the built-in tiers alone.
    /// Meaningless placeholders are decided first, so committing <c>temp</c> or
    /// <c>foo</c> to a glossary cannot make them meaningful; every other tier
    /// yields to the project, because a declared noun is domain vocabulary by
    /// definition.
    /// </param>
    public static TokenTier Classify(
        string token, DomainVocabulary? vocabulary = null)
    {
        var lower = token.ToLowerInvariant();

        if (IsMeaninglessSingleLetter(lower)
            || GenericTokenVocabulary.MeaninglessPlaceholders.Contains(lower))
        {
            return TokenTier.Meaningless;
        }

        if (vocabulary is not null && vocabulary.IsDomainNoun(lower))
        {
            return TokenTier.NotGeneric;
        }

        if (GenericTokenVocabulary.VagueWords.Contains(lower))
        {
            return TokenTier.Vague;
        }

        if (GenericTokenVocabulary.TypedGenericWords.Contains(lower))
        {
            return TokenTier.TypedGeneric;
        }

        return TokenTier.NotGeneric;
    }

    /// <summary>Maps a tier to its contribution to a name's score.</summary>
    /// <param name="tier">The tier to weight.</param>
    /// <returns>
    /// <c>0.0</c> for <see cref="TokenTier.Meaningless"/>, <c>0.2</c> for
    /// <see cref="TokenTier.Vague"/>, <c>0.5</c> for
    /// <see cref="TokenTier.TypedGeneric"/>, and <c>1.0</c> otherwise. These
    /// weights are a cross-language contract shared with every NarrativeTrace
    /// runtime — changing one changes every published score.
    /// </returns>
    public static double Score(TokenTier tier)
    {
        return tier switch
        {
            TokenTier.Meaningless => 0.0,
            TokenTier.Vague => 0.2,
            TokenTier.TypedGeneric => 0.5,
            _ => 1.0,
        };
    }

    private static bool IsMeaninglessSingleLetter(string lower)
    {
        return lower.Length == 1 && lower[0] is >= 'a' and <= 'z';
    }
}
