// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Text;

namespace NarrativeTrace.Tooling.Init;

/// <summary>
/// The one line that says a copied <c>SKILL.md</c> is ours and which carrier it came from.
/// </summary>
/// <remarks>
/// <para>INTENT: the installer must be able to tell a page it wrote from a page somebody else wrote,
/// years later and with no other state. That is what makes an upgrade safe (overwrite ours), a
/// foreign directory safe (refuse), and an uninstall safe (delete only ours).</para>
/// <para>The line goes AFTER the YAML frontmatter, never before it: a comment above the opening
/// <c>---</c> stops the frontmatter from parsing, and every skill runtime reads that frontmatter
/// first.</para>
/// <para>Matching is anchored at column 0, the same rule the managed block's markers follow, so a
/// page that quotes the line in an example is not mistaken for an installed page.</para>
/// </remarks>
internal static class Provenance
{
    internal const string Prefix = "<!-- installed by narrativetrace init from ";

    internal const string Suffix = " — edit the catalogue, not this file -->";

    private const string FrontmatterFence = "---";

    /// <summary>The provenance line for one carrier coordinate.</summary>
    internal static string Line(string coordinate) => Prefix + coordinate + Suffix;

    /// <summary>The coordinate a page was installed from, or <see langword="null"/> when the page is not ours.</summary>
    internal static string? CoordinateIn(string page)
    {
        foreach (var raw in MarkedBlock.SplitKeepingTerminators(page))
        {
            var coordinate = CoordinateOf(Content(raw));
            if (coordinate is not null)
            {
                return coordinate;
            }
        }

        return null;
    }

    /// <summary>The coordinate one line carries, or <see langword="null"/> when the line is not a provenance line.</summary>
    private static string? CoordinateOf(string line)
    {
        if (!IsProvenance(line))
        {
            return null;
        }

        var coordinate = line[Prefix.Length..^Suffix.Length].Trim();
        return coordinate.Length == 0 ? null : coordinate;
    }

    /// <summary>
    /// The page with exactly one provenance line for <paramref name="coordinate"/>, placed after the
    /// frontmatter — replacing any line a previous install left, so stamping twice is stamping once.
    /// </summary>
    internal static string Stamp(string page, string coordinate)
    {
        var eol = MarkedBlock.EolOf(page);
        var output = new StringBuilder();
        var insertAfter = FrontmatterEnd(page);
        var number = 0;
        foreach (var raw in MarkedBlock.SplitKeepingTerminators(page))
        {
            number++;
            if (!IsProvenance(Content(raw)))
            {
                output.Append(raw);
            }

            if (number == insertAfter)
            {
                output.Append(Line(coordinate)).Append(eol);
            }
        }

        return insertAfter == 0 ? Line(coordinate) + eol + eol + output : output.ToString();
    }

    /// <summary>
    /// A whole line, not a fragment: the length guard matters because <see cref="Prefix"/> ends with
    /// a space and <see cref="Suffix"/> starts with one, so the two can OVERLAP in a hand-edited line.
    /// </summary>
    private static bool IsProvenance(string line) =>
        line.Length >= Prefix.Length + Suffix.Length
            && line.StartsWith(Prefix, StringComparison.Ordinal)
            && line.EndsWith(Suffix, StringComparison.Ordinal);

    /// <summary>The 1-based line number of the frontmatter's closing fence, or 0 when there is none.</summary>
    private static int FrontmatterEnd(string page)
    {
        var lines = MarkedBlock.SplitKeepingTerminators(page);
        if (lines.Count == 0 || !string.Equals(Content(lines[0]), FrontmatterFence, StringComparison.Ordinal))
        {
            return 0;
        }

        for (var i = 1; i < lines.Count; i++)
        {
            if (string.Equals(Content(lines[i]), FrontmatterFence, StringComparison.Ordinal))
            {
                return i + 1;
            }
        }

        return 0;
    }

    /// <summary>A line without its terminator.</summary>
    private static string Content(string line)
    {
        var text = line.EndsWith('\n') ? line[..^1] : line;
        return text.EndsWith('\r') ? text[..^1] : text;
    }
}
