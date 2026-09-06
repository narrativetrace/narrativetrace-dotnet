// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace NarrativeTrace.Build;

/// <summary>
/// The staleness header a translated document carries on line 1: the English
/// source it was translated from, that source's git blob hash prefix at the
/// moment of translation, and — optionally — the date a native speaker last
/// reviewed the translation against its source.
/// </summary>
/// <remarks>
/// <see cref="Reviewed"/> is <c>null</c> for both "no <c>| reviewed:</c>
/// clause at all" and the explicit <c>| reviewed: -</c> marker; both mean
/// "not yet reviewed". <see cref="TranslationReviewSupport"/> is what acts on
/// it — this record only carries it.
/// </remarks>
internal sealed record TranslationHeader(string SourcePath, string BlobHashPrefix, string? Reviewed = null);

/// <summary>
/// Backs the <c>TranslationCheck</c> target — verifies every translated
/// document is in sync with its English source via the blob-hash staleness
/// header defined in the i18n terminology conventions.
/// </summary>
internal static class TranslationCheckSupport
{
    private static readonly Regex HeaderPattern = new(
        @"^<!-- source: (\S+) blob ([0-9a-f]{12}) \| translated: \d{4}-\d{2}-\d{2}"
            + @"(?: \| reviewed: (\d{4}-\d{2}-\d{2}|-))? -->$",
        RegexOptions.CultureInvariant);

    /// <summary>
    /// A translation directory is named after its BCP-47 language tag
    /// (<c>es</c>, <c>zh-CN</c>, <c>pt-BR</c>) — which is what distinguishes
    /// it from sibling content directories such as <c>guides/</c> or the
    /// convention's own <c>i18n/</c>.
    /// </summary>
    private static readonly Regex LanguageDirectoryPattern = new(
        "^[a-z]{2}(-[A-Za-z0-9]{2,8})?$",
        RegexOptions.CultureInvariant);

    /// <summary>
    /// Parses a staleness header line; returns <c>null</c> when the line is
    /// not a valid header.
    /// </summary>
    public static TranslationHeader? ParseHeader(string firstLine)
    {
        var match = HeaderPattern.Match(firstLine.Trim());
        if (!match.Success)
            return null;
        var reviewedGroup = match.Groups[3];
        var reviewed = reviewedGroup.Success && reviewedGroup.Value != "-" ? reviewedGroup.Value : null;
        return new TranslationHeader(match.Groups[1].Value, match.Groups[2].Value, reviewed);
    }

    /// <summary>
    /// Computes the git blob hash of <paramref name="content"/>: SHA-1 over
    /// <c>"blob &lt;size&gt;\0"</c> followed by the raw bytes — the same value
    /// <c>git hash-object</c> prints, without spawning git.
    /// </summary>
    public static string GitBlobHash(byte[] content)
    {
        var prefix = Encoding.UTF8.GetBytes($"blob {content.Length}\0");
        var payload = new byte[prefix.Length + content.Length];
        prefix.CopyTo(payload, 0);
        content.CopyTo(payload, prefix.Length);
#pragma warning disable CA5350, S4790 // Git's object format is SHA-1; not a security decision.
        var hash = SHA1.HashData(payload);
#pragma warning restore CA5350, S4790
        return Convert.ToHexStringLower(hash);
    }

    /// <summary>
    /// Verifies every translated document under <paramref name="repoRoot"/>;
    /// returns the problems sorted by file path, empty when all translations
    /// are in sync. Checked files: every <c>*.md</c> under a language
    /// directory (a BCP-47-shaped directory name anywhere below
    /// <c>documentation/</c> — header mandatory), plus root-level <c>*.md</c>
    /// files that carry a header (headerless root files are English
    /// originals, and are skipped).
    /// </summary>
    public static IReadOnlyList<string> Check(string repoRoot)
    {
        var root = Path.GetFullPath(repoRoot);
        var problems = TranslatedFiles(root)
            .Select(file => CheckFile(root, file))
            .OfType<string>()
            .ToList();
        problems.Sort(StringComparer.Ordinal);
        return problems;
    }

    /// <summary>
    /// Every file this check discovers as a translation, as absolute paths —
    /// shared with <see cref="TranslationStructureSupport"/> and
    /// <see cref="TranslationReviewSupport"/> so "what counts as a
    /// translation" is defined exactly once.
    /// </summary>
    internal static IEnumerable<string> TranslatedFiles(string root)
    {
        var documentation = Path.Combine(root, "documentation");
        var translated = Directory.Exists(documentation)
            ? Directory.EnumerateFiles(documentation, "*.md", SearchOption.AllDirectories)
                .Where(file => IsUnderLanguageDirectory(documentation, file))
            : Enumerable.Empty<string>();
        var rootOriginals = Directory.EnumerateFiles(root, "*.md", SearchOption.TopDirectoryOnly)
            .Where(file => ParseHeader(FirstLine(file)) is not null);
        return translated.Concat(rootOriginals);
    }

    private static bool IsUnderLanguageDirectory(string documentation, string file) =>
        Path.GetRelativePath(documentation, file)
            .Replace('\\', '/')
            .Split('/')
            .SkipLast(1)
            .Any(segment => LanguageDirectoryPattern.IsMatch(segment));

    private static string FirstLine(string file)
    {
        using var reader = new StreamReader(file, Encoding.UTF8);
        return reader.ReadLine() ?? string.Empty;
    }

    private static string? CheckFile(string root, string file)
    {
        var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
        if (ParseHeader(FirstLine(file)) is not { } header)
            return $"{relative}: missing or malformed staleness header on line 1";

        var source = Path.GetFullPath(Path.Combine(root, header.SourcePath));
        if (!source.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            return $"{relative}: source path '{header.SourcePath}' escapes the repository root";
        if (!File.Exists(source))
            return $"{relative}: source '{header.SourcePath}' is missing";

        var actual = GitBlobHash(File.ReadAllBytes(source))[..12];
        return actual == header.BlobHashPrefix
            ? null
            : $"{relative}: stale — header records {header.BlobHashPrefix} but '{header.SourcePath}' "
                + $"is now {actual}; re-translate the delta and restamp line 1";
    }
}
