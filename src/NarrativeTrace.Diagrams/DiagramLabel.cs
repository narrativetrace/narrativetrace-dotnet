// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Text;

namespace NarrativeTrace.Diagrams;

/// <summary>
/// One piece of diagram text guaranteed to have passed through <see cref="DiagramText"/>, the one
/// sanitizer both sequence grammars share.
/// </summary>
/// <remarks>
/// <para>
/// "Call me before a string enters a diagram line" used to be a convention every call site had to
/// remember on its own — <see cref="DiagramText"/>'s functions are plain <see cref="string"/>
/// transforms that nothing stopped a caller from skipping. Wrapping the sanitized text in a type
/// makes the convention structural: an <see cref="ISequenceGrammar"/> hook that declares a
/// <see cref="DiagramLabel"/> parameter cannot be called with a raw trace string, sanitized or not,
/// because the constructor is private — nothing outside this file can build one.
/// </para>
/// <para>
/// <see cref="Identifier"/>, <see cref="QuotedIdentifier"/>, <see cref="Message"/> and
/// <see cref="Alias"/> are the only routes from untrusted trace metadata into a label — each
/// delegates to <see cref="DiagramText"/>'s existing sanitizer, unchanged. <see cref="CheckMark"/>
/// is the one exception: a fixed program literal, never trace-derived, so it needs no sanitizing —
/// it is still built through this type's own private constructor, not a public escape hatch.
/// <see cref="WithParameters"/> and <see cref="AliasedAs"/> compose labels that are already
/// sanitized, joining their text with literal punctuation that never came from the trace
/// (<c>(</c>, <c>, </c>, <c>: </c>, <c>as</c>) — so composition can never reopen the hole the
/// sanitizer closed.
/// </para>
/// </remarks>
internal readonly record struct DiagramLabel
{
    private readonly string? _text;

    private DiagramLabel(string text) => _text = text;

    /// <summary>The sanitized text, safe to append directly to a diagram line. Never <see langword="null"/>.</summary>
    public string Text => _text ?? "";

    /// <summary>
    /// Sanitizes one piece of trace metadata (class name, method name, parameter name, exception
    /// type) for interpolation into either diagram grammar. See <see cref="DiagramText.Identifier"/>.
    /// </summary>
    /// <param name="raw">The metadata field as captured, which may be anything.</param>
    public static DiagramLabel Identifier(string raw) => new(DiagramText.Identifier(raw));

    /// <summary>
    /// A sanitized identifier, quoted when it contains a character that would otherwise end an
    /// unquoted token (<c>. - : &lt; &gt;</c> or a space) — the label a participant declaration
    /// uses. See <see cref="DiagramText.QuoteIfNeeded"/>.
    /// </summary>
    /// <param name="raw">The metadata field as captured, which may be anything.</param>
    public static DiagramLabel QuotedIdentifier(string raw) =>
        new(DiagramText.QuoteIfNeeded(DiagramText.Identifier(raw)));

    /// <summary>The message text for a return arrow — a rendered value, folded and safe to embed. See <see cref="DiagramText.Message"/>.</summary>
    /// <param name="text">The rendered trace value to sanitize.</param>
    public static DiagramLabel Message(string text) => new(DiagramText.Message(text));

    /// <summary>
    /// A Mermaid/PlantUML participant alias, already guaranteed safe to emit unquoted by
    /// <see cref="AliasGenerator"/>'s own contract. This factory wraps it in the type without
    /// sanitizing further — it exists so every grammar hook can stay uniformly typed, not because
    /// the alias needs another pass through <see cref="DiagramText"/>.
    /// </summary>
    /// <param name="safeAlias">An alias produced by <see cref="AliasGenerator.Generate"/>.</param>
    public static DiagramLabel Alias(string safeAlias) => new(safeAlias);

    /// <summary>The completion mark for a return with no rendered value (e.g. void) — a fixed program literal, not trace-derived.</summary>
    public static readonly DiagramLabel CheckMark = new("✔");

    /// <summary>
    /// This label (a sanitized method name) followed by its parameter list, comma-joined and
    /// parenthesized: <c>method(nameA: valueA, nameB: valueB)</c>. The parentheses, colon and
    /// separator are literal, not trace-derived, so this cannot reintroduce anything the sanitizer
    /// folded.
    /// </summary>
    /// <param name="parameters">Each parameter's already-sanitized name and rendered value.</param>
    public DiagramLabel WithParameters(IReadOnlyList<(DiagramLabel Name, DiagramLabel Value)> parameters)
    {
        var sb = new StringBuilder(Text);
        sb.Append('(');
        AppendParameters(sb, parameters);
        sb.Append(')');
        return new DiagramLabel(sb.ToString());
    }

    private static void AppendParameters(
        StringBuilder sb, IReadOnlyList<(DiagramLabel Name, DiagramLabel Value)> parameters)
    {
        for (var i = 0; i < parameters.Count; i++)
        {
            if (i > 0)
            {
                sb.Append(", ");
            }

            sb.Append(parameters[i].Name.Text);
            sb.Append(": ");
            sb.Append(parameters[i].Value.Text);
        }
    }

    /// <summary>
    /// This label (a participant alias) followed by the display name it stands for:
    /// <c>X as Name</c> — the participant declaration line both grammars emit.
    /// </summary>
    /// <param name="displayName">The already-sanitized display name.</param>
    public DiagramLabel AliasedAs(DiagramLabel displayName) => new(Text + " as " + displayName.Text);

    /// <summary>Returns <see cref="Text"/>.</summary>
    public override string ToString() => Text;
}
