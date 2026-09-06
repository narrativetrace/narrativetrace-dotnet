// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Glossary;
using Xunit;

namespace NarrativeTrace.Glossary.Tests;

public class GlossaryJsonReaderTests
{

    private const string FullDocument = """
        {
          "schemaVersion": 1,
          "contexts": {
            "billing": {
              "packages": ["Acme.Billing"],
              "description": "Charging, invoicing, funds"
            },
            "_unassigned": {
              "packages": []
            }
          },
          "terms": [
            {
              "term": "overdraft account",
              "context": "billing",
              "kind": "noun-phrase",
              "status": "curated",
              "definition": "Account permitted to go below zero.",
              "translations": { "es": "cuenta con descubierto" },
              "synonyms": [
                { "alias": "account with overdraft", "note": "legacy v1 API phrasing" }
              ],
              "sources": ["Billing.OverdraftService.OpenOverdraftAccount"],
              "firstSeen": "2026-08-11"
            }
          ]
        }
        """;

    [Fact]
    public void Reads_a_complete_document()
    {
        var glossary = GlossaryJsonReader.Read(FullDocument);

        Assert.Equal(1, glossary.SchemaVersion);
        Assert.Equal(2, glossary.Contexts.Count);
        Assert.Equal(["Acme.Billing"], glossary.Contexts["billing"].Packages);
        Assert.Equal("Charging, invoicing, funds", glossary.Contexts["billing"].Description);
        Assert.Null(glossary.Contexts["_unassigned"].Description);
        var term = Assert.Single(glossary.Terms);
        Assert.Equal("overdraft account", term.Term);
        Assert.Equal(TermKind.NounPhrase, term.Kind);
        Assert.Equal(TermStatus.Curated, term.Status);
        Assert.Equal("cuenta con descubierto", term.Translations["es"]);
        Assert.Equal("account with overdraft", Assert.Single(term.Synonyms).Alias);
        Assert.Equal(new DateTime(2026, 8, 11, 0, 0, 0, DateTimeKind.Utc), term.FirstSeen);
    }

    [Fact]
    public void Reads_the_abbreviations_section_of_a_schema_2_document()
    {
        var glossary = GlossaryJsonReader.Read("""
            {
              "schemaVersion": 2,
              "contexts": {},
              "abbreviations": { "fx": "foreign exchange", "calc": "calculate" },
              "terms": []
            }
            """);

        Assert.Equal(2, glossary.SchemaVersion);
        Assert.Equal("foreign exchange", glossary.Abbreviations["fx"]);
        Assert.Equal("calculate", glossary.Abbreviations["calc"]);
    }

    [Fact]
    public void Accepts_the_abbreviations_section_on_a_schema_1_document()
    {
        var glossary = GlossaryJsonReader.Read("""
            {
              "schemaVersion": 1,
              "contexts": {},
              "abbreviations": { "fx": "foreign exchange" },
              "terms": []
            }
            """);

        Assert.Equal(1, glossary.SchemaVersion);
        Assert.Equal("foreign exchange", glossary.Abbreviations["fx"]);
    }

    [Fact]
    public void A_document_without_the_section_has_no_abbreviations()
    {
        var glossary = GlossaryJsonReader.Read(FullDocument);

        Assert.Empty(glossary.Abbreviations);
    }

    [Fact]
    public void Reads_a_minimal_term_without_optional_keys()
    {
        var glossary = GlossaryJsonReader.Read("""
            {
              "schemaVersion": 1,
              "contexts": { "billing": { "packages": [] } },
              "terms": [
                { "term": "charge", "context": "billing", "kind": "word",
                  "status": "harvested", "firstSeen": "2026-08-11" }
              ]
            }
            """);

        var term = Assert.Single(glossary.Terms);
        Assert.Null(term.Definition);
        Assert.Empty(term.Translations);
        Assert.Empty(term.Synonyms);
        Assert.Empty(term.Sources);
    }

    [Fact]
    public void Explicit_null_definition_reads_as_absent()
    {
        var glossary = GlossaryJsonReader.Read("""
            {
              "schemaVersion": 1,
              "contexts": { "billing": { "packages": [] } },
              "terms": [
                { "term": "charge", "context": "billing", "kind": "word",
                  "status": "harvested", "definition": null, "firstSeen": "2026-08-11" }
              ]
            }
            """);

        Assert.Null(Assert.Single(glossary.Terms).Definition);
    }

    [Theory]
    [InlineData("""{"schemaVersion": 1, "contexts": {}, "terms": [], "extra": 1}""",
        "unknown key 'extra' in glossary")]
    [InlineData("""{"contexts": {}, "terms": []}""",
        "missing required key 'schemaVersion'")]
    [InlineData("""{"schemaVersion": 1, "terms": []}""",
        "missing required key 'contexts'")]
    [InlineData("""{"schemaVersion": 1, "contexts": {}}""",
        "missing required key 'terms'")]
    [InlineData("""{"schemaVersion": null, "contexts": {}, "terms": []}""",
        "missing required key 'schemaVersion'")]
    [InlineData("""{"schemaVersion": "1", "contexts": {}, "terms": []}""",
        "schemaVersion must be a JSON integer")]
    [InlineData("""{"schemaVersion": 1, "contexts": [], "terms": []}""",
        "contexts must be a JSON object")]
    [InlineData("""{"schemaVersion": 1, "contexts": {}, "terms": {}}""",
        "terms must be a JSON array")]
    [InlineData("""{"schemaVersion": 2, "contexts": {}, "terms": [], "abbreviations": []}""",
        "abbreviations must be a JSON object")]
    [InlineData("""{"schemaVersion": 2, "contexts": {}, "terms": [], "abbreviations": {"fx": 1}}""",
        "abbreviation 'fx' must be a JSON string")]
    [InlineData("""{"schemaVersion": 2, "contexts": {}, "terms": [], "abbreviations": {"fx": ""}}""",
        "abbreviation 'fx' has a blank expansion")]
    [InlineData("""{"schemaVersion": 2, "contexts": {}, "terms": [], "abbreviations": {"foreign exchange": "fx"}}""",
        "abbreviation 'foreign exchange' must be a single token")]
    public void Rejects_malformed_roots(string json, string expected)
    {
        var ex = Assert.Throws<ArgumentException>(() => GlossaryJsonReader.Read(json));

        Assert.Contains(expected, ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("""{"billing": {"packages": [], "color": "red"}}""",
        "unknown key 'color' in context 'billing'")]
    [InlineData("""{"billing": {}}""",
        "missing required key 'packages'")]
    [InlineData("""{"billing": {"packages": "Acme"}}""",
        "packages must be a JSON array")]
    [InlineData("""{"billing": 1}""",
        "context 'billing' must be a JSON object")]
    public void Rejects_malformed_contexts(string contexts, string expected)
    {
        var json = $$"""{"schemaVersion": 1, "contexts": {{contexts}}, "terms": []}""";

        var ex = Assert.Throws<ArgumentException>(() => GlossaryJsonReader.Read(json));

        Assert.Contains(expected, ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("""{"term": "t", "context": "billing", "kind": "word", "status": "harvested", "firstSeen": "2026-08-11", "oops": 1}""",
        "unknown key 'oops' in term")]
    [InlineData("""{"context": "billing", "kind": "word", "status": "harvested", "firstSeen": "2026-08-11"}""",
        "missing required key 'term'")]
    [InlineData("""{"term": "t", "context": "billing", "kind": "verb", "status": "harvested", "firstSeen": "2026-08-11"}""",
        "unknown term kind 'verb'")]
    [InlineData("""{"term": "t", "context": "billing", "kind": "word", "status": "reviewed", "firstSeen": "2026-08-11"}""",
        "unknown term status 'reviewed'")]
    [InlineData("""{"term": "t", "context": "billing", "kind": "word", "status": "harvested", "firstSeen": "11.08.2026"}""",
        "invalid firstSeen date '11.08.2026'")]
    [InlineData("""{"term": "t", "context": "billing", "kind": "word", "status": "harvested", "firstSeen": "2026-13-45"}""",
        "invalid firstSeen date '2026-13-45'")]
    [InlineData("""{"term": "t", "context": "billing", "kind": "word", "status": "harvested", "firstSeen": "2026-08-11", "synonyms": [{"alias": "a", "why": "x"}]}""",
        "unknown key 'why' in synonym")]
    [InlineData("""{"term": "t", "context": "billing", "kind": "word", "status": "harvested", "firstSeen": "2026-08-11", "translations": {"es": 1}}""",
        "translation 'es' must be a JSON string")]
    [InlineData("""{"term": "t", "context": "billing", "kind": "word", "status": "harvested", "firstSeen": "2026-08-11", "sources": [1]}""",
        "sources element must be a JSON string")]
    public void Rejects_malformed_terms(string term, string expected)
    {
        var json =
            """{"schemaVersion": 1, "contexts": {"billing": {"packages": []}}, "terms": ["""
            + term + "]}";

        var ex = Assert.Throws<ArgumentException>(() => GlossaryJsonReader.Read(json));

        Assert.Contains(expected, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Structural_rules_are_enforced_on_read()
    {
        var ex = Assert.Throws<ArgumentException>(() => GlossaryJsonReader.Read("""
            {"schemaVersion": 1, "contexts": {},
             "terms": [{"term": "t", "context": "ghost", "kind": "word",
                        "status": "harvested", "firstSeen": "2026-08-11"}]}
            """));

        Assert.Contains("undeclared context 'ghost'", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Rejects_null_input()
    {
        Assert.Throws<ArgumentNullException>(() => GlossaryJsonReader.Read(null!));
    }
}
