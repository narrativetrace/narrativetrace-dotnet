// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.SkillsEvals;
using Xunit;

namespace NarrativeTrace.SkillsEvals.Tests;

/// <summary>
/// The Cursor lane end to end through the runner's own entry point (Phase 8 D4): with the committed
/// ledger's allowance of 0 it prints one SKIP line naming the tool, exits green, and writes nothing.
/// </summary>
public sealed class CursorLaneSkipTests
{
    private static readonly string[] Argv =
        ["--skill", "narrativetrace-doctor", "--case", "happy-path", "--platform", "cursor", "--model", "auto"];

    [Fact]
    public void A_cursor_trial_with_an_allowance_of_zero_skips_with_one_line_and_exits_green()
    {
        var output = new StringWriter();
        var error = new StringWriter();

        var exit = EvalRunner.Run(Argv, output, error);

        Assert.Equal(0, exit);
        var skip = Assert.Single(output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries), line => line.StartsWith("SKIP", StringComparison.Ordinal));
        Assert.Contains("`agent`", skip, StringComparison.Ordinal);
        Assert.DoesNotContain("trial 1/", output.ToString(), StringComparison.Ordinal);
        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public void A_skipped_trial_appends_neither_a_ledger_row_nor_a_spend_row()
    {
        var ledger = Path.Combine(RepoRoot.Locate(), "skills-evals", "ledger");
        var before = (File.ReadAllText(Path.Combine(ledger, "runs.jsonl")), File.ReadAllText(Path.Combine(ledger, "quota.md")));

        EvalRunner.Run(Argv, new StringWriter(), new StringWriter());

        Assert.Equal(before, (File.ReadAllText(Path.Combine(ledger, "runs.jsonl")), File.ReadAllText(Path.Combine(ledger, "quota.md"))));
    }
}
