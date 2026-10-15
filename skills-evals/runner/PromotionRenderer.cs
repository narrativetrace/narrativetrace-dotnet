// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Text;
using NarrativeTrace.Skills;

namespace NarrativeTrace.SkillsEvals;

/// <summary>
/// Regenerates <c>skills-evals/ledger/promotion.md</c> — the skill × platform matrix — from the
/// typed skill catalogue and <c>ledger/runs.jsonl</c>. Pure function: this is BUILD OUTPUT, meant
/// to be regenerated rather than hand-edited, mirroring the Python/Java reference. A cell reads
/// <c>not yet run</c> (no rows), <c>approved</c> (a green <c>trigger</c> case and a green
/// <c>happy-path</c> case), or the latest trial's outcome as <c>green (model, date)</c> /
/// <c>red (model, date)</c>.
/// </summary>
public static class PromotionRenderer
{
    private static readonly Platform[] Columns = [Platform.Claude, Platform.Codex, Platform.Antigravity, Platform.Cursor];

    /// <summary>Regenerates the full <c>promotion.md</c> content from the catalogue and the ledger.</summary>
    public static string Render(IReadOnlyList<Skill> skills, IReadOnlyList<RunLedgerRow> runs)
    {
        var sb = new StringBuilder();
        sb.Append("# Promotion Matrix\n\n");
        sb.Append("A cell turns `approved` once that platform has one green `trigger` case and\n");
        sb.Append("one green `happy-path` case for the skill, at its current wording and\n");
        sb.Append("fixture. A wording or fixture change clears that skill's row.\n\n");
        sb.Append("| Skill | Claude | Codex | Antigravity | Cursor |\n");
        sb.Append("|---|---|---|---|---|\n");
        foreach (var skill in skills)
        {
            AppendRow(sb, skill.CanonicalName, runs);
        }

        sb.Append('\n');
        sb.Append("Every skill is expected to eventually be approved by every platform; an\n");
        sb.Append("empty cell does not block the nightly build, but it blocks the skill's\n");
        sb.Append("promotion to fully-approved status in the catalogue.\n");
        return sb.ToString();
    }

    private static void AppendRow(StringBuilder sb, string skill, IReadOnlyList<RunLedgerRow> runs)
    {
        sb.Append("| ").Append(skill);
        foreach (var platform in Columns)
        {
            sb.Append(" | ").Append(Cell(skill, platform, runs));
        }

        sb.Append(" |\n");
    }

    private static string Cell(string skill, Platform platform, IReadOnlyList<RunLedgerRow> runs)
    {
        var rows = runs.Where(r => r.Skill == skill && r.Platform == platform).ToList();
        if (rows.Count == 0)
        {
            return "not yet run";
        }

        if (IsApproved(rows))
        {
            return "approved";
        }

        var latest = rows[^1];
        var date = latest.Date.Length >= 10 ? latest.Date[..10] : latest.Date;
        return (latest.Pass ? "green (" : "red (") + latest.Model + ", " + date + ")";
    }

    private static bool IsApproved(IReadOnlyList<RunLedgerRow> rows) =>
        rows.Any(r => r.CaseName.Contains("happy-path", StringComparison.Ordinal) && r.Pass)
        && rows.Any(r => r.CaseName.Contains("trigger", StringComparison.Ordinal) && r.Pass);
}
