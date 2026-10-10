// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Globalization;
using NarrativeTrace.Tooling.Doctor;
using NarrativeTrace.Tooling.Frameworks;
using Xunit;

namespace NarrativeTrace.Cli.Tests;

/// <summary>
/// Phase 6 D1's "one source, three surfaces": the doctor, <c>llms-full.md</c>'s framework table and
/// <c>llms.txt</c>'s covered-frameworks line all read <see cref="FrameworkTable"/>. The committed pages
/// hold the rendered blocks; these tests fail while either disagrees with the table (the skills
/// devtool's <c>render</c> rewrites them).
/// </summary>
public sealed class FrameworkTableDocsTests
{
    private static string Read(params string[] relative) =>
        File.ReadAllText(Path.Combine([RepoRoot.Find(), .. relative])).Replace("\r\n", "\n", StringComparison.Ordinal);

    [Fact]
    public void Llms_full_carries_the_framework_table_rendered_from_the_doctors_own_rows()
    {
        var page = Read(FrameworkTableDocs.LlmsFull.Split('/'));

        Assert.Equal(page, FrameworkTableDocs.RenderLlmsFull(page));
        Assert.Contains("<!-- framework-table:begin -->\n### Framework table", page, StringComparison.Ordinal);
    }

    [Fact]
    public void Llms_txt_carries_the_covered_frameworks_line_rendered_from_the_same_rows()
    {
        var page = Read(FrameworkTableDocs.LlmsTxt.Split('/'));

        Assert.Equal(page, FrameworkTableDocs.RenderLlmsTxt(page));
        Assert.Contains("<!-- covered-frameworks:begin -->\nCovered frameworks", page, StringComparison.Ordinal);
    }

    [Fact]
    public void The_table_has_one_line_per_row_naming_its_add_command_and_its_check()
    {
        var section = FrameworkTableDocs.LlmsFullSection();

        Assert.All(FrameworkTable.Rows, row =>
        {
            var line = section.Split('\n').Single(l => l.StartsWith($"| {row.Name} |", StringComparison.Ordinal));
            Assert.Contains($"`dotnet add package {row.Package.PackageId}`", line, StringComparison.Ordinal);
            Assert.Contains(
                row.Check switch
                {
                    CheckBinding.WiringCheck c => $"`{c.Id}`",
                    CheckBinding.ExistingCheck c => $"`{c.Id}` (existing)",
                    CheckBinding.NoCheck c => $"none — {c.Reason}",
                    _ => throw new InvalidOperationException(),
                },
                line,
                StringComparison.Ordinal);
        });
    }

    [Fact]
    public void Every_source_wired_rows_snippet_renders_under_the_same_snippet_marker_the_resource_uses()
    {
        var section = FrameworkTableDocs.LlmsFullSection();

        Assert.All(WiringSnippets.Entries, pair =>
        {
            var marker = $"<!-- snippet: {pair.Value.Fixture}" + (pair.Value.Region is null ? "" : $" region={pair.Value.Region}") + " -->";
            Assert.Contains($"{marker}\n```csharp\n{pair.Value.Body}```\n<!-- /snippet -->", section, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void The_covered_line_names_every_row_in_table_order_with_its_check_or_reason()
    {
        var line = FrameworkTableDocs.LlmsTxtLine();
        var at = 0;

        foreach (var row in FrameworkTable.Rows)
        {
            var next = line.IndexOf(row.Name + " (", at, StringComparison.Ordinal);
            Assert.True(next >= at, $"{row.Id} is missing or out of order");
            at = next;
        }
    }

    [Fact]
    public void Llms_txt_names_the_doctors_check_count_as_the_registry_holds_it()
    {
        var words = new[] { "zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten", "eleven",
            "twelve", "thirteen", "fourteen", "fifteen", "sixteen", "seventeen", "eighteen", "nineteen", "twenty" };
        var count = DoctorChecks.All.Count;

        Assert.Contains(
            $"its {words[count]} checks",
            Read("documentation", "guides", "llms.txt"),
            StringComparison.Ordinal);
        Assert.True(count < words.Length, count.ToString(CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Splicing_a_document_without_both_markers_is_refused()
    {
        Assert.Throws<ArgumentException>(() => FrameworkTableDocs.RenderLlmsTxt("no markers here"));
        Assert.Throws<ArgumentException>(() => FrameworkTableDocs.RenderLlmsTxt("<!-- covered-frameworks:end -->\n<!-- covered-frameworks:begin -->\n"));
    }
}
