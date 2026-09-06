// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Build;
using Xunit;

namespace Build.Tests;

/// <summary>
/// Covers <see cref="TranslationStructureSupport"/> — heading tree, code
/// block, table and link structure parity between a translation and its
/// English source.
/// </summary>
public sealed class TranslationStructureSupportTests : IDisposable
{
    private readonly string _repo = Directory.CreateTempSubdirectory("nt-translation-structure").FullName;

    public void Dispose() => Directory.Delete(_repo, recursive: true);

    private string Write(string relativePath, string content)
    {
        var file = Path.Combine(_repo, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, content);
        return file;
    }

    [Fact]
    public void Profile_counts_headings_code_blocks_and_table_shape()
    {
        var profile = TranslationStructureSupport.Profile("""
            # Title
            ## Sub

            ```csharp
            var x = 1;
            ```

            | A | B |
            |---|---|
            | 1 | 2 |
            | 3 | 4 |
            """);

        Assert.Equal([1, 2], profile.HeadingLevels);
        // The closing fence marker line itself is never appended to the captured block — it only
        // ever flushes it — so the block's content runs through the last line of code, not the
        // closing "```". Source and translation are affected identically, so parity still holds.
        Assert.Equal(["```csharp\nvar x = 1;"], profile.CodeBlocks);
        var table = Assert.Single(profile.Tables);
        Assert.Equal((2, 2), table);
    }

    [Fact]
    public void A_heading_inside_a_fence_is_not_counted()
    {
        var profile = TranslationStructureSupport.Profile("""
            # Title

            ```
            # not a heading
            ```
            """);

        Assert.Equal([1], profile.HeadingLevels);
    }

    [Fact]
    public void Identical_documents_compare_clean()
    {
        var text = "# Title\n\nSome text.\n";
        var comparison = TranslationStructureSupport.Compare(
            TranslationStructureSupport.Profile(text), TranslationStructureSupport.Profile(text), "source", "translation");

        Assert.Empty(comparison.Failures);
        Assert.Empty(comparison.Warnings);
    }

    [Fact]
    public void A_missing_heading_is_a_failure()
    {
        var source = TranslationStructureSupport.Profile("# Title\n\n## Sub\n");
        var translation = TranslationStructureSupport.Profile("# Title\n");

        var comparison = TranslationStructureSupport.Compare(source, translation, "source", "translation");

        var failure = Assert.Single(comparison.Failures);
        Assert.Contains("heading count", failure, StringComparison.Ordinal);
    }

    [Fact]
    public void A_reordered_heading_level_is_a_failure()
    {
        var source = TranslationStructureSupport.Profile("# Title\n\n## Sub\n\n### Sub-sub\n");
        var translation = TranslationStructureSupport.Profile("# Title\n\n### Sub\n\n## Sub-sub\n");

        var comparison = TranslationStructureSupport.Compare(source, translation, "source", "translation");

        Assert.Equal(2, comparison.Failures.Count);
    }

    [Fact]
    public void A_vanished_code_block_is_a_failure()
    {
        var source = TranslationStructureSupport.Profile("```csharp\nvar x = 1;\n```\n");
        var translation = TranslationStructureSupport.Profile("No code here.\n");

        var comparison = TranslationStructureSupport.Compare(source, translation, "source", "translation");

        Assert.Contains(comparison.Failures, f => f.Contains("code block count", StringComparison.Ordinal));
    }

    [Fact]
    public void A_code_block_with_translated_comments_only_warns()
    {
        var source = TranslationStructureSupport.Profile("```csharp\n// comment\nvar x = 1;\n```\n");
        var translation = TranslationStructureSupport.Profile("```csharp\n// comentario\nvar x = 1;\n```\n");

        var comparison = TranslationStructureSupport.Compare(source, translation, "source", "translation");

        Assert.Empty(comparison.Failures);
        var warning = Assert.Single(comparison.Warnings);
        Assert.Contains("code block 1", warning, StringComparison.Ordinal);
    }

    [Fact]
    public void A_table_missing_a_row_is_a_failure()
    {
        var source = TranslationStructureSupport.Profile("| A | B |\n|---|---|\n| 1 | 2 |\n| 3 | 4 |\n");
        var translation = TranslationStructureSupport.Profile("| A | B |\n|---|---|\n| 1 | 2 |\n");

        var comparison = TranslationStructureSupport.Compare(source, translation, "source", "translation");

        var failure = Assert.Single(comparison.Failures);
        Assert.Contains("rows 1 vs 2", failure, StringComparison.Ordinal);
    }

    [Fact]
    public void A_table_missing_a_column_is_a_failure()
    {
        var source = TranslationStructureSupport.Profile("| A | B |\n|---|---|\n| 1 | 2 |\n");
        var translation = TranslationStructureSupport.Profile("| A |\n|---|\n| 1 |\n");

        var comparison = TranslationStructureSupport.Compare(source, translation, "source", "translation");

        var failure = Assert.Single(comparison.Failures);
        Assert.Contains("columns 1 vs 2", failure, StringComparison.Ordinal);
    }

    [Fact]
    public void A_resolving_relative_link_is_not_reported()
    {
        Write("documentation/guides/es/target.md", "# Target\n");
        var file = Write("documentation/guides/es/source.md", "See [target](target.md).\n");

        Assert.Empty(TranslationStructureSupport.BrokenLinks(file, _repo));
    }

    [Fact]
    public void A_broken_relative_link_is_reported()
    {
        var file = Write("documentation/guides/es/source.md", "See [target](missing.md).\n");

        var problem = Assert.Single(TranslationStructureSupport.BrokenLinks(file, _repo));

        Assert.Contains("missing.md", problem, StringComparison.Ordinal);
        Assert.Contains("does not resolve", problem, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("[external](https://example.com/x)")]
    [InlineData("[anchor](#section)")]
    public void External_and_anchor_only_links_are_skipped(string markdown)
    {
        var file = Write("documentation/guides/es/source.md", $"See {markdown}.\n");

        Assert.Empty(TranslationStructureSupport.BrokenLinks(file, _repo));
    }

    [Fact]
    public void A_link_inside_a_fenced_code_block_is_ignored()
    {
        var file = Write("documentation/guides/es/source.md", "```\n[not a link](nowhere.md)\n```\n");

        Assert.Empty(TranslationStructureSupport.BrokenLinks(file, _repo));
    }

    [Fact]
    public void CheckAll_runs_over_every_translated_file_and_reports_nothing_when_all_match()
    {
        var source = "# Title\n\n## Sub\n";
        var hash = TranslationCheckSupport.GitBlobHash(System.Text.Encoding.UTF8.GetBytes(source))[..12];
        Write("documentation/guides/installation.md", source);
        Write("documentation/guides/es/guia-de-instalacion.md",
            $"<!-- source: documentation/guides/installation.md blob {hash} | translated: 2026-08-13 -->\n# Título\n\n## Sub\n");

        var comparison = TranslationStructureSupport.CheckAll(_repo);

        Assert.Empty(comparison.Failures);
        Assert.Empty(comparison.Warnings);
    }

    [Fact]
    public void CheckAll_reports_a_structural_drift()
    {
        var source = "# Title\n\n## Sub\n";
        var hash = TranslationCheckSupport.GitBlobHash(System.Text.Encoding.UTF8.GetBytes(source))[..12];
        Write("documentation/guides/installation.md", source);
        Write("documentation/guides/es/guia-de-instalacion.md",
            $"<!-- source: documentation/guides/installation.md blob {hash} | translated: 2026-08-13 -->\n# Título\n");

        var comparison = TranslationStructureSupport.CheckAll(_repo);

        Assert.Single(comparison.Failures);
    }
}
