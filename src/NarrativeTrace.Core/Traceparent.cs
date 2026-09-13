// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Globalization;

namespace NarrativeTrace.Core;

/// <summary>
/// W3C Trace Context <c>traceparent</c> header value — the wire format a trace
/// crosses a process boundary in.
/// </summary>
/// <remarks>
/// <para>
/// Inbound, <see cref="Parse"/> reads the header and the trace continues
/// instead of starting over; outbound, a client formats one (<see cref="Format"/>)
/// so the next service can do the same. See
/// <see cref="INarrativeContext.AdoptTraceparent"/> for the adopt side of the
/// boundary, and <c>NarrativeTraceMiddleware</c> for where an inbound header is
/// actually read.
/// </para>
/// <para>
/// <see cref="Parse"/> returns <see langword="null"/> for anything malformed
/// rather than throwing — the caller is on a request path, and a bad header
/// from a stranger must degrade to "start a fresh trace", never to a failed
/// request. The constructor is the opposite regime (fail fast and loud), so
/// construct one directly only from ids you already trust.
/// </para>
/// <para>
/// Parsing is strict, matching the W3C ABNF: lowercase hex only, no
/// surrounding whitespace, no all-zero trace id or parent id, and the
/// forbidden <c>ff</c> version is rejected. A version above <c>00</c> may
/// carry trailing fields, which are ignored; version <c>00</c> may not.
/// </para>
/// <para>
/// Spec: https://www.w3.org/TR/trace-context/#traceparent-header
/// </para>
/// </remarks>
public sealed record Traceparent
{
    /// <summary>Canonical lowercase header name, matched case-insensitively on receipt.</summary>
    public const string HeaderName = "traceparent";

    private const string Version = "00";
    private const string ForbiddenVersion = "ff";
    private const char Delimiter = '-';
    private const int VersionLength = 2;
    private const int FlagsLength = 2;
    private const int FieldCount = 4;
    private const int TraceIdLength = 32;
    private const int SpanIdLength = 16;

    /// <summary>Trace the caller is part of, adopted verbatim by the receiver.</summary>
    public TraceId TraceId { get; }

    /// <summary>The caller's own span id — the receiver's parent span.</summary>
    public SpanId ParentSpanId { get; }

    /// <summary>Raw W3C trace-flags byte (0-255); bit 0 is the sampled flag.</summary>
    public int TraceFlags { get; }

    /// <summary>Wraps already-trusted ids as a traceparent. Rejects empty ids and out-of-range flags.</summary>
    /// <param name="traceId">The trace id; must not be <see cref="Core.TraceId.Empty"/>.</param>
    /// <param name="parentSpanId">The parent span id; must not be <see cref="Core.SpanId.Empty"/>.</param>
    /// <param name="traceFlags">The raw flags byte, 0-255.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="traceId"/> or <paramref name="parentSpanId"/> is empty, or
    /// <paramref name="traceFlags"/> is outside one unsigned byte.
    /// </exception>
    public Traceparent(TraceId traceId, SpanId parentSpanId, int traceFlags)
    {
        if (traceId.IsEmpty)
        {
            throw new ArgumentException("traceId must not be empty", nameof(traceId));
        }

        if (parentSpanId.IsEmpty)
        {
            throw new ArgumentException("parentSpanId must not be empty", nameof(parentSpanId));
        }

        if (traceFlags is < 0 or > 0xff)
        {
            throw new ArgumentException("traceFlags must be a single unsigned byte", nameof(traceFlags));
        }

        TraceId = traceId;
        ParentSpanId = parentSpanId;
        TraceFlags = traceFlags;
    }

    /// <summary>Parses a <c>traceparent</c> header value.</summary>
    /// <param name="headerValue">Raw header value, possibly <see langword="null"/> or malformed.</param>
    /// <returns>The parsed value, or <see langword="null"/> when the header is absent or does not conform.</returns>
    /// <remarks>Never throws — see the type's remarks on why parsing must be total.</remarks>
    public static Traceparent? Parse(string? headerValue)
    {
        if (headerValue is null)
        {
            return null;
        }

        var fields = headerValue.Split(Delimiter);
        return IsWellFormed(fields) ? Build(fields) : null;
    }

    private static bool IsWellFormed(string[] fields)
    {
        if (fields.Length < FieldCount || !IsValidVersion(fields[0], fields.Length))
        {
            return false;
        }

        if (!IsId(fields[1], TraceIdLength) || !IsId(fields[2], SpanIdLength))
        {
            return false;
        }

        return IsHex(fields[3], FlagsLength) && !HasEmptyTrailingField(fields);
    }

    private static Traceparent Build(string[] fields)
    {
        var flags = int.Parse(fields[3], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return new Traceparent(new TraceId(fields[1]), new SpanId(fields[2]), flags);
    }

    /// <summary>Renders this value as a version-<c>00</c> header.</summary>
    /// <returns>The header value, always 55 characters.</returns>
    public string Format()
    {
        return $"{Version}{Delimiter}{TraceId.Value}{Delimiter}{ParentSpanId.Value}{Delimiter}{HexByte(TraceFlags)}";
    }

    /// <summary>Whether the W3C sampled flag (bit 0 of <see cref="TraceFlags"/>) is set.</summary>
    public bool Sampled => (TraceFlags & 1) != 0;

    /// <summary>Returns <see cref="Format"/>, making the type transparent in string contexts.</summary>
    public override string ToString() => Format();

    /// <summary>Version <c>00</c> is exactly four fields; a higher one may append more, <c>ff</c> is not.</summary>
    private static bool IsValidVersion(string version, int fieldCount)
    {
        if (!IsHex(version, VersionLength) || version == ForbiddenVersion)
        {
            return false;
        }

        return version != Version || fieldCount == FieldCount;
    }

    /// <summary>A trailing empty field means a stray delimiter, not an extension the spec allows.</summary>
    private static bool HasEmptyTrailingField(string[] fields) => fields[^1].Length == 0;

    private static bool IsId(string value, int length) => IsHex(value, length) && !IsAllZero(value);

    private static bool IsAllZero(string value)
    {
        foreach (var c in value)
        {
            if (c != '0')
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsHex(string value, int length)
    {
        if (value.Length != length)
        {
            return false;
        }

        foreach (var c in value)
        {
            if ((c < '0' || c > '9') && (c < 'a' || c > 'f'))
            {
                return false;
            }
        }

        return true;
    }

    private static string HexByte(int value) => value.ToString("x2", CultureInfo.InvariantCulture);
}
