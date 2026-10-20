// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.IO.Compression;
using System.Buffers.Binary;
using System.Text;

namespace NarrativeTrace.Skills.Tests.Render;

/// <summary>Builds a minimal, valid, flat-colour PNG for tests — no imaging library needed.</summary>
internal static class PngFixtures
{
    private static readonly byte[] Signature = [0x89, (byte)'P', (byte)'N', (byte)'G', (byte)'\r', (byte)'\n', 0x1a, (byte)'\n'];

    internal static byte[] Flat(int width, int height, byte shade)
    {
        using var output = new MemoryStream();
        output.Write(Signature);
        WriteChunk(output, "IHDR", Ihdr(width, height));
        WriteChunk(output, "IDAT", DeflatedRows(width, height, shade));
        WriteChunk(output, "IEND", []);
        return output.ToArray();
    }

    private static byte[] Ihdr(int width, int height)
    {
        var data = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(data.AsSpan(0), width);
        BinaryPrimitives.WriteInt32BigEndian(data.AsSpan(4), height);
        data[8] = 8; // bit depth
        data[9] = 2; // colour type: RGB
        return data;
    }

    private static byte[] DeflatedRows(int width, int height, byte shade)
    {
        var row = new byte[1 + (width * 3)];
        Array.Fill(row, shade, 1, row.Length - 1);
        using var deflated = new MemoryStream();
        using (var zlib = new ZLibStream(deflated, CompressionLevel.Optimal, leaveOpen: true))
        {
            for (var y = 0; y < height; y++)
            {
                zlib.Write(row);
            }
        }

        return deflated.ToArray();
    }

    private static void WriteChunk(Stream output, string type, byte[] data)
    {
        var length = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, data.Length);
        output.Write(length);
        var typeAndData = new byte[4 + data.Length];
        Encoding.ASCII.GetBytes(type).CopyTo(typeAndData, 0);
        data.CopyTo(typeAndData, 4);
        output.Write(typeAndData);
        var crc = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crc, Crc32(typeAndData));
        output.Write(crc);
    }

    private static uint Crc32(byte[] bytes)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in bytes)
        {
            crc ^= b;
            for (var bit = 0; bit < 8; bit++)
            {
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
            }
        }

        return ~crc;
    }
}
