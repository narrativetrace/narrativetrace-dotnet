// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Glossary;
using Xunit;

namespace NarrativeTrace.Glossary.Tests;

public class GlossaryTermTests
{
    private static readonly DateTime FirstSeen = new(2026, 8, 11, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Holds_all_fields()
    {
        var term = new GlossaryTerm(
            "overdraft account",
            "billing",
            TermKind.NounPhrase,
            TermStatus.Curated,
            "Account permitted to go below zero up to an agreed limit.",
            new Dictionary<string, string> { ["es"] = "cuenta con descubierto" },
            [new SynonymAlias("account with overdraft", "legacy v1 API phrasing")],
            ["Billing.OverdraftService.OpenOverdraftAccount"],
            FirstSeen);

        Assert.Equal("overdraft account", term.Term);
        Assert.Equal("billing", term.Context);
        Assert.Equal(TermKind.NounPhrase, term.Kind);
        Assert.Equal(TermStatus.Curated, term.Status);
        Assert.Equal("cuenta con descubierto", term.Translations["es"]);
        Assert.Single(term.Synonyms);
        Assert.Single(term.Sources);
        Assert.Equal(FirstSeen, term.FirstSeen);
    }

    [Fact]
    public void Minimal_harvested_term_needs_no_human_fields()
    {
        var term = Harvested("overdraft account");

        Assert.Null(term.Definition);
        Assert.Empty(term.Translations);
        Assert.Empty(term.Synonyms);
        Assert.Empty(term.Sources);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Blank_term_is_rejected(string? text)
    {
        Assert.Throws<ArgumentException>(() => Harvested(text!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Blank_context_is_rejected(string? context)
    {
        Assert.Throws<ArgumentException>(() => new GlossaryTerm(
            "overdraft account", context!, TermKind.NounPhrase, TermStatus.Harvested,
            null, new Dictionary<string, string>(), [], [], FirstSeen));
    }

    [Fact]
    public void Undefined_kind_and_status_are_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GlossaryTerm(
            "t", "c", (TermKind)99, TermStatus.Harvested,
            null, new Dictionary<string, string>(), [], [], FirstSeen));
        Assert.Throws<ArgumentOutOfRangeException>(() => new GlossaryTerm(
            "t", "c", TermKind.Word, (TermStatus)99,
            null, new Dictionary<string, string>(), [], [], FirstSeen));
    }

    [Fact]
    public void First_seen_with_a_time_component_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => new GlossaryTerm(
            "t", "c", TermKind.Word, TermStatus.Harvested,
            null, new Dictionary<string, string>(), [], [],
            new DateTime(2026, 8, 11, 10, 30, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public void Collections_are_copied_defensively()
    {
        var translations = new Dictionary<string, string> { ["es"] = "cuenta" };
        var synonyms = new List<SynonymAlias> { new("old phrasing") };
        var sources = new List<string> { "Billing.Service" };
        var term = new GlossaryTerm(
            "t", "c", TermKind.Word, TermStatus.Harvested,
            null, translations, synonyms, sources, FirstSeen);

        translations["en"] = "account";
        synonyms.Add(new SynonymAlias("another"));
        sources.Add("Billing.Other");

        Assert.Single(term.Translations);
        Assert.Single(term.Synonyms);
        Assert.Single(term.Sources);
    }

    [Fact]
    public void Term_key_identity_is_context_plus_term()
    {
        var key = TermKey.Of(Harvested("overdraft account"));

        Assert.Equal(new TermKey("billing", "overdraft account"), key);
        Assert.NotEqual(new TermKey("sales", "overdraft account"), key);
    }

    private static GlossaryTerm Harvested(string term)
    {
        return new GlossaryTerm(
            term, "billing", TermKind.NounPhrase, TermStatus.Harvested,
            null, new Dictionary<string, string>(), [], [], FirstSeen);
    }
}
