// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Globalization;

namespace NarrativeTrace.Core;

/// <summary>
/// Field-level difference between two structured renderings of the same
/// entity.
/// </summary>
/// <remarks>
/// When a captured value reappears inside one trace slightly changed — the
/// multi-currency case: a ledger returns an expense at <c>100.00 USD</c>, the
/// calculator receives it normalized to <c>92.00 EUR</c> — a second full render
/// hides the one field that moved inside a ~300-character blob. This composes
/// the compact form <c>{Amount: 100→92, Currency: "USD"→"EUR"}</c> that
/// <see cref="ValueReferenceIndex"/> appends to the reference label, so the
/// change prints AS a diff.
/// <para>
/// The delta is computed from the two <em>structured</em> trees and formats
/// only scalar leaves. It never reconstructs a flat render from a structured
/// value: the flat and structured channels are captured independently
/// (<see cref="ValueRenderer.Render"/> / <c>RenderStructured</c>) and deriving
/// one from the other is a divergence class the reference implementation has
/// already paid for twice. Any difference that is not a scalar-to-scalar field
/// change therefore yields <see langword="null"/>, and the caller falls back to
/// the full flat render.
/// </para>
/// <para>
/// Scalars are formatted the way <see cref="ValueRenderer"/> prints them —
/// invariant-culture numbers, lower-case booleans, round-trip timestamps — so
/// a diff and a full render never disagree about the same value.
/// <see cref="RenderedValue.DoubleVal"/> carries a <c>double</c>, so a
/// <c>decimal</c> captured as <c>100.00</c> formats here as <c>100</c>: the
/// scale is lost at capture, not at render, and the reference line still shows
/// the original text.
/// </para>
/// </remarks>
internal static class ValueDelta
{
    /// <summary>
    /// Longest string value shown on either side of a field change before it
    /// is elided.
    /// </summary>
    private const int MaxScalarLength = 60;

    /// <summary>
    /// Composes the delta of <paramref name="changed"/> against
    /// <paramref name="reference"/>, or <see langword="null"/> when the pair
    /// cannot be expressed as a scalar field diff — a different type, a
    /// different field set, no difference at all, or a changed field that is
    /// itself structured.
    /// </summary>
    /// <param name="reference">
    /// The structured form already emitted in full in this document.
    /// </param>
    /// <param name="changed">
    /// The structured form of the later, differing emission.
    /// </param>
    public static string? Between(
        RenderedValue? reference, RenderedValue? changed)
    {
        if (reference is not RenderedValue.ObjectVal from
            || changed is not RenderedValue.ObjectVal to
            || !Comparable(from, to))
        {
            return null;
        }

        var parts = ChangedFields(from, to);
        return parts is null || parts.Count == 0
            ? null
            : "{" + string.Join(", ", parts) + "}";
    }

    /// <summary>
    /// Whether the two values describe the same shape, and so can be diffed at
    /// all: same type name and the same set of field names.
    /// </summary>
    private static bool Comparable(
        RenderedValue.ObjectVal from, RenderedValue.ObjectVal to)
    {
        return string.Equals(
                from.TypeName, to.TypeName, StringComparison.Ordinal)
            && SameFieldNames(from, to);
    }

    /// <summary>
    /// The <c>Field: before→after</c> parts for every field that moved, or
    /// <see langword="null"/> as soon as one of them is structured and so has
    /// no one-line form.
    /// </summary>
    private static List<string>? ChangedFields(
        RenderedValue.ObjectVal from, RenderedValue.ObjectVal to)
    {
        var parts = new List<string>();
        foreach (var field in from.Fields)
        {
            var after = to.Fields[field.Key];
            if (SameValue(field.Value, after))
            {
                continue;
            }

            if (ScalarChange(field.Value, after) is not { } change)
            {
                return null;
            }

            parts.Add(field.Key + ": " + change);
        }

        return parts;
    }

    private static bool SameFieldNames(
        RenderedValue.ObjectVal a, RenderedValue.ObjectVal b)
    {
        return a.Fields.Count == b.Fields.Count
            && a.Fields.Keys.All(b.Fields.ContainsKey);
    }

    /// <summary>
    /// Structural equality for two structured values.
    /// </summary>
    /// <remarks>
    /// The record equality generated for
    /// <see cref="RenderedValue.ObjectVal"/> and
    /// <see cref="RenderedValue.ListVal"/> compares their dictionary and list
    /// members with the default equality comparer, which is reference equality
    /// — so two captures holding identical content read as different, every
    /// field of a re-captured object looks changed, and no delta would ever be
    /// expressible. This walks the values instead. (The reference
    /// implementation gets this for free: Java's <c>Map</c> and <c>List</c>
    /// equality is structural.)
    /// </remarks>
    private static bool SameValue(RenderedValue a, RenderedValue b)
    {
        if (a is RenderedValue.ObjectVal objectA
            && b is RenderedValue.ObjectVal objectB)
        {
            return SameObject(objectA, objectB);
        }

        if (a is RenderedValue.ListVal listA
            && b is RenderedValue.ListVal listB)
        {
            return SameList(listA, listB);
        }

        return a.Equals(b);
    }

    private static bool SameObject(
        RenderedValue.ObjectVal a, RenderedValue.ObjectVal b)
    {
        return string.Equals(a.TypeName, b.TypeName, StringComparison.Ordinal)
            && SameFieldNames(a, b)
            && a.Fields.All(f => SameValue(f.Value, b.Fields[f.Key]));
    }

    /// <summary>
    /// Element-wise walk rather than <c>Zip</c>: Core also targets
    /// netstandard2.0, which has no tuple-returning <c>Zip</c> overload.
    /// </summary>
    private static bool SameList(
        RenderedValue.ListVal a, RenderedValue.ListVal b)
    {
        if (a.Elements.Count != b.Elements.Count)
        {
            return false;
        }

        for (var i = 0; i < a.Elements.Count; i++)
        {
            if (!SameValue(a.Elements[i], b.Elements[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static string? ScalarChange(RenderedValue before, RenderedValue after)
    {
        var from = Scalar(before);
        var to = Scalar(after);
        return from is null || to is null ? null : from + "→" + to;
    }

    /// <summary>
    /// Formats a scalar leaf the way the flat renderer prints it — a string
    /// quoted and control-escaped — or <see langword="null"/> for a structured
    /// leaf, which has no unambiguous one-line form.
    /// </summary>
    private static string? Scalar(RenderedValue value)
    {
        return value is RenderedValue.StringVal s
            ? "\"" + Cap(ControlEscape.Sanitize(s.Value)) + "\""
            : BareScalar(value);
    }

    /// <summary>
    /// The unquoted scalars: numbers, booleans, <c>null</c>, and timestamps in
    /// round-trip form. <see langword="null"/> for
    /// <see cref="RenderedValue.ObjectVal"/> and
    /// <see cref="RenderedValue.ListVal"/> — a nested object or list is what
    /// makes a change inexpressible as a one-line diff.
    /// </summary>
    private static string? BareScalar(RenderedValue value)
    {
        return value switch
        {
            RenderedValue.LongVal l =>
                l.Value.ToString(CultureInfo.InvariantCulture),
            RenderedValue.DoubleVal d =>
                d.Value.ToString(CultureInfo.InvariantCulture),
            RenderedValue.BooleanVal b => b.Value ? "true" : "false",
            RenderedValue.InstantVal i =>
                DateTimeOffset.FromUnixTimeMilliseconds(i.EpochMillis)
                    .UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
            RenderedValue.NullVal => "null",
            _ => null,
        };
    }

    private static string Cap(string text)
    {
        return text.Length > MaxScalarLength
            ? text[..MaxScalarLength] + "…"
            : text;
    }
}
