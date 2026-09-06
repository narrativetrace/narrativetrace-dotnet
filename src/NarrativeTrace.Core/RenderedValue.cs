// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Core;

/// <summary>
/// Structured value representation preserving original .NET types.
/// </summary>
/// <remarks>
/// Alongside the flat string rendering (<see cref="ValueRenderer.Render"/>),
/// this sealed hierarchy preserves type information for downstream consumers
/// that benefit from it — primarily OTel exporters that can emit typed span
/// attributes (long, double, boolean) and structured object fields as
/// dot-separated keys. Existing renderers (Markdown, prose, logs) continue
/// using the flat string form; the structured form is an optional companion
/// captured at the same boundary.
/// </remarks>
public abstract record RenderedValue
{
    private RenderedValue() { }

    /// <summary>A string value (strings, enums, chars, ToString fallbacks).</summary>
    public sealed record StringVal(string Value) : RenderedValue;

    /// <summary>An integral numeric value (int, long, short, byte).</summary>
    public sealed record LongVal(long Value) : RenderedValue;

    /// <summary>A floating-point value (float, double, decimal).</summary>
    public sealed record DoubleVal(double Value) : RenderedValue;

    /// <summary>A boolean value.</summary>
    public sealed record BooleanVal(bool Value) : RenderedValue;

    /// <summary>A timestamp as epoch milliseconds.</summary>
    public sealed record InstantVal(long EpochMillis) : RenderedValue;

    /// <summary>A structured object with named, typed fields.</summary>
    public sealed record ObjectVal(
        string TypeName,
        IReadOnlyDictionary<string, RenderedValue> Fields) : RenderedValue;

    /// <summary>An ordered collection of values.</summary>
    public sealed record ListVal(
        IReadOnlyList<RenderedValue> Elements) : RenderedValue;

    /// <summary>A null value.</summary>
    public sealed record NullVal : RenderedValue;
}
