// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NarrativeTrace.Core;

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
/// flush left. <c>mask</c> takes a comma-separated list of names, each
/// applied in turn to both sides before <see cref="Check"/> compares them:
/// </para>
/// <list type="bullet">
/// <item><c>duration</c> strips <c> &#8212; \d+(\.\d+)?ms</c> (leading space
/// included) — deleted rather than replaced with a placeholder, because the
/// renderers in this family omit the whole suffix, not just the digits, when
/// a call's measured duration rounds to zero ticks
/// (<c>IndentedTextRenderer</c>'s <c>AppendDuration</c>:
/// <c>DurationTicks &lt;= 0</c> appends nothing at all). A placeholder
/// substitution only normalizes the *digits*, so a page capturing one run
/// with a nonzero duration and a later run landing exactly on zero ticks
/// would still read as drifted — the reader-visible "timing varies" is
/// defined as varying between *some* number of milliseconds and *none
/// rendered*, not just between numbers, so the mask has to erase the whole
/// optional fragment on both sides.</item>
/// <item><c>traceName</c> *(since 0.1.4, unreleased)* replaces the
/// trace/run three-word phrase — and, where adjacent, the 7-hex trace-id
/// fragment — wherever a <c>trace:</c>/<c>run:</c>/<c>trace_name:</c>/
/// <c>runName:</c> label, a <c>The trace ...:</c> prose lead-in, or a
/// <c>## Trace: ... —</c> Markdown title carries one, since every one of
/// those is derived from a randomly generated id and would otherwise make
/// embedded live output fail this check on every regeneration.</item>
/// </list>
/// <para>
/// <see cref="Sync"/> never masks: it writes the real captured value
/// (present or absent), which is what the reader-visible caveat already
/// tells the reader to expect.
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
    // family emits (" -- 8ms"); — spelled out so the literal character
    // survives any editor that isn't UTF-8-safe. The leading space is part
    // of the match (and gets erased with it, see ApplyMask) so a run whose
    // renderer omits the whole suffix — DurationTicks rounding to zero — masks
    // identically to a run that renders it: nothing left behind to still
    // read as drift.
    private static readonly Regex DurationMask = new(
        " — \\d+(\\.\\d+)?ms", RegexOptions.CultureInvariant);

    // A label immediately followed by "adjective noun verb", optionally the
    // "(1234567)" trace-id fragment — the exact shapes TraceNamer-derived
    // text appears in across every renderer and frontmatter field this
    // repository writes *(since 0.1.4, unreleased)*.
    private static readonly Regex TraceLabelMask = new(
        @"(?i)\b(trace_name|traceName|runName|run|trace):(\s*)[a-z]+ [a-z]+ [a-z]+(\s*\([0-9a-f]{7}\))?",
        RegexOptions.CultureInvariant);
    private static readonly Regex TraceProseMask = new(
        @"The trace [a-z]+ [a-z]+ [a-z]+:", RegexOptions.CultureInvariant);
    private static readonly Regex TraceTitleMask = new(
        @"## Trace: [a-z]+ [a-z]+ [a-z]+ — ", RegexOptions.CultureInvariant);

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

    /// <summary>
    /// Every page this check watches: every <c>*.md</c> under
    /// <c>documentation/</c> or at the repo root, plus <c>llms.txt</c> —
    /// the agent-facing index is a <c>.txt</c> file so it falls outside the
    /// Markdown glob, but its own "copy this" block is exactly the kind of
    /// quickstart code+output pairing rule 8 exists to keep honest, so it
    /// gets the same marker treatment as every other quickstart page —
    /// and every <c>.claude/skills/&lt;segment&gt;/SKILL.md</c>: a skill page's
    /// snippet steps embed the identical example sources through the identical
    /// <c>&lt;!-- snippet: PATH --&gt;</c> convention (see
    /// <c>NarrativeTrace.Skills.Render.ClaudeSkillRenderer</c>), so this
    /// check's drift coverage extends there too rather than leaving it to
    /// <c>SkillsLint</c>'s independent (and differently-shaped) fresh-render
    /// comparison alone.
    /// </summary>
    private static IEnumerable<string> MarkdownFiles(string root)
    {
        var documentation = Path.Combine(root, "documentation");
        var underDocs = Directory.Exists(documentation)
            ? Directory.EnumerateFiles(documentation, "*.md", SearchOption.AllDirectories)
            : Enumerable.Empty<string>();
        var atRoot = Directory.EnumerateFiles(root, "*.md", SearchOption.TopDirectoryOnly);
        var llmsTxt = Directory.Exists(documentation)
            ? Directory.EnumerateFiles(documentation, "llms.txt", SearchOption.AllDirectories)
            : Enumerable.Empty<string>();
        var claudeSkills = Path.Combine(root, ".claude", "skills");
        var skillPages = Directory.Exists(claudeSkills)
            ? Directory.EnumerateFiles(claudeSkills, "SKILL.md", SearchOption.AllDirectories)
            : Enumerable.Empty<string>();
        return underDocs.Concat(atRoot).Concat(llmsTxt).Concat(skillPages)
            .OrderBy(f => f, StringComparer.Ordinal);
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
            if (!TryParseMarker(marker, out var sourcePath, out var region, out var masks, out var markerError))
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

            var maskedPage = ApplyMask(pageContent, masks);
            var maskedSource = ApplyMask(sourceContent, masks);
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
        Match marker, out string sourcePath, out string? region, out SnippetMasks masks, out string? error)
    {
        sourcePath = marker.Groups["path"].Value;
        region = marker.Groups["region"].Success ? marker.Groups["region"].Value : null;
        var mask = marker.Groups["mask"].Success ? marker.Groups["mask"].Value : null;
        masks = SnippetMasks.None;
        if (mask is null)
        {
            error = null;
            return true;
        }

        foreach (var name in mask.Split(','))
        {
            switch (name)
            {
                case "duration":
                    masks = masks with { Duration = true };
                    break;
                case "traceName":
                    masks = masks with { TraceName = true };
                    break;
                default:
                    error = $"unknown mask '{name}' — only duration and traceName are supported";
                    return false;
            }
        }

        error = null;
        return true;
    }

    /// <summary>
    /// The <c>mask=</c> names parsed from one snippet marker, each an
    /// independent normalization applied to both sides before comparison —
    /// see the type's own remarks *(since 0.1.4, unreleased)*.
    /// </summary>
    private readonly record struct SnippetMasks(bool Duration, bool TraceName)
    {
        public static readonly SnippetMasks None = new(false, false);
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

        var text = LicenseHeaderStripper.Strip(File.ReadAllText(full).Replace("\r\n", "\n"));
        if (region is null)
        {
            error = null;
            return text.TrimEnd('\n');
        }

        return ExtractRegion(text, relativePath, region, out error);
    }

    // StripLicenseHeader/LeadingCommentBlockEnd moved to NarrativeTrace.Core.LicenseHeaderStripper
    // (2026-09-13): NarrativeTrace.Skills's own SnippetResolver needs the identical stripping
    // logic (a rendered SKILL.md page embeds a real source file verbatim too, and hits the exact
    // same publish-time-header drift), so this is now the one shared implementation both call —
    // never a second copy of the same algorithm. See that class's own remarks for the full
    // rationale (unchanged from here).

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

    private static string ApplyMask(string content, SnippetMasks masks)
    {
        var result = content;
        if (masks.Duration)
        {
            result = DurationMask.Replace(result, "");
        }

        if (masks.TraceName)
        {
            result = MaskTraceName(result);
        }

        return result;
    }

    private static string MaskTraceName(string content)
    {
        var masked = TraceLabelMask.Replace(
            content, m => $"{m.Groups[1].Value}:{m.Groups[2].Value}NAME NAME NAME");
        masked = TraceProseMask.Replace(masked, "The trace NAME NAME NAME:");
        return TraceTitleMask.Replace(masked, "## Trace: NAME NAME NAME — ");
    }

    private static string[] ToLines(string text) => text.Replace("\r\n", "\n").Split('\n');

    private static string Join(IReadOnlyList<string> lines, int fromInclusive, int toExclusive) =>
        string.Join("\n", lines.Skip(fromInclusive).Take(toExclusive - fromInclusive));
}
