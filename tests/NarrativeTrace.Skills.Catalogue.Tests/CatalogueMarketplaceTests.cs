// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Skills.Catalogue;
using NarrativeTrace.Skills.Render;
using Xunit;

namespace NarrativeTrace.Skills.Tests;

/// <summary>
/// The shape of the marketplace listing this repository publishes, asserted on the typed source
/// and on its rendered JSON. Carries no vendor dependency — <c>claude plugin validate</c> is a
/// separate, heavy-tier check (<c>build/Build.cs</c>'s <c>VendorValidate</c> target).
/// </summary>
public sealed class CatalogueMarketplaceTests
{
    [Fact]
    public void The_marketplace_and_its_one_plugin_share_the_runtime_slug_name()
    {
        Assert.Equal("narrativetrace-dotnet", SkillCatalogue.Marketplace.Name);
    }

    [Fact]
    public void The_plugin_source_is_a_relative_path_the_vendor_accepts()
    {
        var source = SkillCatalogue.Marketplace.PluginSource;

        Assert.StartsWith("./", source, StringComparison.Ordinal);
        Assert.DoesNotContain("..", source, StringComparison.Ordinal);
        Assert.DoesNotContain('\\', source);
    }

    [Fact]
    public void The_plugin_source_is_the_claude_flavour_page_root()
    {
        Assert.Equal("./.claude", SkillCatalogue.Marketplace.PluginSource);
    }

    [Fact]
    public void The_listing_declares_the_open_licence_the_pages_ship_under()
    {
        Assert.Equal("Apache-2.0", SkillCatalogue.Marketplace.License);
    }

    [Fact]
    public void The_listing_carries_search_keywords_and_a_homepage()
    {
        Assert.Contains("narrativetrace", SkillCatalogue.Marketplace.Keywords);
        Assert.Contains("dotnet", SkillCatalogue.Marketplace.Keywords);
        Assert.StartsWith("https://", SkillCatalogue.Marketplace.Homepage, StringComparison.Ordinal);
    }

    [Fact]
    public void The_rendered_file_carries_every_key_the_vendor_requires()
    {
        var json = MarketplaceJsonRenderer.Render(SkillCatalogue.Marketplace);

        Assert.Contains("\"name\": \"narrativetrace-dotnet\"", json, StringComparison.Ordinal);
        Assert.Contains("\"owner\": {", json, StringComparison.Ordinal);
        Assert.Contains("\"plugins\": [", json, StringComparison.Ordinal);
        Assert.Contains("\"source\": \"./.claude\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void The_rendered_file_names_the_one_plugin_exactly_once_besides_the_marketplace_itself()
    {
        var json = MarketplaceJsonRenderer.Render(SkillCatalogue.Marketplace);

        Assert.Equal(3, json.Split("\"name\": \"narrativetrace-dotnet\"", StringSplitOptions.None).Length);
    }

    [Fact]
    public void The_rendered_file_carries_no_version_key_and_no_version_literal()
    {
        var json = MarketplaceJsonRenderer.Render(SkillCatalogue.Marketplace);

        Assert.DoesNotContain("\"version\"", json, StringComparison.Ordinal);
        Assert.DoesNotMatch(@"\d+\.\d+\.\d+", json);
    }
}
