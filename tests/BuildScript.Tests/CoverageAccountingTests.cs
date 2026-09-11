// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Build;
using Xunit;

namespace Build.Tests;

/// <summary>
/// Guards the coverage GATE-or-EXEMPTION accounting the <c>Coverage</c>
/// target enforces. A test project absent from both maps used to run with
/// coverage silently collected and nothing enforced; this is the check that
/// turns that silence into a build failure.
/// </summary>
public sealed class CoverageAccountingTests
{
    [Fact]
    public void A_project_present_in_neither_map_is_unaccounted()
    {
        var unaccounted = CoverageAccounting.Unaccounted(
            ["NarrativeTrace.Core.Tests", "NarrativeTrace.Orphan.Tests"],
            gated: ["NarrativeTrace.Core.Tests"],
            exempted: []);

        var name = Assert.Single(unaccounted);
        Assert.Equal("NarrativeTrace.Orphan.Tests", name);
    }

    [Fact]
    public void A_gated_project_is_accounted_for()
    {
        var unaccounted = CoverageAccounting.Unaccounted(
            ["NarrativeTrace.Core.Tests"],
            gated: ["NarrativeTrace.Core.Tests"],
            exempted: []);

        Assert.Empty(unaccounted);
    }

    [Fact]
    public void An_exempted_project_is_accounted_for()
    {
        var unaccounted = CoverageAccounting.Unaccounted(
            ["NarrativeTrace.ArchTests"],
            gated: [],
            exempted: ["NarrativeTrace.ArchTests"]);

        Assert.Empty(unaccounted);
    }

    [Fact]
    public void Multiple_unaccounted_projects_are_all_reported_sorted()
    {
        var unaccounted = CoverageAccounting.Unaccounted(
            ["NarrativeTrace.Zebra.Tests", "NarrativeTrace.Alpha.Tests"],
            gated: [],
            exempted: []);

        Assert.Equal(
            ["NarrativeTrace.Alpha.Tests", "NarrativeTrace.Zebra.Tests"], unaccounted);
    }

    [Fact]
    public void A_duplicate_name_is_reported_once()
    {
        var unaccounted = CoverageAccounting.Unaccounted(
            ["NarrativeTrace.Orphan.Tests", "NarrativeTrace.Orphan.Tests"],
            gated: [],
            exempted: []);

        Assert.Single(unaccounted);
    }

    [Fact]
    public void A_name_gated_and_exempted_at_once_is_flagged_as_doubly_accounted()
    {
        var doubly = CoverageAccounting.DoublyAccounted(
            gated: ["NarrativeTrace.Core.Tests"],
            exempted: ["NarrativeTrace.Core.Tests"]);

        var name = Assert.Single(doubly);
        Assert.Equal("NarrativeTrace.Core.Tests", name);
    }

    [Fact]
    public void No_real_project_is_both_gated_and_exempted()
    {
        var doubly = CoverageAccounting.DoublyAccounted(
            CoverageAccounting.Thresholds.Keys.ToList(),
            CoverageAccounting.Exemptions.Keys.ToList());

        Assert.Empty(doubly);
    }

    /// <summary>
    /// The live check, against the real solution and the real maps — mirrors
    /// <c>TestProjectSelectionTests</c>'s own "against the real repo" case.
    /// This is what actually proves the two maps in <see cref="CoverageAccounting"/>
    /// cover every project on disk today, not a synthetic name list that
    /// could drift from it.
    /// </summary>
    [Fact]
    public void Every_currently_selected_test_project_is_accounted_for()
    {
        var repoRoot = RepositoryPath.Root();
        var projectNames = TestProjectSelection.TestProjectFiles(repoRoot)
            .Select(Path.GetFileNameWithoutExtension)
            .Where(name => name is not null && TestProjectSelection.IsSelected(name))
            .Select(name => name!);

        var unaccounted = CoverageAccounting.Unaccounted(
            projectNames,
            CoverageAccounting.Thresholds.Keys.ToList(),
            CoverageAccounting.Exemptions.Keys.ToList());

        Assert.Empty(unaccounted);
    }
}
