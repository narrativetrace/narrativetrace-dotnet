// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Globalization;

namespace NarrativeTrace.SkillsEvals;

/// <summary>One row of <c>ledger/quota.md</c>'s allowance table: a platform's plan tier and weekly cap.</summary>
public sealed record QuotaAllowance(Platform Platform, string PlanTier, int WeeklyAllowance);

/// <summary>One appended row of <c>ledger/quota.md</c>'s Spend log: one sporadic-lane trial.</summary>
public sealed record QuotaSpendRow(string Date, Platform Platform, string Skill, string CaseName, string Week)
{
    /// <summary>The exact pipe-table row appended to the Spend log for one sporadic-lane trial.</summary>
    public string ToMarkdownRow() =>
        $"| {Date} | {Platform.WireName()} | {Skill} | {CaseName} | {Week} |";
}

/// <summary><c>ledger/quota.md</c>, parsed: the allowance table plus every appended spend-log row.</summary>
public sealed record QuotaLedger(IReadOnlyList<QuotaAllowance> Allowances, IReadOnlyList<QuotaSpendRow> Spend);

/// <summary>Whether a sporadic-lane trial may spend quota right now, and why not when it may not.</summary>
public sealed record QuotaDecision(bool Allowed, string? Reason)
{
    /// <summary>The allowed decision (no reason).</summary>
    public static QuotaDecision Allow() => new(true, null);

    /// <summary>A refused decision carrying a non-blank reason.</summary>
    public static QuotaDecision Refused(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("a refused QuotaDecision's reason must not be blank", nameof(reason));
        }

        return new QuotaDecision(false, reason);
    }
}
