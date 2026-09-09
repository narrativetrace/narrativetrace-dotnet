// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Build;
using Xunit;

namespace Build.Tests;

/// <summary>
/// Covers the pure logic behind <c>VerifyPublication</c> — see
/// <see cref="PublicationVerificationSupport"/>. No network, no filesystem: presence classification,
/// URL construction, backoff, and the packable/non-packable manifest split (including the
/// structural warning that would have caught <c>NarrativeTrace.Benchmarks</c> shipping in 0.1.0).
/// </summary>
public sealed class PublicationVerificationSupportTests
{
    // ---------------------------------------------------------------- manifest derivation

    [Fact]
    public void Packable_projects_land_in_ExpectedOnRegistry()
    {
        var manifest = PublicationVerificationSupport.BuildManifest(new[]
        {
            new PublicationVerificationSupport.ProjectPackability("src/NarrativeTrace.Core/NarrativeTrace.Core.csproj", "NarrativeTrace.Core", IsPackable: true),
        });

        var entry = Assert.Single(manifest.ExpectedOnRegistry);
        Assert.Equal("NarrativeTrace.Core", entry.PackageId);
        Assert.Empty(manifest.ExpectedOffRegistry);
    }

    [Fact]
    public void Non_packable_projects_land_in_ExpectedOffRegistry()
    {
        var manifest = PublicationVerificationSupport.BuildManifest(new[]
        {
            new PublicationVerificationSupport.ProjectPackability("tests/NarrativeTrace.Core.Tests/NarrativeTrace.Core.Tests.csproj", "NarrativeTrace.Core.Tests", IsPackable: false),
        });

        var entry = Assert.Single(manifest.ExpectedOffRegistry);
        Assert.Equal("NarrativeTrace.Core.Tests", entry.PackageId);
        Assert.Empty(manifest.ExpectedOnRegistry);
    }

    [Fact]
    public void A_packable_project_under_src_raises_no_warning()
    {
        var manifest = PublicationVerificationSupport.BuildManifest(new[]
        {
            new PublicationVerificationSupport.ProjectPackability("src/NarrativeTrace.Core/NarrativeTrace.Core.csproj", "NarrativeTrace.Core", IsPackable: true),
        });

        Assert.Empty(manifest.Warnings);
    }

    /// <summary>
    /// Reproduces the exact historical shape: a benchmark harness under <c>benchmarks/</c> with
    /// nothing setting <c>IsPackable</c>, so it reads packable by MSBuild's own default — the
    /// defect that let <c>NarrativeTrace.Benchmarks</c> ship in the 0.1.0 release.
    /// </summary>
    [Fact]
    public void A_packable_project_outside_src_raises_a_warning_naming_it()
    {
        var manifest = PublicationVerificationSupport.BuildManifest(new[]
        {
            new PublicationVerificationSupport.ProjectPackability(
                "benchmarks/NarrativeTrace.Benchmarks/NarrativeTrace.Benchmarks.csproj",
                "NarrativeTrace.Benchmarks", IsPackable: true),
        });

        var warning = Assert.Single(manifest.Warnings);
        Assert.Contains("NarrativeTrace.Benchmarks", warning, StringComparison.Ordinal);
        Assert.Contains("benchmarks/NarrativeTrace.Benchmarks", warning, StringComparison.Ordinal);
    }

    [Fact]
    public void A_backslash_project_path_is_normalized_before_the_prefix_check()
    {
        var manifest = PublicationVerificationSupport.BuildManifest(new[]
        {
            new PublicationVerificationSupport.ProjectPackability(
                @"src\NarrativeTrace.Core\NarrativeTrace.Core.csproj", "NarrativeTrace.Core", IsPackable: true),
        });

        Assert.Empty(manifest.Warnings);
    }

    [Fact]
    public void Both_buckets_are_sorted_by_package_id()
    {
        var manifest = PublicationVerificationSupport.BuildManifest(new[]
        {
            new PublicationVerificationSupport.ProjectPackability("src/Z/Z.csproj", "NarrativeTrace.Z", IsPackable: true),
            new PublicationVerificationSupport.ProjectPackability("src/A/A.csproj", "NarrativeTrace.A", IsPackable: true),
        });

        Assert.Equal(
            new[] { "NarrativeTrace.A", "NarrativeTrace.Z" },
            manifest.ExpectedOnRegistry.Select(p => p.PackageId));
    }

    // ---------------------------------------------------------------- URL construction

    [Fact]
    public void Nupkg_url_lowercases_id_and_version()
    {
        var url = PublicationVerificationSupport.NupkgUrl(
            "https://api.nuget.org/v3-flatcontainer", "NarrativeTrace.Core", "0.1.0");

        Assert.Equal(
            "https://api.nuget.org/v3-flatcontainer/narrativetrace.core/0.1.0/narrativetrace.core.0.1.0.nupkg",
            url);
    }

    [Fact]
    public void Nuspec_url_lowercases_id_and_version()
    {
        var url = PublicationVerificationSupport.NuspecUrl(
            "https://api.nuget.org/v3-flatcontainer", "NarrativeTrace.Core", "0.1.0");

        Assert.Equal(
            "https://api.nuget.org/v3-flatcontainer/narrativetrace.core/0.1.0/narrativetrace.core.nuspec",
            url);
    }

    // ---------------------------------------------------------------- presence classification

    [Fact]
    public void Status_200_classifies_as_present()
    {
        Assert.Equal(PublicationVerificationSupport.Presence.Present, PublicationVerificationSupport.ClassifyHttpStatus(200));
    }

    [Fact]
    public void Status_404_classifies_as_lagging()
    {
        Assert.Equal(PublicationVerificationSupport.Presence.Lagging, PublicationVerificationSupport.ClassifyHttpStatus(404));
    }

    [Fact]
    public void Status_500_classifies_as_missing()
    {
        Assert.Equal(PublicationVerificationSupport.Presence.Missing, PublicationVerificationSupport.ClassifyHttpStatus(500));
    }

    [Fact]
    public void No_response_at_all_classifies_as_missing()
    {
        Assert.Equal(PublicationVerificationSupport.Presence.Missing, PublicationVerificationSupport.ClassifyHttpStatus(0));
    }

    [Fact]
    public void Worst_of_all_present_is_present()
    {
        Assert.Equal(
            PublicationVerificationSupport.Presence.Present,
            PublicationVerificationSupport.Worst(new[]
            {
                PublicationVerificationSupport.Presence.Present,
                PublicationVerificationSupport.Presence.Present,
            }));
    }

    [Fact]
    public void Worst_prefers_lagging_over_present()
    {
        Assert.Equal(
            PublicationVerificationSupport.Presence.Lagging,
            PublicationVerificationSupport.Worst(new[]
            {
                PublicationVerificationSupport.Presence.Present,
                PublicationVerificationSupport.Presence.Lagging,
            }));
    }

    [Fact]
    public void Worst_prefers_missing_even_over_present()
    {
        Assert.Equal(
            PublicationVerificationSupport.Presence.Missing,
            PublicationVerificationSupport.Worst(new[]
            {
                PublicationVerificationSupport.Presence.Present,
                PublicationVerificationSupport.Presence.Missing,
                PublicationVerificationSupport.Presence.Lagging,
            }));
    }

    // ---------------------------------------------------------------- backoff

    [Fact]
    public void Backoff_doubles_each_round()
    {
        Assert.Equal(30, PublicationVerificationSupport.NextBackoffSeconds(15, capSeconds: 120));
    }

    [Fact]
    public void Backoff_is_capped()
    {
        Assert.Equal(60, PublicationVerificationSupport.NextBackoffSeconds(45, capSeconds: 60));
    }
}
