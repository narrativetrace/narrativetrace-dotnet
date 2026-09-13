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

    internal static string Unified(string baseline, string current)
    {
        var baselineLines = ToLines(baseline);
        var currentLines = ToLines(current);
        return Render(LcsTable(baselineLines, currentLines), baselineLines, currentLines);
    }

    private static int[,] LcsTable(IReadOnlyList<string> baseline, IReadOnlyList<string> current)
    {
        var table = new int[baseline.Count + 1, current.Count + 1];
        for (var i = baseline.Count - 1; i >= 0; i--)
        {
            for (var j = current.Count - 1; j >= 0; j--)
            {
                table[i, j] = baseline[i] == current[j]
                    ? table[i + 1, j + 1] + 1
                    : Math.Max(table[i + 1, j], table[i, j + 1]);
            }
        }

        return table;
    }

    private static string Render(
        int[,] table, IReadOnlyList<string> baseline, IReadOnlyList<string> current)
    {
        var sb = new StringBuilder();
        var (i, j) = RenderCommon(sb, table, baseline, current);
        RenderTail(sb, baseline, i, '-');
        RenderTail(sb, current, j, '+');
        return sb.ToString();
    }

    /// <summary>Walks both documents while either still has lines, then reports how far each got.</summary>
    private static (int Baseline, int Current) RenderCommon(
        StringBuilder sb, int[,] table, IReadOnlyList<string> baseline, IReadOnlyList<string> current)
    {
        var i = 0;
        var j = 0;
        while (i < baseline.Count && j < current.Count)
        {
            if (baseline[i] == current[j])
            {
                sb.Append(' ').Append(baseline[i++]).Append('\n');
                j++;
            }
            else if (table[i + 1, j] >= table[i, j + 1])
            {
                sb.Append('-').Append(baseline[i++]).Append('\n');
            }
            else
            {
                sb.Append('+').Append(current[j++]).Append('\n');
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
