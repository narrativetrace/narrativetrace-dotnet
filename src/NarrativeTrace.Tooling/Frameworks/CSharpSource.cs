// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Text;

namespace NarrativeTrace.Tooling.Frameworks;

/// <summary>
/// C# source with its comments removed — so wiring that is commented out is not wiring — while every
/// string and character literal stays exactly as written (a <c>//</c> inside <c>"http://…"</c> is text).
/// </summary>
/// <remarks>
/// A single forward scan that knows the literal forms the comment markers can hide in: regular and
/// interpolated strings (backslash escapes, ending at a newline when unterminated), verbatim strings
/// (<c>""</c> escapes), raw string literals (a run of three or more quotes closed by the same run) and
/// character literals. A block comment becomes one space, a line comment ends before its newline. An
/// interpolation hole holding a nested string literal may end a string early; the cost is a comment
/// marker read as text, never a crash or a loop.
/// </remarks>
internal static class CSharpSource
{
    /// <summary><paramref name="source"/> without its comments.</summary>
    public static string WithoutComments(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var result = new StringBuilder(source.Length);
        var i = 0;
        while (i < source.Length)
        {
            var end = SkipOrCopy(source, i, result);
            i = end > i ? end : i + 1;
        }

        return result.ToString();
    }

    /// <summary>Handles the token at <paramref name="i"/>; returns the index after it.</summary>
    private static int SkipOrCopy(string source, int i, StringBuilder result)
    {
        var c = source[i];
        if (c == '/' && At(source, i + 1, '/'))
        {
            var newline = source.IndexOf('\n', i);
            return newline < 0 ? source.Length : newline;
        }

        if (c == '/' && At(source, i + 1, '*'))
        {
            var close = source.IndexOf("*/", i + 2, StringComparison.Ordinal);
            result.Append(' ');
            return close < 0 ? source.Length : close + 2;
        }

        var end = LiteralEnd(source, i);
        result.Append(source, i, end - i);
        return end;
    }

    /// <summary>The index after the literal starting at <paramref name="i"/>, or <c>i + 1</c> for any other character.</summary>
    private static int LiteralEnd(string source, int i)
    {
        var c = source[i];
        if (c == '@' && At(source, i + 1, '"'))
        {
            return VerbatimEnd(source, i + 2);
        }

        if (c == '@' && At(source, i + 1, '$') && At(source, i + 2, '"'))
        {
            return VerbatimEnd(source, i + 3);
        }

        if (c == '"' && At(source, i + 1, '"') && At(source, i + 2, '"'))
        {
            return RawEnd(source, i);
        }

        return c is '"' or '\'' ? EscapedEnd(source, i + 1, c) : i + 1;
    }

    private static int EscapedEnd(string source, int from, char quote)
    {
        var i = from;
        while (i < source.Length && source[i] != quote && source[i] != '\n')
        {
            i += source[i] == '\\' ? 2 : 1;
        }

        return Math.Min(i + 1, source.Length);
    }

    private static int VerbatimEnd(string source, int from)
    {
        var i = from;
        while (i < source.Length)
        {
            if (source[i] == '"' && !At(source, i + 1, '"'))
            {
                return i + 1;
            }

            i += source[i] == '"' ? 2 : 1;
        }

        return source.Length;
    }

    private static int RawEnd(string source, int from)
    {
        var quotes = 0;
        while (At(source, from + quotes, '"'))
        {
            quotes++;
        }

        var close = source.IndexOf(new string('"', quotes), from + quotes, StringComparison.Ordinal);
        return close < 0 ? source.Length : close + quotes;
    }

    private static bool At(string source, int index, char expected) =>
        index < source.Length && source[index] == expected;
}
