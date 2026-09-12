// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Globalization;
using NarrativeTrace.Build;
using Xunit;

namespace Build.Tests;

/// <summary>
/// Covers <see cref="PublishedVersionSupport"/> — the pure logic behind the
/// generated "docs vs published" banner under the <c>llms.txt</c> H1 (family
/// design note <c>docs-vs-published-gate-2026-09-12.md</c> §1): registry-JSON
/// parsing, the three banner forms (equal / differ / offline), their exact
/// inverse parse, cache freshness, and the marker-block upsert. No network,
/// no filesystem.
/// </summary>
public sealed class PublishedVersionSupportTests
{
    // ---------------------------------------------------------------- FlatContainerIndexUrl

    [Fact]
    public void FlatContainerIndexUrl_lowercases_the_package_id()
    {
        Assert.Equal(
            "https://api.nuget.org/v3-flatcontainer/narrativetrace.core/index.json",
            PublishedVersionSupport.FlatContainerIndexUrl(
                "https://api.nuget.org/v3-flatcontainer", "NarrativeTrace.Core"));
    }

    // ---------------------------------------------------------------- ParseLatestVersion

    [Fact]
    public void ParseLatestVersion_picks_the_highest_stable_version()
    {
        var json = """{"versions": ["0.1.0", "0.1.1", "0.1.3"]}""";

        Assert.Equal("0.1.3", PublishedVersionSupport.ParseLatestVersion(json));
    }

    [Fact]
    public void ParseLatestVersion_ignores_registration_order_and_sorts_numerically()
    {
        // "0.1.10" must beat "0.1.9" numerically, not lexicographically.
        var json = """{"versions": ["0.1.9", "0.1.10", "0.1.2"]}""";

        Assert.Equal("0.1.10", PublishedVersionSupport.ParseLatestVersion(json));
    }

    [Fact]
    public void ParseLatestVersion_skips_prerelease_versions()
    {
        var json = """{"versions": ["0.1.3", "0.1.4-rc1"]}""";

        Assert.Equal("0.1.3", PublishedVersionSupport.ParseLatestVersion(json));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("""{"versions": []}""")]
    [InlineData("""{"no-versions-key": true}""")]
    public void ParseLatestVersion_returns_null_for_anything_unusable(string? json)
    {
        Assert.Null(PublishedVersionSupport.ParseLatestVersion(json));
    }

    // ---------------------------------------------------------------- BuildLine (the three cases)

    [Fact]
    public void BuildLine_when_versions_are_equal()
    {
        Assert.Equal(
            "*(Docs and published both at 0.1.3.)*",
            PublishedVersionSupport.BuildLine("0.1.3", "0.1.3"));
    }

    [Fact]
    public void BuildLine_when_versions_differ()
    {
        Assert.Equal(
            "*(These docs describe 0.1.4; published is 0.1.3.)*",
            PublishedVersionSupport.BuildLine("0.1.4", "0.1.3"));
    }

    [Fact]
    public void BuildLine_when_offline_never_guesses()
    {
        Assert.Equal(
            "*(These docs describe 0.1.4; published: unknown offline.)*",
            PublishedVersionSupport.BuildLine("0.1.4", null));
    }

    // ---------------------------------------------------------------- TryParseLine (the inverse)

    [Theory]
    [InlineData("0.1.3", "0.1.3")]
    [InlineData("0.1.4", "0.1.3")]
    public void TryParseLine_round_trips_through_BuildLine(string repo, string? published)
    {
        var line = PublishedVersionSupport.BuildLine(repo, published);

        var ok = PublishedVersionSupport.TryParseLine(line, out var describedVersion, out var publishedVersion);

        Assert.True(ok);
        Assert.Equal(repo, describedVersion);
        Assert.Equal(published, publishedVersion);
    }

    [Fact]
    public void TryParseLine_round_trips_the_offline_form()
    {
        var line = PublishedVersionSupport.BuildLine("0.1.4", null);

        var ok = PublishedVersionSupport.TryParseLine(line, out var describedVersion, out var publishedVersion);

        Assert.True(ok);
        Assert.Equal("0.1.4", describedVersion);
        Assert.Null(publishedVersion);
    }

    [Fact]
    public void TryParseLine_rejects_a_line_that_is_not_one_of_the_three_forms()
    {
        var ok = PublishedVersionSupport.TryParseLine("Docs are current.", out _, out _);

        Assert.False(ok);
    }

    // ---------------------------------------------------------------- cache freshness

    [Fact]
    public void A_cache_entry_within_the_max_age_is_fresh()
    {
        var now = DateTimeOffset.UtcNow;
        var entry = new PublishedVersionSupport.CacheEntry("0.1.3", now.AddMinutes(-30));

        Assert.True(PublishedVersionSupport.IsFresh(entry, now));
    }

    [Fact]
    public void A_cache_entry_past_the_max_age_is_stale()
    {
        var now = DateTimeOffset.UtcNow;
        var entry = new PublishedVersionSupport.CacheEntry("0.1.3", now.AddHours(-2));

        Assert.False(PublishedVersionSupport.IsFresh(entry, now));
    }

    [Fact]
    public void Cache_round_trips_through_serialization()
    {
        var entry = new PublishedVersionSupport.CacheEntry(
            "0.1.3", DateTimeOffset.Parse("2026-09-12T00:00:00Z", CultureInfo.InvariantCulture));

        var parsed = PublishedVersionSupport.ParseCache(PublishedVersionSupport.SerializeCache(entry));

        Assert.Equal(entry, parsed);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json")]
    public void ParseCache_returns_null_for_missing_or_corrupt_content(string? content)
    {
        Assert.Null(PublishedVersionSupport.ParseCache(content));
    }

    // ---------------------------------------------------------------- UpsertBlock / ExtractLine

    [Fact]
    public void UpsertBlock_inserts_the_block_right_after_the_H1_when_none_exists()
    {
        var file = "# NarrativeTrace .NET\n\n> Code is the log.\n";

        var updated = PublishedVersionSupport.UpsertBlock(
            file, "*(Docs and published both at 0.1.3.)*", "<!-- published-version checked X -->");

        Assert.Equal(
            "*(Docs and published both at 0.1.3.)*",
            PublishedVersionSupport.ExtractLine(updated));
        Assert.StartsWith("# NarrativeTrace .NET\n", updated, StringComparison.Ordinal);
        Assert.Contains("> Code is the log.", updated, StringComparison.Ordinal);
    }

    [Fact]
    public void UpsertBlock_replaces_an_existing_block_in_place()
    {
        var file = "# NarrativeTrace .NET\n"
            + $"{PublishedVersionSupport.BlockStart}\n"
            + "*(Docs and published both at 0.1.3.)* <!-- published-version checked OLD -->\n"
            + $"{PublishedVersionSupport.BlockEnd}\n\n"
            + "> Code is the log.\n";

        var updated = PublishedVersionSupport.UpsertBlock(
            file, "*(These docs describe 0.1.4; published is 0.1.3.)*", "<!-- published-version checked NEW -->");

        Assert.Equal(
            "*(These docs describe 0.1.4; published is 0.1.3.)*",
            PublishedVersionSupport.ExtractLine(updated));
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(updated, PublishedVersionSupport.BlockStart));
        Assert.Contains("> Code is the log.", updated, StringComparison.Ordinal);
    }

    [Fact]
    public void ExtractLine_returns_null_when_no_block_exists()
    {
        Assert.Null(PublishedVersionSupport.ExtractLine("# NarrativeTrace .NET\n\n> Code is the log.\n"));
    }

    // ---------------------------------------------------------------- provenance comment

    [Fact]
    public void BuildProvenanceComment_discloses_a_live_fetch()
    {
        var comment = PublishedVersionSupport.BuildProvenanceComment(
            DateTimeOffset.Parse("2026-09-12T13:00:00Z", CultureInfo.InvariantCulture), cacheAge: null);

        Assert.Contains("live", comment, StringComparison.Ordinal);
        Assert.DoesNotContain("cached", comment, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildProvenanceComment_discloses_a_cache_hit_and_its_age()
    {
        var comment = PublishedVersionSupport.BuildProvenanceComment(
            DateTimeOffset.Parse("2026-09-12T13:00:00Z", CultureInfo.InvariantCulture), cacheAge: TimeSpan.FromMinutes(12));

        Assert.Contains("cached", comment, StringComparison.Ordinal);
        Assert.Contains("12m", comment, StringComparison.Ordinal);
    }
}
