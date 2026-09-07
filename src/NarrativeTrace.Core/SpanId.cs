// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Core;

/// <summary>
/// Strongly-typed 16-character lowercase hex span identifier (W3C format).
/// </summary>
/// <remarks>
/// The span-scoped counterpart to <see cref="TraceId"/>: a trace id is 32 hex
/// characters and identifies the whole trace, a span id is 16 and identifies one
/// operation within it. Keeping them as distinct types is what stops the two
/// being passed in the wrong argument position, since both are otherwise just
/// hex strings. Any non-<see cref="Empty"/> instance is guaranteed well-formed,
/// and <see cref="Empty"/> is <c>default(SpanId)</c>, so an uninitialized field
/// reads as "no span" rather than as a malformed id.
/// </remarks>
public readonly record struct SpanId
{
    /// <summary>
    /// The raw identifier: 16 lowercase hex characters, or the empty string for
    /// <see cref="Empty"/> (which includes a defaulted instance). Never
    /// <see langword="null"/>.
    /// </summary>
    public string Value => _value ?? "";

    private readonly string? _value;

    /// <summary>Wraps a W3C span id after validating its shape.</summary>
    /// <param name="value">
    /// Exactly 16 lowercase hex characters. Uppercase hex is rejected rather
    /// than normalized, to keep ids byte-identical with the Java runtime and
    /// with W3C <c>traceparent</c> headers.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="value"/> is null, is not 16 characters, or contains a
    /// character outside <c>0-9a-f</c>. Passing a 32-character trace id here is
    /// the common mistake and is rejected on length.
    /// </exception>
    public SpanId(string value)
    {
        if (!SpanIdGenerator.IsValidSpanId(value))
            throw new ArgumentException(
                $"SpanId must be 16 lowercase hex chars, got: '{value}'", nameof(value));
        _value = value;
    }

    /// <summary>
    /// The sentinel meaning "no span in flight". The only <see cref="SpanId"/>
    /// that does not satisfy the 16-hex-character invariant, and the value of
    /// <c>default(SpanId)</c>.
    /// </summary>
    /// <remarks>
    /// Equal to <c>default(SpanId)</c>, deliberately — an array slot or a list
    /// grown by capacity compares equal to this and reports
    /// <see cref="IsEmpty"/>, needing no special case.
    /// </remarks>
    public static readonly SpanId Empty = default;

    /// <summary>Whether this is <see cref="Empty"/> — that is, no span is in flight.</summary>
    /// <remarks>Safe on any instance, <c>default(SpanId)</c> included; never throws.</remarks>
    public bool IsEmpty => Value.Length == 0;

    /// <summary>Returns <see cref="Value"/> — the raw hex id, or "" when empty.</summary>
    public override string ToString() => Value;
}
