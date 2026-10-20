// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Buffers.Binary;

namespace NarrativeTrace.Skills.Render;

/// <summary>
/// Reads a PNG's width and height straight out of its <c>IHDR</c> chunk — no imaging library, no
/// dependency. The OpenAI package check uses this to verify the two listing icons are square without
/// decoding a single pixel.
/// </summary>
internal static class PngDimensions
{
    private static readonly byte[] Signature = [0x89, (byte)'P', (byte)'N', (byte)'G', (byte)'\r', (byte)'\n', 0x1a, (byte)'\n'];
    private const int IhdrWidthOffset = 16;
    private const int IhdrHeightOffset = 20;

    /// <summary>The image width in pixels.</summary>
    /// <param name="png">The PNG file's bytes.</param>
    /// <exception cref="ArgumentException">The bytes are not a PNG, or the width is outside the format's range.</exception>
    internal static int Width(byte[] png) => ReadDimension(png, IhdrWidthOffset);

    /// <summary>The image height in pixels.</summary>
    /// <param name="png">The PNG file's bytes.</param>
    /// <exception cref="ArgumentException">The bytes are not a PNG, or the height is outside the format's range.</exception>
    internal static int Height(byte[] png) => ReadDimension(png, IhdrHeightOffset);

    private static int ReadDimension(byte[] png, int offset)
    {
        ArgumentNullException.ThrowIfNull(png);
        if (png.Length < IhdrHeightOffset + 4)
        {
            throw new ArgumentException("not a PNG: too short to hold a signature and IHDR", nameof(png));
        }

        if (!png.AsSpan(0, Signature.Length).SequenceEqual(Signature))
        {
            throw new ArgumentException("not a PNG: bad signature", nameof(png));
        }

        var value = BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(offset));
        return value > int.MaxValue
            ? throw new ArgumentException("not a PNG: dimension beyond the format's 2^31-1 limit", nameof(png))
            : (int)value;
    }
}
