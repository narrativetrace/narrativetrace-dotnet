// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Build;
using Xunit;

namespace Build.Tests;

/// <summary>
/// Covers the marker machinery behind the <c>SnippetCheck</c> / <c>SnippetSync</c>
/// build targets — rule 8 (docs as tests): a quickstart's code and output
/// blocks are embedded from a real project, never typed into the page.
/// </summary>
public sealed class SnippetCheckSupportTests : IDisposable
{
    private readonly string _repo = Directory.CreateTempSubdirectory("nt-snippet-check").FullName;

    public void Dispose() => Directory.Delete(_repo, recursive: true);

    private void Write(string relativePath, string content)
    {
        var file = Path.Combine(_repo, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, content);
    }

    private string Read(string relativePath) => File.ReadAllText(Path.Combine(_repo, relativePath));

    [Fact]
    public void A_block_matching_its_source_reports_no_problems()
    {
        Write("src/Program.cs", "Console.WriteLine(\"hi\");\n");
        Write("documentation/quickstart.md",
            "# Quickstart\n\n"
            + "<!-- snippet: src/Program.cs -->\n"
            + "```csharp\n"
            + "Console.WriteLine(\"hi\");\n"
            + "```\n"
            + "<!-- /snippet -->\n");

        Assert.Empty(SnippetCheckSupport.Check(_repo));
    }

    [Fact]
    public void A_block_that_has_drifted_from_its_source_names_both_paths()
    {
        Write("src/Program.cs", "Console.WriteLine(\"hi\");\n");
        Write("documentation/quickstart.md",
            "<!-- snippet: src/Program.cs -->\n"
            + "```csharp\n"
            + "Console.WriteLine(\"bye\");\n"
            + "```\n"
            + "<!-- /snippet -->\n");

        var problem = Assert.Single(SnippetCheckSupport.Check(_repo));

        Assert.Contains("documentation/quickstart.md", problem, StringComparison.Ordinal);
        Assert.Contains("src/Program.cs", problem, StringComparison.Ordinal);
        Assert.Contains("drifted", problem, StringComparison.Ordinal);
    }

    [Fact]
    public void Mask_duration_ignores_a_changed_timing_but_not_a_changed_line()
    {
        Write("out/trace.txt", "PlaceOrder(...) — 8ms\n");
        Write("documentation/quickstart.md",
            "<!-- snippet: out/trace.txt mask=duration -->\n"
            + "```text\n"
            + "PlaceOrder(...) — 13ms\n"
            + "```\n"
            + "<!-- /snippet -->\n");

        Assert.Empty(SnippetCheckSupport.Check(_repo));
    }

    [Fact]
    public void Unknown_mask_is_reported_as_a_problem()
    {
        Write("out/trace.txt", "line\n");
        Write("documentation/quickstart.md",
            "<!-- snippet: out/trace.txt mask=bogus -->\n"
            + "```text\n"
            + "line\n"
            + "```\n"
            + "<!-- /snippet -->\n");

        var problem = Assert.Single(SnippetCheckSupport.Check(_repo));

        Assert.Contains("unknown mask", problem, StringComparison.Ordinal);
    }

    [Fact]
    public void A_region_embeds_only_the_window_between_its_markers_dedented()
    {
        Write("src/Program.cs",
            "using System;\n\n"
            + "class Program {\n"
            + "    // snippet:begin body\n"
            + "    static void Main() {\n"
            + "        Console.WriteLine(\"hi\");\n"
            + "    }\n"
            + "    // snippet:end body\n"
            + "}\n");
        Write("documentation/quickstart.md",
            "<!-- snippet: src/Program.cs region=body -->\n"
            + "```csharp\n"
            + "static void Main() {\n"
            + "    Console.WriteLine(\"hi\");\n"
            + "}\n"
            + "```\n"
            + "<!-- /snippet -->\n");

        Assert.Empty(SnippetCheckSupport.Check(_repo));
    }

    [Fact]
    public void A_missing_region_is_reported()
    {
        Write("src/Program.cs", "no regions here\n");
        Write("documentation/quickstart.md",
            "<!-- snippet: src/Program.cs region=body -->\n"
            + "```csharp\n"
            + "anything\n"
            + "```\n"
            + "<!-- /snippet -->\n");

        var problem = Assert.Single(SnippetCheckSupport.Check(_repo));

        Assert.Contains("region 'body' not found", problem, StringComparison.Ordinal);
    }

    [Fact]
    public void A_missing_source_file_is_reported()
    {
        Write("documentation/quickstart.md",
            "<!-- snippet: src/Gone.cs -->\n"
            + "```csharp\n"
            + "anything\n"
            + "```\n"
            + "<!-- /snippet -->\n");

        var problem = Assert.Single(SnippetCheckSupport.Check(_repo));

        Assert.Contains("is missing", problem, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("<!-- snippet: src/Program.cs -->\nnot a fence\n```\n<!-- /snippet -->\n", "right after the marker")]
    [InlineData("<!-- snippet: src/Program.cs -->\n```csharp\nunclosed\n", "never closed")]
    [InlineData("<!-- snippet: src/Program.cs -->\n```csharp\nx\n```\nnot the close marker\n", "right after the closing fence")]
    public void Malformed_marker_shapes_are_reported(string page, string expectedFragment)
    {
        Write("src/Program.cs", "x\n");
        Write("documentation/quickstart.md", page);

        var problem = Assert.Single(SnippetCheckSupport.Check(_repo));

        Assert.Contains(expectedFragment, problem, StringComparison.Ordinal);
    }

    [Fact]
    public void A_marker_inside_a_translated_mirror_is_rejected()
    {
        Write("src/Program.cs", "x\n");
        Write("documentation/quickstart.md", "# Quickstart\n");
        Write("documentation/es/quickstart.md",
            "<!-- source: documentation/quickstart.md blob "
            + TranslationCheckSupport.GitBlobHash(System.Text.Encoding.UTF8.GetBytes("# Quickstart\n"))[..12]
            + " | translated: 2026-09-11 -->\n"
            + "<!-- snippet: src/Program.cs -->\n```csharp\nx\n```\n<!-- /snippet -->\n");

        var problem = Assert.Single(SnippetCheckSupport.Check(_repo));

        Assert.Contains("documentation/es/quickstart.md", problem, StringComparison.Ordinal);
        Assert.Contains("must not carry snippet markers", problem, StringComparison.Ordinal);
    }

    [Fact]
    public void Sync_rewrites_a_drifted_block_and_reports_the_change()
    {
        Write("src/Program.cs", "Console.WriteLine(\"hi\");\n");
        Write("documentation/quickstart.md",
            "before\n\n"
            + "<!-- snippet: src/Program.cs -->\n"
            + "```csharp\n"
            + "Console.WriteLine(\"bye\");\n"
            + "```\n"
            + "<!-- /snippet -->\n\n"
            + "after\n");

        var changes = SnippetCheckSupport.Sync(_repo);

        Assert.Single(changes);
        Assert.Contains("src/Program.cs", changes[0], StringComparison.Ordinal);
        var updated = Read("documentation/quickstart.md");
        Assert.Contains("Console.WriteLine(\"hi\");", updated, StringComparison.Ordinal);
        Assert.DoesNotContain("Console.WriteLine(\"bye\");", updated, StringComparison.Ordinal);
        Assert.StartsWith("before\n\n<!-- snippet:", updated, StringComparison.Ordinal);
        Assert.EndsWith("<!-- /snippet -->\n\nafter\n", updated, StringComparison.Ordinal);
        Assert.Empty(SnippetCheckSupport.Check(_repo));
    }

    [Fact]
    public void Sync_is_idempotent_and_leaves_an_up_to_date_page_untouched()
    {
        Write("src/Program.cs", "Console.WriteLine(\"hi\");\n");
        var page =
            "<!-- snippet: src/Program.cs -->\n"
            + "```csharp\n"
            + "Console.WriteLine(\"hi\");\n"
            + "```\n"
            + "<!-- /snippet -->\n";
        Write("documentation/quickstart.md", page);

        var changes = SnippetCheckSupport.Sync(_repo);

        Assert.Empty(changes);
        Assert.Equal(page, Read("documentation/quickstart.md"));
    }

    [Fact]
    public void Sync_never_writes_to_a_translated_mirror()
    {
        Write("src/Program.cs", "x\n");
        var mirror =
            "<!-- source: documentation/quickstart.md blob "
            + TranslationCheckSupport.GitBlobHash(System.Text.Encoding.UTF8.GetBytes("# Quickstart\n"))[..12]
            + " | translated: 2026-09-11 -->\n"
            + "some prose, not English\n";
        Write("documentation/quickstart.md", "# Quickstart\n");
        Write("documentation/es/quickstart.md", mirror);

        SnippetCheckSupport.Sync(_repo);

        Assert.Equal(mirror, Read("documentation/es/quickstart.md"));
    }

    [Fact]
    public void An_empty_repository_has_nothing_to_check_or_sync()
    {
        Assert.Empty(SnippetCheckSupport.Check(_repo));
        Assert.Empty(SnippetCheckSupport.Sync(_repo));
    }

    // The publish script stamps exactly this shape (SPDX + notice + copyright, no blank line
    // after) into every shipped source file at publish time only — never in-tree — so a page
    // matching the in-tree source must keep matching the same file's cold public-snapshot copy.
    private const string LicenseHeader =
        "// SPDX-License-Identifier: BUSL-1.1\n"
        + "// Licensed under the Business Source License 1.1 (see LICENSE)\n"
        + "// Copyright (c) 2026 Empower Agile\n";

    [Fact]
    public void A_license_header_on_the_source_is_ignored_on_both_sides()
    {
        Write("src/Program.cs", LicenseHeader + "Console.WriteLine(\"hi\");\n");
        Write("documentation/quickstart.md",
            "<!-- snippet: src/Program.cs -->\n"
            + "```csharp\n"
            + "Console.WriteLine(\"hi\");\n"
            + "```\n"
            + "<!-- /snippet -->\n");

        Assert.Empty(SnippetCheckSupport.Check(_repo));

        var changes = SnippetCheckSupport.Sync(_repo);
        Assert.Empty(changes);
        Assert.DoesNotContain("SPDX", Read("documentation/quickstart.md"), StringComparison.Ordinal);
    }

    [Fact]
    public void A_license_header_with_a_blank_line_after_it_is_stripped_along_with_the_blank_line()
    {
        Write("src/Program.cs", LicenseHeader + "\nConsole.WriteLine(\"hi\");\n");
        Write("documentation/quickstart.md",
            "<!-- snippet: src/Program.cs -->\n"
            + "```csharp\n"
            + "Console.WriteLine(\"hi\");\n"
            + "```\n"
            + "<!-- /snippet -->\n");

        Assert.Empty(SnippetCheckSupport.Check(_repo));
    }

    [Fact]
    public void A_source_without_a_license_header_is_unchanged()
    {
        Write("src/Program.cs", "Console.WriteLine(\"hi\");\n");
        Write("documentation/quickstart.md",
            "<!-- snippet: src/Program.cs -->\n"
            + "```csharp\n"
            + "Console.WriteLine(\"hi\");\n"
            + "```\n"
            + "<!-- /snippet -->\n");

        Assert.Empty(SnippetCheckSupport.Check(_repo));
    }

    [Fact]
    public void A_non_license_leading_comment_is_preserved_and_must_appear_in_the_page()
    {
        Write("src/Program.cs", "// A worked example, not the license header.\nConsole.WriteLine(\"hi\");\n");
        Write("documentation/quickstart.md",
            "<!-- snippet: src/Program.cs -->\n"
            + "```csharp\n"
            + "Console.WriteLine(\"hi\");\n"
            + "```\n"
            + "<!-- /snippet -->\n");

        var problem = Assert.Single(SnippetCheckSupport.Check(_repo));

        Assert.Contains("drifted", problem, StringComparison.Ordinal);
    }
}
