// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Core;

/// <summary>
/// Strongly-typed 32-character lowercase hex trace identifier (W3C format).
/// </summary>
/// <remarks>
/// Exists so a trace id cannot be confused with a <see cref="SpanId"/> or with
/// an arbitrary string at a call site: the constructor validates, so any
/// non-<see cref="Empty"/> instance is guaranteed well-formed and downstream
/// code never re-checks. <see cref="Empty"/> is the one value that bypasses
/// validation — it represents "no trace in flight", not a zero-valued id, and
/// it is the only way to obtain a <see cref="TraceId"/> whose
/// <see cref="Value"/> is not 32 hex characters. It is also
/// <c>default(TraceId)</c>, so an uninitialized field or an array slot reads as
/// "no trace" rather than as a malformed id.
/// </remarks>
/// <example>
/// <code>
/// var id = new TraceId("4bf92f3577b34da6a3ce929d0e0e4736");
/// Console.WriteLine(id.HumanName); // "cool duo heats"
/// </code>
/// </example>
public readonly record struct TraceId
{
    /// <summary>
    /// The raw identifier: 32 lowercase hex characters, or the empty string for
    /// <see cref="Empty"/> (which includes a defaulted instance). Never
    /// <see langword="null"/>.
    /// </summary>
    public string Value => _value ?? "";

    private readonly string? _value;

    /// <summary>Wraps a W3C trace id after validating its shape.</summary>
    /// <param name="value">
    /// Exactly 32 lowercase hex characters. Uppercase hex is rejected rather
    /// than normalized, because the id is compared byte-for-byte against ids
    /// emitted by the Java edition and by W3C <c>traceparent</c> headers.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="value"/> is null, is not 32 characters, or contains a
    /// character outside <c>0-9a-f</c>. Use <see cref="Empty"/> for the
    /// no-trace case instead of passing an empty string.
    /// </exception>
    public TraceId(string value)
    {
        if (!SpanIdGenerator.IsValidTraceId(value))
            throw new ArgumentException(
                $"TraceId must be 32 lowercase hex chars, got: '{value}'", nameof(value));
        _value = value;
    }

    /// <summary>
    /// The sentinel meaning "no trace in flight". The only <see cref="TraceId"/>
    /// that does not satisfy the 32-hex-character invariant.
    /// </summary>
    /// <remarks>
    /// Equal to <c>default(TraceId)</c>, deliberately: a defaulted struct, an
    /// array slot, or a list grown by capacity all compare equal to this and
    /// report <see cref="IsEmpty"/>, so scanning a collection for empties needs
    /// no special case for uninitialized entries.
    /// </remarks>
    public static readonly TraceId Empty = default;

    /// <summary>Whether this is <see cref="Empty"/> — that is, no trace is in flight.</summary>
    /// <remarks>Safe on any instance, <c>default(TraceId)</c> included; never throws.</remarks>
    public bool IsEmpty => Value.Length == 0;

    /// <summary>
    /// A deterministic, human-readable "adjective noun verb" name derived
    /// from this trace ID (empty when the trace ID is empty). Cross-language
    /// stable via <see cref="TraceNamer"/>.
    /// </summary>
    /// <remarks>
    /// Stable across processes, machines and language editions: the same trace
    /// id always yields the same name, so the name can be quoted in a bug report
    /// and matched later. Not unique — distinct ids can collide on a name, so
    /// this is a label for humans, never a lookup key.
    /// </remarks>
    public string HumanName => IsEmpty ? "" : TraceNamer.Name(Value);

    /// <summary>Returns <see cref="Value"/> — the raw hex id, or "" when empty.</summary>
    public override string ToString() => Value;
}
