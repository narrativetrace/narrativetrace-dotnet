// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Security.Cryptography;

namespace NarrativeTrace.Core;

/// <summary>
/// Generates and validates W3C-compatible trace and span identifiers.
/// </summary>
public static class SpanIdGenerator
{
    /// <summary>
    /// Generates a cryptographically random trace ID.
    /// </summary>
    public static TraceId GenerateTraceId()
    {
        return new TraceId(GenerateHex(16));
    }
    /// <summary>
    /// Generates a cryptographically random span ID.
    /// </summary>
    public static SpanId GenerateSpanId()
    {
        return new SpanId(GenerateHex(8));
    }

    /// <summary>
    /// Returns true if the value is a valid 32-character lowercase hex trace ID (non-zero).
    /// </summary>
    public static bool IsValidTraceId(string? id)
    {
        return IsValidHexId(id, 32);
    }

    /// <summary>
    /// Returns true if the value is a valid 16-character lowercase hex span ID (non-zero).
    /// </summary>
    public static bool IsValidSpanId(string? id)
    {
        return IsValidHexId(id, 16);
    }

    private static bool IsValidHexId(string? id, int expectedLength)
    {
        if (id is null || id.Length != expectedLength)
            return false;

        var allZero = true;
        for (var i = 0; i < id.Length; i++)
        {
            var c = id[i];
            if (c is not (>= '0' and <= '9' or >= 'a' and <= 'f'))
                return false;
            if (c != '0')
                allZero = false;
        }

        return !allZero;
    }

#if NET5_0_OR_GREATER
    private static string GenerateHex(int byteCount)
    {
        Span<byte> bytes = stackalloc byte[byteCount];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToHexStringLower(bytes);
    }
#else
    private static string GenerateHex(int byteCount)
    {
        var bytes = new byte[byteCount];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
    }
#endif
}
