// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Text;

namespace NarrativeTrace.Tooling.Init;

/// <summary>
/// The smallest unified diff that tells the truth about one file.
/// </summary>
/// <remarks>
/// <para>INTENT: <c>--dry-run</c> has to SHOW what would change, and the tooling library takes zero
/// dependencies. The algorithm is deliberately not Myers: the common prefix and suffix are trimmed
/// and everything between them is shown as removed-then-added, in one hunk with three lines of
/// context.</para>
/// <para>The result is a correct unified diff, not a MINIMAL one. For what the installer actually
/// edits — a delimited section inside a file, or a page it rewrites whole — the trimmed middle IS the
/// change, and trading minimality away buys a diff with no quadratic table, safe on a file of any
/// size. A line keeps a carriage return it had, so a change of line endings shows as a changed line
/// rather than as nothing at all.</para>
/// </remarks>
internal static class UnifiedDiff
{
    private const int Context = 3;

    private const string NoNewline = "\\ No newline at end of file";

    /// <summary>The diff of one file, or <c>""</c> when the two texts are equal.</summary>
    /// <param name="path">The label both sides are named with.</param>
    /// <param name="before">The whole file before, <c>""</c> when it did not exist.</param>
    /// <param name="after">The whole file after, <c>""</c> when it is being deleted.</param>
    internal static string Render(string path, string before, string after)
    {
        if (string.Equals(before, after, StringComparison.Ordinal))
        {
            return "";
        }

        var old = Lines(before);
        var current = Lines(after);
        var prefix = CommonPrefix(old, current);
        var suffix = CommonSuffix(old, current, prefix);
        var output = new StringBuilder();
        output.Append("--- ").Append(before.Length == 0 ? "/dev/null" : "a/" + path).Append('\n');
        output.Append("+++ ").Append(after.Length == 0 ? "/dev/null" : "b/" + path).Append('\n');
        AppendHunk(output, old, current, prefix, suffix, before, after);
        return output.ToString();
    }

    private static void AppendHunk(
        StringBuilder output,
        List<string> old,
        List<string> current,
        int prefix,
        int suffix,
        string before,
        string after)
    {
        var start = Math.Max(0, prefix - Context);
        var oldEnd = Math.Min(old.Count, old.Count - suffix + Context);
        var newEnd = Math.Min(current.Count, current.Count - suffix + Context);
        output.Append("@@ -")
            .Append(Header(start, oldEnd - start, old.Count == 0))
            .Append(" +")
            .Append(Header(start, newEnd - start, current.Count == 0))
            .Append(" @@\n");
        for (var i = start; i < prefix; i++)
        {
            output.Append(' ').Append(old[i]).Append('\n');
        }

        AppendSide(output, old, prefix, old.Count - suffix, '-', before);
        AppendSide(output, current, prefix, current.Count - suffix, '+', after);
        for (var i = old.Count - suffix; i < oldEnd; i++)
        {
            output.Append(' ').Append(old[i]).Append('\n');
        }
    }

    /// <summary>The <c>start,count</c> half of a hunk header: a side with no lines starts at 0.</summary>
    private static string Header(int start, int count, bool empty) =>
        empty ? "0,0" : (start + 1) + "," + count;

    private static void AppendSide(
        StringBuilder output, List<string> lines, int from, int to, char sign, string text)
    {
        for (var i = from; i < to; i++)
        {
            output.Append(sign).Append(lines[i]).Append('\n');
            if (i == lines.Count - 1 && !text.EndsWith('\n'))
            {
                output.Append(NoNewline).Append('\n');
            }
        }
    }

    /// <summary>Lines without their newline; a carriage return stays, so an ending change is visible.</summary>
    private static List<string> Lines(string text) =>
        MarkedBlock.SplitKeepingTerminators(text)
            .Select(line => line.EndsWith('\n') ? line[..^1] : line)
            .ToList();

    private static int CommonPrefix(List<string> old, List<string> current)
    {
        var i = 0;
        while (i < old.Count && i < current.Count && string.Equals(old[i], current[i], StringComparison.Ordinal))
        {
            i++;
        }

        return i;
    }

    private static int CommonSuffix(List<string> old, List<string> current, int prefix)
    {
        var i = 0;
        while (i < old.Count - prefix
            && i < current.Count - prefix
            && string.Equals(old[^(i + 1)], current[^(i + 1)], StringComparison.Ordinal))
        {
            i++;
        }

        return i;
    }
}
