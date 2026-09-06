// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Glossary;
using Xunit;

namespace NarrativeTrace.Glossary.Tests;

public class GlossaryMergerTests
{
    private static readonly DateTime Today = new(2026, 8, 11, 10, 0, 0, DateTimeKind.Utc);

    private static readonly Glossary EmptyBilling = new(
        1,
        new Dictionary<string, BoundedContext>
        {
            ["billing"] = new("billing", ["Acme.Billing"]),
        },
        []);

    private readonly GlossaryMerger merger = new(() => Today);

    [Fact]
    public void Adds_unseen_term_as_harvested_with_first_seen_from_clock()
    {
        var harvest = new HarvestResult(
            [Candidate("billing", "overdraft account", "OverdraftService.open")]);

        var result = merger.Merge(EmptyBilling, harvest);

        var added = Assert.Single(result.NewTerms);
        Assert.Equal("overdraft account", added.Term);
        Assert.Equal("billing", added.Context);
        Assert.Equal(TermKind.NounPhrase, added.Kind);
        Assert.Equal(TermStatus.Harvested, added.Status);
        Assert.Equal(["OverdraftService.open"], added.Sources);
        Assert.Equal(new DateTime(2026, 8, 11, 0, 0, 0, DateTimeKind.Utc), added.FirstSeen);
        Assert.Contains(added, result.Glossary.Terms);
        Assert.Empty(result.SuppressedAliasUses);
    }

    [Fact]
    public void Carries_the_declared_abbreviations_through_a_harvest_untouched()
    {
        var declared = new Glossary(
            2,
            new Dictionary<string, BoundedContext>
            {
                ["billing"] = new("billing", ["Acme.Billing"]),
            },
            [],
            new Dictionary<string, string> { ["fx"] = "foreign exchange" });
        var harvest = new HarvestResult(
            [Candidate("billing", "overdraft account", "OverdraftService.open")]);

        var result = merger.Merge(declared, harvest);

        Assert.Equal("foreign exchange", result.Glossary.Abbreviations["fx"]);
        Assert.Single(result.Glossary.Abbreviations);
        Assert.Single(result.NewTerms);
    }

    [Fact]
    public void Harvesting_never_declares_an_abbreviation()
    {
        var harvest = new HarvestResult(
            [Candidate("billing", "fx", "PricingService.convert")]);

        var result = merger.Merge(EmptyBilling, harvest);

        Assert.Empty(result.Glossary.Abbreviations);
    }

    [Fact]
    public void Collects_at_most_three_distinct_source_sites_per_new_term()
    {
        var harvest = new HarvestResult(
        [
            Candidate("billing", "overdraft account", "A.a"),
            Candidate("billing", "overdraft account", "B.b"),
            Candidate("billing", "overdraft account", "C.c"),
            Candidate("billing", "overdraft account", "D.d"),
        ]);

        var result = merger.Merge(EmptyBilling, harvest);

        Assert.Equal(["A.a", "B.b", "C.c"], Assert.Single(result.NewTerms).Sources);
    }

    [Fact]
    public void Leaves_existing_term_completely_untouched()
    {
        var curated = new GlossaryTerm(
            "overdraft account", "billing", TermKind.NounPhrase, TermStatus.Curated,
            "Human definition.",
            new Dictionary<string, string> { ["es"] = "cuenta con descubierto" },
            [], ["Old.site"], new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var existing = new Glossary(1, EmptyBilling.Contexts, [curated]);
        var harvest = new HarvestResult(
            [Candidate("billing", "overdraft account", "New.site")]);

        var result = merger.Merge(existing, harvest);

        Assert.Empty(result.NewTerms);
        Assert.Same(curated, Assert.Single(result.Glossary.Terms));
    }

    [Fact]
    public void Declares_missing_unassigned_context_with_description()
    {
        var harvest = new HarvestResult(
            [Candidate("_unassigned", "ticket", "TicketDesk.open")]);

        var result = merger.Merge(EmptyBilling, harvest);

        Assert.Contains("billing", result.Glossary.Contexts.Keys);
        var unassigned = result.Glossary.Contexts["_unassigned"];
        Assert.Empty(unassigned.Packages);
        Assert.Equal("Harvested terms not yet mapped to a context", unassigned.Description);
    }

    [Fact]
    public void Declares_other_missing_contexts_without_description()
    {
        var harvest = new HarvestResult([Candidate("warehouse", "pallet", "Depot.store")]);

        var result = merger.Merge(EmptyBilling, harvest);

        Assert.Null(result.Glossary.Contexts["warehouse"].Description);
    }

    [Fact]
    public void Suppresses_deprecated_alias_use_instead_of_adding_it()
    {
        var canonical = new GlossaryTerm(
            "overdraft account", "billing", TermKind.NounPhrase, TermStatus.Curated,
            null, new Dictionary<string, string>(),
            [new SynonymAlias("account with overdraft")], [],
            new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var existing = new Glossary(1, EmptyBilling.Contexts, [canonical]);
        var aliasUse = Candidate("billing", "account with overdraft", "AccountService.open");

        var result = merger.Merge(existing, new HarvestResult([aliasUse]));

        Assert.Empty(result.NewTerms);
        Assert.Same(canonical, Assert.Single(result.Glossary.Terms));
        Assert.Equal([aliasUse], result.SuppressedAliasUses);
    }

    [Fact]
    public void Alias_suppression_is_scoped_to_its_context()
    {
        var canonical = new GlossaryTerm(
            "overdraft account", "billing", TermKind.NounPhrase, TermStatus.Curated,
            null, new Dictionary<string, string>(),
            [new SynonymAlias("account with overdraft")], [],
            new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var contexts = new Dictionary<string, BoundedContext>
        {
            ["billing"] = EmptyBilling.Contexts["billing"],
            ["support"] = new("support", ["Acme.Support"]),
        };
        var existing = new Glossary(1, contexts, [canonical]);
        var billingUse = Candidate("billing", "account with overdraft", "AccountService.open");
        var supportUse = Candidate("support", "account with overdraft", "HelpDesk.describe");

        var result = merger.Merge(existing, new HarvestResult([billingUse, supportUse]));

        Assert.Equal([billingUse], result.SuppressedAliasUses);
        var added = Assert.Single(result.NewTerms);
        Assert.Equal("account with overdraft", added.Term);
        Assert.Equal("support", added.Context);
        Assert.Equal(TermStatus.Harvested, added.Status);
    }

    [Fact]
    public void Rejects_null_arguments()
    {
        Assert.Throws<ArgumentNullException>(() => new GlossaryMerger(null!));
        Assert.Throws<ArgumentNullException>(
            () => merger.Merge(null!, new HarvestResult([])));
        Assert.Throws<ArgumentNullException>(() => merger.Merge(EmptyBilling, null!));
    }

    private static HarvestCandidate Candidate(string context, string phrase, string site)
    {
        return new HarvestCandidate(
            context, phrase, TermKind.NounPhrase, site, "someIdentifier", 1);
    }
}
