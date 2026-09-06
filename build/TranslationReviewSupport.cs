// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace NarrativeTrace.Build;

/// <summary>
/// Backs the <c>| reviewed:</c> half of the translation platform — a
/// translation can be structurally in sync with its source (what
/// <c>TranslationCheck</c>'s other halves verify) and still be a machine
/// translation nobody fluent has read. The header's optional
/// <c>| reviewed: &lt;date|-&gt;</c> clause is how a native-speaker pass gets
/// recorded; this class reads it back.
/// </summary>
/// <remarks>
/// Deliberately warn-only everywhere in this file — per-commit review status
/// is a nudge (<see cref="SummaryLine"/>), not a gate. Gating publish on it is
/// an explicit owner decision this platform does not make.
/// </remarks>
internal static class TranslationReviewSupport
{
    /// <summary>Every discovered translation with no <c>| reviewed:</c> date — absent and <c>| reviewed: -</c> both count.</summary>
    public static IReadOnlyList<string> Unreviewed(string repoRoot)
    {
        var root = Path.GetFullPath(repoRoot);
        return TranslationCheckSupport.TranslatedFiles(root)
            .Where(IsUnreviewed)
            .Select(file => Path.GetRelativePath(root, file).Replace('\\', '/'))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();
    }

    private static bool IsUnreviewed(string file)
    {
        using var reader = new StreamReader(file, Encoding.UTF8);
        var header = TranslationCheckSupport.ParseHeader(reader.ReadLine() ?? string.Empty);
        return header is not null && header.Reviewed is null;
    }

    /// <summary>The one-line, warn-only summary <c>TranslationCheck</c> prints every build.</summary>
    public static string SummaryLine(string repoRoot)
    {
        var count = Unreviewed(repoRoot).Count;
        return $"TranslationCheck: {count} translated document(s) unreviewed "
            + "(run ./build.sh TranslationStatus for the full list)";
    }

    /// <summary>The human dashboard <c>TranslationStatus</c> prints: every language's coverage and review counts.</summary>
    public static string StatusReport(string repoRoot, I18nManifest? manifest)
    {
        if (manifest is null)
            return $"TranslationStatus: no manifest at {I18nManifestSupport.ManifestRelativePath}";

        var lines = new List<string> { "TranslationStatus:" };
        var unreviewed = Unreviewed(repoRoot);
        foreach (var language in manifest.Languages)
            lines.Add(LanguageLine(unreviewed, manifest, language));
        return string.Join("\n", lines);
    }

    private static string LanguageLine(IReadOnlyList<string> unreviewed, I18nManifest manifest, I18nLanguage language)
    {
        var total = manifest.Documents.Count;
        var translated = manifest.Documents.Count(d => d.Translations.ContainsKey(language.Code));
        var unreviewedInLanguage = unreviewed.Count(path => path.Contains($"/{language.Code}/", StringComparison.Ordinal));
        return $"  {language.Code} ({language.Status.Label()}): {translated}/{total} translated, "
            + $"{unreviewedInLanguage} unreviewed";
    }
}
