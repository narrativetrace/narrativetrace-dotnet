// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Text;

namespace NarrativeTrace.Core;

/// <summary>
/// Escapers for text interpolated into Markdown output.
/// </summary>
/// <remarks>
/// <see cref="MarkdownRenderer"/> embeds rendered trace values,
/// exception messages, error context, and narration — any of which may
/// carry attacker-influenced content — into a Markdown document later
/// viewed in a browser or preview. This helper is the single place that
/// neutralizes characters capable of injecting active markup.
/// <see cref="Text"/> HTML-escapes prose placed as active Markdown;
/// <see cref="Code"/> widens the backtick fence around inline code so
/// content cannot terminate the span early.
/// </remarks>
/// <remarks>
/// @edgeCase Both sinks first run the text through <see cref="ControlEscape"/>. Every site that
/// interpolates here is a single line — an error line, an error-context line, an italic narration
/// line, an inline code span — so a raw line break is markup: it ends the line and starts whatever
/// follows as document structure. An exception message with a newline and a fence in it opened a
/// fenced code block from inside a value; exception messages commonly echo user input, which is
/// exactly the reason this class exists. Control characters render as the same inert escapes the
/// rest of the product uses, so the text stays on its line and stays readable.
/// </remarks>
internal static class MarkdownEscape
{
    /// <summary>
    /// Neutralizes control characters, then HTML-escapes <c>&amp;</c>, <c>&lt;</c> and <c>&gt;</c>
    /// so interpolated prose cannot introduce active HTML, and a raw line break cannot open document
    /// structure (a fence, a heading, a blockquote) from inside a single-line value.
    /// </summary>
    public static string Text(string text)
    {
        var safe = ControlEscape.Sanitize(text);
        var sb = new StringBuilder(safe.Length);
        foreach (var c in safe)
        {
            sb.Append(c switch
            {
                '&' => "&amp;",
                '<' => "&lt;",
                '>' => "&gt;",
                var other => other.ToString(),
            });
        }

        return sb.ToString();
    }

    /// <summary>
    /// Neutralizes control characters, then wraps <paramref name="content"/> in an inline code span
    /// whose backtick fence is longer than any run of backticks inside it, so the content cannot
    /// terminate the span early — and, with control characters rendered inert first, a line break
    /// cannot end the span either.
    /// </summary>
    public static string Code(string content)
    {
        var safe = ControlEscape.Sanitize(content);
        var maxRun = LongestBacktickRun(safe);
        if (maxRun == 0)
        {
            return $"`{safe}`";
        }

        var fence = new string('`', maxRun + 1);
        return $"{fence} {safe} {fence}";
    }

    private static int LongestBacktickRun(string s)
    {
        var max = 0;
        var run = 0;
        foreach (var c in s)
        {
            if (c == '`')
            {
                run++;
                max = Math.Max(max, run);
            }
            else
            {
                run = 0;
            }
        }

        return max;
    }
}
