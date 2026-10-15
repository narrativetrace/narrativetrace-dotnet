// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Tooling.Init;
using NarrativeTrace.Tooling.Platforms;
using Xunit;

namespace NarrativeTrace.Cli.Tests;

/// <summary>
/// Phase 8's "one source, many surfaces": the Cursor plugin's one rule, the docs page's table and the
/// pointers the installer writes all read <see cref="PlatformTable"/>. The committed files hold the
/// rendered result; these tests fail while any disagrees (the skills devtool's <c>render</c> rewrites
/// them), and read the REAL repository, from another assembly than the code they hold to account.
/// </summary>
public sealed class PlatformFilesDriftTests
{
    private const string CursorRulePath = ".cursor/rules/narrativetrace.mdc";

    private static string Read(string relative) =>
        File.ReadAllText(Path.Combine(RepoRoot.Find(), relative.Replace('/', Path.DirectorySeparatorChar)))
            .Replace("\r\n", "\n", StringComparison.Ordinal);

    [Fact]
    public void The_cursor_rule_the_plugin_ships_is_the_table_rows_pointer_with_no_stamp()
    {
        var rule = (Pointer.Rule)PlatformTable.Row("cursor")!.Pointer;

        Assert.Equal(CursorRule.Render(rule.Text, ""), Read(CursorRulePath));
    }

    [Fact]
    public void The_cursor_plugin_manifest_points_at_the_rule_file_this_test_holds()
    {
        Assert.Contains($"\"rules\": \"./{CursorRulePath}\"", Read(".cursor-plugin/plugin.json"), StringComparison.Ordinal);
        Assert.Equal(CursorRulePath, PlatformTable.Row("cursor")!.Pointer.Path);
    }

    [Fact]
    public void The_docs_page_table_is_the_table_rendered()
    {
        var page = Read(PlatformTableDocs.Page);

        Assert.Equal(page, PlatformTableDocs.Splice(page));
        Assert.Contains("<!-- platform-table:begin -->\n| Platform |", page, StringComparison.Ordinal);
    }

    [Fact]
    public void The_page_is_reachable_from_the_agent_entry_points()
    {
        Assert.Contains("use-with-your-agent.md", Read("documentation/guides/llms.txt"), StringComparison.Ordinal);
        Assert.Contains("use-with-your-agent.md", Read("README.md"), StringComparison.Ordinal);
        Assert.Contains("use-with-your-agent.md", Read("documentation/agent-skills.md"), StringComparison.Ordinal);
    }

    [Fact]
    public void Every_platform_the_table_names_is_named_on_the_page()
    {
        var page = Read(PlatformTableDocs.Page);

        Assert.All(PlatformTable.Rows, row => Assert.Contains(row.Name, page, StringComparison.Ordinal));
    }
}
