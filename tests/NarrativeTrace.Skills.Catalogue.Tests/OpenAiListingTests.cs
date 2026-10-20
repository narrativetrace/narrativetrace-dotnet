// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Skills.Catalogue;
using Xunit;

namespace NarrativeTrace.Skills.Tests;

/// <summary>
/// The OpenAI directory's own listing fields beside the vendor-neutral <see cref="MarketplaceListing"/>:
/// <c>DisplayName</c> and <c>ShortDescription</c> carry the directory's 30-character ceiling, so a
/// longer value never reaches a render.
/// </summary>
public sealed class OpenAiListingTests
{
    private static readonly MarketplaceListing Base = new(
        Name: "narrativetrace-x",
        Owner: new MarketplaceOwner("NarrativeTrace", "https://narrativetrace.ai"),
        Description: "The marketplace description.",
        PluginDescription: "The plugin description.",
        PluginSource: "./.claude",
        License: "Apache-2.0",
        Homepage: "https://narrativetrace.ai",
        Keywords: ["tracing", "agent-skills"]);

    private static OpenAiListing Listing(string displayName, string shortDescription) => new(
        Base,
        displayName,
        shortDescription,
        "Developer Tools",
        ["Read", "Write"],
        ["add narrative tracing to my service"],
        "#4A4A4A",
        "https://narrativetrace.ai");

    [Fact]
    public void Carries_the_base_listing_and_the_openai_only_fields()
    {
        var listing = Listing("NarrativeTrace for X", "NarrativeTrace skills for X");

        Assert.Equal(Base, listing.Base);
        Assert.Equal("NarrativeTrace for X", listing.DisplayName);
        Assert.Equal("Developer Tools", listing.Category);
        Assert.Equal(["Read", "Write"], listing.Capabilities);
        Assert.Equal(["add narrative tracing to my service"], listing.DefaultPrompts);
        Assert.Equal("#4A4A4A", listing.BrandColor);
        Assert.Equal("https://narrativetrace.ai", listing.WebsiteUrl);
    }

    [Fact]
    public void A_thirty_character_display_name_and_short_description_are_accepted()
    {
        var thirty = new string('x', OpenAiListing.MaxListingFieldLength);

        var listing = Listing(thirty, thirty);

        Assert.Equal(30, listing.DisplayName.Length);
        Assert.Equal(30, listing.ShortDescription.Length);
    }

    [Fact]
    public void A_thirty_one_character_display_name_is_rejected()
    {
        var ex = Assert.Throws<ArgumentException>(() => Listing(new string('x', 31), "short"));

        Assert.Contains("DisplayName", ex.Message, StringComparison.Ordinal);
        Assert.Contains("30", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_thirty_one_character_short_description_is_rejected()
    {
        var ex = Assert.Throws<ArgumentException>(() => Listing("name", new string('x', 31)));

        Assert.Contains("ShortDescription", ex.Message, StringComparison.Ordinal);
        Assert.Contains("30", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_with_expression_cannot_smuggle_in_an_over_long_display_name()
    {
        var listing = Listing("name", "short");

        Assert.Throws<ArgumentException>(() => listing with { DisplayName = new string('x', 31) });
    }

    [Fact]
    public void Capabilities_and_default_prompts_are_defensively_copied()
    {
        var capabilities = new List<string> { "Read" };
        var copied = new OpenAiListing(Base, "name", "short", "Developer Tools", capabilities, ["p"], "#4A4A4A", "https://narrativetrace.ai");

        capabilities.Add("Write");

        Assert.Equal(["Read"], copied.Capabilities);
    }

    [Fact]
    public void Null_capabilities_prompts_or_base_are_refused_at_construction_not_at_render()
    {
        Assert.Throws<ArgumentNullException>(() => new OpenAiListing(null!, "n", "s", "c", ["a"], ["p"], "#000", "u"));
        Assert.Throws<ArgumentNullException>(() => new OpenAiListing(Base, "n", "s", "c", null!, ["p"], "#000", "u"));
        Assert.Throws<ArgumentNullException>(() => new OpenAiListing(Base, "n", "s", "c", ["a"], null!, "#000", "u"));
        Assert.Throws<ArgumentNullException>(() => new OpenAiListing(Base, null!, "s", "c", ["a"], ["p"], "#000", "u"));
    }

    [Fact]
    public void Characters_are_counted_in_utf16_units_so_a_value_the_directory_might_count_longer_is_never_let_through()
    {
        var fifteenEmoji = string.Concat(Enumerable.Repeat("\U0001F600", 15));

        Assert.Equal(30, Listing(fifteenEmoji, "s").DisplayName.Length);
        Assert.Throws<ArgumentException>(() => Listing(string.Concat(Enumerable.Repeat("\U0001F600", 16)), "s"));
    }

    [Fact]
    public void The_catalogue_composes_the_directory_listing_from_the_marketplace_listing()
    {
        var directory = SkillCatalogue.OpenAiDirectory;

        Assert.Equal(SkillCatalogue.Marketplace, directory.Base);
        Assert.Equal("NarrativeTrace for .NET", directory.DisplayName);
        Assert.Equal(SkillCatalogue.Marketplace.Homepage, directory.WebsiteUrl);
        Assert.Equal("#00845a", directory.BrandColor);
    }

    [Fact]
    public void The_directory_default_prompts_are_trigger_phrases_of_the_catalogues_own_skills()
    {
        Assert.Equal(3, SkillCatalogue.OpenAiDirectory.DefaultPrompts.Count);
        Assert.All(SkillCatalogue.OpenAiDirectory.DefaultPrompts, prompt =>
            Assert.Contains(SkillCatalogue.Skills, skill => skill.Description.Contains($"'{prompt}'", StringComparison.Ordinal)
                || skill.Description.Contains($"\"{prompt}\"", StringComparison.Ordinal)));
    }
}
