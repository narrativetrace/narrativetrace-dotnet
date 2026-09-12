// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Text;
using NarrativeTrace.Build;
using Xunit;

namespace Build.Tests;

/// <summary>
/// Covers <see cref="TranslationPlatformSupport"/> — the orchestrator behind
/// the <c>TranslationCheck</c> target, including graceful degradation
/// without a manifest, and a regression test against this repository's own
/// real manifest and documents.
/// </summary>
public sealed class TranslationPlatformSupportTests : IDisposable
{
    private readonly string _repo = Directory.CreateTempSubdirectory("nt-translation-platform").FullName;

    public void Dispose() => Directory.Delete(_repo, recursive: true);

    private void Write(string relativePath, string content)
    {
        var file = Path.Combine(_repo, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, content);
    }

    [Fact]
    public void Without_a_manifest_it_degrades_to_the_staleness_only_check_with_one_warning()
    {
        Write("README.md", "# NarrativeTrace .NET\n");

        var result = TranslationPlatformSupport.RunAll(_repo);

        Assert.Empty(result.Failures);
        var warning = Assert.Single(result.Warnings);
        Assert.Contains("no manifest", warning, StringComparison.Ordinal);
    }

    [Fact]
    public void A_stale_translation_is_a_failure_even_without_a_manifest()
    {
        Write("README.md", "# NarrativeTrace .NET v2\n");
        Write("LEAME.md", "<!-- source: README.md blob aaaaaaaaaaaa | translated: 2026-08-13 -->\n# NarrativeTrace .NET\n");

        var result = TranslationPlatformSupport.RunAll(_repo);

        Assert.Contains(result.Failures, f => f.Contains("stale", StringComparison.Ordinal));
    }

    [Fact]
    public void With_a_manifest_an_in_progress_gap_is_a_warning_not_a_failure()
    {
        Write("documentation/i18n/manifest.json", """
            {
              "languages": [
                { "code": "es", "displayName": "Español", "directory": "documentation/es",
                  "index": "documentation/es/index.md", "rootReadme": "LEAME.md", "status": "in-progress" }
              ],
              "documents": [ { "source": "documentation/sixty-seconds.md" } ]
            }
            """);
        // Español has no sibling file on disk yet, so both English index pages render it plain.
        Write("README.md", "# NarrativeTrace .NET\n\n**English** | Español\n");
        Write("documentation/guides/README.md", "# Guides\n\n**English** | Español\n");

        var result = TranslationPlatformSupport.RunAll(_repo);

        Assert.Empty(result.Failures);
        Assert.Contains(result.Warnings, w => w.Contains("es (in-progress)", StringComparison.Ordinal));
    }

    [Fact]
    public void The_real_repository_manifest_and_documents_pass_with_only_warnings()
    {
        var repoRoot = RepositoryPath.Root();

        var result = TranslationPlatformSupport.RunAll(repoRoot);

        Assert.True(
            result.Failures.Count == 0,
            "Real-repo translation platform failures:\n" + string.Join("\n", result.Failures));
    }
}
