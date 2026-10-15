// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Tooling.Platforms;
using Xunit;

namespace NarrativeTrace.Tooling.Tests.Platforms;

/// <summary>The platform table rendered for readers (Phase 8 D5): the table at the top of the docs page.</summary>
public sealed class PlatformTableDocsTests
{
    [Fact]
    public void The_table_has_one_row_per_platform_in_table_order_under_one_header()
    {
        var table = PlatformTableDocs.Table();

        Assert.StartsWith(
            "| Platform | How a stranger gets the skills | What the agent finds after `init` | Evidence | Verified |\n" +
            "|---|---|---|---|---|\n",
            table,
            StringComparison.Ordinal);
        var last = -1;
        foreach (var row in PlatformTable.Rows)
        {
            var at = table.IndexOf($"| {row.Name} |", StringComparison.Ordinal);
            Assert.True(at > last, row.Id);
            last = at;
            Assert.Contains(row.InstallPath, table, StringComparison.Ordinal);
            Assert.Contains(row.AgentFinds, table, StringComparison.Ordinal);
            Assert.Contains(row.Verified, table, StringComparison.Ordinal);
        }

        Assert.Equal(2 + PlatformTable.Rows.Count, table.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);
    }

    [Fact]
    public void A_row_names_its_evidence_lane_or_says_it_is_a_manual_trial()
    {
        var table = PlatformTableDocs.Table();

        Assert.Contains("Tier B lane `cursor`", table, StringComparison.Ordinal);
        Assert.Contains("Tier B lane `antigravity`", table, StringComparison.Ordinal);
        Assert.Contains("manual trial", table, StringComparison.Ordinal);
    }

    [Fact]
    public void No_cell_breaks_the_table_with_a_pipe_or_a_newline()
    {
        foreach (var line in PlatformTableDocs.Table().Split('\n', StringSplitOptions.RemoveEmptyEntries).Skip(2))
        {
            Assert.Equal(7, line.Split('|').Length);
        }
    }

    [Fact]
    public void The_table_splices_between_its_markers_and_nowhere_else_and_splicing_twice_changes_nothing()
    {
        const string page = "before\n<!-- platform-table:begin -->\nstale\n<!-- platform-table:end -->\nafter\n";

        var rendered = PlatformTableDocs.Splice(page);

        Assert.StartsWith("before\n<!-- platform-table:begin -->\n| Platform |", rendered, StringComparison.Ordinal);
        Assert.EndsWith("<!-- platform-table:end -->\nafter\n", rendered, StringComparison.Ordinal);
        Assert.DoesNotContain("stale", rendered, StringComparison.Ordinal);
        Assert.Equal(rendered, PlatformTableDocs.Splice(rendered));
    }

    [Fact]
    public void A_page_without_the_markers_is_refused()
    {
        var error = Assert.Throws<ArgumentException>(() => PlatformTableDocs.Splice("no markers here"));

        Assert.Contains("platform-table", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_cell_that_would_carry_a_pipe_is_escaped_rather_than_adding_a_column()
    {
        Assert.Equal("a \\| b c", PlatformTableDocs.Cell("a | b\nc"));
    }
}
