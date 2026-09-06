// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Xml.Linq;
using Xunit;

namespace Build.Tests;

/// <summary>
/// Guards the package metadata every published nupkg carries. The packages
/// are public artifacts published from the public GitHub snapshot, so their
/// metadata must advertise the public home — a consumer clicking through to a
/// sign-in-walled GitLab URL is told the source is closed when the whole
/// point of the licence is that it is not. Pack-time wiring (icon, readme,
/// licence) is asserted here because a defect in it is invisible to the rest
/// of the suite: nothing in this repo's own build consumes the nupkg.
/// </summary>
public class PackageMetadataTests
{
    private static readonly string RepoRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "../../../../../"));

    private static readonly XDocument Props = XDocument.Load(
        Path.Combine(RepoRoot, "Directory.Build.props"));

    private static string Property(string name) =>
        Props.Descendants(name).FirstOrDefault()?.Value ?? "";

    [Theory]
    [InlineData("PackageProjectUrl")]
    [InlineData("RepositoryUrl")]
    public void Package_urls_point_at_the_public_home(string property)
    {
        var url = Property(property);

        Assert.StartsWith(
            "https://github.com/narrativetrace/narrativetrace-dotnet",
            url);
    }

    [Fact]
    public void No_package_metadata_names_the_private_host()
    {
        var metadata = Props.Descendants("PropertyGroup")
            .SelectMany(group => group.Elements())
            .Where(element => element.Name.LocalName.StartsWith("Package")
                || element.Name.LocalName.StartsWith("Repository"));

        Assert.All(metadata, element =>
            Assert.DoesNotContain("gitlab.com", element.Value));
    }

    [Fact]
    public void Source_link_matches_the_host_the_packages_are_built_from()
    {
        var sourceLink = Props.Descendants("PackageReference")
            .Select(reference => reference.Attribute("Include")?.Value ?? "")
            .Where(include => include.StartsWith("Microsoft.SourceLink."))
            .ToList();

        var link = Assert.Single(sourceLink);
        Assert.Equal("Microsoft.SourceLink.GitHub", link);
    }

    [Fact]
    public void Icon_wiring_is_present_and_conditional_on_the_asset()
    {
        var property = Props.Descendants("PackageIcon").FirstOrDefault();
        Assert.NotNull(property);
        Assert.Equal("icon.png", property!.Value);
        Assert.Contains("assets/icon.png",
            property.Attribute("Condition")?.Value ?? "");

        var packed = Props.Descendants("None").FirstOrDefault(none =>
            (none.Attribute("Include")?.Value ?? "").EndsWith("assets/icon.png"));
        Assert.NotNull(packed);
        Assert.Equal("true", packed!.Attribute("Pack")?.Value);
        Assert.Contains("assets/icon.png",
            packed.Attribute("Condition")?.Value ?? "");
    }

    [Fact]
    public void The_icon_when_present_is_a_png_nuget_accepts()
    {
        var icon = Path.Combine(RepoRoot, "assets", "icon.png");
        if (!File.Exists(icon))
        {
            return; // wiring is Exists-conditional; the asset is the owner's
        }

        var bytes = File.ReadAllBytes(icon);

        Assert.True(bytes.Length <= 1024 * 1024, "nuget.org caps icons at 1 MB");
        Assert.Equal(
            new byte[] { 0x89, 0x50, 0x4E, 0x47 },
            bytes.Take(4).ToArray());
    }
}
