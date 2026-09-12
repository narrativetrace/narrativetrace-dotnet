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
/// Backs the <c>SnippetCheck</c> / <c>SnippetSync</c> targets — rule 8
/// (docs as tests): a quickstart's code and output blocks are embedded
/// from a real, tested project, never typed into the page. One family-wide
/// marker convention, implemented here the way <see cref="TranslationCheckSupport"/> implements
/// the sibling blob-hash convention — plain, Nuke-independent code the test
/// suite can reference directly.
/// </summary>
/// <remarks>
/// <para>
/// A marked block looks like:
/// <code>
/// &lt;!-- snippet: examples/NarrativeTrace.Examples.SixtySeconds/Program.cs --&gt;
/// ```csharp
/// …the file, verbatim…
/// ```
/// &lt;!-- /snippet --&gt;
/// </code>
/// The fenced block must sit on the line immediately after the marker, and
/// <c>&lt;!-- /snippet --&gt;</c> on the line immediately after the closing
/// fence — matching the design note's template exactly, so a marker either
/// parses or names precisely which of those two lines is missing.
/// </para>
/// <para>
/// <c>region=NAME</c> selects the window between a <c>snippet:begin NAME</c>
/// and <c>snippet:end NAME</c> comment pair inside the source file (any
/// comment syntax — the region markers are matched by substring, not by a
/// specific <c>//</c> or <c>#</c> prefix) rather than the whole file, and a
/// shared leading indentation is stripped so an embedded method body starts
/// flush left. <c>mask=duration</c> replaces <c>&#8212; \d+(\.\d+)?ms</c> with
/// <c>&#8212; Nms</c> on both sides before <see cref="Check"/> compares them, so a
/// re-run's different timing never fails the gate — <see cref="Sync"/> never
/// masks: it writes the real captured duration, which is what "timing
/// varies" already tells the reader to expect.
/// </para>
/// <para>
/// Translated mirrors (<see cref="TranslationCheckSupport.TranslatedFiles"/>)
/// must never carry a marker at all — <see cref="Check"/> fails one that
/// does, and <see cref="Sync"/> never touches one, matching the design
/// note's "Sync... English pages only; the fix is the translator restamping
/// after snippet-sync on the English" rule.
/// </para>
/// </remarks>
internal static class SnippetCheckSupport
{
    private static readonly Regex OpenMarker = new(
        @"^<!-- snippet: (?<path>\S+)(?: region=(?<region>\S+))?(?: mask=(?<mask>\S+))? -->$",
        RegexOptions.CultureInvariant);

    private const string CloseMarker = "<!-- /snippet -->";
    private const string SnippetMarkerToken = "<!-- snippet:";

    // U+2014 EM DASH, matching the duration suffix every renderer in this
    // family emits ("... -- 8ms"); — spelled out so the literal
    // character survives any editor that isn't UTF-8-safe.
    private static readonly Regex DurationMask = new(
        "— \\d+(\\.\\d+)?ms", RegexOptions.CultureInvariant);

    /// <summary>
    /// Every problem found: a malformed marker, a missing source or region,
    /// a drifted block (masked comparison), or a marker inside a translated
    /// mirror. Empty when every marked block matches its source.
    /// </summary>
    public static IReadOnlyList<string> Check(string repoRoot)
    {
        var root = Path.GetFullPath(repoRoot);
        var translations = TranslatedSet(root);
        var problems = new List<string>();

        foreach (var file in MarkdownFiles(root))
        {
            var relative = Relative(root, file);
            var text = File.ReadAllText(file);
            if (translations.Contains(file))
            {
                if (text.Contains(SnippetMarkerToken, StringComparison.Ordinal))
                    problems.Add($"{relative}: translated mirrors must not carry snippet markers "
                        + "— edit the English source and restamp the translation instead");
                continue;
            }

            if (!text.Contains(SnippetMarkerToken, StringComparison.Ordinal))
                continue;

            problems.AddRange(CheckFile(root, relative, text));
        }

        return problems;
    }

    /// <summary>
    /// Rewrites every marked block in every English page to match its
    /// source (region-extracted where <c>region=</c> is given), unmasked.
    /// Returns one description per block actually changed; a page whose
    /// blocks already match is left byte-for-byte alone. Never touches a
    /// translated mirror.
    /// </summary>
    public static IReadOnlyList<string> Sync(string repoRoot)
    {
        var root = Path.GetFullPath(repoRoot);
        var translations = TranslatedSet(root);
        var changes = new List<string>();

        foreach (var file in MarkdownFiles(root))
        {
            if (translations.Contains(file))
                continue;

            var text = File.ReadAllText(file);
            if (!text.Contains(SnippetMarkerToken, StringComparison.Ordinal))
                continue;

            var relative = Relative(root, file);
            var (updated, fileChanges) = SyncFile(root, relative, text);
            if (fileChanges.Count == 0)
                continue;

            File.WriteAllText(file, updated);
            changes.AddRange(fileChanges);
        }

        return changes;
    }

    private static HashSet<string> TranslatedSet(string root) =>
        new(TranslationCheckSupport.TranslatedFiles(root), StringComparer.Ordinal);

    private static string Relative(string root, string file) =>
        Path.GetRelativePath(root, file).Replace('\\', '/');

    private static IEnumerable<string> MarkdownFiles(string root)
    {
        var documentation = Path.Combine(root, "documentation");
        var underDocs = Directory.Exists(documentation)
            ? Directory.EnumerateFiles(documentation, "*.md", SearchOption.AllDirectories)
            : Enumerable.Empty<string>();
        var atRoot = Directory.EnumerateFiles(root, "*.md", SearchOption.TopDirectoryOnly);
        return underDocs.Concat(atRoot).OrderBy(f => f, StringComparer.Ordinal);
    }

    private static IEnumerable<string> CheckFile(string root, string relative, string text)
    {
        var lines = ToLines(text);
        var i = 0;
        while (i < lines.Length)
        {
            var marker = OpenMarker.Match(lines[i]);
            if (!marker.Success)
            {
                i++;
                continue;
            }

            var lineNumber = i + 1;
            if (!TryParseMarker(marker, out var sourcePath, out var region, out var maskDuration, out var markerError))
            {
                yield return $"{relative}:{lineNumber}: {markerError}";
                i++;
                continue;
            }

            if (!TryExtractFence(lines, i, out var fenceOpen, out var fenceClose, out var closeLine, out var fenceError))
            {
                yield return $"{relative}:{lineNumber}: {fenceError}";
                i++;
                continue;
            }

            var pageContent = Join(lines, fenceOpen + 1, fenceClose);
            var sourceContent = ReadSnippetSource(root, sourcePath, region, out var sourceError);
            if (sourceError is not null)
            {
                yield return $"{relative}:{lineNumber}: {sourceError}";
                i = closeLine + 1;
                continue;
            }

            var maskedPage = ApplyMask(pageContent, maskDuration);
            var maskedSource = ApplyMask(sourceContent, maskDuration);
            if (!string.Equals(maskedPage, maskedSource, StringComparison.Ordinal))
            {
                var regionSuffix = region is null ? "" : $" region={region}";
                yield return $"{relative}:{lineNumber}: block has drifted from its source — "
                    + $"'{relative}' (line {lineNumber}) no longer matches '{sourcePath}'{regionSuffix}; "
                    + "run ./build.sh SnippetSync and inspect the diff";
            }

            i = closeLine + 1;
        }
    }

    private static (string Text, IReadOnlyList<string> Changes) SyncFile(
        string root, string relative, string text)
    {
        var newline = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var lines = ToLines(text);
        var result = new List<string>(lines.Length);
        var changes = new List<string>();

        var i = 0;
        while (i < lines.Length)
        {
            result.Add(lines[i]);
            var marker = OpenMarker.Match(lines[i]);
            if (!marker.Success
                || !TryParseMarker(marker, out var sourcePath, out var region, out _, out _)
                || !TryExtractFence(lines, i, out var fenceOpen, out var fenceClose, out var closeLine, out _))
            {
                // Not a marker, or a malformed one — Check reports the latter; Sync leaves it alone.
                i++;
                continue;
            }

            var sourceContent = ReadSnippetSource(root, sourcePath, region, out var sourceError);
            if (sourceError is not null)
            {
                // Check reports the missing/malformed source; Sync copies the
                // block through unchanged one line at a time (none of them
                // match OpenMarker, so the loop just falls through to i++).
                i++;
                continue;
            }

            var pageContent = Join(lines, fenceOpen + 1, fenceClose);
            if (!string.Equals(pageContent, sourceContent, StringComparison.Ordinal))
            {
                var regionSuffix = region is null ? "" : $" region={region}";
                changes.Add($"{relative}:{i + 1}: resynced from '{sourcePath}'{regionSuffix}");
            }

            result.Add(lines[fenceOpen]);
            result.AddRange(sourceContent.Length == 0
                ? Array.Empty<string>()
                : sourceContent.Split('\n'));
            result.Add(lines[fenceClose]);
            result.Add(lines[closeLine]);
            i = closeLine + 1;
        }

        return (string.Join(newline, result), changes);
    }

    private static bool TryParseMarker(
        Match marker, out string sourcePath, out string? region, out bool maskDuration, out string? error)
    {
        sourcePath = marker.Groups["path"].Value;
        region = marker.Groups["region"].Success ? marker.Groups["region"].Value : null;
        var mask = marker.Groups["mask"].Success ? marker.Groups["mask"].Value : null;
        maskDuration = mask == "duration";
        if (mask is not null && !maskDuration)
        {
            error = $"unknown mask '{mask}' — only mask=duration is supported";
            return false;
        }

        error = null;
        return true;
    }

    /// <summary>
    /// Locates the fenced block immediately following a marker line: the
    /// opening fence must be the very next line, and
    /// <c>&lt;!-- /snippet --&gt;</c> the line immediately after the closing
    /// fence — the design note's template, rigidly. Returns the zero-based
    /// indexes of the opening fence, the closing fence, and the close-marker
    /// line.
    /// </summary>
    private static bool TryExtractFence(
        IReadOnlyList<string> lines, int markerIndex,
        out int fenceOpen, out int fenceClose, out int closeMarkerLine, out string? error)
    {
        fenceOpen = markerIndex + 1;
        fenceClose = -1;
        closeMarkerLine = -1;

        if (fenceOpen >= lines.Count || !lines[fenceOpen].TrimEnd().StartsWith("```", StringComparison.Ordinal))
        {
            error = "expected a fenced code block on the line right after the marker";
            return false;
        }

        for (var j = fenceOpen + 1; j < lines.Count; j++)
        {
            if (lines[j].TrimEnd() != "```")
                continue;
            fenceClose = j;
            break;
        }

        if (fenceClose < 0)
        {
            error = "fenced code block opened by the marker is never closed";
            return false;
        }

        closeMarkerLine = fenceClose + 1;
        if (closeMarkerLine >= lines.Count || lines[closeMarkerLine].TrimEnd() != CloseMarker)
        {
            error = "expected <!-- /snippet --> on the line right after the closing fence";
            return false;
        }

        error = null;
        return true;
    }

    /// <summary>
    /// Reads the snippet source: the whole file, or — with
    /// <paramref name="region"/> — the dedented window between its
    /// <c>snippet:begin NAME</c> / <c>snippet:end NAME</c> comments
    /// (exclusive of both marker lines). Line endings normalized to
    /// <c>\n</c>; no trailing newline.
    /// </summary>
    private static string ReadSnippetSource(string root, string relativePath, string? region, out string? error)
    {
        var full = Path.GetFullPath(Path.Combine(root, relativePath));
        if (!full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            error = $"source path '{relativePath}' escapes the repository root";
            return "";
        }

        if (!File.Exists(full))
        {
            error = $"source '{relativePath}' is missing";
            return "";
        }

        var text = StripLicenseHeader(File.ReadAllText(full).Replace("\r\n", "\n"));
        if (region is null)
        {
            error = null;
            return text.TrimEnd('\n');
        }

        return ExtractRegion(text, relativePath, region, out error);
    }

    /// <summary>
    /// Strips a leading license-header comment block from the SOURCE side only — never the
    /// page's fenced block, and <see cref="Sync"/> never writes one back to either side. The
    /// header is stamped into a shipped file only by the publish script, at publish time, never
    /// in-tree (see the license-stamping-public-only convention), so an example project like
    /// <c>examples/NarrativeTrace.Examples.SixtySeconds/Program.cs</c> matches the page it is
    /// embedded into while developed here, then would silently drift the moment the cold public
    /// snapshot's copy of the same file gained three comment lines the page never shows — this
    /// keeps the comparison (and any resync) blind to a header that exists on one side only.
    /// </summary>
    /// <remarks>
    /// Deliberately narrow: only an initial run of <c>//</c> lines, or one leading
    /// <c>/* ... */</c> block, whose own text contains <c>SPDX-License-Identifier</c> or
    /// <c>Licensed under</c> is stripped — matching the two phrases the publish script's own
    /// header actually carries (<c>scripts/publish-public.sh</c>'s <c>HEADER_SPDX</c> /
    /// <c>HEADER_NOTICE</c>), so a copyright line alone still counts once either phrase is
    /// present anywhere in the same leading block. Any other leading comment — a file banner, an
    /// unrelated copyright notice, a doc comment — is left exactly as it was; this is not a
    /// general "skip the top comment" heuristic. A single blank line immediately after the
    /// stripped block is stripped too, so a header that happens to be followed by one leaves no
    /// gap the page's fenced block would then have to match.
    /// </remarks>
    private static string StripLicenseHeader(string text)
    {
        var lines = text.Split('\n');
        var end = LeadingCommentBlockEnd(lines);
        if (end == 0)
        {
            return text;
        }

        var header = string.Join("\n", lines[..end]);
        if (!header.Contains("SPDX-License-Identifier", StringComparison.Ordinal)
            && !header.Contains("Licensed under", StringComparison.Ordinal))
        {
            return text;
        }

        var start = end < lines.Length && lines[end].Length == 0 ? end + 1 : end;
        return string.Join("\n", lines[start..]);
    }

    /// <summary>
    /// The exclusive end index of the file's leading comment — one <c>/* ... */</c> block, or a
    /// contiguous run of <c>//</c> lines — or <c>0</c> when the file does not open with a
    /// comment at all (including an unterminated <c>/*</c>, which is left alone rather than
    /// guessed at).
    /// </summary>
    private static int LeadingCommentBlockEnd(string[] lines)
    {
        if (lines.Length == 0)
        {
            return 0;
        }

        var first = lines[0].TrimStart();
        if (first.StartsWith("/*", StringComparison.Ordinal))
        {
            var close = Array.FindIndex(lines, l => l.TrimEnd().EndsWith("*/", StringComparison.Ordinal));
            return close < 0 ? 0 : close + 1;
        }

        if (!first.StartsWith("//", StringComparison.Ordinal))
        {
            return 0;
        }

        var i = 0;
        while (i < lines.Length && lines[i].TrimStart().StartsWith("//", StringComparison.Ordinal))
        {
            i++;
        }

        return i;
    }

    private static string ExtractRegion(string text, string relativePath, string region, out string? error)
    {
        var lines = text.Split('\n');
        var begin = Array.FindIndex(lines, l => l.Contains($"snippet:begin {region}", StringComparison.Ordinal));
        var end = Array.FindIndex(lines, l => l.Contains($"snippet:end {region}", StringComparison.Ordinal));
        if (begin < 0 || end < 0 || end <= begin)
        {
            error = $"region '{region}' not found (snippet:begin/end pair) in '{relativePath}'";
            return "";
        }

        error = null;
        var window = lines[(begin + 1)..end];
        return string.Join("\n", Dedent(window));
    }

    /// <summary>Strips the common leading whitespace shared by every non-blank line.</summary>
    private static IReadOnlyList<string> Dedent(IReadOnlyList<string> lines)
    {
        var indents = lines
            .Where(l => l.Trim().Length > 0)
            .Select(l => l.Length - l.TrimStart(' ').Length);
        var shared = indents.DefaultIfEmpty(0).Min();
        return shared == 0
            ? lines
            : lines.Select(l => l.Length >= shared ? l[shared..] : l).ToList();
    }

    private static string ApplyMask(string content, bool maskDuration) =>
        maskDuration ? DurationMask.Replace(content, "— Nms") : content;

    private static string[] ToLines(string text) => text.Replace("\r\n", "\n").Split('\n');

    private static string Join(IReadOnlyList<string> lines, int fromInclusive, int toExclusive) =>
        string.Join("\n", lines.Skip(fromInclusive).Take(toExclusive - fromInclusive));
}
