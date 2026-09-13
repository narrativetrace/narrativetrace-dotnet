// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Build;
using Xunit;

namespace Build.Tests;

/// <summary>
/// Covers <see cref="UnreleasedMarkerSupport"/> — the pure marker-counting logic behind the
/// <c>llms.txt</c> banner's unreleased-count clause (design note
/// <c>docs-vs-published-gate-2026-09-12.md</c> §1 item 8). No filesystem: <c>Build.cs</c> supplies
/// file contents already read from disk.
/// </summary>
public sealed class UnreleasedMarkerSupportTests
{
    [Fact]
    public void Count_is_zero_for_content_with_no_marker()
    {
        Assert.Equal(0, UnreleasedMarkerSupport.Count("Nothing unreleased here.\n"));
    }

    [Fact]
    public void Count_finds_a_single_marker()
    {
        Assert.Equal(
            1, UnreleasedMarkerSupport.Count("Behavior A *(since 0.1.4, unreleased)* is new.\n"));
    }

    [Fact]
    public void Count_finds_several_markers_in_one_file()
    {
        var content = "A *(since 0.1.4, unreleased)* and B *(since 0.1.5, unreleased)* "
            + "and C *(since 0.1.4, unreleased)*.\n";

        Assert.Equal(3, UnreleasedMarkerSupport.Count(content));
    }

    [Fact]
    public void Count_tolerates_extra_whitespace_after_the_comma()
    {
        // The same tolerance scripts/publish-public.sh's tag-time gate applies — it parses
        // markers "more loosely than it rewrites".
        Assert.Equal(1, UnreleasedMarkerSupport.Count("*(since 0.1.4,  unreleased)*\n"));
    }

    [Fact]
    public void Count_does_not_match_a_released_since_marker()
    {
        Assert.Equal(0, UnreleasedMarkerSupport.Count("Behavior A *(since 0.1.4)* shipped.\n"));
    }

    [Fact]
    public void CountAll_sums_across_every_file()
    {
        var files = new[]
        {
            "A *(since 0.1.4, unreleased)*.\n",
            "No markers here.\n",
            "B *(since 0.1.5, unreleased)* and C *(since 0.1.5, unreleased)*.\n",
        };

        Assert.Equal(3, UnreleasedMarkerSupport.CountAll(files));
    }

    [Fact]
    public void CountAll_is_zero_for_an_empty_file_set()
    {
        Assert.Equal(0, UnreleasedMarkerSupport.CountAll([]));
    }
}
