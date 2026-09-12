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
    // Every character this escaper rewrites grows by three or four, so a little
    // slack keeps the builder from resizing on the first entity.
    private const int EscapeHeadroom = 8;

    /// <summary>
    /// Neutralizes control characters, then HTML-escapes <c>&amp;</c>, <c>&lt;</c> and <c>&gt;</c>
    /// so interpolated prose cannot introduce active HTML, and a raw line break cannot open document
    /// structure (a fence, a heading, a blockquote) from inside a single-line value.
    /// </summary>
    public static string Text(string text)
    {
        var safe = ControlEscape.Sanitize(text);
        var first = IndexOfHtmlSpecial(safe);
        if (first < 0)
        {
            return safe;
        }

        var sb = new StringBuilder(safe.Length + EscapeHeadroom);
        sb.Append(safe, 0, first);
        for (var i = first; i < safe.Length; i++)
        {
            AppendEscaped(sb, safe[i]);
        }

        return sb.ToString();
    }

    /// <summary>
    /// The first character this escaper would rewrite, or <c>-1</c> when there is
    /// none.
    /// </summary>
    /// <remarks>
    /// Nearly every string interpolated into the document — a class name, a
    /// method name, a parameter name, an ordinary rendered value — contains no
    /// <c>&amp;</c>, <c>&lt;</c> or <c>&gt;</c>, and for those the escaped form
    /// is the input. The previous shape allocated a
    /// <see cref="StringBuilder"/> <em>and</em> a fresh one-character string per
    /// character scanned, on a path the renderer walks several times per traced
    /// node; scanning first is one allocation-free pass that lets the clean case
    /// return what <see cref="ControlEscape"/> already produced.
    /// </remarks>
    private static int IndexOfHtmlSpecial(string safe)
    {
        for (var i = 0; i < safe.Length; i++)
        {
            if (safe[i] is '&' or '<' or '>')
            {
                return i;
            }
        }

        return -1;
    }

    private static void AppendEscaped(StringBuilder sb, char c)
    {
        switch (c)
        {
            case '&': sb.Append("&amp;"); break;
            case '<': sb.Append("&lt;"); break;
            case '>': sb.Append("&gt;"); break;
            default: sb.Append(c); break;
        }
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
