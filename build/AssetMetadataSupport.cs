// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace NarrativeTrace.Build;

/// <summary>
/// Backs the <c>AssetMetadataCheck</c> build target: committed binary assets under
/// <c>assets/</c> (the NuGet package icon today, any future artwork tomorrow) must carry
/// no embedded text metadata chunk at all — no editor, exporter or provenance tool gets to
/// stash a byte anywhere <c>scripts/publish-public.sh</c>'s text-only <c>grep -I</c> gates
/// could ever miss it.
/// </summary>
/// <remarks>
/// Direct cause: <c>assets/icon.png</c> shipped in every published NuGet package (until
/// stripped 2026-09-13) carrying an embedded C2PA provenance chunk naming the AI vendor —
/// the publish script's trace and secrets/identity gates never saw it because their content
/// scan is <c>grep -I</c>, which skips binary files outright. The publish script now also
/// scans staged binaries with <c>grep -a</c> (its own trace-gate binary pass), but that only
/// catches a GATED WORD inside the metadata at publish time; a chunk that is clean today and
/// an unreviewed identity/provenance leak tomorrow would still ship. This check is the
/// stronger, structural rule for the one place such metadata is known to arrive: no committed
/// image under <c>assets/</c> may carry a text chunk of any kind, so nothing has to be
/// "gated-word"-clean to pass — it has to carry no metadata text at all. Cheap and build-free
/// (a handful of small files, no <see cref="Compile"/> dependency), so it rides every commit
/// like <see cref="HeaderAbsenceSupport"/>, not just the publish-time gate.
/// </remarks>
internal static class AssetMetadataSupport
{
    /// <summary>
    /// PNG text-chunk type tags (<c>iTXt</c>/<c>tEXt</c>/<c>zTXt</c>) plus the XMP/C2PA
    /// provenance markers that ride inside them (or inside a JPEG APP1 segment) — the same
    /// set <c>scripts/publish-public.sh</c>'s documentation names for the per-commit sweep
    /// (<c>grep -a -c -E 'iTXt|tEXt|zTXt|XML:com\.adobe\.xmp|c2pa'</c>).
    /// </summary>
    private static readonly Regex MetadataMarker = new(
        @"iTXt|tEXt|zTXt|XML:com\.adobe\.xmp|c2pa",
        RegexOptions.CultureInvariant);

    private static readonly string[] ImageExtensions = [".png", ".jpg", ".jpeg", ".gif", ".ico", ".webp"];

    /// <summary>
    /// Verifies no image file under <paramref name="repoRoot"/>'s <c>assets/</c> directory
    /// carries a text metadata chunk; returns the offending paths, sorted, empty when the
    /// directory is clean (including when it does not exist).
    /// </summary>
    public static IReadOnlyList<string> Check(string repoRoot)
    {
        var assetsDir = Path.Combine(Path.GetFullPath(repoRoot), "assets");
        if (!Directory.Exists(assetsDir))
            return [];

        return Directory.EnumerateFiles(assetsDir, "*", SearchOption.AllDirectories)
            .Where(file => ImageExtensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))
            .Where(file => MetadataMarker.IsMatch(ReadAsLatin1(file)))
            .Select(file => $"{RelativePath(repoRoot, file)}: carries an embedded text metadata "
                + "chunk (iTXt/tEXt/zTXt/XMP/C2PA) — committed assets must ship with none; "
                + "strip it before committing (e.g. `exiftool -all= <file>`)")
            .OrderBy(problem => problem, StringComparer.Ordinal)
            .ToList();
    }

    // Read raw bytes as Latin-1 (one byte -> one char, lossless round trip, never throws on
    // arbitrary binary content) so the regex matches an ASCII metadata-chunk marker wherever
    // it sits in the byte stream — the same "binary content, treated as text" idea as the
    // publish script's own `grep -a`, just done in-process rather than by shelling out.
    private static string ReadAsLatin1(string file) =>
        Encoding.Latin1.GetString(File.ReadAllBytes(file));

    private static string RelativePath(string root, string file) =>
        Path.GetRelativePath(Path.GetFullPath(root), file).Replace('\\', '/');
}
