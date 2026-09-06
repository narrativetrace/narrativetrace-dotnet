// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Text;

namespace NarrativeTrace.Diagrams;

/// <summary>
/// Sanitizers for text interpolated into sequence-diagram grammars.
/// </summary>
/// <remarks>
/// Both the Mermaid and PlantUML renderers embed rendered trace values
/// (which <c>ValueRenderer</c> may preserve verbatim, control characters
/// included) into single-line message text. This helper is the one place
/// that neutralizes characters capable of breaking out of that line.
/// Diagram grammars are line-oriented: a raw CR/LF terminates the current
/// statement and starts a new one the engine parses as a directive —
/// Mermaid <c>click</c> interactions, PlantUML <c>!include</c>/<c>
/// !includeurl</c> preprocessor directives (SSRF / local-file read).
/// Folding every control character to a space is toolchain-independent
/// and keeps the whole rendered value on one physical line.
/// </remarks>
internal static class DiagramText
{
    /// <summary>
    /// Folds every control character in <paramref name="text"/> to a space so
    /// the value stays on a single physical line of message text.
    /// </summary>
    /// <param name="text">The rendered trace value to sanitize.</param>
    /// <returns>
    /// <paramref name="text"/> with each control character replaced by a
    /// space; all other characters are preserved verbatim.
    /// </returns>
    public static string Message(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            sb.Append(char.IsControl(c) ? ' ' : c);
        }

        return sb.ToString();
    }

    private const int MaxIdentifierLength = 100;

    /// <summary>
    /// Sanitizes a piece of trace <em>metadata</em> — a class name, method
    /// name, parameter name, or exception type name — for interpolation into
    /// either diagram grammar.
    /// </summary>
    /// <remarks>
    /// Unlike a rendered value, metadata is never quoted at every use site
    /// (an arrow line emits a method name and parameter names bare), so this
    /// goes further than <see cref="Message"/>: control characters are
    /// folded to a space (as in <see cref="Message"/>), a literal quote
    /// becomes an apostrophe (neither grammar can escape a quote
    /// <em>inside</em> a quoted name — the character must stop being a
    /// quote), a doubled <c>%</c> (Mermaid's comment opener) is broken up,
    /// and the result is length-capped. Empty or all-control input renders
    /// as the marker <c>&lt;unnamed&gt;</c> rather than an empty token, which
    /// both grammars treat as a parse error in a participant declaration.
    /// </remarks>
    public static string Identifier(string text)
    {
        var folded = Message(text).Replace('"', '\'').Replace("%%", "% %");
        var capped = folded.Length > MaxIdentifierLength
            ? folded[..MaxIdentifierLength]
            : folded;
        return capped.Trim().Length == 0 ? "<unnamed>" : capped;
    }

    /// <summary>
    /// Quotes a participant display name that contains a character
    /// (<c>.</c>, <c>-</c>, <c>:</c>, <c>&lt;</c>, <c>&gt;</c>, or space)
    /// capable of breaking the diagram participant grammar; leaves simple
    /// names untouched.
    /// </summary>
    /// <remarks>
    /// Callers pass text already sanitized by <see cref="Identifier"/> — this
    /// method only ever decides whether to add quotes, never what is safe to
    /// put inside them.
    /// </remarks>
    public static string QuoteIfNeeded(string name)
    {
        // S3267: LINQ over a string boxes the char enumerator and allocates a
        // closure per participant; diagram rendering is allocation-gated.
#pragma warning disable S3267
        foreach (var c in name)
        {
            if (c is '.' or '-' or ':' or ' ' or '<' or '>')
            {
                return "\"" + name + "\"";
            }
        }
#pragma warning restore S3267

        return name;
    }
}
