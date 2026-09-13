// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Build;
using Xunit;

namespace Build.Tests;

/// <summary>
/// Covers the embedded-text-metadata ban behind the <c>AssetMetadataCheck</c> build target —
/// the per-commit, structural counterpart to <c>scripts/publish-public.sh</c>'s own binary
/// trace-gate pass, which <c>PublishScriptBinaryAssetTests</c> covers from the publish side.
/// </summary>
public sealed class AssetMetadataSupportTests : IDisposable
{
    private readonly string _repo = Directory.CreateTempSubdirectory("nt-asset-metadata").FullName;

    public void Dispose() => Directory.Delete(_repo, recursive: true);

    private void WriteBytes(string relativePath, byte[] bytes)
    {
        var file = Path.Combine(_repo, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllBytes(file, bytes);
    }

    [Fact]
    public void No_assets_directory_is_clean()
    {
        Assert.Empty(AssetMetadataSupport.Check(_repo));
    }

    [Fact]
    public void A_PNG_with_no_metadata_chunk_is_clean()
    {
        WriteBytes("assets/icon.png", PngBytes.Clean());

        Assert.Empty(AssetMetadataSupport.Check(_repo));
    }

    [Fact]
    public void A_PNG_carrying_an_iTXt_chunk_is_reported()
    {
        WriteBytes("assets/icon.png", PngBytes.WithMarker("iTXt"));

        var problem = Assert.Single(AssetMetadataSupport.Check(_repo));

        Assert.Contains("assets/icon.png", problem, StringComparison.Ordinal);
        Assert.Contains("metadata", problem, StringComparison.Ordinal);
    }

    [Fact]
    public void A_PNG_carrying_a_C2PA_marker_is_reported()
    {
        WriteBytes("assets/icon.png", PngBytes.WithMarker("c2pa"));

        Assert.Single(AssetMetadataSupport.Check(_repo));
    }

    [Fact]
    public void A_PNG_carrying_an_XMP_marker_is_reported()
    {
        WriteBytes("assets/icon.png", PngBytes.WithMarker("XML:com.adobe.xmp"));

        Assert.Single(AssetMetadataSupport.Check(_repo));
    }

    [Fact]
    public void A_non_image_file_under_assets_is_not_scanned()
    {
        WriteBytes("assets/README.md", PngBytes.WithMarker("iTXt"));

        Assert.Empty(AssetMetadataSupport.Check(_repo));
    }

    [Fact]
    public void An_image_outside_assets_is_not_scanned()
    {
        WriteBytes("documentation/icon.png", PngBytes.WithMarker("iTXt"));

        Assert.Empty(AssetMetadataSupport.Check(_repo));
    }

    [Fact]
    public void Multiple_offending_images_are_all_reported_sorted()
    {
        WriteBytes("assets/b.png", PngBytes.WithMarker("iTXt"));
        WriteBytes("assets/a.png", PngBytes.WithMarker("iTXt"));

        var problems = AssetMetadataSupport.Check(_repo);

        Assert.Equal(2, problems.Count);
        Assert.True(string.CompareOrdinal(problems[0], problems[1]) < 0);
    }
}
