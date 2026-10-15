// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Skills.Catalogue;
using NarrativeTrace.SkillsEvals;
using Xunit;

namespace NarrativeTrace.SkillsEvals.Tests;

public sealed class PromotionRendererTests
{
    private static RunLedgerRow Row(string skill, string caseName, Platform platform, bool pass, string date) =>
        new(date, skill, caseName, platform, "haiku", 1, pass);

    [Fact]
    public void With_no_runs_every_cell_reads_not_yet_run()
    {
        var md = PromotionRenderer.Render(SkillCatalogue.Skills, []);

        Assert.Contains("| narrativetrace-doctor | not yet run | not yet run | not yet run |", md, StringComparison.Ordinal);
        Assert.Contains("| add-narrative-tracing | not yet run | not yet run | not yet run |", md, StringComparison.Ordinal);
    }

    [Fact]
    public void Reproduces_the_repos_header_and_footer()
    {
        var md = PromotionRenderer.Render(SkillCatalogue.Skills, []);

        Assert.StartsWith("# Promotion Matrix\n\nA cell turns `approved`", md, StringComparison.Ordinal);
        Assert.EndsWith("promotion to fully-approved status in the catalogue.\n", md, StringComparison.Ordinal);
    }

    [Fact]
    public void The_latest_trial_outcome_shows_green_or_red_with_model_and_date()
    {
        var runs = new[]
        {
            Row("add-narrative-tracing", "init-prompt-empty-project", Platform.Claude, false, "2026-09-25T10:00:00Z"),
            Row("add-narrative-tracing", "init-prompt-empty-project", Platform.Claude, true, "2026-09-26T10:00:00Z"),
        };

        var md = PromotionRenderer.Render(SkillCatalogue.Skills, runs);

        Assert.Contains("green (haiku, 2026-09-26)", md, StringComparison.Ordinal);
        Assert.DoesNotContain("red (haiku", md, StringComparison.Ordinal);
    }

    [Fact]
    public void A_green_trigger_and_a_green_happy_path_promote_the_cell_to_approved()
    {
        var runs = new[]
        {
            Row("narrativetrace-doctor", "trigger-sample", Platform.Claude, true, "2026-09-26T10:00:00Z"),
            Row("narrativetrace-doctor", "happy-path", Platform.Claude, true, "2026-09-26T11:00:00Z"),
        };

        var md = PromotionRenderer.Render(SkillCatalogue.Skills, runs);

        Assert.Contains("| narrativetrace-doctor | approved |", md, StringComparison.Ordinal);
    }

    [Fact]
    public void The_matrix_has_a_cursor_column_after_antigravity()
    {
        var row = Row("narrativetrace-doctor", "happy-path", Platform.Cursor, true, "2026-10-10T10:00:00Z");

        var md = PromotionRenderer.Render(SkillCatalogue.Skills, [row]);

        Assert.Contains("| Skill | Claude | Codex | Antigravity | Cursor |\n|---|---|---|---|---|\n", md, StringComparison.Ordinal);
        Assert.Contains("| narrativetrace-doctor | not yet run | not yet run | not yet run | green (haiku, 2026-10-10) |", md, StringComparison.Ordinal);
    }

    [Fact]
    public void The_committed_promotion_matrix_is_what_the_ledger_renders()
    {
        var root = RepoRoot.Locate();
        var runs = RunLedgerRow.ParseJsonl(File.ReadAllText(Path.Combine(root, "skills-evals", "ledger", "runs.jsonl")));

        Assert.Equal(
            PromotionRenderer.Render(SkillCatalogue.Skills, runs),
            File.ReadAllText(Path.Combine(root, "skills-evals", "ledger", "promotion.md")));
    }
}
