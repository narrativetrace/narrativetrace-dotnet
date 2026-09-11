// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Build;
using Xunit;

namespace Build.Tests;

/// <summary>
/// Guards the mutation-TESTED-or-TEST-SUITE-or-EXEMPTED accounting
/// <see cref="MutationAccounting"/> enforces. A project absent from all three buckets used to be
/// able to ship unmutated forever with nothing saying so; this is the check that turns that
/// silence into a build failure.
/// </summary>
public sealed class MutationAccountingTests
{
    [Fact]
    public void A_project_in_none_of_the_three_buckets_is_unaccounted()
    {
        var unaccounted = MutationAccounting.Unaccounted(
            ["NarrativeTrace.Core", "NarrativeTrace.Orphan"],
            tested: ["NarrativeTrace.Core"],
            exempted: []);

        var name = Assert.Single(unaccounted);
        Assert.Equal("NarrativeTrace.Orphan", name);
    }

    [Fact]
    public void A_mutation_tested_project_is_accounted_for()
    {
        var unaccounted = MutationAccounting.Unaccounted(
            ["NarrativeTrace.Core"], tested: ["NarrativeTrace.Core"], exempted: []);

        Assert.Empty(unaccounted);
    }

    [Fact]
    public void A_project_whose_name_ends_in_Tests_is_accounted_for_without_being_listed_anywhere()
    {
        var unaccounted = MutationAccounting.Unaccounted(
            ["NarrativeTrace.Core.Tests"], tested: [], exempted: []);

        Assert.Empty(unaccounted);
    }

    [Fact]
    public void An_explicitly_exempted_project_is_accounted_for()
    {
        var unaccounted = MutationAccounting.Unaccounted(
            ["NarrativeTrace.Benchmarks"], tested: [], exempted: ["NarrativeTrace.Benchmarks"]);

        Assert.Empty(unaccounted);
    }

    [Fact]
    public void Multiple_unaccounted_projects_are_all_reported_sorted()
    {
        var unaccounted = MutationAccounting.Unaccounted(
            ["NarrativeTrace.Zebra", "NarrativeTrace.Alpha"], tested: [], exempted: []);

        Assert.Equal(["NarrativeTrace.Alpha", "NarrativeTrace.Zebra"], unaccounted);
    }

    [Fact]
    public void A_duplicate_name_is_reported_once()
    {
        var unaccounted = MutationAccounting.Unaccounted(
            ["NarrativeTrace.Orphan", "NarrativeTrace.Orphan"], tested: [], exempted: []);

        Assert.Single(unaccounted);
    }

    [Fact]
    public void A_name_mutation_tested_and_exempted_at_once_is_doubly_accounted()
    {
        var doubly = MutationAccounting.DoublyAccounted(
            ["NarrativeTrace.Core"], tested: ["NarrativeTrace.Core"], exempted: ["NarrativeTrace.Core"]);

        var name = Assert.Single(doubly);
        Assert.Equal("NarrativeTrace.Core", name);
    }

    [Fact]
    public void A_test_suite_name_also_carrying_an_explicit_exemption_is_doubly_accounted()
    {
        // A test project's name already exempts it structurally (it ends in "Tests"); also adding
        // it to Exemptions is redundant bookkeeping nobody reconciled — flag it the same way.
        var doubly = MutationAccounting.DoublyAccounted(
            ["NarrativeTrace.Orphan.Tests"], tested: [], exempted: ["NarrativeTrace.Orphan.Tests"]);

        var name = Assert.Single(doubly);
        Assert.Equal("NarrativeTrace.Orphan.Tests", name);
    }

    [Fact]
    public void No_real_project_is_doubly_accounted()
    {
        var repoRoot = RepositoryPath.Root();
        var names = MutationAccounting.AllProjectNames(repoRoot);
        var tested = MutationAccounting.TestedProjectNames(repoRoot).ToList();
        var exempted = MutationAccounting.Exemptions.Keys.ToList();

        var doubly = MutationAccounting.DoublyAccounted(names, tested, exempted);

        Assert.Empty(doubly);
    }

    /// <summary>
    /// The live check, against the real repo and the real <see cref="MutationAccounting.Exemptions"/>
    /// map — mirrors <c>CoverageAccountingTests.Every_currently_selected_test_project_is_accounted_for</c>.
    /// This is what actually proves every project on disk today is mutation-tested, a test suite by
    /// name, or exempted — not a synthetic name list that could drift from what is really there.
    /// </summary>
    [Fact]
    public void Every_real_project_is_accounted_for()
    {
        var repoRoot = RepositoryPath.Root();
        var names = MutationAccounting.AllProjectNames(repoRoot);
        var tested = MutationAccounting.TestedProjectNames(repoRoot).ToList();
        var exempted = MutationAccounting.Exemptions.Keys.ToList();

        var unaccounted = MutationAccounting.Unaccounted(names, tested, exempted);

        Assert.Empty(unaccounted);
    }

    /// <summary>
    /// <see cref="MutationAccounting.TestedProjectNames"/> reads the real
    /// <c>stryker-config*.json</c> files rather than a hand-copied list — this pins today's known
    /// set so a config silently disappearing (or appearing) is visible as a test failure, not just
    /// a accounting-check side effect.
    /// </summary>
    [Fact]
    public void TestedProjectNames_matches_the_real_stryker_configs()
    {
        var tested = MutationAccounting.TestedProjectNames(RepositoryPath.Root());

        Assert.Equal(
            new HashSet<string>
            {
                "NarrativeTrace.Core", "NarrativeTrace.Clarity", "NarrativeTrace.Glossary", "NarrativeTrace.Proxy",
            },
            tested);
    }
}
