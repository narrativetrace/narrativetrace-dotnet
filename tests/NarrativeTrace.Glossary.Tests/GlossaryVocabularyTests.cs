// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Clarity;
using Xunit;

namespace NarrativeTrace.Glossary.Tests;

/// <summary>
/// The committed glossary as clarity's vocabulary: what it teaches the scorers,
/// and what it must never contribute.
/// </summary>
public class GlossaryVocabularyTests
{
    private static readonly DateTime FirstSeen =
        new(2026, 8, 11, 0, 0, 0, DateTimeKind.Utc);

    private const string GlossaryJson = """
        {
          "schemaVersion": 1,
          "contexts": {"trading": {"packages": ["Acme.Trading"]}},
          "terms": [
            {
              "term": "settle trade",
              "context": "trading",
              "kind": "verb-phrase",
              "status": "curated",
              "firstSeen": "2020-01-01"
            }
          ]
        }
        """;

    private static GlossaryTerm Term(
        string text, TermKind kind, TermStatus status, params SynonymAlias[] synonyms)
    {
        return new GlossaryTerm(
            text, "trading", kind, status,
            null, new Dictionary<string, string>(), synonyms, [], FirstSeen);
    }

    private static Glossary GlossaryOf(params GlossaryTerm[] terms)
    {
        return new Glossary(
            1,
            new Dictionary<string, BoundedContext>(StringComparer.Ordinal)
            {
                ["trading"] = new BoundedContext("trading", ["Acme.Trading"]),
            },
            terms);
    }

    [Fact]
    public void A_verb_phrase_declares_its_leading_verb_and_trailing_nouns()
    {
        var vocabulary = GlossaryVocabulary.Of(
            GlossaryOf(Term("settle trade", TermKind.VerbPhrase, TermStatus.Curated)));

        Assert.True(vocabulary.IsDomainVerb("settle"));
        Assert.True(vocabulary.IsDomainNoun("trade"));
        Assert.False(vocabulary.IsDomainNoun("settle"));
        Assert.False(vocabulary.IsDomainVerb("trade"));
    }

    [Fact]
    public void A_noun_phrase_declares_every_token_as_a_noun()
    {
        var vocabulary = GlossaryVocabulary.Of(
            GlossaryOf(Term("credit tranche", TermKind.NounPhrase, TermStatus.Curated)));

        Assert.True(vocabulary.IsDomainNoun("credit"));
        Assert.True(vocabulary.IsDomainNoun("tranche"));
        Assert.Empty(vocabulary.Verbs);
    }

    [Fact]
    public void A_word_declares_itself_as_a_noun_but_not_as_shorthand()
    {
        var vocabulary = GlossaryVocabulary.Of(
            GlossaryOf(Term("fx", TermKind.Word, TermStatus.Harvested)));

        Assert.True(vocabulary.IsDomainNoun("fx"));
        Assert.False(vocabulary.IsAcceptedAbbreviation("fx"));
    }

    [Fact]
    public void The_abbreviations_section_carries_its_expansion_into_the_vocabulary()
    {
        var glossary = new Glossary(
            2,
            new Dictionary<string, BoundedContext>(StringComparer.Ordinal)
            {
                ["trading"] = new BoundedContext("trading", ["Acme.Trading"]),
            },
            [],
            new Dictionary<string, string> { ["fx"] = "foreign exchange" });

        var vocabulary = GlossaryVocabulary.Of(glossary);

        Assert.True(vocabulary.IsAcceptedAbbreviation("fx"));
        Assert.Equal("foreign exchange", vocabulary.ExpansionOf("fx"));
        Assert.False(vocabulary.IsDomainNoun("fx"));
    }

    /// <summary>
    /// The item-43 defect, inverted: a committed phrase must not accept its
    /// own tokens as shorthand.
    /// </summary>
    [Fact]
    public void A_committed_phrase_does_not_accept_its_tokens_as_shorthand()
    {
        var vocabulary = GlossaryVocabulary.Of(
            GlossaryOf(Term("calc total", TermKind.NounPhrase, TermStatus.Harvested)));

        Assert.True(vocabulary.IsDomainNoun("calc"));
        Assert.False(vocabulary.IsAcceptedAbbreviation("calc"));
    }

    [Fact]
    public void Harvested_terms_count_because_the_commit_is_the_approval()
    {
        var vocabulary = GlossaryVocabulary.Of(
            GlossaryOf(Term("fold position", TermKind.VerbPhrase, TermStatus.Harvested)));

        Assert.True(vocabulary.IsDomainVerb("fold"));
    }

    [Fact]
    public void Stale_terms_are_no_longer_vocabulary()
    {
        var vocabulary = GlossaryVocabulary.Of(
            GlossaryOf(Term("unwind position", TermKind.VerbPhrase, TermStatus.Stale)));

        Assert.True(vocabulary.IsEmpty);
    }

    [Fact]
    public void Template_entries_are_narration_text_not_vocabulary()
    {
        var vocabulary = GlossaryVocabulary.Of(
            GlossaryOf(Term("settled {amount}", TermKind.Template, TermStatus.Curated)));

        Assert.True(vocabulary.IsEmpty);
    }

    [Fact]
    public void Deprecated_synonyms_never_become_vocabulary()
    {
        var vocabulary = GlossaryVocabulary.Of(
            GlossaryOf(Term(
                "tranche", TermKind.Word, TermStatus.Curated,
                new SynonymAlias("slice"))));

        Assert.True(vocabulary.IsDomainNoun("tranche"));
        Assert.False(vocabulary.IsDomainNoun("slice"));
        Assert.False(vocabulary.IsAcceptedAbbreviation("slice"));
    }

    [Fact]
    public void Every_bounded_context_contributes()
    {
        var contexts = new Dictionary<string, BoundedContext>(StringComparer.Ordinal)
        {
            ["trading"] = new BoundedContext("trading", ["Acme.Trading"]),
            ["billing"] = new BoundedContext("billing", ["Acme.Billing"]),
        };
        var billing = new GlossaryTerm(
            "invoice", "billing", TermKind.Word, TermStatus.Curated,
            null, new Dictionary<string, string>(), [], [], FirstSeen);

        var vocabulary = GlossaryVocabulary.Of(new Glossary(
            1, contexts, [Term("tranche", TermKind.Word, TermStatus.Curated), billing]));

        Assert.True(vocabulary.IsDomainNoun("tranche"));
        Assert.True(vocabulary.IsDomainNoun("invoice"));
    }

    [Fact]
    public void Rejects_a_null_glossary()
    {
        Assert.Throws<ArgumentNullException>(() => GlossaryVocabulary.Of(null!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void No_path_means_no_committed_vocabulary(string? path)
    {
        Assert.True(GlossaryVocabulary.FromFile(path).IsEmpty);
    }

    [Fact]
    public void A_missing_file_means_no_committed_vocabulary()
    {
        var directory = TempDirectory();

        Assert.True(GlossaryVocabulary.FromFile(
            Path.Combine(directory, "glossary.json")).IsEmpty);
        Assert.True(GlossaryVocabulary.From(directory).IsEmpty);
        Assert.True(GlossaryVocabulary.From(null).IsEmpty);
    }

    [Fact]
    public void Reads_the_committed_glossary_from_its_file()
    {
        var directory = TempDirectory();
        var path = Path.Combine(directory, "glossary.json");
        File.WriteAllText(path, GlossaryJson);

        foreach (var vocabulary in new[]
        {
            GlossaryVocabulary.FromFile(path), GlossaryVocabulary.From(directory),
        })
        {
            Assert.True(vocabulary.IsDomainVerb("settle"));
            Assert.True(vocabulary.IsDomainNoun("trade"));
        }
    }

    [Fact]
    public void A_malformed_glossary_fails_loudly_rather_than_scoring_without_it()
    {
        var path = Path.Combine(TempDirectory(), "glossary.json");
        File.WriteAllText(path, "{ not json");

        Assert.ThrowsAny<ArgumentException>(
            () => GlossaryVocabulary.FromFile(path));
    }

    private static string TempDirectory()
    {
        var directory = Path.Combine(
            Path.GetTempPath(), "narrativetrace-glossary-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
