// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.SkillsEvals;
using Xunit;

namespace NarrativeTrace.SkillsEvals.Tests;

public sealed class SporadicPolicyTests
{
    private static readonly QuotaLedger CodexAllowed =
        new([new QuotaAllowance(Platform.Codex, "basic", 4)], []);

    private static readonly DateOnly Today = new(2026, 9, 28);

    // A green deterministic-tier check: the real seam runs ./build.sh and throws on a non-zero
    // exit, so a no-op stands in for "SkillsLint and SkillsReplay passed".
    private static void TierGreen(IReadOnlyList<string> _, string __)
    {
        // intentionally empty — green tiers do nothing but return.
    }

    private static void TierRed(IReadOnlyList<string> _, string __) =>
        throw new InvalidOperationException("tiers red");

    [Fact]
    public void Claude_is_exempt_even_when_the_tier_check_would_throw()
    {
        // The tier runner would throw, but a non-sporadic platform never invokes it.
        var ex = Record.Exception(() =>
            SporadicPolicy.BeforeTrial(Platform.Claude, "/repo", TierRed, new QuotaLedger([], []), Today));

        Assert.Null(ex);
    }

    [Fact]
    public void A_sporadic_lane_with_green_tiers_and_spare_quota_is_allowed()
    {
        var ex = Record.Exception(() =>
            SporadicPolicy.BeforeTrial(Platform.Codex, "/repo", TierGreen, CodexAllowed, Today));

        Assert.Null(ex);
    }

    [Fact]
    public void A_sporadic_lane_refuses_when_the_deterministic_tiers_are_red()
    {
        Assert.Throws<InvalidOperationException>(() =>
            SporadicPolicy.BeforeTrial(Platform.Codex, "/repo", TierRed, CodexAllowed, Today));
    }

    [Fact]
    public void A_sporadic_lane_refuses_when_the_weekly_allowance_is_spent()
    {
        var week = QuotaMarkdown.IsoWeek(Today);
        var spent = Enumerable.Range(0, 4)
            .Select(_ => new QuotaSpendRow("2026-09-28", Platform.Codex, "s", "c", week))
            .ToList();
        var ledger = CodexAllowed with { Spend = spent };

        var ex = Assert.Throws<InvalidOperationException>(() =>
            SporadicPolicy.BeforeTrial(Platform.Codex, "/repo", TierGreen, ledger, Today));
        Assert.Contains("allowance", ex.Message, StringComparison.Ordinal);
    }

    // --- the skip line ---------------------------------------------------------------------------

    private static readonly QuotaLedger NotInstalled =
        new([new QuotaAllowance(Platform.Cursor, "not installed", 0)], []);

    [Fact]
    public void A_lane_with_a_weekly_allowance_of_zero_skips_with_one_line_naming_its_tool()
    {
        var reason = SporadicPolicy.SkipReason(Platform.Cursor, NotInstalled);

        Assert.NotNull(reason);
        Assert.StartsWith("SKIP", reason, StringComparison.Ordinal);
        Assert.Contains("cursor", reason, StringComparison.Ordinal);
        Assert.Contains("`agent`", reason, StringComparison.Ordinal);
        Assert.Contains("ledger/quota.md", reason, StringComparison.Ordinal);
        Assert.DoesNotContain('\n', reason);
    }

    [Fact]
    public void A_lane_with_an_allowance_does_not_skip_and_neither_does_the_exempt_lane()
    {
        Assert.Null(SporadicPolicy.SkipReason(Platform.Codex, CodexAllowed));
        Assert.Null(SporadicPolicy.SkipReason(Platform.Claude, NotInstalled));
    }

    /// <summary>A missing row is a ledger the owner has not finished — a refusal, never a skip.</summary>
    [Fact]
    public void A_lane_with_no_allowance_row_at_all_does_not_skip_so_the_refusal_still_speaks()
    {
        Assert.Null(SporadicPolicy.SkipReason(Platform.Cursor, CodexAllowed));
    }

    [Fact]
    public void A_spent_allowance_is_a_refusal_and_not_a_skip()
    {
        var spent = new QuotaLedger(
            [new QuotaAllowance(Platform.Cursor, "pro", 1)],
            [new QuotaSpendRow("2026-09-28", Platform.Cursor, "s", "c", QuotaMarkdown.IsoWeek(Today))]);

        Assert.Null(SporadicPolicy.SkipReason(Platform.Cursor, spent));
    }

    [Fact]
    public void The_real_ledger_lists_cursor_at_zero_so_the_lane_skips_today()
    {
        var ledger = QuotaMarkdown.Parse(File.ReadAllText(
            Path.Combine(RepoRoot.Locate(), "skills-evals", "ledger", "quota.md")));

        Assert.NotNull(SporadicPolicy.SkipReason(Platform.Cursor, ledger));
    }
}
