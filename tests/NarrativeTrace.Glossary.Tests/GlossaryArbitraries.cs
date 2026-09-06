// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using FsCheck;
using FsCheck.Fluent;
using NarrativeTrace.Glossary;

namespace NarrativeTrace.Glossary.Tests;

/// <summary>
/// Shared FsCheck generators for structurally valid glossaries.
/// </summary>
/// <remarks>
/// Generation is constructive — every sample satisfies the
/// <see cref="Glossary"/> invariants by design: term texts are globally
/// unique, and alias texts always end in <c>" alias"</c> while canonical
/// texts never do, so alias/term disjointness holds without filtering.
/// </remarks>
internal static class GlossaryArbitraries
{
    private static readonly string[] ContextPool =
        ["billing", "support", "shipping", "_unassigned"];

    private static readonly string[] TermPool =
        ["alpha", "beta", "overdraft account", "payment plan",
         "café crédit", "say \"hi\"\tterm"];

    public static Arbitrary<Glossary> Glossaries()
    {
        return GlossaryGen().ToArbitrary();
    }

    public static Arbitrary<HarvestResult> Harvests()
    {
        return HarvestGen().ToArbitrary();
    }

    /// <summary>Candidate phrases overlap glossary term and alias texts to exercise every merge branch.</summary>
    private static Gen<HarvestResult> HarvestGen()
    {
        var candidate =
            from context in Gen.Elements(
                "billing", "support", "shipping", "_unassigned", "warehouse")
            from phrase in Gen.Elements(
                "alpha", "beta", "overdraft account", "payment plan",
                "old phrasing alias", "legacy wording alias", "fresh term")
            from kind in Gen.Elements(
                TermKind.Word, TermKind.NounPhrase, TermKind.VerbPhrase, TermKind.Template)
            from site in Gen.Elements("A.a", "B.b", "C.c")
            from identifier in Gen.Elements("someIdentifier", "otherIdentifier")
            select new HarvestCandidate(context, phrase, kind, site, identifier, 1);
        return from size in Gen.Choose(0, 8)
               from candidates in candidate.ListOf(size)
               select new HarvestResult(
                   candidates
                       .GroupBy(c => (c.Context, c.Phrase, c.Kind, c.Site))
                       .Select(bucket => bucket.First())
                       .ToList());
    }

    private static Gen<Glossary> GlossaryGen()
    {
        return from contextMask in Gen.Choose(1, (1 << ContextPool.Length) - 1)
               from termMask in Gen.Choose(0, (1 << TermPool.Length) - 1)
               let names = Mask(ContextPool, contextMask)
               from terms in Sequence(
                   Mask(TermPool, termMask).Select(text => TermGen(text, names)).ToList())
               from abbreviations in AbbreviationsGen()
               select new Glossary(
                   1,
                   names.ToDictionary(n => n, Context, StringComparer.Ordinal),
                   terms,
                   abbreviations);
    }

    /// <summary>
    /// Accepted shorthand, including the empty section — the case whose file
    /// must stay byte-identical to its pre-schema-2 form.
    /// </summary>
    private static Gen<Dictionary<string, string>> AbbreviationsGen()
    {
        (string Token, string Expansion)[] pool =
            [("fx", "foreign exchange"), ("calc", "calculate"), ("p&l", "profit \u0026 \"loss\"")];
        return Gen.Choose(0, (1 << pool.Length) - 1).Select(mask =>
            Mask(pool, mask).ToDictionary(
                pair => pair.Token, pair => pair.Expansion, StringComparer.Ordinal));
    }

    private static BoundedContext Context(string name)
    {
        var packages = name.StartsWith('_')
            ? Array.Empty<string>()
            : [$"Acme.{char.ToUpperInvariant(name[0])}{name.Substring(1)}"];
        return new BoundedContext(name, packages, $"Context \"{name}\"");
    }

    private static Gen<GlossaryTerm> TermGen(string text, IReadOnlyList<string> contexts)
    {
        return from context in Gen.Elements(contexts.ToArray())
               from kind in Gen.Elements(
                   TermKind.Word, TermKind.NounPhrase, TermKind.VerbPhrase, TermKind.Template)
               from status in Gen.Elements(
                   TermStatus.Harvested, TermStatus.Curated, TermStatus.Stale)
               from definition in Gen.Elements("Means something.", "Multi\nline é", "x", null)
               from translations in TranslationsGen()
               from synonyms in SynonymsGen()
               from sourcesMask in Gen.Choose(0, 3)
               from firstSeen in Gen.Elements(
                   new DateTime(2026, 8, 11, 0, 0, 0, DateTimeKind.Utc),
                   new DateTime(2025, 1, 31, 0, 0, 0, DateTimeKind.Utc))
               select new GlossaryTerm(
                   text, context, kind, status, definition, translations, synonyms,
                   Mask(["a.B.c", "d.E.f"], sourcesMask), firstSeen);
    }

    private static Gen<Dictionary<string, string>> TranslationsGen()
    {
        (string Locale, string Text)[] pool = [("es", "uno"), ("de", "dós"), ("fr", "z\"z")];
        return Gen.Choose(0, (1 << pool.Length) - 1).Select(mask =>
            Mask(pool, mask).ToDictionary(
                pair => pair.Locale, pair => pair.Text, StringComparer.Ordinal));
    }

    private static Gen<List<SynonymAlias>> SynonymsGen()
    {
        string[] pool = ["old phrasing", "legacy wording"];
        return from mask in Gen.Choose(0, (1 << pool.Length) - 1)
               from note in Gen.Elements("historic", "renamed 2025", null)
               select Mask(pool, mask)
                   .Select(alias => new SynonymAlias($"{alias} alias", note))
                   .ToList();
    }

    /// <summary>Deterministic subset selection by bitmask, preserving pool order.</summary>
    private static List<T> Mask<T>(IReadOnlyList<T> pool, int mask)
    {
        return pool.Where((_, index) => (mask & (1 << index)) != 0).ToList();
    }

    /// <summary>Chains independent generators into one list-valued generator.</summary>
    private static Gen<List<T>> Sequence<T>(IReadOnlyList<Gen<T>> gens)
    {
        var acc = Gen.Constant(new List<T>());
        foreach (var gen in gens)
        {
            var current = gen;
            acc = acc.SelectMany(list => current.Select(
                item => list.Concat([item]).ToList()));
        }

        return acc;
    }
}
