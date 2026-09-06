// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Glossary;
using Xunit;

namespace NarrativeTrace.Glossary.Tests;

public sealed class GlossaryUsageReportTests : IDisposable
{
    private readonly string tempDir = Path.Combine(
        Path.GetTempPath(), $"glossary-usage-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(tempDir))
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Renders_new_terms_violations_and_aggregated_usage()
    {
        // Candidates arrive in the harvester's sorted (context, phrase, kind, site)
        // order; the report aggregates by first occurrence.
        var harvest = new HarvestResult(
        [
            Candidate("billing", "charge", "A.a", 1),
            Candidate("billing", "overdraft account", "A.a", 2),
            Candidate("billing", "overdraft account", "B.b", 1),
        ]);
        var newTerm = new GlossaryTerm(
            "charge", "billing", TermKind.Word, TermStatus.Harvested,
            null, new Dictionary<string, string>(), [], [],
            new DateTime(2026, 8, 11, 0, 0, 0, DateTimeKind.Utc));
        var violation = new VocabularyViolation(
            "billing", "account with overdraft", "overdraft account",
            "A.open", "accountWithOverdraft", "overdraftAccount", 2);

        var json = GlossaryUsageReport.Render(harvest, [newTerm], [violation]);

        Assert.Equal(
            """
            {
              "newTerms": [
                { "term": "charge", "context": "billing" }
              ],
              "violations": [
                { "context": "billing", "alias": "account with overdraft", "canonicalTerm": "overdraft account", "site": "A.open", "identifier": "accountWithOverdraft", "suggestedIdentifier": "overdraftAccount", "occurrences": 2 }
              ],
              "usage": [
                { "context": "billing", "phrase": "charge", "occurrences": 1 },
                { "context": "billing", "phrase": "overdraft account", "occurrences": 3 }
              ]
            }

            """.Replace("\r\n", "\n"),
            json);
    }

    [Fact]
    public void Empty_run_renders_empty_arrays()
    {
        var json = GlossaryUsageReport.Render(new HarvestResult([]), [], []);

        Assert.Equal(
            "{\n  \"newTerms\": [],\n  \"violations\": [],\n  \"usage\": []\n}\n",
            json);
    }

    [Fact]
    public void Violation_without_suggestion_omits_the_key()
    {
        var violation = new VocabularyViolation(
            "billing", "alias phrase", "canonical", "A.a", "legacyName", null, 1);

        var json = GlossaryUsageReport.Render(new HarvestResult([]), [], [violation]);

        Assert.DoesNotContain("suggestedIdentifier", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Writes_report_creating_parent_directories()
    {
        var file = Path.Combine(tempDir, "nested", GlossaryUsageReport.FileName);

        GlossaryUsageReport.Write(file, new HarvestResult([]), [], []);

        Assert.True(File.Exists(file));
        Assert.EndsWith("\n", File.ReadAllText(file), StringComparison.Ordinal);
    }

    [Fact]
    public void Rejects_null_and_blank_arguments()
    {
        var harvest = new HarvestResult([]);

        Assert.Throws<ArgumentNullException>(
            () => GlossaryUsageReport.Render(null!, [], []));
        Assert.Throws<ArgumentNullException>(
            () => GlossaryUsageReport.Render(harvest, null!, []));
        Assert.Throws<ArgumentNullException>(
            () => GlossaryUsageReport.Render(harvest, [], null!));
        Assert.Throws<ArgumentException>(
            () => GlossaryUsageReport.Write(" ", harvest, [], []));
    }

    private static HarvestCandidate Candidate(
        string context, string phrase, string site, int occurrences)
    {
        return new HarvestCandidate(
            context, phrase, TermKind.NounPhrase, site, "someIdentifier", occurrences);
    }
}
