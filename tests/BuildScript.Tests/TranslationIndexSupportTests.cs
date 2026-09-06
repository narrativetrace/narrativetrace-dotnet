// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Build;
using Xunit;

namespace Build.Tests;

/// <summary>
/// Covers <see cref="TranslationIndexSupport"/> — the root-README and
/// guides-index sibling-index families: language menu integrity for both,
/// plus row/link integrity for the guides family only.
/// </summary>
public sealed class TranslationIndexSupportTests : IDisposable
{
    private readonly string _repo = Directory.CreateTempSubdirectory("nt-translation-index").FullName;

    public void Dispose() => Directory.Delete(_repo, recursive: true);

    private void Write(string relativePath, string content)
    {
        var file = Path.Combine(_repo, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, content);
    }

    private static I18nLanguage Es() =>
        new("es", "Español", "documentation/guides/es", "documentation/guides/es/guia.md", "LEAME.md", I18nStatus.InProgress);

    private static I18nLanguage ZhComplete() =>
        new("zh-CN", "简体中文", "documentation/guides/zh-CN", "documentation/guides/zh-CN/guia.md", "自述文件.md", I18nStatus.Complete);

    /// <summary>The manifest matching <see cref="WriteFullyWiredTree"/>'s single guide row, so tests isolating one failure don't also trip the guides-row orphan check.</summary>
    private static I18nManifest FullManifest(I18nLanguage language) =>
        new(
            "en",
            [language],
            [
                new I18nDocument(
                    "documentation/guides/installation.md",
                    new Dictionary<string, string> { [language.Code] = $"guides/{language.Code}/guia-de-instalacion.md" }),
            ]);

    private void WriteFullyWiredTree(I18nLanguage language)
    {
        Write("README.md", "# NarrativeTrace .NET\n\n**English** | [Español](LEAME.md)\n");
        Write(language.RootReadme, $"# NarrativeTrace .NET\n\n[English](README.md) | **{language.DisplayName}**\n");
        Write("documentation/guides/README.md",
            $"# Guides\n\n**English** | [Español](es/guia.md)\n\n| Guide | X |\n|---|---|\n| [Installation](installation.md) | y |\n");
        Write(language.Index,
            $"# Guides\n\n[English](../README.md) | **{language.DisplayName}**\n\n"
                + "| Guide | X |\n|---|---|\n| [Instalación](guia-de-instalacion.md) | y |\n");
        Write($"documentation/guides/{language.Code}/guia-de-instalacion.md", "# Instalación\n");
    }

    [Fact]
    public void MenuLine_returns_first_non_blank_line_after_the_h1()
    {
        var line = TranslationIndexSupport.MenuLine("# Title\n\n**English** | [Español](x.md)\n\nBody\n");

        Assert.Equal("**English** | [Español](x.md)", line);
    }

    [Fact]
    public void MenuLine_returns_null_without_an_h1()
    {
        Assert.Null(TranslationIndexSupport.MenuLine("Just prose, no heading.\n"));
    }

    [Fact]
    public void DocumentTargets_reads_table_rows_and_skips_the_separator()
    {
        var targets = TranslationIndexSupport.DocumentTargets(
            "| Guide | Read it to... |\n|---|---|\n| [Installation](installation.md) | ... |\n");

        Assert.Equal(["installation.md"], targets);
    }

    [Fact]
    public void A_fully_wired_language_reports_nothing()
    {
        var language = Es();
        WriteFullyWiredTree(language);

        Assert.Empty(TranslationIndexSupport.CheckAll(_repo, FullManifest(language)));
    }

    [Fact]
    public void A_wrong_root_readme_menu_is_reported()
    {
        var language = Es();
        WriteFullyWiredTree(language);
        Write(language.RootReadme, $"# NarrativeTrace .NET\n\n[English](README.md) | plain {language.DisplayName}\n");

        var problem = Assert.Single(TranslationIndexSupport.CheckAll(_repo, FullManifest(language)));

        Assert.Contains(language.RootReadme, problem, StringComparison.Ordinal);
        Assert.Contains("language menu", problem, StringComparison.Ordinal);
    }

    [Fact]
    public void English_root_readme_missing_the_language_segment_is_reported()
    {
        var language = Es();
        WriteFullyWiredTree(language);
        Write("README.md", "# NarrativeTrace .NET\n\n**English**\n");

        var problem = Assert.Single(TranslationIndexSupport.CheckAll(_repo, FullManifest(language)));

        Assert.StartsWith("README.md", problem, StringComparison.Ordinal);
    }

    [Fact]
    public void A_language_with_no_root_readme_yet_is_not_a_failure_when_in_progress()
    {
        // Español has no sibling file on disk yet, so both English pages render it plain, not linked.
        Write("README.md", "# NarrativeTrace .NET\n\n**English** | Español\n");
        Write("documentation/guides/README.md", "# Guides\n\n**English** | Español\n");
        var manifest = new I18nManifest("en", [Es()], []);

        Assert.Empty(TranslationIndexSupport.CheckAll(_repo, manifest));
    }

    [Fact]
    public void A_complete_language_with_no_root_readme_is_a_failure()
    {
        Write("README.md", "# NarrativeTrace .NET\n\n**English**\n");
        Write("documentation/guides/README.md", "# Guides\n\n**English**\n");
        var manifest = new I18nManifest("en", [ZhComplete()], []);

        var problems = TranslationIndexSupport.CheckAll(_repo, manifest);

        Assert.Contains(problems, p => p.Contains("自述文件.md", StringComparison.Ordinal) && p.Contains("complete", StringComparison.Ordinal));
    }

    [Fact]
    public void A_complete_language_with_no_guides_index_is_a_failure()
    {
        var language = ZhComplete();
        Write("README.md", "# NarrativeTrace .NET\n\n**English**\n");
        Write(language.RootReadme, $"# NarrativeTrace .NET\n\n[English](README.md) | **{language.DisplayName}**\n");
        Write("documentation/guides/README.md", "# Guides\n\n**English**\n");
        var manifest = new I18nManifest("en", [language], []);

        var problems = TranslationIndexSupport.CheckAll(_repo, manifest);

        Assert.Contains(problems, p => p.Contains(language.Index, StringComparison.Ordinal) && p.Contains("guides index", StringComparison.Ordinal));
    }

    [Fact]
    public void A_guides_index_missing_a_row_for_a_translated_guide_is_reported()
    {
        var language = Es();
        WriteFullyWiredTree(language);
        Write(language.Index, $"# Guides\n\n[English](../README.md) | **{language.DisplayName}**\n\nNo table here.\n");
        var manifest = new I18nManifest(
            "en",
            [language],
            [
                new I18nDocument(
                    "documentation/guides/installation.md",
                    new Dictionary<string, string> { [language.Code] = "guides/es/guia-de-instalacion.md" }),
            ]);

        var problems = TranslationIndexSupport.CheckAll(_repo, manifest);

        Assert.Contains(problems, p => p.Contains("missing an index row", StringComparison.Ordinal) && p.Contains("guia-de-instalacion.md", StringComparison.Ordinal));
    }

    [Fact]
    public void A_guides_index_row_for_an_untranslated_document_is_an_orphan()
    {
        var language = Es();
        WriteFullyWiredTree(language);
        // The manifest declares no translation for this language at all, so the existing row is orphaned.
        var manifest = new I18nManifest(
            "en",
            [language],
            [new I18nDocument("documentation/guides/installation.md", new Dictionary<string, string>())]);

        var problems = TranslationIndexSupport.CheckAll(_repo, manifest);

        Assert.Contains(problems, p => p.Contains("is not a manifest document", StringComparison.Ordinal));
    }

    [Fact]
    public void A_guides_index_row_target_that_does_not_resolve_is_reported()
    {
        var language = Es();
        WriteFullyWiredTree(language);
        File.Delete(Path.Combine(_repo, "documentation/guides/es/guia-de-instalacion.md"));
        var manifest = new I18nManifest(
            "en",
            [language],
            [
                new I18nDocument(
                    "documentation/guides/installation.md",
                    new Dictionary<string, string> { [language.Code] = "guides/es/guia-de-instalacion.md" }),
            ]);

        var problems = TranslationIndexSupport.CheckAll(_repo, manifest);

        Assert.Contains(problems, p => p.Contains("does not resolve", StringComparison.Ordinal));
    }
}
