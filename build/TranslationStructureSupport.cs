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

/// <summary>One document's structural shape: heading levels in order, fenced code blocks verbatim, table shapes.</summary>
internal sealed record StructureProfile(
    IReadOnlyList<int> HeadingLevels,
    IReadOnlyList<string> CodeBlocks,
    IReadOnlyList<(int Rows, int Columns)> Tables);

/// <summary>Structural problems between a translation and its source: hard <see cref="Failures"/> and softer <see cref="Warnings"/>.</summary>
internal sealed record StructureComparison(IReadOnlyList<string> Failures, IReadOnlyList<string> Warnings)
{
    public static readonly StructureComparison Empty = new([], []);
}

/// <summary>
/// Backs the structure-parity half of <c>TranslationCheck</c> — a translation
/// may say anything it wants in prose, but its mechanical skeleton (heading
/// tree, code sample count, links, table shapes) must mirror its English
/// source. A drifted heading tree, a vanished code sample, a table missing a
/// row, or a link that no longer resolves is usually a translation that fell
/// behind a source edit — those are hard <see cref="StructureComparison.Failures"/>.
/// </summary>
/// <remarks>
/// A fenced code block's <em>content</em> is deliberately only a
/// <see cref="StructureComparison.Warnings"/> signal, not a failure, once its
/// block <em>count</em> already matches its source. Real translations in this
/// repository legitimately vary code-block content in ways
/// the i18n terminology conventions sanction: comments are translated,
/// <c>&lt;placeholder&gt;</c> labels inside illustrative syntax are localized,
/// and a demonstration command can legitimately change a literal argument
/// (<c>--lang es</c> vs. <c>--lang zh-CN</c>). None of that is
/// machine-distinguishable from a real drift with dependency-free line
/// parsing, so content drift is surfaced for a human to read, never used to
/// fail the build; only the block <em>count</em> (a whole example gained or
/// lost) is a hard failure.
/// </remarks>
internal static class TranslationStructureSupport
{
    private static readonly Regex Heading = new(@"^(#{1,6})\s", RegexOptions.CultureInvariant);
    private static readonly Regex LinkTarget = new(@"\[[^\]]*]\(([^)]+)\)", RegexOptions.CultureInvariant);
    private static readonly Regex ExternalScheme = new(@"^([a-zA-Z][a-zA-Z0-9+.-]*):", RegexOptions.CultureInvariant);

    /// <summary>Parses <paramref name="text"/> into its structural shape, skipping anything inside a fenced code block.</summary>
    public static StructureProfile Profile(string text)
    {
        var headingLevels = new List<int>();
        var codeBlocks = new List<string>();
        var tables = new List<(int, int)>();
        List<string>? fence = null;
        var table = new List<string>();

        foreach (var line in text.Replace("\r\n", "\n").Split('\n'))
        {
            if (fence is not null)
            {
                if (IsFenceMarker(line))
                {
                    codeBlocks.Add(string.Join("\n", fence));
                    fence = null;
                }
                else
                {
                    fence.Add(line);
                }
                continue;
            }
            if (IsFenceMarker(line))
            {
                fence = [line];
                continue;
            }
            if (IsTableLine(line))
            {
                table.Add(line);
                continue;
            }
            FlushTable(table, tables);
            table = [];
            var level = HeadingLevel(line);
            if (level is not null)
                headingLevels.Add(level.Value);
        }
        FlushTable(table, tables);
        return new StructureProfile(headingLevels, codeBlocks, tables);
    }

    private static bool IsFenceMarker(string line) => line.TrimStart().StartsWith("```", StringComparison.Ordinal);

    private static int? HeadingLevel(string line)
    {
        var match = Heading.Match(line);
        return match.Success ? match.Groups[1].Length : null;
    }

    private static bool IsTableLine(string line) => line.TrimStart().StartsWith('|');

    private static bool IsSeparatorLine(string line) =>
        line.Length > 0 && line.All(c => "|-: \t".Contains(c)) && line.Contains('-');

    private static void FlushTable(List<string> lines, List<(int, int)> into)
    {
        if (lines.Count < 2 || !IsSeparatorLine(lines[1]))
            return;
        into.Add((lines.Count - 2, ColumnCount(lines[0])));
    }

    private static int ColumnCount(string headerLine)
    {
        var trimmed = headerLine.Trim();
        if (trimmed.StartsWith('|'))
            trimmed = trimmed[1..];
        if (trimmed.EndsWith('|'))
            trimmed = trimmed[..^1];
        return trimmed.Split('|').Length;
    }

    /// <summary>Structural differences between <paramref name="translation"/> (labeled <paramref name="translationLabel"/>) and its <paramref name="source"/>.</summary>
    public static StructureComparison Compare(
        StructureProfile source, StructureProfile translation, string sourceLabel, string translationLabel)
    {
        var failures = new List<string>();
        failures.AddRange(CompareHeadings(source.HeadingLevels, translation.HeadingLevels, sourceLabel, translationLabel));
        failures.AddRange(CompareTables(source.Tables, translation.Tables, sourceLabel, translationLabel));
        var (codeFailures, codeWarnings) = CompareCodeBlocks(source.CodeBlocks, translation.CodeBlocks, sourceLabel, translationLabel);
        failures.AddRange(codeFailures);
        return new StructureComparison(failures, codeWarnings);
    }

    private static IEnumerable<string> CompareHeadings(
        IReadOnlyList<int> source, IReadOnlyList<int> translation, string sourceLabel, string translationLabel)
    {
        if (source.Count != translation.Count)
        {
            yield return $"{translationLabel}: heading count {translation.Count} vs {sourceLabel}'s {source.Count}";
            yield break;
        }
        for (var i = 0; i < source.Count; i++)
        {
            if (source[i] != translation[i])
            {
                yield return $"{translationLabel}: heading {i + 1} is level {translation[i]}, "
                    + $"{sourceLabel}'s heading {i + 1} is level {source[i]}";
            }
        }
    }

    /// <summary>Block <em>count</em> mismatches are failures (a whole example vanished); content drift is a warning — see the class doc.</summary>
    private static (List<string> Failures, List<string> Warnings) CompareCodeBlocks(
        IReadOnlyList<string> source, IReadOnlyList<string> translation, string sourceLabel, string translationLabel)
    {
        if (source.Count != translation.Count)
        {
            return ([$"{translationLabel}: code block count {translation.Count} vs {sourceLabel}'s {source.Count}"], []);
        }
        var warnings = new List<string>();
        for (var i = 0; i < source.Count; i++)
        {
            if (!string.Equals(source[i], translation[i], StringComparison.Ordinal))
            {
                warnings.Add($"{translationLabel}: code block {i + 1} differs from {sourceLabel} — verify by hand "
                    + "(translated comments, localized placeholders and per-language example values are expected)");
            }
        }
        return ([], warnings);
    }

    private static IEnumerable<string> CompareTables(
        IReadOnlyList<(int Rows, int Columns)> source,
        IReadOnlyList<(int Rows, int Columns)> translation,
        string sourceLabel,
        string translationLabel)
    {
        if (source.Count != translation.Count)
        {
            yield return $"{translationLabel}: table count {translation.Count} vs {sourceLabel}'s {source.Count}";
            yield break;
        }
        for (var i = 0; i < source.Count; i++)
        {
            var mismatch = TableShapeMismatch(source[i], translation[i], i, sourceLabel, translationLabel);
            if (mismatch is not null)
                yield return mismatch;
        }
    }

    private static string? TableShapeMismatch(
        (int Rows, int Columns) source, (int Rows, int Columns) translation, int index, string sourceLabel, string translationLabel)
    {
        if (source == translation)
            return null;
        var parts = new List<string>();
        if (source.Rows != translation.Rows)
            parts.Add($"rows {translation.Rows} vs {source.Rows}");
        if (source.Columns != translation.Columns)
            parts.Add($"columns {translation.Columns} vs {source.Columns}");
        return $"{translationLabel}: table {index + 1} shape mismatches {sourceLabel} ({string.Join(", ", parts)})";
    }

    /// <summary>Relative links in <paramref name="file"/> that do not resolve to a file or directory on disk; external/anchor links are skipped.</summary>
    public static IReadOnlyList<string> BrokenLinks(string file, string repoRoot)
    {
        var label = Path.GetRelativePath(repoRoot, file).Replace('\\', '/');
        var nonFenced = WithoutFencedLines(File.ReadAllText(file));
        var directory = Path.GetDirectoryName(file) ?? repoRoot;
        return LinkTarget.Matches(nonFenced)
            .Select(m => Before(m.Groups[1].Value, '#').Trim())
            .Where(target => target.Length > 0 && !ExternalScheme.IsMatch(target))
            .Where(target => !TargetExists(directory, target))
            .Distinct()
            .Select(target => $"{label}: link target '{target}' does not resolve")
            .ToList();
    }

    private static string Before(string value, char separator)
    {
        var index = value.IndexOf(separator);
        return index < 0 ? value : value[..index];
    }

    private static bool TargetExists(string directory, string target)
    {
        var resolved = Path.GetFullPath(Path.Combine(directory, target));
        return File.Exists(resolved) || Directory.Exists(resolved);
    }

    private static string WithoutFencedLines(string text)
    {
        var fenced = false;
        var kept = new List<string>();
        foreach (var line in text.Replace("\r\n", "\n").Split('\n'))
        {
            var marker = IsFenceMarker(line);
            if (marker)
                fenced = !fenced;
            if (!fenced && !marker)
                kept.Add(line);
        }
        return string.Join("\n", kept);
    }

    /// <summary>Runs structure-parity across every discovered translation with a resolvable header and source.</summary>
    public static StructureComparison CheckAll(string repoRoot)
    {
        var root = Path.GetFullPath(repoRoot);
        var failures = new List<string>();
        var warnings = new List<string>();
        foreach (var file in TranslationCheckSupport.TranslatedFiles(root))
        {
            var (fileFailures, fileWarnings) = CheckOne(root, file);
            failures.AddRange(fileFailures);
            warnings.AddRange(fileWarnings);
        }
        failures.Sort(StringComparer.Ordinal);
        warnings.Sort(StringComparer.Ordinal);
        return new StructureComparison(failures, warnings);
    }

    private static StructureComparison CheckOne(string repoRoot, string file)
    {
        using var reader = new StreamReader(file, System.Text.Encoding.UTF8);
        var header = TranslationCheckSupport.ParseHeader(reader.ReadLine() ?? string.Empty);
        if (header is null)
            return StructureComparison.Empty;

        var source = Path.Combine(repoRoot, header.SourcePath);
        if (!File.Exists(source))
            return StructureComparison.Empty;

        var label = Path.GetRelativePath(repoRoot, file).Replace('\\', '/');
        var structural = Compare(Profile(File.ReadAllText(source)), Profile(File.ReadAllText(file)), header.SourcePath, label);
        return new StructureComparison(
            [.. structural.Failures, .. BrokenLinks(file, repoRoot)],
            structural.Warnings);
    }
}
