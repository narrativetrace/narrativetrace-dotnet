// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using Xunit;

namespace NarrativeTrace.Clarity.Tests;

/// <summary>
/// The project's own vocabulary: what a committed glossary teaches the scorers,
/// and what it must never override.
/// </summary>
public class DomainVocabularyTests
{
    [Fact]
    public void Empty_vocabulary_knows_nothing()
    {
        Assert.True(DomainVocabulary.Empty.IsEmpty);
        Assert.False(DomainVocabulary.Empty.IsDomainVerb("fold"));
        Assert.False(DomainVocabulary.Empty.IsDomainNoun("tranche"));
        Assert.False(DomainVocabulary.Empty.IsAcceptedAbbreviation("fx"));
        Assert.Null(DomainVocabulary.Empty.ExpansionOf("fx"));
    }

    [Fact]
    public void Recognizes_declared_verbs_and_nouns_and_keeps_them_apart()
    {
        var vocabulary = DomainVocabulary.Of(["fold"], ["tranche"]);

        Assert.True(vocabulary.IsDomainVerb("fold"));
        Assert.True(vocabulary.IsDomainNoun("tranche"));
        Assert.False(vocabulary.IsDomainVerb("tranche"));
        Assert.False(vocabulary.IsDomainNoun("fold"));
    }

    [Fact]
    public void Either_half_populated_is_not_empty()
    {
        Assert.False(DomainVocabulary.Of(["fold"], []).IsEmpty);
        Assert.False(DomainVocabulary.Of([], ["tranche"]).IsEmpty);
        Assert.True(DomainVocabulary.Of(["credit tranche"], [" "]).IsEmpty);
    }

    [Fact]
    public void Only_the_abbreviations_section_declares_shorthand()
    {
        var vocabulary = DomainVocabulary.Of(
            ["calc"], ["fx"], new Dictionary<string, string> { ["fx"] = "foreign exchange" });

        Assert.True(vocabulary.IsAcceptedAbbreviation("fx"));
        Assert.Equal("foreign exchange", vocabulary.ExpansionOf("fx"));
        Assert.False(vocabulary.IsAcceptedAbbreviation("calc"));
        Assert.False(vocabulary.IsAcceptedAbbreviation("mgr"));
        Assert.Null(vocabulary.ExpansionOf("calc"));
    }

    [Fact]
    public void An_accepted_abbreviation_is_vocabulary_even_without_a_matching_term()
    {
        var vocabulary = DomainVocabulary.Of(
            [], [], new Dictionary<string, string> { ["fx"] = "foreign exchange" });

        Assert.False(vocabulary.IsEmpty);
    }

    [Fact]
    public void Matches_regardless_of_case()
    {
        var vocabulary = DomainVocabulary.Of(["Fold"], ["Tranche"]);

        Assert.True(vocabulary.IsDomainVerb("FOLD"));
        Assert.True(vocabulary.IsDomainNoun("tranche"));
        Assert.True(
            DomainVocabulary
                .Of([], [], new Dictionary<string, string> { ["FX"] = "foreign exchange" })
                .IsAcceptedAbbreviation("fx"));
    }

    [Fact]
    public void Drops_blank_and_multi_word_entries()
    {
        var vocabulary = DomainVocabulary.Of(["fold ", " "], ["credit tranche", ""]);

        Assert.True(vocabulary.IsDomainVerb("fold"));
        Assert.False(vocabulary.IsDomainNoun("credit tranche"));
        Assert.False(vocabulary.IsDomainNoun("credit"));
        Assert.False(vocabulary.IsAcceptedAbbreviation(string.Empty));
        Assert.Equal(["fold"], vocabulary.Verbs);
        Assert.Empty(vocabulary.Nouns);
    }

    [Fact]
    public void Rejects_null_arguments()
    {
        Assert.Throws<ArgumentNullException>(
            () => DomainVocabulary.Of(null!, []));
        Assert.Throws<ArgumentNullException>(
            () => DomainVocabulary.Of([], null!));
        Assert.Throws<ArgumentNullException>(
            () => DomainVocabulary.Empty.IsAcceptedAbbreviation(null!));
        Assert.Throws<ArgumentNullException>(
            () => DomainVocabulary.Empty.ExpansionOf(null!));
    }

    [Fact]
    public void A_declared_verb_the_dictionaries_do_not_know_becomes_domain()
    {
        Assert.Equal(VerbCategory.Unknown, VerbDictionary.Classify("fold"));
        Assert.Equal(
            VerbCategory.Domain,
            VerbDictionary.Classify("fold", DomainVocabulary.Of(["fold"], [])));
    }

    [Fact]
    public void A_declared_verb_outranks_the_standard_tier()
    {
        Assert.Equal(VerbCategory.Standard, VerbDictionary.Classify("send"));
        Assert.Equal(
            VerbCategory.Domain,
            VerbDictionary.Classify("send", DomainVocabulary.Of(["send"], [])));
    }

    [Theory]
    [InlineData("process")]
    [InlineData("handle")]
    public void Generic_verbs_stay_generic_however_a_project_declares_them(string verb)
    {
        var vocabulary = DomainVocabulary.Of(["process", "handle"], []);

        Assert.Equal(
            VerbCategory.Generic, VerbDictionary.Classify(verb, vocabulary));
    }

    [Fact]
    public void Boolean_prefixes_stay_boolean_however_a_project_declares_them()
    {
        Assert.Equal(
            VerbCategory.Boolean,
            VerbDictionary.Classify("is", DomainVocabulary.Of(["is"], [])));
    }

    [Fact]
    public void Declared_nouns_are_not_verbs()
    {
        Assert.Equal(
            VerbCategory.Unknown,
            VerbDictionary.Classify(
                "tranche", DomainVocabulary.Of([], ["tranche"])));
    }

    /// <summary>
    /// Load-bearing since the project vocabulary landed: Classify decides
    /// generic before the project's verbs, which puts generic ahead of
    /// standard. That reordering is behaviour-preserving only while these two
    /// tiers are disjoint — Java asserts the same with a third overlap check.
    /// </summary>
    [Fact]
    public void Generic_and_standard_verbs_are_disjoint()
    {
        var standard = new HashSet<string>(
            VerbDictionary.StandardVerbs, StringComparer.Ordinal);

        Assert.DoesNotContain(VerbDictionary.GenericVerbs, standard.Contains);
    }

    [Theory]
    [InlineData("position")]
    [InlineData("record")]
    public void A_declared_noun_lifts_a_broad_or_vague_word_to_domain(string token)
    {
        var vocabulary = DomainVocabulary.Of([], ["position", "record"]);

        Assert.Equal(
            TokenTier.NotGeneric,
            GenericTokenDetector.Classify(token, vocabulary));
    }

    [Theory]
    [InlineData("temp")]
    [InlineData("foo")]
    [InlineData("x")]
    public void Meaningless_placeholders_are_not_rescued_by_being_written_down(
        string token)
    {
        var vocabulary = DomainVocabulary.Of([], ["temp", "foo", "x"]);

        Assert.Equal(
            TokenTier.Meaningless,
            GenericTokenDetector.Classify(token, vocabulary));
    }

    [Fact]
    public void Declared_verbs_are_not_nouns()
    {
        Assert.Equal(
            TokenTier.Vague,
            GenericTokenDetector.Classify(
                "data", DomainVocabulary.Of(["data"], [])));
    }

    [Fact]
    public void Accepted_shorthand_stops_being_an_abbreviation()
    {
        var vocabulary = DomainVocabulary.Of(
            [], [], new Dictionary<string, string> { ["acc"] = "account", ["fx"] = "foreign exchange" });

        Assert.NotNull(AbbreviationDictionary.Classify("acc"));
        Assert.Null(AbbreviationDictionary.Classify("acc", vocabulary));
        Assert.Null(AbbreviationDictionary.Classify("fx", vocabulary));
    }

    /// <summary>
    /// The defect the <c>abbreviations</c> section exists to fix: before it,
    /// any token of any committed phrase was accepted shorthand, so committing
    /// <c>calc total</c> silently dropped the <c>calc</c> spell-out for the
    /// whole repository. Acceptance is a declaration now, not a side effect.
    /// </summary>
    [Fact]
    public void A_token_of_a_committed_phrase_is_not_accepted_shorthand()
    {
        var vocabulary = DomainVocabulary.Of([], ["calc", "total"]);

        Assert.False(vocabulary.IsAcceptedAbbreviation("calc"));
        Assert.Equal(
            AbbreviationTier.Ambiguous, AbbreviationDictionary.Classify("calc", vocabulary));
    }

    [Fact]
    public void Undeclared_abbreviations_stay_penalized()
    {
        Assert.Equal(
            AbbreviationTier.WellKnown,
            AbbreviationDictionary.Classify(
                "mgr", DomainVocabulary.Of([], ["acc"])));
    }

    private static TraceTree Tree(string methodName, params string[] parameterNames)
    {
        var parameters = parameterNames
            .Select(name => new ParameterCapture(name, "\"x\"", false))
            .ToList();
        return new TraceTree(
        [
            new TraceNode(
                new MethodSignature("TrancheService", methodName, parameters),
                new Returned(null),
                [],
                0),
        ]);
    }

    [Fact]
    public void A_project_vocabulary_raises_the_score_of_its_own_words()
    {
        var tree = Tree("FoldTranche", "tranche");
        var vocabulary = DomainVocabulary.Of(["fold"], ["tranche"]);

        var without = ClarityAnalyzer.Analyze(tree);
        var with = ClarityAnalyzer.Analyze(tree, EmptyNames, vocabulary);

        Assert.True(with.Method > without.Method);
        Assert.True(with.Overall > without.Overall);
    }

    [Fact]
    public void An_omitted_vocabulary_scores_exactly_as_a_null_one()
    {
        var tree = Tree("CalculateTotal", "orderAmount");

        // ClarityResult is a record whose Issues list compares by reference, so
        // the dimension scores are what "identical" means here.
        AssertSameScores(
            ClarityAnalyzer.Analyze(tree),
            ClarityAnalyzer.Analyze(tree, EmptyNames, DomainVocabulary.Empty));
    }

    [Fact]
    public void Declaring_a_generic_verb_changes_no_score()
    {
        var tree = Tree("ProcessTranche", "tranche");
        var nounsOnly = DomainVocabulary.Of([], ["tranche"]);
        var withGenericVerb = DomainVocabulary.Of(["process"], ["tranche"]);

        AssertSameScores(
            ClarityAnalyzer.Analyze(tree, EmptyNames, nounsOnly),
            ClarityAnalyzer.Analyze(tree, EmptyNames, withGenericVerb));
    }

    [Fact]
    public void Notes_spell_out_an_accepted_abbreviation_from_the_project_glossary()
    {
        var vocabulary = DomainVocabulary.Of(
            ["settle"], [], new Dictionary<string, string> { ["fx"] = "foreign exchange" });

        Assert.Equal(
            "Verb 'settle' + noun 'fx' (foreign exchange)",
            ElementNoteComposer.MethodNote("settleFx", vocabulary));
    }

    [Fact]
    public void Notes_name_a_declared_verb_as_a_verb_rather_than_a_generic_one()
    {
        var vocabulary = DomainVocabulary.Of(["process"], []);

        Assert.StartsWith(
            "Generic verb 'process'",
            ElementNoteComposer.MethodNote("processTranche", vocabulary),
            StringComparison.Ordinal);
    }

    private static void AssertSameScores(
        ClarityResult expected, ClarityResult actual)
    {
        Assert.Equal(expected.Overall, actual.Overall);
        Assert.Equal(expected.Method, actual.Method);
        Assert.Equal(expected.Class, actual.Class);
        Assert.Equal(expected.Parameter, actual.Parameter);
        Assert.Equal(expected.Structural, actual.Structural);
        Assert.Equal(expected.Cohesion, actual.Cohesion);
        Assert.Equal(expected.Issues.Count, actual.Issues.Count);
    }

    private static readonly HashSet<string> EmptyNames =
        new(StringComparer.Ordinal);
}
