// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Glossary;
using Xunit;

namespace NarrativeTrace.Glossary.Tests;

public class GlossaryJsonWriterTests
{
    private static readonly DateTime FirstSeen = new(2026, 8, 11, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Writes_empty_glossary()
    {
        var json = GlossaryJsonWriter.Write(new Glossary(1, new Dictionary<string, BoundedContext>(), []));

        Assert.Equal(
            "{\n  \"schemaVersion\": 1,\n  \"contexts\": {},\n  \"terms\": []\n}\n",
            json);
    }

    [Fact]
    public void Stamps_schema_2_and_writes_the_section_when_abbreviations_are_declared()
    {
        var glossary = new Glossary(
            1,
            new Dictionary<string, BoundedContext>(),
            [],
            new Dictionary<string, string> { ["fx"] = "foreign exchange", ["calc"] = "calculate" });

        Assert.Equal(
            """
            {
              "schemaVersion": 2,
              "contexts": {},
              "abbreviations": {
                "calc": "calculate",
                "fx": "foreign exchange"
              },
              "terms": []
            }

            """.Replace("\r\n", "\n"),
            GlossaryJsonWriter.Write(glossary));
    }

    [Fact]
    public void A_glossary_without_abbreviations_stays_byte_identical_to_its_schema_1_form()
    {
        var withoutSection = new Glossary(1, new Dictionary<string, BoundedContext>(), []);

        var json = GlossaryJsonWriter.Write(withoutSection);

        Assert.Equal(
            "{\n  \"schemaVersion\": 1,\n  \"contexts\": {},\n  \"terms\": []\n}\n",
            json);
        Assert.DoesNotContain("abbreviations", json, StringComparison.Ordinal);
    }

    [Fact]
    public void A_schema_2_glossary_that_lost_its_abbreviations_is_not_stamped_back_down()
    {
        var emptied = new Glossary(2, new Dictionary<string, BoundedContext>(), []);

        var json = GlossaryJsonWriter.Write(emptied);

        Assert.Equal(
            "{\n  \"schemaVersion\": 2,\n  \"contexts\": {},\n  \"terms\": []\n}\n",
            json);
    }

    [Fact]
    public void Writes_full_document_in_canonical_layout()
    {
        var glossary = new Glossary(
            1,
            new Dictionary<string, BoundedContext>
            {
                ["billing"] = new(
                    "billing", ["Acme.Billing"], "Charging, invoicing, funds"),
                ["_unassigned"] = new("_unassigned", []),
            },
            [
                new GlossaryTerm(
                    "overdraft account", "billing", TermKind.NounPhrase, TermStatus.Curated,
                    "Account permitted to go below zero.",
                    new Dictionary<string, string> { ["es"] = "cuenta con descubierto" },
                    [new SynonymAlias("account with overdraft", "legacy v1 API phrasing")],
                    ["Billing.OverdraftService.OpenOverdraftAccount"],
                    FirstSeen),
            ]);

        Assert.Equal(
            """
            {
              "schemaVersion": 1,
              "contexts": {
                "_unassigned": {
                  "packages": []
                },
                "billing": {
                  "packages": ["Acme.Billing"],
                  "description": "Charging, invoicing, funds"
                }
              },
              "terms": [
                {
                  "term": "overdraft account",
                  "context": "billing",
                  "kind": "noun-phrase",
                  "status": "curated",
                  "definition": "Account permitted to go below zero.",
                  "translations": {
                    "es": "cuenta con descubierto"
                  },
                  "synonyms": [
                    { "alias": "account with overdraft", "note": "legacy v1 API phrasing" }
                  ],
                  "sources": ["Billing.OverdraftService.OpenOverdraftAccount"],
                  "firstSeen": "2026-08-11"
                }
              ]
            }

            """.Replace("\r\n", "\n"),
            GlossaryJsonWriter.Write(glossary));
    }

    [Fact]
    public void Optional_term_fields_are_omitted_when_absent()
    {
        var glossary = OneTermGlossary(new GlossaryTerm(
            "charge", "billing", TermKind.Word, TermStatus.Harvested,
            null, new Dictionary<string, string>(), [], [], FirstSeen));

        var json = GlossaryJsonWriter.Write(glossary);

        Assert.DoesNotContain("definition", json, StringComparison.Ordinal);
        Assert.DoesNotContain("translations", json, StringComparison.Ordinal);
        Assert.DoesNotContain("synonyms", json, StringComparison.Ordinal);
        Assert.DoesNotContain("sources", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Synonym_note_is_omitted_when_absent()
    {
        var glossary = OneTermGlossary(new GlossaryTerm(
            "charge", "billing", TermKind.Word, TermStatus.Curated,
            null, new Dictionary<string, string>(),
            [new SynonymAlias("bill")], [], FirstSeen));

        Assert.Contains(
            "{ \"alias\": \"bill\" }", GlossaryJsonWriter.Write(glossary), StringComparison.Ordinal);
    }

    [Fact]
    public void Translations_are_sorted_by_locale()
    {
        var glossary = OneTermGlossary(new GlossaryTerm(
            "charge", "billing", TermKind.Word, TermStatus.Curated, null,
            new Dictionary<string, string> { ["fr"] = "débit", ["de"] = "Abbuchung" },
            [], [], FirstSeen));

        var json = GlossaryJsonWriter.Write(glossary);

        Assert.True(
            json.IndexOf("\"de\"", StringComparison.Ordinal)
            < json.IndexOf("\"fr\"", StringComparison.Ordinal));
    }

    [Fact]
    public void Escapes_quotes_backslashes_newlines_and_control_characters()
    {
        var glossary = OneTermGlossary(new GlossaryTerm(
            "charge", "billing", TermKind.Word, TermStatus.Curated,
            "a \"quoted\" \\ line\nnext\u0001", new Dictionary<string, string>(),
            [], [], FirstSeen));

        Assert.Contains(
            "\"definition\": \"a \\\"quoted\\\" \\\\ line\\nnext\\u0001\"",
            GlossaryJsonWriter.Write(glossary),
            StringComparison.Ordinal);
    }

    [Fact]
    public void Output_round_trips_through_the_reader()
    {
        var glossary = OneTermGlossary(new GlossaryTerm(
            "overdraft account", "billing", TermKind.NounPhrase, TermStatus.Curated,
            "Definition.", new Dictionary<string, string> { ["es"] = "cuenta" },
            [new SynonymAlias("account with overdraft")], ["Billing.Service"], FirstSeen));

        var json = GlossaryJsonWriter.Write(glossary);
        var reread = GlossaryJsonReader.Read(json);

        Assert.Equal(json, GlossaryJsonWriter.Write(reread));
    }

    [Fact]
    public void Rejects_null_glossary()
    {
        Assert.Throws<ArgumentNullException>(() => GlossaryJsonWriter.Write(null!));
    }

    private static Glossary OneTermGlossary(GlossaryTerm term)
    {
        return new Glossary(
            1,
            new Dictionary<string, BoundedContext>
            {
                ["billing"] = new("billing", []),
            },
            [term]);
    }
}
