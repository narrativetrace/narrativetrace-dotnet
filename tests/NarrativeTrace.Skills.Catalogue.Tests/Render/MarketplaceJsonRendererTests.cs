// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Skills.Render;
using Xunit;

namespace NarrativeTrace.Skills.Tests.Render;

public sealed class MarketplaceJsonRendererTests
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
    public void Renders_the_exact_expected_json_for_a_simple_listing()
    {
        var rendered = MarketplaceJsonRenderer.Render(Simple);

        Assert.Equal(
            "{\n" +
            "  \"name\": \"narrativetrace-example\",\n" +
            "  \"owner\": {\n" +
            "    \"name\": \"Example Owner\",\n" +
            "    \"url\": \"https://example.test\"\n" +
            "  },\n" +
            "  \"description\": \"An example marketplace.\",\n" +
            "  \"plugins\": [\n" +
            "    {\n" +
            "      \"name\": \"narrativetrace-example\",\n" +
            "      \"source\": \"./.claude\",\n" +
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
            rendered,
            ignoreLineEndingDifferences: false);
    }

    [Fact]
    public void Escapes_hostile_characters_in_descriptions()
    {
        var hostile = Simple with { Description = "Has \"quotes\", a\\backslash, a\ttab and a\nnewline." };

        var rendered = MarketplaceJsonRenderer.Render(hostile);

        Assert.Contains(
            "\"description\": \"Has \\\"quotes\\\", a\\\\backslash, a\\ttab and a\\nnewline.\"",
            rendered, StringComparison.Ordinal);
    }

    [Fact]
    public void Rendering_twice_produces_byte_identical_output()
    {
        Assert.Equal(MarketplaceJsonRenderer.Render(Simple), MarketplaceJsonRenderer.Render(Simple));
    }

    [Fact]
    public void Rendering_the_real_catalogue_marketplace_never_throws_and_carries_no_version()
    {
        var rendered = MarketplaceJsonRenderer.Render(NarrativeTrace.Skills.Catalogue.SkillCatalogue.Marketplace);

        Assert.NotEmpty(rendered);
        Assert.DoesNotContain("\"version\"", rendered, StringComparison.Ordinal);
    }
}
