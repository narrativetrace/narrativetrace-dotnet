// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Clarity;
using NarrativeTrace.Core;
using NarrativeTrace.Glossary;
using Xunit;

namespace NarrativeTrace.Glossary.Tests.E2E;

/// <summary>
/// Full-pipeline walkthroughs 1 and 2 of the plan, exercised from outside the
/// production assembly through the public API only — internal leakage into
/// the pipeline surface would fail to compile here.
/// </summary>
public class GlossaryPipelineEndToEndTests
{
    private static readonly DateTime Today = new(2026, 8, 11, 0, 0, 0, DateTimeKind.Utc);

    private static readonly Glossary Empty = new(
        1,
        new Dictionary<string, BoundedContext>
        {
            ["billing"] = new("billing", ["Acme.Billing"]),
        },
        []);

    [Fact]
    public void Walkthrough_one_harvests_new_vocabulary_from_traces()
    {
        var harvester = new GlossaryHarvester(
            new ContextResolver(Empty), _ => "Acme.Billing");
        var run = harvester.Harvest(
            [Tree(Call("OverdraftService", "openOverdraftAccount", "overdraftAccountId"))]);

        var merge = new GlossaryMerger(() => Today).Merge(Empty, run);

        var newTerms = merge.NewTerms.Select(term => term.Term).ToList();
        Assert.Contains("open overdraft account", newTerms);
        Assert.Contains("overdraft account", newTerms);
        Assert.Contains("overdraft", newTerms);
        Assert.All(merge.NewTerms, term => Assert.Equal(TermStatus.Harvested, term.Status));
    }

    [Fact]
    public void Walkthrough_two_flags_deprecated_synonym_phrasing()
    {
        var curated = CuratedGlossary();
        var harvester = new GlossaryHarvester(
            new ContextResolver(curated), _ => "Acme.Billing");
        var run = harvester.Harvest(
            [Tree(Call("AccountService", "openAccountWithOverdraft"))]);

        var merge = new GlossaryMerger(() => Today).Merge(curated, run);
        var violations = VocabularyViolations.Collect(curated, merge.SuppressedAliasUses);

        Assert.DoesNotContain(
            merge.Glossary.Terms, term => term.Term == "account with overdraft");
        var violation = Assert.Single(violations);
        Assert.Equal("openOverdraftAccount", violation.SuggestedIdentifier);
        AssertSurfacesReportViolation(merge, violations);
    }

    private static void AssertSurfacesReportViolation(
        MergeResult merge, IReadOnlyList<VocabularyViolation> violations)
    {
        var summary = VocabularySummaryFormatter.FormatSummary(
            merge.NewTerms.Count, violations);
        Assert.Contains("deprecated synonym in use", summary, StringComparison.Ordinal);
        Assert.Contains(
            "openAccountWithOverdraft → use openOverdraftAccount"
            + " (billing: \"overdraft account\")",
            summary,
            StringComparison.Ordinal);

        var issue = Assert.Single(NonCanonicalTermIssues.From(violations));
        Assert.Equal("non-canonical-term", issue.Category);
        Assert.Equal(
            "billing.AccountService.openAccountWithOverdraft", issue.Element);

        var markdown = GlossaryMarkdownRenderer.Render(merge.Glossary);
        Assert.Contains("## billing", markdown, StringComparison.Ordinal);
        Assert.Contains(
            "account with overdraft — legacy phrasing", markdown, StringComparison.Ordinal);
    }

    /// <summary>Curated glossary as a developer would commit it, round-tripped through the file form.</summary>
    private static Glossary CuratedGlossary()
    {
        var curatedTerm = new GlossaryTerm(
            "overdraft account", "billing", TermKind.NounPhrase, TermStatus.Curated,
            "Account permitted to go below zero.",
            new Dictionary<string, string> { ["es"] = "cuenta con descubierto" },
            [new SynonymAlias("account with overdraft", "legacy phrasing")],
            ["OverdraftService.openOverdraftAccount"],
            Today);
        var written = GlossaryJsonWriter.Write(
            new Glossary(1, Empty.Contexts, [curatedTerm]));
        return GlossaryJsonReader.Read(written);
    }

    private static TraceTree Tree(params TraceNode[] roots)
    {
        return new TraceTree(roots);
    }

    private static TraceNode Call(
        string className, string methodName, params string[] parameterNames)
    {
        var captures = parameterNames
            .Select(name => new ParameterCapture(name, "\"v\"", false))
            .ToArray();
        return new TraceNode(
            new MethodSignature(className, methodName, captures),
            new Incomplete(),
            [],
            0);
    }
}
