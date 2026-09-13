// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace Build.Tests;

/// <summary>
/// Builds minimal, structurally-plausible PNG byte arrays for tests that must plant (or rule
/// out) an embedded text metadata chunk — never a real image library, and never a real PNG
/// encoder: <see cref="AssetMetadataSupportTests"/> and <see cref="PublishScriptBinaryAssetTests"/>
/// both scan raw bytes (a regex over Latin-1 text, or <c>grep -a</c>), so what matters is the PNG
/// signature up front, enough binary filler (including NUL bytes, so a shell-side <c>grep -I</c>
/// classifies the file as binary, matching a real image) and — for the "dirty" cases — the marker
/// text sitting somewhere in that filler. Nothing here is ever a valid, decodable PNG.
/// </summary>
internal static class PngBytes
{
    private static readonly byte[] Signature = [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>Signature plus opaque binary filler (NUL bytes included) and no marker text at all.</summary>
    public static byte[] Clean() => Build(text: null);

    /// <summary>Signature plus filler carrying <paramref name="marker"/> alone (no vendor word).</summary>
    public static byte[] WithMarker(string marker) => Build(marker);

    /// <summary>Signature plus filler carrying <paramref name="marker"/> immediately followed by <paramref name="word"/> — the shape a real embedded-provenance chunk takes (a chunk-type tag, then the text it carries).</summary>
    public static byte[] WithMarkerAndWord(string marker, string word) => Build($"{marker} {word}");

    private static byte[] Build(string? text)
    {
        using var stream = new MemoryStream();
        stream.Write(Signature);
        // Fake IHDR chunk (length + type + a width/height/bit-depth payload that is mostly
        // zero bytes) — real PNGs carry exactly this shape right after the signature, and the
        // zero bytes guarantee there is binary content here even before any filler below.
        stream.Write([0, 0, 0, 13, (byte)'I', (byte)'H', (byte)'D', (byte)'R', 0, 0, 0, 0, 0, 0, 0, 0, 8, 6, 0, 0, 0]);
        stream.Write(new byte[48]); // opaque filler, all zero bytes
        if (text is not null)
            stream.Write(System.Text.Encoding.ASCII.GetBytes(text));
        stream.Write(new byte[16]); // trailing filler, all zero bytes
        stream.Write([0, 0, 0, 0, (byte)'I', (byte)'E', (byte)'N', (byte)'D']);
        return stream.ToArray();
    }
}
