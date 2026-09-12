// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Globalization;
using System.Text;

namespace NarrativeTrace.Core;

/// <summary>
/// Neutralizes control characters in text bound for line-oriented or
/// terminal sinks.
/// </summary>
/// <remarks>
/// Rendered values, exception messages, and error context flow into
/// logger messages and console renderers. A raw CR/LF forges an extra
/// log line (CWE-117) that a line-based parser or SIEM cannot
/// distinguish from a real one; a raw ESC (0x1B) injects ANSI/OSC
/// terminal sequences. This helper renders every control character as
/// a visible, inert escape so no sink emits a raw control byte.
/// Unlike JSON escaping, it does not touch quotes or backslashes —
/// those are legitimate value content, so <c>C:\temp</c> stays
/// readable.
/// </remarks>
/// <remarks>
/// @edgeCase Unpaired surrogates are escaped the same way, and for the same reason. A lone
/// <c>\uD800</c> is not a control character, but it is not a code point either: no UTF-8 sink can
/// encode it, so <see cref="System.Text.Encoding.GetBytes(string)"/>'s default fallback raises
/// <see cref="System.Text.EncoderFallbackException"/> and a value the application merely
/// <em>returned</em> fails the run that traced it. Escaping it here keeps the text visible,
/// lossless and encodable, exactly as for a control character. A well-formed pair is left alone —
/// an emoji is ordinary content.
/// </remarks>
public static class ControlEscape
{
    /// <summary>
    /// Replaces control characters and unpaired surrogates with visible, inert escape sequences.
    /// </summary>
    /// <param name="text">The raw text — a rendered value, exception message, or error context.</param>
    /// <returns>
    /// The text with every unencodable character rendered as a printable escape.
    /// Quotes and backslashes are deliberately left alone, so <c>C:\temp</c>
    /// stays readable — meaning the result is safe for a line-oriented sink but
    /// is <b>not</b> escaped for JSON, YAML or HTML. Use
    /// <see cref="YamlEscape"/> for frontmatter.
    /// </returns>
    /// <remarks>
    /// Apply once, at the sink. Sanitizing already-sanitized text escapes the
    /// backslashes of the previous pass and is not idempotent.
    /// </remarks>
    public static string Sanitize(string text)
    {
        var first = FirstIndexNeedingEscape(text);
        if (first < 0)
        {
            return text;
        }

        var sb = new StringBuilder(text.Length);
        sb.Append(text, 0, first);
        var index = first;
        while (index < text.Length)
        {
            index = AppendOne(sb, text, index);
        }

        return sb.ToString();
    }

    /// <summary>
    /// The first position this escaper would change, or <c>-1</c> when it would
    /// change nothing.
    /// </summary>
    /// <remarks>
    /// Virtually every string reaching this method — a class name, a method
    /// name, a parameter name, an ordinary rendered value — carries no control
    /// character and no lone surrogate at all, and for those the correct output
    /// is the input. Building a <see cref="StringBuilder"/> and a second string
    /// to reproduce it byte for byte is pure waste on the only path that is
    /// always taken: every Markdown/indented-text/prose seam sanitizes, several
    /// times per rendered node. Scanning first costs one pass with no
    /// allocation and lets the clean case return the original instance.
    /// </remarks>
    private static int FirstIndexNeedingEscape(string text)
    {
        var i = 0;
        while (i < text.Length)
        {
            var c = text[i];
            if (char.IsControl(c))
            {
                return i;
            }

            if (!char.IsSurrogate(c))
            {
                i++;
                continue;
            }

            // A well-formed pair is ordinary content (an emoji) and is copied
            // through, so the scan steps over BOTH halves; reaching a surrogate
            // that does not open one means the text carries a lone surrogate,
            // which AppendPlainOrEscaped does rewrite.
            if (!IsWellFormedPairAt(text, i))
            {
                return i;
            }

            i += 2;
        }

        return -1;
    }

    private static bool IsWellFormedPairAt(string text, int i)
    {
        return char.IsHighSurrogate(text[i])
            && i + 1 < text.Length
            && char.IsLowSurrogate(text[i + 1]);
    }

    /// <summary>
    /// Appends whatever starts at <paramref name="index"/> — one character, one mnemonic escape, or
    /// one well-formed surrogate pair.
    /// </summary>
    /// <returns>The index to read from next, which is two ahead for a pair.</returns>
    private static int AppendOne(StringBuilder sb, string text, int index)
    {
        switch (text[index])
        {
            case '\n': sb.Append("\\n"); return index + 1;
            case '\r': sb.Append("\\r"); return index + 1;
            case '\t': sb.Append("\\t"); return index + 1;
            case '\b': sb.Append("\\b"); return index + 1;
            case '\f': sb.Append("\\f"); return index + 1;
            default: return AppendPlainOrEscaped(sb, text, index);
        }
    }

    /// <summary>
    /// The characters with no mnemonic: a control character or a lone surrogate becomes
    /// <c>\uXXXX</c>, a well-formed pair is copied through, and everything else is itself.
    /// </summary>
    /// <returns>The index to read from next.</returns>
    private static int AppendPlainOrEscaped(StringBuilder sb, string text, int index)
    {
        var c = text[index];
        if (char.IsControl(c))
        {
            AppendUnicodeEscape(sb, c);
            return index + 1;
        }

        if (char.IsHighSurrogate(c) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1]))
        {
            sb.Append(c).Append(text[index + 1]);
            return index + 2;
        }

        if (char.IsSurrogate(c))
        {
            AppendUnicodeEscape(sb, c);
            return index + 1;
        }

        sb.Append(c);
        return index + 1;
    }

    private static void AppendUnicodeEscape(StringBuilder sb, char c)
    {
        sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
    }
}
