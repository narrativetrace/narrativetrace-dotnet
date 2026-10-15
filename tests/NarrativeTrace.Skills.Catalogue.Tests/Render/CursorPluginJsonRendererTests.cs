// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Skills.Render;
using Xunit;

namespace NarrativeTrace.Skills.Tests.Render;

/// <summary>
/// The Cursor plugin manifest pair (Phase 8 D3), rendered from the SAME listing that renders the
/// Claude marketplace: one catalogue, one listing, two vendors' files.
/// </summary>
public sealed class CursorPluginJsonRendererTests
{
    private static readonly MarketplaceListing Simple = new(
        Name: "narrativetrace-example",
        Owner: new MarketplaceOwner("Example Owner", "https://example.test"),
        Description: "An example marketplace.",
        PluginDescription: "An example plugin.",
        PluginSource: "./.claude",
        License: "Apache-2.0",
        Homepage: "https://example.test",
        Keywords: ["one", "two"]);

    [Fact]
    public void Renders_the_exact_plugin_manifest_the_plugin_carries_the_open_standard_skills_and_one_rule()
    {
        Assert.Equal(
            "{\n" +
            "  \"name\": \"narrativetrace-example\",\n" +
            "  \"description\": \"An example plugin.\",\n" +
            "  \"author\": {\n" +
            "    \"name\": \"Example Owner\"\n" +
            "  },\n" +
            "  \"license\": \"Apache-2.0\",\n" +
            "  \"homepage\": \"https://example.test\",\n" +
            "  \"keywords\": [\n" +
            "    \"one\",\n" +
            "    \"two\"\n" +
            "  ],\n" +
            "  \"skills\": \"./.agents/skills/\",\n" +
            "  \"rules\": \"./.cursor/rules/narrativetrace.mdc\"\n" +
            "}\n",
            CursorPluginJsonRenderer.RenderPlugin(Simple),
            ignoreLineEndingDifferences: false);
    }

    [Fact]
    public void Renders_the_exact_marketplace_manifest_listing_the_one_plugin_rooted_at_the_repository()
    {
        Assert.Equal(
            "{\n" +
            "  \"name\": \"narrativetrace-example\",\n" +
            "  \"owner\": {\n" +
            "    \"name\": \"Example Owner\"\n" +
            "  },\n" +
            "  \"metadata\": {\n" +
            "    \"description\": \"An example marketplace.\"\n" +
            "  },\n" +
            "  \"plugins\": [\n" +
            "    {\n" +
            "      \"name\": \"narrativetrace-example\",\n" +
            "      \"source\": \".\",\n" +
            "      \"description\": \"An example plugin.\",\n" +
            "      \"license\": \"Apache-2.0\",\n" +
            "      \"homepage\": \"https://example.test\",\n" +
            "      \"keywords\": [\n" +
            "        \"one\",\n" +
            "        \"two\"\n" +
            "      ]\n" +
            "    }\n" +
            "  ]\n" +
            "}\n",
            CursorPluginJsonRenderer.RenderMarketplace(Simple),
            ignoreLineEndingDifferences: false);
    }

    [Fact]
    public void Escapes_hostile_characters_in_both_manifests()
    {
        var hostile = Simple with { PluginDescription = "Has \"quotes\", a\\backslash and a\nnewline." };

        foreach (var rendered in new[] { CursorPluginJsonRenderer.RenderPlugin(hostile), CursorPluginJsonRenderer.RenderMarketplace(hostile) })
        {
            Assert.Contains("\"description\": \"Has \\\"quotes\\\", a\\\\backslash and a\\nnewline.\"", rendered, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void The_real_catalogue_manifests_carry_no_version_and_name_the_same_plugin_as_the_claude_marketplace()
    {
        var listing = NarrativeTrace.Skills.Catalogue.SkillCatalogue.Marketplace;
        var claude = MarketplaceJsonRenderer.Render(listing);
        var description = System.Text.Json.JsonSerializer.Serialize(listing.PluginDescription, new System.Text.Json.JsonSerializerOptions
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        });

        foreach (var rendered in new[] { claude, CursorPluginJsonRenderer.RenderPlugin(listing), CursorPluginJsonRenderer.RenderMarketplace(listing) })
        {
            Assert.Contains($"\"name\": \"{listing.Name}\"", rendered, StringComparison.Ordinal);
            Assert.Contains(description, rendered, StringComparison.Ordinal);
            Assert.DoesNotContain("\"version\"", rendered, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Rendering_twice_produces_byte_identical_output()
    {
        Assert.Equal(CursorPluginJsonRenderer.RenderPlugin(Simple), CursorPluginJsonRenderer.RenderPlugin(Simple));
        Assert.Equal(CursorPluginJsonRenderer.RenderMarketplace(Simple), CursorPluginJsonRenderer.RenderMarketplace(Simple));
    }
}
