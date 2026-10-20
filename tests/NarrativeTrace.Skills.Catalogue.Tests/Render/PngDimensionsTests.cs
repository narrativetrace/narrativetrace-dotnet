// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Skills.Render;
using Xunit;

namespace NarrativeTrace.Skills.Tests.Render;

public sealed class PngDimensionsTests
{
    [Fact]
    public void Reads_width_and_height_from_a_flat_png()
    {
        var png = PngFixtures.Flat(64, 32, 0x4a);

        Assert.Equal(64, PngDimensions.Width(png));
        Assert.Equal(32, PngDimensions.Height(png));
    }

    [Fact]
    public void Rejects_bytes_too_short_to_hold_a_signature_and_ihdr()
    {
        Assert.Throws<ArgumentException>(() => PngDimensions.Width(new byte[10]));
        Assert.Throws<ArgumentException>(() => PngDimensions.Height(new byte[23]));
    }

    [Fact]
    public void Rejects_bytes_with_the_wrong_signature()
    {
        var bogus = new byte[24];
        bogus[0] = 1;

        Assert.Throws<ArgumentException>(() => PngDimensions.Height(bogus));
    }

    [Fact]
    public void Rejects_a_signature_that_is_wrong_only_in_its_last_byte()
    {
        var png = PngFixtures.Flat(64, 64, 0x4a);
        png[7] = 0x0b;

        Assert.Throws<ArgumentException>(() => PngDimensions.Width(png));
    }

    [Fact]
    public void Reads_a_dimension_with_the_high_byte_set_as_a_large_positive_or_refuses_it_never_negative()
    {
        var png = PngFixtures.Flat(1, 1, 0);
        png[16] = 0x80; // width >= 2^31 is not a valid PNG (the spec caps at 2^31-1)

        Assert.Throws<ArgumentException>(() => PngDimensions.Width(png));
    }

    [Fact]
    public void The_two_committed_assets_are_square_pngs_of_at_least_48_pixels()
    {
        var assets = Path.Combine(TestPaths.RepoRoot(), "src", "NarrativeTrace.Skills.Catalogue", "openai-plugin", "assets");

        foreach (var name in new[] { "logo.png", "composer-icon.png" })
        {
            var bytes = File.ReadAllBytes(Path.Combine(assets, name));
            Assert.Equal(PngDimensions.Width(bytes), PngDimensions.Height(bytes));
            Assert.InRange(PngDimensions.Width(bytes), 48, int.MaxValue);
            Assert.InRange(bytes.Length, 1, 5 * 1024 * 1024);
        }
    }
}
