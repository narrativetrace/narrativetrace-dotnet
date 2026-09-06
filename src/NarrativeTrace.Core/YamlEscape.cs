// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Globalization;
using System.Text;

namespace NarrativeTrace.Core;

/// <summary>
/// Neutralizes user-controlled strings before they land in YAML
/// frontmatter. A value containing YAML-significant characters — mid-string
/// (<c>: # " \</c>, a control character), or leading (a YAML indicator
/// character such as <c>[</c>, <c>&amp;</c>, <c>*</c>, a backtick) — is
/// emitted as a quoted scalar with escaping, so a scenario name like
/// <c>deploy: prod</c> or one containing a newline cannot inject keys or
/// terminate the frontmatter block (JVM-edition
/// <c>FrontmatterBuilder.yamlSafe</c> parity).
/// </summary>
public static class YamlEscape
{
    /// <summary>Escapes a string for safe use as a YAML scalar value.</summary>
    /// <param name="value">The raw, possibly user-controlled text.</param>
    /// <returns>
    /// The value unchanged when it needs no quoting, otherwise a double-quoted
    /// scalar with every hazardous character escaped. Because safe input
    /// is returned verbatim, the result is <b>not</b> always quoted — do not
    /// wrap it in quotes yourself.
    /// </returns>
    /// <remarks>
    /// Escapes for the <i>scalar value</i> position only. It is not a general
    /// YAML serializer and must not be used to build keys or structure.
    /// </remarks>
    public static string Scalar(string value)
    {
        return NeedsQuoting(value) ? Quote(value) : value;
    }

    // A value starting with a YAML indicator character is a parse error or a
    // structural surprise (an anchor, an alias, a flow sequence/mapping
    // opener) even when it holds none of the mid-string trigger characters
    // below — e.g. a class name naming an F# identifier declared with
    // backticks, or one crafted to start with `[`/`&`/`*`. Checking only for
    // ": # \" \\ \n \r" anywhere in the value (as this method used to) is a
    // deny-list of the mid-string hazards and misses the leading-character
    // ones entirely.
    private static bool StartsWithIndicator(char c) =>
        c is '-' or '?' or ':' or ',' or '[' or ']' or '{' or '}'
            or '#' or '&' or '*' or '!' or '|' or '>' or '\'' or '"'
            or '%' or '@' or '`';

    // YAML's "allowed here without escaping" set is narrower than "not a
    // control character": the C1 range (U+0080-U+009F) is a control range
    // ISOControl also covers but this method previously did not check for at
    // all, found by OutputFormatPropertyTests' corpus-driven fuzz route
    // (case "c1-controls") after the metadata fuzz target was added — a
    // raw C1 character broke the YAML scanner even though it triggered
    // neither the old mid-string set nor a leading-indicator check. A lone
    // surrogate is not valid UTF-8/16 text at all — unlike a control
    // character, it has no valid \uXXXX escape either (see AppendOne's
    // remark), so it still needs quoting to trigger the replacement.
    //
    // A leading or trailing space forces quoting too — found by the same
    // metadata fuzz route with a class name of " `'" (space, backtick,
    // apostrophe): a real YAML parser strips leading/trailing blanks from
    // an unquoted plain scalar (so the round trip would silently lose them
    // even without an indicator character in play) and, worse, treats them
    // as insignificant separator whitespace when deciding where the scalar
    // itself starts — a leading space does not shield an indicator
    // character right behind it, so checking only value[0] missed this
    // case entirely: value[0] was the harmless space, not the reserved
    // backtick one character later that a real scanner treats as the
    // scalar's actual first token.
    private static bool NeedsQuoting(string value)
    {
        if (value.Length > 0
            && (value[0] == ' ' || value[^1] == ' ' || StartsWithIndicator(value[0])))
        {
            return true;
        }

        // S3267: LINQ over a string boxes the char enumerator and allocates a
        // closure on every rendered value; renderers are gated at 0 bytes.
#pragma warning disable S3267
        foreach (var c in value)
        {
            if (c is ':' or '#' or '"' or '\\'
                || char.IsControl(c) || char.IsSurrogate(c))
            {
                return true;
            }
        }
#pragma warning restore S3267

        return false;
    }

    private static string Quote(string value)
    {
        var sb = new StringBuilder(value.Length + 2);
        sb.Append('"');
        var i = 0;
        while (i < value.Length)
        {
            i = AppendOne(sb, value, i);
        }

        return sb.Append('"').ToString();
    }

    // Mnemonic escapes first, then YAML's own \xXX (byte) / \uXXXX (BMP)
    // hex forms for every other control character. Surrogates (a
    // well-formed pair or a lone one) are handled separately below —
    // frontmatter is read by tooling, so fidelity for a real character, an
    // emoji included, is part of being correct.
    private static int AppendOne(StringBuilder sb, string value, int i)
    {
        var c = value[i];
        switch (c)
        {
            case '\\': sb.Append("\\\\"); return i + 1;
            case '"': sb.Append("\\\""); return i + 1;
            case '\n': sb.Append("\\n"); return i + 1;
            case '\r': sb.Append("\\r"); return i + 1;
            case '\t': sb.Append("\\t"); return i + 1;
        }

        if (char.IsControl(c))
        {
            AppendHexEscape(sb, c);
            return i + 1;
        }

        return AppendSurrogateOrPlain(sb, value, i);
    }

    private static int AppendSurrogateOrPlain(StringBuilder sb, string value, int i)
    {
        var c = value[i];
        if (char.IsHighSurrogate(c) && i + 1 < value.Length
            && char.IsLowSurrogate(value[i + 1]))
        {
            sb.Append(c).Append(value[i + 1]);
            return i + 2;
        }

        if (char.IsSurrogate(c))
        {
            // No YAML escape can stand for a lone surrogate: \uD800 is valid
            // ESCAPE SYNTAX but decodes to the same invalid code unit, and a
            // real YAML parser rejects it right back — confirmed against
            // YamlDotNet by OutputFormatPropertyTests' corpus-driven fuzz
            // route (cases unpaired-high/low-surrogate, reversed-surrogate-
            // pair, surrogate-split-by-text). U+FFFD is the standard
            // Unicode replacement for exactly this situation and is a
            // perfectly ordinary character to every parser.
            sb.Append('\uFFFD');
            return i + 1;
        }

        sb.Append(c);
        return i + 1;
    }

    private static void AppendHexEscape(StringBuilder sb, char c)
    {
        if (c <= 0xFF)
        {
            sb.Append("\\x")
                .Append(((int)c).ToString("X2", CultureInfo.InvariantCulture));
        }
        else
        {
            sb.Append("\\u")
                .Append(((int)c).ToString("X4", CultureInfo.InvariantCulture));
        }
    }
}
