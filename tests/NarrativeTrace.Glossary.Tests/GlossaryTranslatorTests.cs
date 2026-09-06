// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Glossary;
using Xunit;

namespace NarrativeTrace.Glossary.Tests;

/// <summary>Mirrors Java's <c>GlossaryTranslatorTest</c>.</summary>
public class GlossaryTranslatorTests
{
    [Fact]
    public void Translates_an_exact_phrase_entry_for_the_locale()
    {
        var translator = new GlossaryTranslator(GlossaryOf(
            Term("overdraft account", TermKind.NounPhrase, "billing",
                new Dictionary<string, string> { ["es"] = "cuenta con descubierto" })));

        var result = translator.Translate("overdraft account", "billing", "es");

        Assert.Equal("cuenta con descubierto", result.Text);
        Assert.True(result.Complete);
    }

    [Fact]
    public void Falls_back_to_per_token_word_entries_when_no_exact_phrase_matches()
    {
        var translator = new GlossaryTranslator(GlossaryOf(
            Term("payment", TermKind.Word, "billing",
                new Dictionary<string, string> { ["es"] = "pago" }),
            Term("declined", TermKind.Word, "billing",
                new Dictionary<string, string> { ["es"] = "rechazado" })));

        var result = translator.Translate("payment declined", "billing", "es");

        Assert.Equal("pago rechazado", result.Text);
        Assert.True(result.Complete);
    }

    [Fact]
    public void Partial_token_coverage_renders_mixed_text_and_reports_incomplete()
    {
        var translator = new GlossaryTranslator(GlossaryOf(
            Term("payment", TermKind.Word, "billing",
                new Dictionary<string, string> { ["es"] = "pago" })));

        var result = translator.Translate("payment declined", "billing", "es");

        Assert.Equal("pago declined", result.Text);
        Assert.False(result.Complete);
    }

    [Fact]
    public void Unknown_phrase_renders_as_is_and_reports_incomplete()
    {
        var translator = new GlossaryTranslator(GlossaryOf());

        var result = translator.Translate("payment declined", "billing", "es");

        Assert.Equal("payment declined", result.Text);
        Assert.False(result.Complete);
    }

    [Fact]
    public void A_term_from_another_context_never_applies()
    {
        var translator = new GlossaryTranslator(GlossaryOf(
            Term("policy", TermKind.Word, "insurance",
                new Dictionary<string, string> { ["es"] = "póliza" })));

        var result = translator.Translate("policy", "billing", "es");

        Assert.Equal("policy", result.Text);
        Assert.False(result.Complete);
    }

    [Fact]
    public void Exact_entry_without_the_locale_falls_through_to_token_entries()
    {
        var translator = new GlossaryTranslator(GlossaryOf(
            Term("payment declined", TermKind.NounPhrase, "billing",
                new Dictionary<string, string> { ["fr"] = "paiement refusé" }),
            Term("payment", TermKind.Word, "billing",
                new Dictionary<string, string> { ["es"] = "pago" }),
            Term("declined", TermKind.Word, "billing",
                new Dictionary<string, string> { ["es"] = "rechazado" })));

        var result = translator.Translate("payment declined", "billing", "es");

        Assert.Equal("pago rechazado", result.Text);
        Assert.True(result.Complete);
    }

    /// <summary>
    /// Templates carry placeholders and prose, not a normalized phrase, so
    /// they only ever match exactly — a per-token fallback would mangle them.
    /// </summary>
    [Fact]
    public void Template_variant_looks_up_the_raw_template_exactly()
    {
        var translator = new GlossaryTranslator(GlossaryOf(
            Term("Charging {amount} to {customerId}", TermKind.Template, "billing",
                new Dictionary<string, string> { ["es"] = "Cobrando {amount} a {customerId}" }),
            Term("charging", TermKind.Word, "billing",
                new Dictionary<string, string> { ["es"] = "cobrando" })));

        Assert.Equal(
            "Cobrando {amount} a {customerId}",
            translator.TemplateVariant("Charging {amount} to {customerId}", "billing", "es"));
        Assert.Null(translator.TemplateVariant("Charging {amount}", "billing", "es"));
        Assert.Null(
            translator.TemplateVariant("Charging {amount} to {customerId}", "billing", "de"));
    }

    [Fact]
    public void Rejects_null_or_blank_arguments()
    {
        var translator = new GlossaryTranslator(GlossaryOf());

        Assert.Throws<ArgumentException>(() => translator.Translate(null!, "billing", "es"));
        Assert.Throws<ArgumentException>(() => translator.Translate("x", " ", "es"));
        Assert.Throws<ArgumentException>(() => translator.Translate("x", "billing", ""));
        Assert.Throws<ArgumentException>(() => translator.TemplateVariant(" ", "billing", "es"));
        Assert.Throws<ArgumentNullException>(() => new GlossaryTranslator(null!));
    }

    internal static GlossaryTerm Term(
        string term, TermKind kind, string context, IReadOnlyDictionary<string, string> translations)
    {
        return new GlossaryTerm(
            term,
            context,
            kind,
            TermStatus.Curated,
            null,
            translations,
            [],
            [],
            new DateTime(2026, 8, 11, 0, 0, 0, DateTimeKind.Utc));
    }

    internal static Glossary GlossaryOf(params GlossaryTerm[] terms)
    {
        return new Glossary(
            1,
            new Dictionary<string, BoundedContext>
            {
                ["billing"] = new("billing", ["Acme.Billing"]),
                ["insurance"] = new("insurance", ["Acme.Insurance"]),
            },
            terms);
    }
}
