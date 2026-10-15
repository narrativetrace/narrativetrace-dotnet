// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
#nullable enable
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace NarrativeTrace.Build;

/// <summary>
/// Finds the opening code fences that carry no language tag. Context7 indexes a fenced block's
/// language with the snippet it serves; an untagged block is indexed as prose, so every opening
/// fence in an indexed document must name its language (<c>text</c> for plain output).
/// </summary>
internal static class FenceAuditSupport
{
    private static readonly Regex FenceLine = new(
        @"^\s*(?<run>`{3,}|~{3,})(?<info>.*)$",
        RegexOptions.CultureInvariant);

    /// <summary>
    /// The 1-based line numbers of every opening fence in <paramref name="markdown"/> whose
    /// info string is empty. A closing fence is a run of the opening character at least as long
    /// as the opener with no info string; fence-looking lines inside an open block are content.
    /// </summary>
    /// <param name="markdown">The document text.</param>
    /// <returns>Line numbers, ascending; empty when every fence is tagged.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="markdown"/> is <c>null</c>.</exception>
    public static IReadOnlyList<int> UntaggedFenceLines(string markdown)
    {
        if (markdown is null)
            throw new ArgumentNullException(nameof(markdown));
        var untagged = new List<int>();
        string? open = null;
        var lines = markdown.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var match = FenceLine.Match(lines[i].TrimEnd('\r'));
            if (!match.Success)
                continue;
            var run = match.Groups["run"].Value;
            var hasInfo = match.Groups["info"].Value.Trim().Length > 0;
            if (open is null)
            {
                open = run;
                if (!hasInfo)
                    untagged.Add(i + 1);
            }
            else if (run[0] == open[0] && run.Length >= open.Length && !hasInfo)
            {
                open = null;
            }
        }
        return untagged;
    }
}
