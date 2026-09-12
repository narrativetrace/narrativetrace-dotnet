// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Build;
using Xunit;

namespace Build.Tests;

/// <summary>
/// Covers <see cref="TranslationCompletenessSupport"/> — the manifest-driven
/// half of <c>TranslationCheck</c>: a <c>complete</c> language missing a
/// document fails, an <c>in-progress</c> one only warns, and a manifest entry
/// naming a file that does not exist fails regardless of status.
/// </summary>
public sealed class TranslationCompletenessSupportTests : IDisposable
{
    private readonly string _repo = Directory.CreateTempSubdirectory("nt-translation-completeness").FullName;

    public void Dispose() => Directory.Delete(_repo, recursive: true);

    private void Write(string relativePath, string content = "")
    {
        var file = Path.Combine(_repo, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, content);
    }

    private static I18nLanguage Language(string code, I18nStatus status) =>
        new(code, code, $"documentation/{code}", $"documentation/{code}/index.md", $"README.{code}.md", status);

    [Fact]
    public void A_complete_language_missing_a_document_fails()
    {
        var manifest = new I18nManifest(
            "en",
            [Language("es", I18nStatus.Complete)],
            [new I18nDocument("documentation/sixty-seconds.md", new Dictionary<string, string>())]);

        var result = TranslationCompletenessSupport.Check(_repo, manifest);

        var failure = Assert.Single(result.Failures);
        Assert.Contains("es (complete)", failure, StringComparison.Ordinal);
        Assert.Contains("documentation/sixty-seconds.md", failure, StringComparison.Ordinal);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void An_in_progress_language_missing_a_document_only_warns()
    {
        var manifest = new I18nManifest(
            "en",
            [Language("es", I18nStatus.InProgress)],
            [new I18nDocument("documentation/sixty-seconds.md", new Dictionary<string, string>())]);

        var result = TranslationCompletenessSupport.Check(_repo, manifest);

        Assert.Empty(result.Failures);
        var warning = Assert.Single(result.Warnings);
        Assert.Contains("es (in-progress)", warning, StringComparison.Ordinal);
    }

    [Fact]
    public void A_manifest_entry_naming_a_missing_file_fails_even_for_an_in_progress_language()
    {
        var manifest = new I18nManifest(
            "en",
            [Language("es", I18nStatus.InProgress)],
            [
                new I18nDocument(
                    "documentation/guides/installation.md",
                    new Dictionary<string, string> { ["es"] = "guides/es/guia-de-instalacion.md" }),
            ]);

        var result = TranslationCompletenessSupport.Check(_repo, manifest);

        var failure = Assert.Single(result.Failures);
        Assert.Contains("guides/es/guia-de-instalacion.md", failure, StringComparison.Ordinal);
        Assert.Contains("does not exist", failure, StringComparison.Ordinal);
    }

    [Fact]
    public void An_existing_translation_reports_nothing()
    {
        Write("documentation/guides/es/guia-de-instalacion.md", "# Instalación\n");
        var manifest = new I18nManifest(
            "en",
            [Language("es", I18nStatus.Complete)],
            [
                new I18nDocument(
                    "documentation/guides/installation.md",
                    new Dictionary<string, string> { ["es"] = "guides/es/guia-de-instalacion.md" }),
            ]);

        var result = TranslationCompletenessSupport.Check(_repo, manifest);

        Assert.Empty(result.Failures);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Multiple_missing_documents_are_listed_together_in_one_message()
    {
        var manifest = new I18nManifest(
            "en",
            [Language("es", I18nStatus.InProgress)],
            [
                new I18nDocument("documentation/a.md", new Dictionary<string, string>()),
                new I18nDocument("documentation/b.md", new Dictionary<string, string>()),
            ]);

        var warning = Assert.Single(TranslationCompletenessSupport.Check(_repo, manifest).Warnings);

        Assert.Contains("documentation/a.md", warning, StringComparison.Ordinal);
        Assert.Contains("documentation/b.md", warning, StringComparison.Ordinal);
    }

    [Fact]
    public void Results_are_sorted()
    {
        var manifest = new I18nManifest(
            "en",
            [Language("zh-CN", I18nStatus.Complete), Language("es", I18nStatus.Complete)],
            [new I18nDocument("documentation/a.md", new Dictionary<string, string>())]);

        var failures = TranslationCompletenessSupport.Check(_repo, manifest).Failures;

        Assert.Equal(2, failures.Count);
        Assert.True(string.CompareOrdinal(failures[0], failures[1]) < 0);
    }
}
