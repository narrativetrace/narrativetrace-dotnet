// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Glossary;
using Xunit;

namespace NarrativeTrace.Glossary.Tests;

public class TermNormalizerTests
{
    [Theory]
    [InlineData("accountWithOverdraft")]
    [InlineData("AccountWithOverdraft")]
    [InlineData("account_with_overdraft")]
    public void Converges_camel_pascal_and_snake_case_to_one_phrase(string identifier)
    {
        Assert.Equal("account with overdraft", TermNormalizer.Phrase(identifier));
    }

    [Theory]
    [InlineData("overdraftAccounts", "overdraft account")]
    [InlineData("entries", "entry")]
    [InlineData("addresses", "address")]
    [InlineData("taxBoxes", "tax box")]
    public void Singularizes_plural_nouns(string identifier, string expected)
    {
        Assert.Equal(expected, TermNormalizer.Phrase(identifier));
    }

    [Theory]
    [InlineData("status")]
    [InlineData("analysis")]
    [InlineData("progress")]
    public void Keeps_non_plural_trailing_s_words(string word)
    {
        Assert.Equal(word, TermNormalizer.Phrase(word));
    }

    [Theory]
    [InlineData("alias")]
    [InlineData("gas")]
    [InlineData("series")]
    [InlineData("atlas")]
    [InlineData("bias")]
    [InlineData("canvas")]
    [InlineData("chaos")]
    [InlineData("lens")]
    [InlineData("news")]
    [InlineData("species")]
    public void Keeps_s_final_singular_and_invariant_words(string word)
    {
        Assert.Equal(word, TermNormalizer.Phrase(word));
    }

    [Theory]
    [InlineData("cases", "case")]
    [InlineData("responses", "response")]
    [InlineData("phases", "phase")]
    [InlineData("clauses", "clause")]
    [InlineData("purposes", "purpose")]
    [InlineData("houses", "house")]
    [InlineData("promises", "promise")]
    public void Singularizes_se_plurals_by_stripping_only_the_final_s(
        string identifier, string expected)
    {
        Assert.Equal(expected, TermNormalizer.Phrase(identifier));
    }

    [Theory]
    [InlineData("gases", "gas")]
    [InlineData("aliases", "alias")]
    [InlineData("lenses", "lens")]
    [InlineData("statuses", "status")]
    [InlineData("buses", "bus")]
    public void Singularizes_es_plurals_whose_stem_keeps_its_trailing_s(
        string identifier, string expected)
    {
        Assert.Equal(expected, TermNormalizer.Phrase(identifier));
    }

    [Theory]
    [InlineData("containsDuplicates", "contains duplicate")]
    [InlineData("matchesRules", "matches rule")]
    public void Keeps_third_person_verb_tokens_untouched(
        string identifier, string expected)
    {
        Assert.Equal(expected, TermNormalizer.Phrase(identifier));
    }

    [Theory]
    [InlineData("ies", "ie")]
    [InlineData("xes", "xe")]
    public void Plural_suffix_rules_require_a_stem_before_the_suffix(
        string identifier, string expected)
    {
        Assert.Equal(expected, TermNormalizer.Phrase(identifier));
    }

    [Theory]
    [InlineData("markedAsDone", "marked as done")]
    [InlineData("chargeWasApplied", "charge was applied")]
    public void Never_singularizes_stopwords(string identifier, string expected)
    {
        Assert.Equal(expected, TermNormalizer.Phrase(identifier));
    }

    [Fact]
    public void Splits_verb_method_into_verb_phrase_and_object_noun_phrase()
    {
        Assert.Equal(
            [
                new TermCandidate("open account with overdraft", TermKind.VerbPhrase),
                new TermCandidate("account with overdraft", TermKind.NounPhrase),
            ],
            TermNormalizer.MethodCandidates("openAccountWithOverdraft"));
    }

    [Fact]
    public void Single_word_object_becomes_word_candidate()
    {
        Assert.Equal(
            [
                new TermCandidate("charge card", TermKind.VerbPhrase),
                new TermCandidate("card", TermKind.Word),
            ],
            TermNormalizer.MethodCandidates("chargeCards"));
    }

    [Fact]
    public void Bare_verb_method_yields_only_verb_phrase()
    {
        Assert.Equal(
            [new TermCandidate("charge", TermKind.VerbPhrase)],
            TermNormalizer.MethodCandidates("charge"));
    }

    [Fact]
    public void Non_verb_method_yields_noun_candidate()
    {
        Assert.Equal(
            [new TermCandidate("overdraft limit", TermKind.NounPhrase)],
            TermNormalizer.MethodCandidates("overdraftLimit"));
    }

    [Fact]
    public void Object_phrase_drops_leading_stopwords()
    {
        Assert.Equal(
            [
                new TermCandidate("check for duplicate", TermKind.VerbPhrase),
                new TermCandidate("duplicate", TermKind.Word),
            ],
            TermNormalizer.MethodCandidates("checkForDuplicates"));
    }

    [Fact]
    public void Parameter_candidate_strips_trailing_id_role()
    {
        Assert.Equal(
            new TermCandidate("overdraft account", TermKind.NounPhrase),
            TermNormalizer.ParameterCandidate("overdraftAccountId"));
        Assert.Equal(
            new TermCandidate("customer", TermKind.Word),
            TermNormalizer.ParameterCandidate("customerIds"));
        Assert.Null(TermNormalizer.ParameterCandidate("id"));
    }

    [Fact]
    public void Class_candidate_strips_recognized_role_suffix()
    {
        Assert.Equal(
            new TermCandidate("overdraft", TermKind.Word),
            TermNormalizer.ClassCandidate("OverdraftService"));
        Assert.Equal(
            new TermCandidate("payment plan", TermKind.NounPhrase),
            TermNormalizer.ClassCandidate("PaymentPlanRepository"));
        Assert.Equal(
            new TermCandidate("invoice line item", TermKind.NounPhrase),
            TermNormalizer.ClassCandidate("InvoiceLineItem"));
        Assert.Null(TermNormalizer.ClassCandidate("Service"));
    }

    /// <summary>
    /// The C# interface prefix is naming convention, not vocabulary:
    /// <c>IPaymentService</c> must key on the same phrase Java's
    /// <c>PaymentService</c> does, or the shared concept never gets curated —
    /// or translated — once.
    /// </summary>
    [Theory]
    [InlineData("IPaymentService", "payment")]
    [InlineData("IOrderService", "order")]
    [InlineData("IDbConnection", "db connection")]
    [InlineData("IInventoryService", "inventory")]
    public void Class_candidate_drops_the_interface_prefix(string className, string phrase)
    {
        Assert.Equal(phrase, TermNormalizer.ClassCandidate(className)!.Phrase);
    }

    /// <summary>
    /// The guard is the shape of the two characters after the <c>I</c>: a
    /// lower-case second character means the <c>I</c> opened an ordinary word
    /// (<c>Item</c>), an upper-case third means it opened an acronym
    /// (<c>IOManager</c>, whose role suffix then leaves <c>"io"</c>) — those
    /// tokenizations are already the words a reader wants, and the ones Java's
    /// <c>IoManager</c> / <c>IpAddress</c> yield.
    /// </summary>
    [Theory]
    [InlineData("Item", "item")]
    [InlineData("IdentityMap", "identity map")]
    [InlineData("IOManager", "io")]
    [InlineData("IPAddress", "ip address")]
    [InlineData("IO", "io")]
    public void Class_candidate_keeps_a_leading_i_that_is_not_the_interface_prefix(
        string className, string phrase)
    {
        Assert.Equal(phrase, TermNormalizer.ClassCandidate(className)!.Phrase);
    }

    /// <summary>
    /// Prefix first, role suffix second: an interface named for nothing but
    /// its role has no vocabulary left, the same answer the bare class name
    /// already gives.
    /// </summary>
    [Fact]
    public void Class_candidate_of_an_interface_named_only_for_its_role_is_null()
    {
        Assert.Null(TermNormalizer.ClassCandidate("IService"));
    }

    /// <summary>
    /// The prefix rule reads class names only. Neither a parameter nor an
    /// exception type carries the convention, and stripping there would eat
    /// real words.
    /// </summary>
    [Fact]
    public void The_interface_prefix_rule_is_confined_to_class_names()
    {
        Assert.Equal(
            new TermCandidate("i payment", TermKind.NounPhrase),
            TermNormalizer.ParameterCandidate("IPayment"));
        Assert.Equal(
            new TermCandidate("i payment", TermKind.NounPhrase),
            TermNormalizer.ExceptionCandidate("IPaymentException"));
    }

    /// <summary>
    /// TODO item 24's FsCheck-found regression, fixed alongside the "__  " one:
    /// a leading space immediately before an uppercase letter used to close a
    /// bogus whitespace-only token (<see cref="NarrativeTrace.Clarity.IdentifierTokenizer"/>'s
    /// case-boundary check never asked whether the *previous* character was a
    /// letter at all), which downstream joined into a blank phrase and threw.
    /// " Service" must answer the same way "Service" already does — the leading
    /// space carries no vocabulary of its own.
    /// </summary>
    [Fact]
    public void Class_candidate_ignores_a_leading_space_before_the_role_suffix()
    {
        Assert.Null(TermNormalizer.ClassCandidate(" Service"));
    }

    [Fact]
    public void Exception_candidate_strips_exception_and_error_suffixes()
    {
        Assert.Equal(
            new TermCandidate("insufficient fund", TermKind.NounPhrase),
            TermNormalizer.ExceptionCandidate("InsufficientFundsException"));
        Assert.Equal(
            new TermCandidate("timeout", TermKind.Word),
            TermNormalizer.ExceptionCandidate("TimeoutError"));
        Assert.Null(TermNormalizer.ExceptionCandidate("Exception"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Rejects_blank_identifier_everywhere(string? identifier)
    {
        Assert.Throws<ArgumentException>(() => TermNormalizer.Phrase(identifier!));
        Assert.Throws<ArgumentException>(() => TermNormalizer.MethodCandidates(identifier!));
        Assert.Throws<ArgumentException>(() => TermNormalizer.ParameterCandidate(identifier!));
        Assert.Throws<ArgumentException>(() => TermNormalizer.ClassCandidate(identifier!));
        Assert.Throws<ArgumentException>(() => TermNormalizer.ExceptionCandidate(identifier!));
    }

    [Fact]
    public void Candidate_rejects_blank_phrase_and_undefined_kind()
    {
        Assert.Throws<ArgumentException>(() => new TermCandidate(" ", TermKind.Word));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TermCandidate("x", (TermKind)99));
    }

    /// <summary>
    /// A java security fuzz suite finding, the same bug class as a sibling one module over,
    /// mirrored here. <c>__</c> is not blank, so it passed the old guard; the tokenizer reads
    /// no word in it, so normalization produced the empty string — <see cref="TermNormalizer.Phrase"/>
    /// violated its own "never blank" contract, and <see cref="TermNormalizer.MethodCandidates"/>
    /// indexed an empty token list (an unguarded <see cref="ArgumentOutOfRangeException"/>). Both
    /// now refuse a tokenless identifier the same declared way they already refuse a blank one.
    /// <c>"__  "</c> is a second finding of the same class, from this runtime's own security property
    /// suite (<c>ScannerPropertyTests.Normalizing_only_ever_throws_its_declared_guard</c>): trailing
    /// whitespace surviving an underscore split is not blank and not empty either, so it slipped
    /// past both guards until <see cref="NarrativeTrace.Clarity.IdentifierTokenizer"/> itself learned
    /// to drop a token with no letter-or-digit in it.
    /// </summary>
    [Theory]
    [InlineData("_")]
    [InlineData("__")]
    [InlineData("___")]
    [InlineData("__  ")]
    public void Rejects_an_identifier_with_no_readable_word(string identifier)
    {
        Assert.Throws<ArgumentException>(() => TermNormalizer.Phrase(identifier));
        Assert.Throws<ArgumentException>(() => TermNormalizer.MethodCandidates(identifier));
    }

    /// <summary>
    /// The other three already model "nothing to harvest here" with a null return for the
    /// role-suffix-only case — a tokenless identifier is the same answer to the same question, not
    /// a second failure mode, so these must not throw at all.
    /// </summary>
    [Theory]
    [InlineData("_")]
    [InlineData("__")]
    [InlineData("___")]
    [InlineData("__  ")]
    public void Candidate_members_answer_null_for_an_identifier_with_no_readable_word(string identifier)
    {
        Assert.Null(TermNormalizer.ParameterCandidate(identifier));
        Assert.Null(TermNormalizer.ClassCandidate(identifier));
        Assert.Null(TermNormalizer.ExceptionCandidate(identifier));
    }

    /// <summary>
    /// The total sibling of <c>Phrase</c>, for a renderer that must answer
    /// something for every name its wire format permits.
    /// </summary>
    [Fact]
    public void Phrase_or_null_normalizes_like_phrase_and_answers_null_when_nothing_is_readable()
    {
        Assert.Equal("place order", TermNormalizer.PhraseOrNull("placeOrder"));
        Assert.Equal(
            TermNormalizer.Phrase("customerAccounts"),
            TermNormalizer.PhraseOrNull("customerAccounts"));
        Assert.Null(TermNormalizer.PhraseOrNull("$$$"));
        Assert.Null(TermNormalizer.PhraseOrNull("__"));
        Assert.Throws<ArgumentException>(() => TermNormalizer.PhraseOrNull(" "));
    }
}
