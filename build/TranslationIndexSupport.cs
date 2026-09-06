// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace NarrativeTrace.Build;

/// <summary>
/// Backs the index/menu-integrity half of <c>TranslationCheck</c>. This port
/// has two sibling-index families, both hand-edited prose that drifts
/// silently:
/// <list type="bullet">
/// <item>the <b>root README family</b> — <c>README.md</c>, and each
/// language's root README (<c>LEAME.md</c>, <c>自述文件.md</c>, …), which
/// double as the documentation-root index in this port (there is no separate
/// <c>documentation/README.md</c>, unlike the Java golden source);</item>
/// <item>the <b>guides index family</b> — <c>documentation/guides/README.md</c>
/// and each language's sibling guide index (<c>documentation/guides/es/guias-de-usuario.md</c>,
/// …), which lists exactly the guides that language currently has.</item>
/// </list>
/// Both families share one convention, simpler than the Java golden source's:
/// every index carries a language menu directly under its H1 — the current
/// language bold and unlinked, every other language linked when its sibling
/// file exists, plain text otherwise. Unlike Java's <c>documentation/README.md</c>
/// asymmetry (English always a link, even on its own page), this port has no
/// such special case — English is bold on its own page like every other
/// language, matching what every shipped README in this repository actually
/// does.
/// </summary>
/// <remarks>
/// Row-set integrity (every row names a currently-translated document, every
/// document has a row, every row target resolves) is only enforced for the
/// <b>guides</b> family: its index is a plain Markdown table scoped exactly to
/// the manifest's guide documents. The root README family is not row-checked
/// — it is marketing prose with a "Documentation" section threaded among many
/// unrelated links (LICENSE, examples, the demo script), so a rigid row
/// diff would be noise, not signal; only its menu line is gated.
/// </remarks>
internal static class TranslationIndexSupport
{
    private const string EnglishRootReadme = "README.md";
    private const string EnglishGuidesIndex = "documentation/guides/README.md";
    private static readonly Regex RowLink = new(@"\[[^\]]*]\(([^)]+)\)", RegexOptions.CultureInvariant);

    /// <summary>The menu line: the first non-blank line after the document's H1, or <c>null</c> when there is no H1.</summary>
    public static string? MenuLine(string text)
    {
        var lines = text.Replace("\r\n", "\n").Split('\n');
        var h1 = Array.FindIndex(lines, l => l.TrimStart().StartsWith("# ", StringComparison.Ordinal));
        if (h1 == -1)
            return null;
        for (var i = h1 + 1; i < lines.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(lines[i]))
                return lines[i].Trim();
        }
        return null;
    }

    /// <summary>The link targets named in this index's document table rows — the menu line's own links are excluded.</summary>
    public static IReadOnlyList<string> DocumentTargets(string text) =>
        text.Replace("\r\n", "\n").Split('\n')
            .Where(line => line.TrimStart().StartsWith('|') && !IsSeparatorRow(line))
            .SelectMany(line => RowLink.Matches(line).Select(m => m.Groups[1].Value))
            .ToList();

    private static bool IsSeparatorRow(string line) =>
        line.Length > 0 && line.All(c => "|-: \t".Contains(c)) && line.Contains('-');

    /// <summary>Every index/menu problem across both sibling-index families.</summary>
    public static IReadOnlyList<string> CheckAll(string repoRoot, I18nManifest manifest)
    {
        var problems = new List<string>();
        problems.AddRange(CheckFamily(repoRoot, manifest, EnglishRootReadme, l => l.RootReadme, "root README"));
        problems.AddRange(CheckFamily(repoRoot, manifest, EnglishGuidesIndex, l => l.Index, "guides index"));
        problems.AddRange(CheckGuidesIndexRows(repoRoot, manifest));
        problems.Sort(StringComparer.Ordinal);
        return problems;
    }

    private static List<string> CheckFamily(
        string repoRoot, I18nManifest manifest, string englishFile, Func<I18nLanguage, string> siblingFile, string kindLabel)
    {
        var problems = new List<string>();
        problems.AddRange(CheckMenu(repoRoot, manifest, englishFile, currentCode: null, englishFile, siblingFile));
        foreach (var language in manifest.Languages)
        {
            var siblingRelative = siblingFile(language);
            if (!File.Exists(Path.Combine(repoRoot, siblingRelative)))
            {
                if (language.Status == I18nStatus.Complete)
                {
                    problems.Add(
                        $"{siblingRelative}: missing — '{language.Code}' is declared complete but has no {kindLabel}");
                }
                continue;
            }
            problems.AddRange(CheckMenu(repoRoot, manifest, siblingRelative, language.Code, englishFile, siblingFile));
        }
        return problems;
    }

    private static IReadOnlyList<string> CheckMenu(
        string repoRoot, I18nManifest manifest, string viewerRelativePath, string? currentCode,
        string englishFile, Func<I18nLanguage, string> siblingFile)
    {
        var viewerAbsolute = Path.Combine(repoRoot, viewerRelativePath);
        if (!File.Exists(viewerAbsolute))
            return [$"{viewerRelativePath}: missing"];

        var viewerDirectory = Path.GetDirectoryName(viewerAbsolute) ?? repoRoot;
        var expected = ExpectedMenu(repoRoot, manifest, currentCode, englishFile, siblingFile, viewerDirectory);
        var actual = MenuLine(File.ReadAllText(viewerAbsolute));
        return actual == expected
            ? []
            : [$"{viewerRelativePath}: language menu is '{actual}', expected '{expected}'"];
    }

    private static string ExpectedMenu(
        string repoRoot, I18nManifest manifest, string? currentCode, string englishFile,
        Func<I18nLanguage, string> siblingFile, string viewerDirectory)
    {
        var segments = new List<string>
        {
            Segment("English", currentCode is null, Path.Combine(repoRoot, englishFile), viewerDirectory),
        };
        foreach (var language in manifest.Languages)
        {
            segments.Add(Segment(
                language.DisplayName,
                language.Code == currentCode,
                Path.Combine(repoRoot, siblingFile(language)),
                viewerDirectory));
        }
        return string.Join(" | ", segments);
    }

    private static string Segment(string displayName, bool isCurrent, string targetAbsolutePath, string viewerDirectory)
    {
        if (isCurrent)
            return $"**{displayName}**";
        if (!File.Exists(targetAbsolutePath))
            return displayName;
        var relative = Path.GetRelativePath(viewerDirectory, targetAbsolutePath).Replace('\\', '/');
        return $"[{displayName}]({relative})";
    }

    /// <summary>
    /// Row-set integrity for each language's guides index: exactly the guide
    /// documents it has translated, every row's link resolving on disk.
    /// </summary>
    private static List<string> CheckGuidesIndexRows(string repoRoot, I18nManifest manifest)
    {
        var guideDocuments = manifest.Documents.Where(d => d.Source.StartsWith("documentation/guides/", StringComparison.Ordinal)).ToList();
        var problems = new List<string>();
        foreach (var language in manifest.Languages)
        {
            var indexFile = Path.Combine(repoRoot, language.Index);
            if (!File.Exists(indexFile))
                continue; // existence (for a `complete` language) is already enforced by CheckFamily.

            var indexDirectory = Path.GetDirectoryName(indexFile) ?? repoRoot;
            var expected = guideDocuments
                .Where(d => d.Translations.ContainsKey(language.Code))
                .Select(d => Path.GetFileName(d.Translations[language.Code]))
                .ToHashSet(StringComparer.Ordinal);
            var actual = DocumentTargets(File.ReadAllText(indexFile)).ToHashSet(StringComparer.Ordinal);

            foreach (var missing in expected.Except(actual).OrderBy(x => x, StringComparer.Ordinal))
                problems.Add($"{language.Index}: missing an index row for '{missing}'");
            foreach (var orphan in actual.Except(expected).OrderBy(x => x, StringComparer.Ordinal))
                problems.Add($"{language.Index}: index row '{orphan}' is not a manifest document for '{language.Code}'");
            foreach (var target in actual.Where(t => !File.Exists(Path.Combine(indexDirectory, t))).OrderBy(x => x, StringComparer.Ordinal))
                problems.Add($"{language.Index}: row target '{target}' does not resolve");
        }
        return problems;
    }
}
