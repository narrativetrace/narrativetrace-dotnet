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
/// Safety properties of phrase translation: with no glossary coverage the
/// phrase passes through byte-identical (never mangled), and a translation is
/// only ever reported complete when the glossary actually covered it.
/// </summary>
public class GlossaryTranslatorProperties
{
    private static readonly Glossary Empty = new(
        1,
        new Dictionary<string, BoundedContext>
        {
            ["billing"] = new("billing", ["Acme.Billing"]),
        },
        []);

    [Property(Arbitrary = [typeof(PhraseArbitraries)])]
    public void An_uncovered_phrase_passes_through_byte_identical_and_incomplete(string phrase)
    {
        var result = new GlossaryTranslator(Empty).Translate(phrase, "billing", "es");

        Assert.Equal(phrase, result.Text);
        Assert.False(result.Complete);
    }

    internal static class PhraseArbitraries
    {
        public static Arbitrary<string> Phrases()
        {
            var word = from size in Gen.Choose(1, 10)
                       from chars in Gen.Elements(Alphabet()).ListOf(size)
                       select new string([.. chars]);
            return (from size in Gen.Choose(1, 5)
                    from words in word.ListOf(size)
                    select string.Join(" ", words)).ToArbitrary();
        }

        private static IEnumerable<char> Alphabet()
        {
            return Enumerable.Range('a', 26).Select(c => (char)c);
        }
    }
}
