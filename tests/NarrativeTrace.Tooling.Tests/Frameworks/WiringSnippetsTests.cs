// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Tooling.Frameworks;
using Xunit;

namespace NarrativeTrace.Tooling.Tests.Frameworks;

/// <summary>
/// The wiring lines the doctor prints come from the embedded <c>wiring-snippets.md</c>, whose blocks
/// <c>SnippetSync</c> writes from the rows' compiled fixtures — so the table and the resource must name
/// the same fixtures, and the parser must read only what a marked block holds.
/// </summary>
public sealed class WiringSnippetsTests
{
    [Fact]
    public void Every_source_wired_row_has_a_section_naming_its_own_fixture_and_region()
    {
        foreach (var row in FrameworkTable.Rows)
        {
            if (row.Wiring is not Wiring.Snippet snippet)
            {
                continue;
            }

            var entry = WiringSnippets.Entry(row.Id);
            Assert.Equal((snippet.Fixture, snippet.Region), (entry.Fixture, entry.Region));
            Assert.False(string.IsNullOrWhiteSpace(entry.Body), $"{row.Id} has an empty block");
        }
    }

    [Fact]
    public void Every_section_belongs_to_a_source_wired_row()
    {
        var sourceWired = FrameworkTable.Rows.Where(row => row.Wiring is Wiring.Snippet).Select(row => row.Id);

        Assert.Equal(sourceWired.Order(), WiringSnippets.Entries.Keys.Order());
    }

    [Fact]
    public void Each_rows_snippet_satisfies_its_own_evidence_so_the_printed_fix_is_the_accepted_one()
    {
        foreach (var row in FrameworkTable.Rows.Where(row => row.Wiring is Wiring.Snippet))
        {
            Assert.True(
                ((Wiring.Snippet)row.Wiring).AppliedIn([WiringSnippets.Text(row.Id)]),
                $"{row.Id}'s own snippet does not satisfy its evidence");
        }
    }

    [Fact]
    public void A_row_the_resource_has_no_section_for_is_a_build_defect_not_a_runtime_case()
    {
        var error = Assert.Throws<InvalidOperationException>(() => WiringSnippets.Entry("msbuild-clarity"));

        Assert.Contains("msbuild-clarity", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_marked_block_parses_into_its_fixture_region_and_body()
    {
        var entries = WiringSnippets.Parse(
            "## row\n\n<!-- snippet: a/B.cs region=wiring -->\n```csharp\nline one\nline two\n```\n<!-- /snippet -->\n");

        Assert.Equal(new WiringSnippets.SnippetEntry("a/B.cs", "wiring", "line one\nline two\n"), entries["row"]);
    }

    [Fact]
    public void A_heading_inside_a_fenced_block_is_body_never_a_new_section()
    {
        var entries = WiringSnippets.Parse(
            "## row\n<!-- snippet: a/B.cs -->\n```md\n## not-a-row\n```\n<!-- /snippet -->\n");

        Assert.Equal(["row"], entries.Keys);
        Assert.Equal("## not-a-row\n", entries["row"].Body);
    }

    [Theory]
    [InlineData("## row\n<!-- snippet: a/B.cs -->\n```csharp\nnever closed\n")]
    [InlineData("## row\n<!-- snippet: a/B.cs -->\nno fence follows\n")]
    [InlineData("## row\n```csharp\nan unmarked block\n```\n")]
    [InlineData("## row\n<!-- snippet:a/B.cs -->\n```csharp\nmarker missing its space\n```\n")]
    [InlineData("## row\n<!-- snippet: -->\n```csharp\nmarker without a path\n```\n")]
    public void Only_a_marker_followed_by_a_closed_fence_is_an_entry(string markdown)
    {
        Assert.Empty(WiringSnippets.Parse(markdown));
    }

    [Fact]
    public void A_section_keeps_its_first_block_when_a_second_follows()
    {
        var entries = WiringSnippets.Parse(
            "## row\n<!-- snippet: a/First.cs -->\n```\none\n```\n## row\n<!-- snippet: a/Second.cs -->\n```\ntwo\n```\n");

        Assert.Equal("a/First.cs", entries["row"].Fixture);
    }

    [Fact]
    public void Windows_line_endings_parse_like_unix_ones()
    {
        var entries = WiringSnippets.Parse("## row\r\n<!-- snippet: a/B.cs -->\r\n```\r\nbody\r\n```\r\n");

        Assert.Equal("body\n", entries["row"].Body);
    }
}
