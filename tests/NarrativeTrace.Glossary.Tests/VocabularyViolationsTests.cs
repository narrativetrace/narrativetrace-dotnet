// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Glossary;
using Xunit;

namespace NarrativeTrace.Glossary.Tests;

public class VocabularyViolationsTests
{
    private static readonly Glossary Glossary = new(
        1,
        new Dictionary<string, BoundedContext>
        {
            ["billing"] = new("billing", ["Acme.Billing"]),
        },
        [
            new GlossaryTerm(
                "overdraft account", "billing", TermKind.NounPhrase, TermStatus.Curated,
                null, new Dictionary<string, string>(),
                [new SynonymAlias("account with overdraft")], [],
                new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc)),
        ]);

    [Fact]
    public void Builds_violation_with_canonical_term_and_rename_suggestion()
    {
        var suppressed = new HarvestCandidate(
            "billing", "account with overdraft", TermKind.NounPhrase,
            "AccountService.openAccountWithOverdraft", "openAccountWithOverdraft", 2);

        var violations = VocabularyViolations.Collect(Glossary, [suppressed]);

        Assert.Equal(
            [
                new VocabularyViolation(
                    "billing", "account with overdraft", "overdraft account",
                    "AccountService.openAccountWithOverdraft", "openAccountWithOverdraft",
                    "openOverdraftAccount", 2),
            ],
            violations);
    }

    [Fact]
    public void Aggregates_occurrences_for_same_identifier_at_same_site()
    {
        var first = new HarvestCandidate(
            "billing", "account with overdraft", TermKind.NounPhrase,
            "A.open", "accountWithOverdraft", 2);
        var second = new HarvestCandidate(
            "billing", "account with overdraft", TermKind.Word,
            "A.open", "accountWithOverdraft", 3);

        var violations = VocabularyViolations.Collect(Glossary, [first, second]);

        Assert.Equal(5, Assert.Single(violations).Occurrences);
    }

    [Fact]
    public void Leaves_suggestion_null_when_no_contiguous_alias_window_exists()
    {
        var suppressed = new HarvestCandidate(
            "billing", "account with overdraft", TermKind.NounPhrase,
            "A.legacyName", "legacyName", 1);

        var violations = VocabularyViolations.Collect(Glossary, [suppressed]);

        Assert.Null(Assert.Single(violations).SuggestedIdentifier);
    }

    [Fact]
    public void Sorts_violations_deterministically()
    {
        var b = new HarvestCandidate(
            "billing", "account with overdraft", TermKind.NounPhrase,
            "B.open", "accountWithOverdraft", 1);
        var a = new HarvestCandidate(
            "billing", "account with overdraft", TermKind.NounPhrase,
            "A.open", "accountWithOverdraft", 1);

        var violations = VocabularyViolations.Collect(Glossary, [b, a]);

        Assert.Equal(["A.open", "B.open"], violations.Select(v => v.Site).ToArray());
    }

    [Fact]
    public void Rejects_candidate_that_is_not_an_alias()
    {
        var notAnAlias = new HarvestCandidate(
            "billing", "fresh term", TermKind.Word, "A.a", "freshTerm", 1);

        Assert.Throws<ArgumentException>(
            () => VocabularyViolations.Collect(Glossary, [notAnAlias]));
    }

    [Fact]
    public void Rejects_null_arguments()
    {
        Assert.Throws<ArgumentNullException>(
            () => VocabularyViolations.Collect(null!, []));
        Assert.Throws<ArgumentNullException>(
            () => VocabularyViolations.Collect(Glossary, null!));
    }

    [Fact]
    public void Violation_record_rejects_blank_fields_and_bad_occurrences()
    {
        Assert.Throws<ArgumentException>(() => new VocabularyViolation(
            " ", "a", "c", "s", "i", null, 1));
        Assert.Throws<ArgumentException>(() => new VocabularyViolation(
            "ctx", "a", "c", "s", "i", null, 0));
    }
}
