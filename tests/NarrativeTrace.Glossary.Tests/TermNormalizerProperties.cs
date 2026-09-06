// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using NarrativeTrace.Glossary;
using Xunit;

namespace NarrativeTrace.Glossary.Tests;

/// <summary>
/// Safety property: normalization converges — camelCase, PascalCase, and
/// snake_case spellings of the same words all produce one normalized phrase.
/// </summary>
public class TermNormalizerProperties
{
    [Property(Arbitrary = [typeof(WordListArbitraries)])]
    public void All_spellings_of_the_same_words_converge_to_one_phrase(List<string> words)
    {
        var camel = CamelCase(words);
        var pascal = Capitalize(camel);
        var snake = string.Join("_", words);

        var fromCamel = TermNormalizer.Phrase(camel);

        Assert.Equal(fromCamel, TermNormalizer.Phrase(pascal));
        Assert.Equal(fromCamel, TermNormalizer.Phrase(snake));
        Assert.Equal(fromCamel.ToLowerInvariant(), fromCamel);
    }

    [Property(Arbitrary = [typeof(WordListArbitraries)])]
    public void Normalization_is_idempotent_on_its_own_output(List<string> words)
    {
        var once = TermNormalizer.Phrase(CamelCase(words));

        var again = TermNormalizer.Phrase(once.Replace(' ', '_'));

        Assert.Equal(once, again);
    }

    /// <summary>
    /// Every emitted token is a fixpoint of singularization, so re-normalizing
    /// any output is the identity. Pins the bug class behind the 2026-08-13
    /// singularizer fix: a rule that coins a stem it would itself re-strip
    /// (<c>alias</c> → <c>alia</c>) breaks term identity in persisted
    /// glossaries.
    /// </summary>
    [Property(Arbitrary = [typeof(TokenArbitraries)])]
    public void Normalization_is_idempotent_on_arbitrary_tokens(string token)
    {
        var once = TermNormalizer.Phrase(token);

        Assert.Equal(once, TermNormalizer.Phrase(once.Replace(' ', '_')));
    }

    private static string CamelCase(List<string> words)
    {
        return words[0] + string.Concat(words.Skip(1).Select(Capitalize));
    }

    private static string Capitalize(string word)
    {
        return char.ToUpperInvariant(word[0]) + word.Substring(1);
    }

    internal static class WordListArbitraries
    {
        public static Arbitrary<List<string>> WordLists()
        {
            var word = Gen.Elements(
                "account", "overdraft", "with", "payment", "plans", "entries",
                "status", "boxes", "customer", "charge", "insufficient", "funds",
                "limit", "for", "aliases", "gases", "series", "cases",
                "responses", "lenses", "news", "species", "always");
            return (from size in Gen.Choose(1, 4)
                    from words in word.ListOf(size)
                    select words.ToList()).ToArbitrary();
        }
    }

    /// <summary>
    /// Plural-shaped random tokens: an arbitrary stem followed by one of the
    /// suffixes the singularizer reacts to. A uniformly random string would
    /// almost never end in <c>-ses</c>, making the idempotence property
    /// vacuous — the suffix is what puts the rules under test.
    /// </summary>
    internal static class TokenArbitraries
    {
        private static readonly string[] PluralSuffixes =
            ["", "s", "es", "ses", "ies", "xes", "zes", "ches", "shes", "ss", "us", "is"];

        public static Arbitrary<string> Tokens()
        {
            return (from size in Gen.Choose(1, 8)
                    from chars in Gen.Elements(Alphabet()).ListOf(size)
                    from suffix in Gen.Elements(PluralSuffixes)
                    select new string([.. chars]) + suffix).ToArbitrary();
        }

        private static IEnumerable<char> Alphabet()
        {
            return Enumerable.Range('a', 26).Select(c => (char)c);
        }
    }
}
