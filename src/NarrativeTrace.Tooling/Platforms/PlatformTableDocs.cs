// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Text;
using NarrativeTrace.Tooling.Frameworks;

namespace NarrativeTrace.Tooling.Platforms;

/// <summary>
/// The platform table, rendered for readers: the table at the top of
/// <c>documentation/use-with-your-agent.md</c>.
/// </summary>
/// <remarks>
/// <para>INTENT: the page's one claim that changes with the vendors — how a stranger gets the skills on
/// each platform and what the agent finds — is a view of <see cref="PlatformTable"/>, never typed. The
/// block sits between <c>&lt;!-- platform-table:begin --&gt;</c>/<c>&lt;!-- platform-table:end --&gt;</c>
/// markers; the skills devtool's <c>render</c> rewrites it and <c>skills lint</c> fails while the page
/// disagrees. The Verified column is the honesty: no test reaches the vendors, so each row carries the
/// date its facts were last checked.</para>
/// <para>A cell never carries a pipe or a newline: translations of the page would mirror the table's
/// shape, and a stray pipe would add a column to the English page only.</para>
/// </remarks>
public static class PlatformTableDocs
{
    /// <summary>The page this table is rendered into, repository-relative.</summary>
    public const string Page = "documentation/use-with-your-agent.md";

    private const string Block = "platform-table";

    /// <summary>The Markdown table: a header, a separator, and one line per platform in table order.</summary>
    internal static string Table()
    {
        var table = new StringBuilder()
            .Append("| Platform | How a stranger gets the skills | What the agent finds after `init` | Evidence | Verified |\n")
            .Append("|---|---|---|---|---|\n");
        foreach (var row in PlatformTable.Rows)
        {
            table.Append(Line(row));
        }

        return table.ToString();
    }

    /// <summary>The page with its table block re-rendered.</summary>
    /// <exception cref="ArgumentException">The page lacks the block's markers.</exception>
    public static string Splice(string page) => FrameworkTableDocs.Splice(page, Block, Table());

    private static string Line(PlatformRow row) =>
        "| " + string.Join(
            " | ",
            new[] { row.Name, row.InstallPath, row.AgentFinds, Evidence(row), row.Verified }.Select(Cell)) + " |\n";

    private static string Evidence(PlatformRow row) =>
        row.EvidenceLane == PlatformRow.Manual ? "manual trial" : $"Tier B lane `{row.EvidenceLane}`";

    /// <summary>A table cell: a pipe is escaped and a newline folded to a space.</summary>
    internal static string Cell(string text) =>
        text.Replace("|", "\\|", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);
}
