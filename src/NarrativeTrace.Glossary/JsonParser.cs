// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Globalization;
using System.Text;

namespace NarrativeTrace.Glossary;

/// <summary>
/// Minimal strict JSON parser for reading <c>glossary.json</c>.
/// </summary>
/// <remarks>
/// The glossary module must add no external runtime dependency (ADR-012), so
/// the reader hand-rolls parsing. Strictness is deliberate — the glossary is
/// a hand-curated committed file, and a typo should fail fast with a
/// position, never be silently ignored. Scope: RFC 8259 objects, arrays,
/// strings (all escapes), integer numbers, <c>true</c>, <c>false</c>,
/// <c>null</c>. Fractional/exponent numbers are rejected — the glossary
/// schema has none. Duplicate object keys and trailing content are errors.
/// JSON <c>null</c> parses to <see cref="Null"/> (never a CLR null) so
/// callers can distinguish "key absent" from "key explicitly null".
/// </remarks>
internal sealed class JsonParser
{
    /// <summary>Sentinel returned for a JSON <c>null</c> literal.</summary>
    internal static readonly object Null = new();

    /// <summary>
    /// Deepest <c>{</c>/<c>[</c> nesting a document may open. Checked once, before any recursive
    /// descent begins: this parser recurses on the CLR call stack for every open container, and a
    /// <see cref="StackOverflowException"/> cannot be caught, so nesting is bounded so a hostile
    /// file cannot exhaust the stack. Ruled once for the family; every runtime that reads a
    /// glossary.json enforces this same number, not a value picked locally for this parser.
    /// </summary>
    internal const int MaxNestingDepth = 16;

    private readonly string text;
    private int pos;

    private JsonParser(string text)
    {
        this.text = text;
    }

    /// <summary>Parses one complete JSON document.</summary>
    /// <returns>
    /// An insertion-ordered <see cref="Dictionary{TKey,TValue}"/>, a
    /// <see cref="List{T}"/>, a <see cref="string"/>, a <see cref="long"/>, a
    /// <see cref="bool"/>, or <see cref="Null"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// Any syntax error, with character position, including nesting past
    /// <see cref="MaxNestingDepth"/>.
    /// </exception>
    internal static object Parse(string text)
    {
        if (text is null)
        {
            throw new ArgumentNullException(nameof(text));
        }

        CheckNestingDepth(text);

        var parser = new JsonParser(text);
        parser.SkipWhitespace();
        var value = parser.ParseValue();
        parser.SkipWhitespace();
        if (parser.pos < text.Length)
        {
            throw parser.Error("trailing content after JSON document");
        }

        return value;
    }

    /// <summary>
    /// Rejects a document whose <c>{</c>/<c>[</c> nesting exceeds <see cref="MaxNestingDepth"/>,
    /// before <see cref="Parse"/> ever recurses into it.
    /// </summary>
    /// <remarks>
    /// A single, non-recursive pass over the raw characters, counting container depth outside
    /// string literals (backslash-escape aware, so a quote inside a string never looks like a
    /// closer, and a bracket character inside a string never counts). This scan has no recursion
    /// of its own, so it cannot itself be the thing that exhausts the stack — it exists precisely
    /// to refuse a hostile depth before the recursive-descent parser below gets a chance to
    /// recurse into it.
    /// </remarks>
    private static void CheckNestingDepth(string text)
    {
        var scanner = new NestingScanner();
        for (var i = 0; i < text.Length; i++)
        {
            scanner.Advance(text[i], i);
        }
    }

    /// <summary>Char-by-char state for <see cref="CheckNestingDepth"/>: container depth and whether the scan is inside a string literal.</summary>
    private struct NestingScanner
    {
        private int depth;
        private bool inString;
        private bool escaped;

        internal void Advance(char c, int pos)
        {
            if (inString)
            {
                AdvanceInString(c);
            }
            else
            {
                AdvanceOutsideString(c, pos);
            }
        }

        private void AdvanceInString(char c)
        {
            if (escaped)
            {
                escaped = false;
            }
            else if (c == '\\')
            {
                escaped = true;
            }
            else if (c == '"')
            {
                inString = false;
            }
        }

        private void AdvanceOutsideString(char c, int pos)
        {
            if (c == '"')
            {
                inString = true;
            }
            else if (c is '{' or '[')
            {
                OpenContainer(pos);
            }
            else if (c is '}' or ']')
            {
                depth--;
            }
        }

        private void OpenContainer(int pos)
        {
            depth++;
            if (depth > MaxNestingDepth)
            {
                throw new ArgumentException(
                    $"invalid JSON at position {pos}: nesting depth {depth} exceeds the "
                    + $"maximum of {MaxNestingDepth}");
            }
        }
    }

    private object ParseValue()
    {
        return Peek() switch
        {
            '{' => ParseObject(),
            '[' => ParseArray(),
            '"' => ParseString(),
            't' => ParseLiteral("true", true),
            'f' => ParseLiteral("false", false),
            'n' => ParseLiteral("null", Null),
            _ => ParseNumber(),
        };
    }

    private Dictionary<string, object> ParseObject()
    {
        Expect('{');
        var obj = new Dictionary<string, object>(StringComparer.Ordinal);
        SkipWhitespace();
        if (Peek() == '}')
        {
            pos++;
            return obj;
        }

        while (true)
        {
            ParseMember(obj);
            if (ConsumeSeparatorUntil('}'))
            {
                return obj;
            }
        }
    }

    private void ParseMember(Dictionary<string, object> obj)
    {
        SkipWhitespace();
        var key = ParseString();
        SkipWhitespace();
        Expect(':');
        SkipWhitespace();
        if (obj.ContainsKey(key))
        {
            throw Error($"duplicate key '{key}'");
        }

        obj[key] = ParseValue();
        SkipWhitespace();
    }

    private List<object> ParseArray()
    {
        Expect('[');
        var array = new List<object>();
        SkipWhitespace();
        if (Peek() == ']')
        {
            pos++;
            return array;
        }

        while (true)
        {
            SkipWhitespace();
            array.Add(ParseValue());
            SkipWhitespace();
            if (ConsumeSeparatorUntil(']'))
            {
                return array;
            }
        }
    }

    /// <summary>Consumes either a <c>,</c> (returning false) or the given closer (returning true).</summary>
    private bool ConsumeSeparatorUntil(char closer)
    {
        var c = Peek();
        if (c == closer)
        {
            pos++;
            return true;
        }

        if (c != ',')
        {
            throw Error($"expected ',' or '{closer}'");
        }

        pos++;
        return false;
    }

    private string ParseString()
    {
        Expect('"');
        var sb = new StringBuilder();
        while (true)
        {
            if (pos >= text.Length)
            {
                throw Error("unterminated string");
            }

            var c = text[pos++];
            if (c == '"')
            {
                return sb.ToString();
            }

            AppendStringChar(sb, c);
        }
    }

    private void AppendStringChar(StringBuilder sb, char c)
    {
        if (c == '\\')
        {
            sb.Append(ParseEscape());
        }
        else if (c < 0x20)
        {
            throw Error("raw control character in string");
        }
        else
        {
            sb.Append(c);
        }
    }

    private char ParseEscape()
    {
        if (pos >= text.Length)
        {
            throw Error("unterminated escape");
        }

        var c = text[pos++];
        return c switch
        {
            '"' => '"',
            '\\' => '\\',
            '/' => '/',
            'b' => '\b',
            'f' => '\f',
            'n' => '\n',
            'r' => '\r',
            't' => '\t',
            'u' => ParseUnicodeEscape(),
            _ => throw Error($"invalid escape '\\{c}'"),
        };
    }

    private char ParseUnicodeEscape()
    {
        if (pos + 4 > text.Length)
        {
            throw Error("unterminated unicode escape");
        }

        var hex = text.Substring(pos, 4);
        if (!ushort.TryParse(
                hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
        {
            throw Error($"invalid unicode escape '\\u{hex}'");
        }

        pos += 4;
        return (char)value;
    }

    private long ParseNumber()
    {
        var start = pos;
        if (Peek() == '-')
        {
            pos++;
        }

        while (pos < text.Length && text[pos] >= '0' && text[pos] <= '9')
        {
            pos++;
        }

        if (pos < text.Length && (text[pos] == '.' || text[pos] == 'e' || text[pos] == 'E'))
        {
            throw Error("fractional numbers are not supported by the glossary schema");
        }

        return ParseLong(text.Substring(start, pos - start));
    }

    private long ParseLong(string digits)
    {
        if (!long.TryParse(
                digits, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture,
                out var value))
        {
            throw Error("invalid number");
        }

        return value;
    }

    private object ParseLiteral(string literal, object value)
    {
        if (string.CompareOrdinal(text, pos, literal, 0, literal.Length) != 0
            || pos + literal.Length > text.Length)
        {
            throw Error("invalid literal");
        }

        pos += literal.Length;
        return value;
    }

    private char Peek()
    {
        if (pos >= text.Length)
        {
            throw Error("unexpected end of input");
        }

        return text[pos];
    }

    private void Expect(char c)
    {
        if (Peek() != c)
        {
            throw Error($"expected '{c}'");
        }

        pos++;
    }

    private void SkipWhitespace()
    {
        while (pos < text.Length && char.IsWhiteSpace(text[pos]))
        {
            pos++;
        }
    }

    private ArgumentException Error(string message)
    {
        return new ArgumentException($"invalid JSON at position {pos}: {message}");
    }
}
