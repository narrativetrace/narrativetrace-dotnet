// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Text;

namespace NarrativeTrace.Core;

/// <summary>
/// Longest-common-subsequence line diff in the conventional format: <c>-</c>
/// removed, <c>+</c> added, one leading space on unchanged context lines.
/// </summary>
/// <remarks>
/// The rendering half of <see cref="StructuralDelta"/> — kept free of any
/// <c>.nt</c> format knowledge so the delta class owns what a change
/// <em>means</em> and this class owns how a change <em>reads</em>. Inputs are
/// whole documents; ties between a deletion and an insertion resolve to the
/// deletion so removed lines always precede their replacements.
/// </remarks>
internal static class LineDiff
{
    /// <summary>
    /// Splits a document into lines the way Java's <c>String.lines()</c>
    /// does: normalizes line endings, and a trailing terminator does not
    /// produce a trailing empty entry.
    /// </summary>
    internal static IReadOnlyList<string> ToLines(string document)
    {
        if (document.Length == 0)
        {
            return [];
        }

        var normalized = document.Replace("\r\n", "\n").Replace('\r', '\n');
        var lines = normalized.Split('\n');
        if (lines.Length == 0 || lines[^1].Length != 0)
        {
            return lines;
        }

        // Array range-slicing (lines[..^1]) needs RuntimeHelpers.GetSubArray,
        // unavailable on netstandard2.0, which this assembly still targets.
        var withoutTrailingEmpty = new string[lines.Length - 1];
        Array.Copy(lines, withoutTrailingEmpty, withoutTrailingEmpty.Length);
        return withoutTrailingEmpty;
    }

    /// <summary>The diff with lines matched on their exact text.</summary>
    internal static string Unified(string baseline, string current)
    {
        return Unified(baseline, current, line => line, (was, _) => was);
    }

    /// <summary>
    /// The diff with lines matched on <paramref name="key"/>, so two lines
    /// whose keys agree are context even when their bytes differ;
    /// <paramref name="context"/> renders such a pair from (baseline line,
    /// current line). Removed lines print as the baseline wrote them, added
    /// lines as the current document does.
    /// </summary>
    internal static string Unified(
        string baseline, string current,
        Func<string, string> key, Func<string, string, string> context)
    {
        var sides = new Sides(ToLines(baseline), ToLines(current), key);
        var sb = new StringBuilder();
        var (i, j) = RenderCommon(sb, LcsTable(sides), sides, context);
        RenderTail(sb, sides.Baseline, i, '-');
        RenderTail(sb, sides.Current, j, '+');
        return sb.ToString();
    }

    /// <summary>Both documents, each as its printed lines and the keys those lines are matched on.</summary>
    private sealed class Sides
    {
        internal Sides(IReadOnlyList<string> baseline, IReadOnlyList<string> current, Func<string, string> key)
        {
            Baseline = baseline;
            Current = current;
            BaselineKeys = baseline.Select(key).ToList();
            CurrentKeys = current.Select(key).ToList();
        }

        internal IReadOnlyList<string> Baseline { get; }

        internal IReadOnlyList<string> Current { get; }

        internal IReadOnlyList<string> BaselineKeys { get; }

        internal IReadOnlyList<string> CurrentKeys { get; }

        internal bool SameKey(int i, int j) =>
            string.Equals(BaselineKeys[i], CurrentKeys[j], StringComparison.Ordinal);
    }

    private static int[,] LcsTable(Sides sides)
    {
        var table = new int[sides.Baseline.Count + 1, sides.Current.Count + 1];
        for (var i = sides.Baseline.Count - 1; i >= 0; i--)
        {
            for (var j = sides.Current.Count - 1; j >= 0; j--)
            {
                table[i, j] = sides.SameKey(i, j)
                    ? table[i + 1, j + 1] + 1
                    : Math.Max(table[i + 1, j], table[i, j + 1]);
            }
        }

        return table;
    }

    /// <summary>Walks both documents while either still has lines, then reports how far each got.</summary>
    private static (int Baseline, int Current) RenderCommon(
        StringBuilder sb, int[,] table, Sides sides, Func<string, string, string> context)
    {
        var i = 0;
        var j = 0;
        while (i < sides.Baseline.Count && j < sides.Current.Count)
        {
            if (sides.SameKey(i, j))
            {
                sb.Append(' ').Append(context(sides.Baseline[i++], sides.Current[j++])).Append('\n');
            }
            else if (table[i + 1, j] >= table[i, j + 1])
            {
                sb.Append('-').Append(sides.Baseline[i++]).Append('\n');
            }
            else
            {
                sb.Append('+').Append(sides.Current[j++]).Append('\n');
            }
        }

        return (i, j);
    }

    /// <summary>Whatever is left of one document once the other ran out — all additions or all removals.</summary>
    private static void RenderTail(StringBuilder sb, IReadOnlyList<string> lines, int from, char marker)
    {
        for (var index = from; index < lines.Count; index++)
        {
            sb.Append(marker).Append(lines[index]).Append('\n');
        }
    }
}
