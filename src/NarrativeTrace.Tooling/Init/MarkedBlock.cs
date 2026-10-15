// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Tooling.Init;

/// <summary>
/// The delimited section the installer owns inside a consumer's file, and every edit that section
/// can undergo.
/// </summary>
/// <remarks>
/// <para>INTENT: one place decides what counts as a marker. Everything the installer writes into a
/// file it did not create lives between <c>&lt;!-- narrativetrace:start … --&gt;</c> and
/// <c>&lt;!-- narrativetrace:end --&gt;</c>, so a re-run replaces exactly that region and an
/// uninstall removes exactly that region — the property that makes both operations safe on a file
/// somebody else owns.</para>
/// <para>The marker rule is narrow on purpose: a marker counts only when its line STARTS with the
/// marker text (column 0, no indentation) and the line is OUTSIDE a fenced code block. A document
/// that shows the markers in an example fence therefore keeps its own meaning, and this
/// repository's own <c>narrativetrace:skills:*</c> markers are not consumer markers — the prefix
/// differs.</para>
/// <para><see cref="Append"/> and <see cref="Remove(string, Region)"/> are inverses: appending
/// separates the block from what was there with exactly one blank line, and removing takes that
/// blank line back. The one thing an append cannot undo is the final newline it adds to a file that
/// had none — a block has to start on its own line.</para>
/// </remarks>
internal static class MarkedBlock
{
    /// <summary>The opening marker, up to but not including the optional coordinate stamp.</summary>
    internal const string StartMarker = "<!-- narrativetrace:start";

    internal const string EndMarker = "<!-- narrativetrace:end -->";

    /// <summary>
    /// Written as the first line of a file the installer CREATED, so an uninstall can tell a file
    /// it may delete from one it merely appended to.
    /// </summary>
    internal const string CreatedNote = "<!-- narrativetrace:created -->";

    private const char Bom = '﻿';

    /// <summary>One managed region inside a file.</summary>
    /// <param name="Start">
    /// Offset of the first character of the opening marker's line, after a byte-order mark on line 1.
    /// </param>
    /// <param name="End">Offset just past the closing marker's line, its terminator included.</param>
    /// <param name="StartLine">1-based line of the opening marker, so a refusal can name it.</param>
    /// <param name="Coordinate">The stamp on the opening marker, empty when it carries none.</param>
    internal sealed record Region(int Start, int End, int StartLine, string Coordinate);

    /// <summary>One line of a file, with everything a marker decision needs.</summary>
    /// <param name="Number">1-based line number.</param>
    /// <param name="Start">Offset of the line's first character, after the byte-order mark on line 1.</param>
    /// <param name="End">Offset just past the line's terminator.</param>
    /// <param name="Content">The line without its terminator, and without a leading byte-order mark.</param>
    /// <param name="InFence">Whether the line sits inside a fenced code block.</param>
    internal sealed record Line(int Number, int Start, int End, string Content, bool InFence);

    /// <summary>What a scan found: every complete region, and every marker that cannot be part of one.</summary>
    internal sealed record Scan(IReadOnlyList<Region> Regions, IReadOnlyList<string> Problems)
    {
        /// <summary>True when the file carries exactly one well-formed region and nothing questionable.</summary>
        internal bool HasExactlyOneRegion => Problems.Count == 0 && Regions.Count == 1;
    }

    /// <summary>
    /// Splits a file into lines that know their offsets and whether they are inside a fence — the
    /// one primitive both the marker scan and the import-line check read the file through.
    /// </summary>
    internal static List<Line> Lines(string text)
    {
        var lines = new List<Line>();
        var inFence = false;
        var offset = 0;
        var number = 0;
        foreach (var raw in SplitKeepingTerminators(text))
        {
            number++;
            var content = Strip(raw, number);
            var fenceLine = IsFence(content);
            var bom = number == 1 && raw.StartsWith(Bom) ? 1 : 0;
            lines.Add(new Line(number, offset + bom, offset + raw.Length, content, inFence || fenceLine));
            if (fenceLine)
            {
                inFence = !inFence;
            }

            offset += raw.Length;
        }

        return lines;
    }

    /// <summary>
    /// Finds every managed region in a file. Never throws: a malformed file is described, not read.
    /// </summary>
    internal static Scan ScanText(string text)
    {
        var regions = new List<Region>();
        var problems = new List<string>();
        var scanner = new Scanner(regions, problems);
        foreach (var line in Lines(text))
        {
            if (!line.InFence)
            {
                scanner.Accept(line);
            }
        }

        scanner.Finish();
        return new Scan(regions, problems);
    }

    /// <summary>
    /// Whether the text ends with a fenced code block still open. Load-bearing: anything appended
    /// to such a file lands INSIDE that fence, where neither this scanner nor any Markdown reader
    /// will see it as a marker or an import — so the next run appends again. Appending is refused.
    /// </summary>
    internal static bool EndsInsideFence(string text) =>
        Lines(text).Count(line => IsFence(line.Content)) % 2 == 1;

    /// <summary>A fenced-code delimiter, recognised at column 0 like every other marker here.</summary>
    private static bool IsFence(string content) =>
        content.StartsWith("```", StringComparison.Ordinal)
            || content.StartsWith("~~~", StringComparison.Ordinal);

    /// <summary>The first line outside a fence whose text is exactly this — what an uninstall removes.</summary>
    internal static Line? LineIs(string text, string wanted) =>
        FirstLine(text, content => string.Equals(content, wanted, StringComparison.Ordinal));

    /// <summary>
    /// The first line outside a fence whose text, trailing whitespace ignored, is this — what an
    /// install reads as "already there", because trailing spaces change nothing for a reader.
    /// </summary>
    internal static Line? LineIsIgnoringTrailingSpace(string text, string wanted) =>
        FirstLine(text, content => string.Equals(content.TrimEnd(), wanted, StringComparison.Ordinal));

    private static Line? FirstLine(string text, Func<string, bool> matches) =>
        Lines(text).FirstOrDefault(line => !line.InFence && matches(line.Content));

    /// <summary>Replaces one region with <paramref name="block"/>, byte for byte everywhere else.</summary>
    internal static string Replace(string text, Region region, string block) =>
        text[..region.Start] + block + text[region.End..];

    /// <summary>
    /// Appends <paramref name="block"/> to <paramref name="text"/>, separated by exactly one blank
    /// line — nothing at all when the file is empty. A file that did not end with a newline gets
    /// one: a block starts on its own line.
    /// </summary>
    internal static string Append(string text, string block)
    {
        if (text.Length == 0)
        {
            return block;
        }

        var eol = EolOf(text);
        var head = text.EndsWith('\n') || text.EndsWith('\r') ? text : text + eol;
        return head + eol + block;
    }

    /// <summary>Removes one region and the single blank line <see cref="Append"/> would have put before it.</summary>
    internal static string Remove(string text, Region region) => Cut(text, region.Start, region.End);

    /// <summary>Removes one line and the single blank line <see cref="Append"/> would have put before it.</summary>
    internal static string Remove(string text, Line line) => Cut(text, line.Start, line.End);

    private static string Cut(string text, int start, int end)
    {
        var head = text[..start];
        var eol = EolOf(text);
        if (head.EndsWith(eol + eol, StringComparison.Ordinal))
        {
            head = head[..^eol.Length];
        }

        return head + text[end..];
    }

    /// <summary>The line ending a file uses, decided by its FIRST terminator; <c>\n</c> when it has none.</summary>
    internal static string EolOf(string text)
    {
        var newline = text.IndexOf('\n', StringComparison.Ordinal);
        if (newline < 0)
        {
            return "\n";
        }

        return newline > 0 && text[newline - 1] == '\r' ? "\r\n" : "\n";
    }

    /// <summary>
    /// The text without a leading byte-order mark. A line's <c>Start</c> is the offset of its first
    /// character AFTER such a mark, so an edit of a region on line 1 leaves the mark where it was; a
    /// caller that asks "is anything left?" strips it, because a mark alone is not content.
    /// </summary>
    internal static string WithoutByteOrderMark(string text) => text.StartsWith(Bom) ? text[1..] : text;

    /// <summary>The same text with every line ending rewritten to <paramref name="eol"/>.</summary>
    internal static string WithEol(string text, string eol) =>
        text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\n", eol, StringComparison.Ordinal);

    /// <summary>Splits into lines that still carry their terminators, so offsets stay exact.</summary>
    internal static List<string> SplitKeepingTerminators(string text)
    {
        var lines = new List<string>();
        var start = 0;
        while (start < text.Length)
        {
            var newline = text.IndexOf('\n', start);
            var endExclusive = newline < 0 ? text.Length : newline + 1;
            lines.Add(text[start..endExclusive]);
            start = endExclusive;
        }

        return lines;
    }

    /// <summary>The line's content for MATCHING only: no terminator, and no byte-order mark on line 1.</summary>
    private static string Strip(string line, int number)
    {
        var content = line.EndsWith('\n') ? line[..^1] : line;
        if (content.EndsWith('\r'))
        {
            content = content[..^1];
        }

        return number == 1 && content.StartsWith(Bom) ? content[1..] : content;
    }

    /// <summary>Walks the lines once, holding the one piece of state a scan needs: the open start marker.</summary>
    private sealed class Scanner(List<Region> regions, List<string> problems)
    {
        private int _openOffset = -1;
        private int _openLine;
        private string _openCoordinate = "";

        internal void Accept(Line line)
        {
            if (IsStartMarker(line.Content))
            {
                OpenMarker(line.Content, line.Start, line.Number);
            }
            else if (line.Content.StartsWith(EndMarker, StringComparison.Ordinal))
            {
                CloseMarker(line.End, line.Number);
            }
        }

        /// <summary>The start marker proper: after the prefix comes a space or the line's end, never more word.</summary>
        private static bool IsStartMarker(string content) =>
            content.StartsWith(StartMarker, StringComparison.Ordinal)
            && (content.Length == StartMarker.Length || content[StartMarker.Length] == ' ');

        private void OpenMarker(string content, int offset, int number)
        {
            if (_openOffset >= 0)
            {
                problems.Add(
                    $"line {number}: a narrativetrace:start marker inside the block opened at line {_openLine}");
                return;
            }

            _openOffset = offset;
            _openLine = number;
            _openCoordinate = CoordinateIn(content);
        }

        private void CloseMarker(int endExclusive, int number)
        {
            if (_openOffset < 0)
            {
                problems.Add($"line {number}: a narrativetrace:end marker with no start above it");
                return;
            }

            regions.Add(new Region(_openOffset, endExclusive, _openLine, _openCoordinate));
            _openOffset = -1;
        }

        internal void Finish()
        {
            if (_openOffset >= 0)
            {
                problems.Add($"line {_openLine}: a narrativetrace:start marker with no end below it");
            }
        }

        private static string CoordinateIn(string content)
        {
            var rest = content[StartMarker.Length..];
            var close = rest.IndexOf("-->", StringComparison.Ordinal);
            return close < 0 ? "" : rest[..close].Trim();
        }
    }
}
