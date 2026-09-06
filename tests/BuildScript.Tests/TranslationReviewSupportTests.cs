// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Text;
using NarrativeTrace.Build;
using Xunit;

namespace Build.Tests;

/// <summary>
/// Covers <see cref="TranslationReviewSupport"/> — the warn-only
/// <c>| reviewed:</c> summary and the <c>TranslationStatus</c> dashboard.
/// </summary>
public sealed class TranslationReviewSupportTests : IDisposable
{
    private readonly string _repo = Directory.CreateTempSubdirectory("nt-translation-review").FullName;

    public void Dispose() => Directory.Delete(_repo, recursive: true);

    private string WriteSource(string relativePath, string content)
    {
        Write(relativePath, content);
        return TranslationCheckSupport.GitBlobHash(Encoding.UTF8.GetBytes(content))[..12];
    }

    private void Write(string relativePath, string content)
    {
        var file = Path.Combine(_repo, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, content);
    }

    [Fact]
    public void A_translation_with_no_reviewed_clause_is_unreviewed()
    {
        var hash = WriteSource("documentation/guides/installation.md", "# Installation\n");
        Write("documentation/guides/es/guia-de-instalacion.md",
            $"<!-- source: documentation/guides/installation.md blob {hash} | translated: 2026-08-13 -->\ncuerpo\n");

        var unreviewed = Assert.Single(TranslationReviewSupport.Unreviewed(_repo));
        Assert.Contains("guia-de-instalacion.md", unreviewed, StringComparison.Ordinal);
    }

    [Fact]
    public void A_translation_with_a_reviewed_date_is_not_unreviewed()
    {
        var hash = WriteSource("documentation/guides/installation.md", "# Installation\n");
        Write("documentation/guides/es/guia-de-instalacion.md",
            $"<!-- source: documentation/guides/installation.md blob {hash} | translated: 2026-08-13 | reviewed: 2026-09-01 -->\ncuerpo\n");

        Assert.Empty(TranslationReviewSupport.Unreviewed(_repo));
    }

    [Fact]
    public void A_dash_reviewed_marker_still_counts_as_unreviewed()
    {
        var hash = WriteSource("documentation/guides/installation.md", "# Installation\n");
        Write("documentation/guides/es/guia-de-instalacion.md",
            $"<!-- source: documentation/guides/installation.md blob {hash} | translated: 2026-08-13 | reviewed: - -->\ncuerpo\n");

        Assert.Single(TranslationReviewSupport.Unreviewed(_repo));
    }

    [Fact]
    public void SummaryLine_reports_the_unreviewed_count()
    {
        var hash = WriteSource("documentation/guides/installation.md", "# Installation\n");
        Write("documentation/guides/es/guia-de-instalacion.md",
            $"<!-- source: documentation/guides/installation.md blob {hash} | translated: 2026-08-13 -->\ncuerpo\n");

        Assert.Contains("1 translated document(s) unreviewed", TranslationReviewSupport.SummaryLine(_repo), StringComparison.Ordinal);
    }

    [Fact]
    public void StatusReport_without_a_manifest_says_so()
    {
        Assert.Contains("no manifest", TranslationReviewSupport.StatusReport(_repo, manifest: null), StringComparison.Ordinal);
    }

    [Fact]
    public void StatusReport_counts_translated_and_unreviewed_per_language()
    {
        var hash = WriteSource("documentation/guides/installation.md", "# Installation\n");
        Write("documentation/guides/es/guia-de-instalacion.md",
            $"<!-- source: documentation/guides/installation.md blob {hash} | translated: 2026-08-13 -->\ncuerpo\n");
        var language = new I18nLanguage(
            "es", "Español", "documentation/guides/es", "documentation/guides/es/guia.md", "LEAME.md", I18nStatus.InProgress);
        var manifest = new I18nManifest(
            "en",
            [language],
            [
                new I18nDocument(
                    "documentation/guides/installation.md",
                    new Dictionary<string, string> { ["es"] = "guides/es/guia-de-instalacion.md" }),
            ]);

        var report = TranslationReviewSupport.StatusReport(_repo, manifest);

        Assert.Contains("es (in-progress): 1/1 translated, 1 unreviewed", report, StringComparison.Ordinal);
    }
}
