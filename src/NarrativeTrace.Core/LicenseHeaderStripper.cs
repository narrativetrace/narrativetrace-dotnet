// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Linq;

namespace NarrativeTrace.Core;

/// <summary>
/// Strips a leading license-header comment block a publish-time stamp adds to a source file — the
/// one piece of logic every place that embeds a real source file verbatim (the documentation
/// build's own <c>snippet-check</c>, and <c>NarrativeTrace.Skills</c>'s <c>SnippetResolver</c>)
/// must agree on, so a file that reads one way in this checkout and another way in the staged
/// public snapshot never makes an embedded copy quietly drift the moment it is published.
/// </summary>
/// <remarks>
/// The header is stamped into a shipped file only by the publish script, at publish time, never
/// in-tree (the license-stamping-public-only convention) — so an in-tree file like
/// <c>examples/NarrativeTrace.Examples.SixtySeconds/Program.cs</c> matches whatever embeds it
/// verbatim while developed here, then would silently drift the moment the staged public
/// snapshot's copy of the same file gained three comment lines the embed never shows. Deliberately
/// narrow: only an initial run of <c>//</c> lines, or one leading <c>/* ... */</c> block, whose own
/// text contains <c>SPDX-License-Identifier</c> or <c>Licensed under</c> is stripped — matching the
/// two phrases the publish script's own header actually carries — so a copyright line alone still
/// counts once either phrase is present anywhere in the same leading block. Any other leading
/// comment — a file banner, an unrelated copyright notice, a doc comment — is left exactly as it
/// was; this is not a general "skip the top comment" heuristic. A single blank line immediately
/// after the stripped block is stripped too, so a header that happens to be followed by one leaves
/// no gap the embedding side would then have to match.
/// </remarks>
public static class LicenseHeaderStripper
{
    /// <summary>Returns <paramref name="text"/> with any leading license-header comment block removed.</summary>
    public static string Strip(string text)
    {
        var lines = text.Split('\n');
        var end = LeadingCommentBlockEnd(lines);
        if (end == 0)
        {
            return text;
        }

        var header = string.Join("\n", lines.Take(end));
        if (header.IndexOf("SPDX-License-Identifier", StringComparison.Ordinal) < 0
            && header.IndexOf("Licensed under", StringComparison.Ordinal) < 0)
        {
            return text;
        }

        var start = end < lines.Length && lines[end].Length == 0 ? end + 1 : end;
        return string.Join("\n", lines.Skip(start));
    }

    /// <summary>
    /// The exclusive end index of the file's leading comment — one <c>/* ... */</c> block, or a
    /// contiguous run of <c>//</c> lines — or <c>0</c> when the file does not open with a comment
    /// at all (including an unterminated <c>/*</c>, which is left alone rather than guessed at).
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
}
