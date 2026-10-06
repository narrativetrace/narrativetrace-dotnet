// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.SkillsEvals;
using Xunit;

namespace NarrativeTrace.SkillsEvals.Tests;

public sealed class QuotaMarkdownTests
{
    // The shape this repo's ledger/quota.md actually carries: an allowance table under the top
    // heading (not a "## Allowance" section), then a "## Spend log" table the runner appends to.
    private const string Ledger =
        "# Tier B Quota Ledger\n\n"
        + "Budget notes...\n\n"
        + "| platform | plan tier | weekly allowance |\n"
        + "|---|---|---|\n"
        + "| codex | basic | 4 |\n"
        + "| gemini | not installed | 0 |\n\n"
        + "## Spend log\n\n"
        + "| date | platform | skill | case | week |\n"
        + "|---|---|---|---|---|\n";

    [Fact]
    public void Parses_the_allowance_table_and_an_empty_spend_log()
    {
        var ledger = QuotaMarkdown.Parse(Ledger);

        Assert.Equal(2, ledger.Allowances.Count);
        Assert.Equal(4, ledger.Allowances.Single(a => a.Platform == Platform.Codex).WeeklyAllowance);
        Assert.Empty(ledger.Spend);
    }

    [Fact]
    public void Iso_week_is_monday_start_padded()
    {
        Assert.Equal("2026-W40", QuotaMarkdown.IsoWeek(new DateOnly(2026, 9, 28)));
    }

    [Fact]
    public void Under_the_allowance_is_allowed()
    {
        var decision = QuotaMarkdown.CheckQuota(QuotaMarkdown.Parse(Ledger), Platform.Codex, new DateOnly(2026, 9, 28));

        Assert.True(decision.Allowed);
    }

    [Fact]
    public void At_the_allowance_boundary_it_is_refused()
    {
        var week = QuotaMarkdown.IsoWeek(new DateOnly(2026, 9, 28));
        var spend = Enumerable.Range(0, 4)
            .Select(_ => new QuotaSpendRow("2026-09-28", Platform.Codex, "s", "c", week))
            .ToList();
        var ledger = QuotaMarkdown.Parse(Ledger) with { Spend = spend };

        var decision = QuotaMarkdown.CheckQuota(ledger, Platform.Codex, new DateOnly(2026, 9, 28));

        Assert.False(decision.Allowed);
        Assert.Contains("allowance", decision.Reason!, StringComparison.Ordinal);
    }

    [Fact]
    public void Spend_in_a_different_week_does_not_count_against_this_week()
    {
        var spend = Enumerable.Range(0, 4)
            .Select(_ => new QuotaSpendRow("2026-09-21", Platform.Codex, "s", "c", "2026-W39"))
            .ToList();
        var ledger = QuotaMarkdown.Parse(Ledger) with { Spend = spend };

        Assert.True(QuotaMarkdown.CheckQuota(ledger, Platform.Codex, new DateOnly(2026, 9, 28)).Allowed);
    }

    [Fact]
    public void A_platform_with_no_allowance_row_is_refused()
    {
        // gemini has a 0 allowance here; a platform absent from the table entirely is refused too.
        var ledger = new QuotaLedger([], []);

        var decision = QuotaMarkdown.CheckQuota(ledger, Platform.Codex, new DateOnly(2026, 9, 28));

        Assert.False(decision.Allowed);
        Assert.Contains("no allowance row", decision.Reason!, StringComparison.Ordinal);
    }

    [Fact]
    public void An_appended_spend_row_matches_the_table_shape()
    {
        var row = new QuotaSpendRow("2026-09-28", Platform.Codex, "add-narrative-tracing", "happy-path", "2026-W40");

        Assert.Equal(
            "| 2026-09-28 | codex | add-narrative-tracing | happy-path | 2026-W40 |\n",
            QuotaMarkdown.AppendedSpendRowLine(row));
    }
}
