// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Globalization;
using System.Text.RegularExpressions;

namespace NarrativeTrace.SkillsEvals;

/// <summary>
/// The sporadic-lanes quota guard, reading/writing this repo's <c>ledger/quota.md</c>: a weekly
/// allowance table (platform, plan tier, cap) plus a Spend log the runner appends one row to per
/// sporadic trial. There is no override flag by design — a spent allowance is fixed by editing the
/// file. This repo's quota.md puts the allowance table under the top heading (not a
/// <c>## Allowance</c> section as the Java port does), so the split point is the <c>## Spend log</c>
/// heading: everything before it is the allowance region, everything after is the spend region.
/// </summary>
public static class QuotaMarkdown
{
    private const string SpendHeading = "## Spend log";

    /// <summary>ISO 8601 week, e.g. <c>2026-W39</c> — Monday-start, week 1 holds the first Thursday.</summary>
    public static string IsoWeek(DateOnly date)
    {
        var day = date.ToDateTime(TimeOnly.MinValue);
        var week = ISOWeek.GetWeekOfYear(day);
        var year = ISOWeek.GetYear(day);
        return $"{year}-W{week.ToString("D2", CultureInfo.InvariantCulture)}";
    }

    /// <summary>Parses the allowance table and the spend log out of <paramref name="content"/>.</summary>
    public static QuotaLedger Parse(string content)
    {
        var split = content.IndexOf(SpendHeading, StringComparison.Ordinal);
        var allowanceRegion = split < 0 ? content : content[..split];
        var spendRegion = split < 0 ? "" : content[split..];
        return new QuotaLedger(ParseAllowances(allowanceRegion), ParseSpend(spendRegion));
    }

    /// <summary>How many sporadic trials <paramref name="platform"/> already spent in <paramref name="week"/>.</summary>
    public static long SpendCountThisWeek(QuotaLedger ledger, Platform platform, string week) =>
        ledger.Spend.Count(row => row.Platform == platform && row.Week == week);

    /// <summary>Whether <paramref name="platform"/> may spend now; no override — the owner edits quota.md.</summary>
    public static QuotaDecision CheckQuota(QuotaLedger ledger, Platform platform, DateOnly now)
    {
        var allowance = ledger.Allowances.FirstOrDefault(a => a.Platform == platform);
        if (allowance is null)
        {
            return QuotaDecision.Refused($"ledger/quota.md carries no allowance row for \"{platform.WireName()}\"");
        }

        var week = IsoWeek(now);
        var spent = SpendCountThisWeek(ledger, platform, week);
        if (spent >= allowance.WeeklyAllowance)
        {
            return QuotaDecision.Refused(
                $"{platform.WireName()}'s weekly allowance ({allowance.WeeklyAllowance}) is spent for "
                + $"{week} ({spent} run(s) already) — no override; the owner edits ledger/quota.md");
        }

        return QuotaDecision.Allow();
    }

    /// <summary>The exact line (row + newline) appended to the Spend log for one sporadic trial.</summary>
    public static string AppendedSpendRowLine(QuotaSpendRow row) => row.ToMarkdownRow() + "\n";

    private static List<QuotaAllowance> ParseAllowances(string region)
    {
        var allowances = new List<QuotaAllowance>();
        foreach (var cells in PipeRows(region))
        {
            if (cells.Count < 3 || Platforms.Parse(cells[0]) is not { } platform)
            {
                continue;
            }

            allowances.Add(new QuotaAllowance(platform, cells[1], ParseIntOrZero(cells[2])));
        }

        return allowances;
    }

    private static List<QuotaSpendRow> ParseSpend(string region)
    {
        var spend = new List<QuotaSpendRow>();
        foreach (var cells in PipeRows(region))
        {
            if (cells.Count < 5 || Platforms.Parse(cells[1]) is not { } platform)
            {
                continue;
            }

            spend.Add(new QuotaSpendRow(cells[0], platform, cells[2], cells[3], cells[4]));
        }

        return spend;
    }

    private static int ParseIntOrZero(string value) =>
        int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : 0;

    private static IEnumerable<List<string>> PipeRows(string region)
    {
        var headerSkipped = false;
        foreach (var raw in region.Split('\n'))
        {
            var line = raw.Trim();
            if (!IsPipeRow(line))
            {
                continue;
            }

            var cells = line[1..^1].Split('|').Select(cell => cell.Trim()).ToList();
            if (cells.All(IsSeparatorCell))
            {
                continue;
            }

            if (!headerSkipped)
            {
                headerSkipped = true;
                continue;
            }

            yield return cells;
        }
    }

    private static bool IsPipeRow(string line) =>
        line.StartsWith('|') && line.EndsWith('|') && line.Length >= 2;

    private static bool IsSeparatorCell(string cell) =>
        Regex.IsMatch(cell, "^:?-+:?$");
}
