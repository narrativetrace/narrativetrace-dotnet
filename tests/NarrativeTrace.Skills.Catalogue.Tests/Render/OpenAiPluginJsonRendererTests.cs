// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Text.Json;
using NarrativeTrace.Skills.Catalogue;
using NarrativeTrace.Skills.Render;
using Xunit;

namespace NarrativeTrace.Skills.Tests.Render;

public sealed class OpenAiPluginJsonRendererTests
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

    private static readonly OpenAiListing Listing = new(
        Base,
        "NarrativeTrace for X",
        "NarrativeTrace skills for X",
        "Developer Tools",
        ["Read", "Write"],
        ["do the thing", "do the other thing"],
        "#4A4A4A",
        "https://narrativetrace.ai");

    private static JsonElement Interface(string json) =>
        JsonDocument.Parse(json).RootElement.GetProperty("extensions").GetProperty("com.openai").GetProperty("interface");

    [Fact]
    public void The_committed_render_maps_every_listing_field_and_carries_no_version()
    {
        var root = JsonDocument.Parse(OpenAiPluginJsonRenderer.Render(Listing)).RootElement;
        var iface = Interface(OpenAiPluginJsonRenderer.Render(Listing));

        Assert.Equal("https://agent-plugins.org/schemas/1.0.0/plugin.schema.json", root.GetProperty("$schema").GetString());
        Assert.Equal("narrativetrace-x", root.GetProperty("name").GetString());
        Assert.Equal("The plugin description.", root.GetProperty("description").GetString());
        Assert.Equal("NarrativeTrace", root.GetProperty("author").GetProperty("name").GetString());
        Assert.Equal("https://narrativetrace.ai", root.GetProperty("homepage").GetString());
        Assert.Equal("Apache-2.0", root.GetProperty("license").GetString());
        Assert.Equal(["tracing", "agent-skills"], root.GetProperty("keywords").EnumerateArray().Select(e => e.GetString()));
        Assert.Equal("NarrativeTrace for X", iface.GetProperty("displayName").GetString());
        Assert.Equal("NarrativeTrace skills for X", iface.GetProperty("shortDescription").GetString());
        Assert.StartsWith("The plugin description. ", iface.GetProperty("longDescription").GetString(), StringComparison.Ordinal);
        Assert.Equal("NarrativeTrace", iface.GetProperty("developerName").GetString());
        Assert.Equal("Developer Tools", iface.GetProperty("category").GetString());
        Assert.Equal(["Read", "Write"], iface.GetProperty("capabilities").EnumerateArray().Select(e => e.GetString()));
        Assert.Equal(["do the thing", "do the other thing"], iface.GetProperty("defaultPrompt").EnumerateArray().Select(e => e.GetString()));
        Assert.Equal("#4A4A4A", iface.GetProperty("brandColor").GetString());
        Assert.Equal("https://narrativetrace.ai", iface.GetProperty("websiteURL").GetString());
        Assert.Equal("./assets/logo.png", iface.GetProperty("logo").GetString());
        Assert.Equal("./assets/composer-icon.png", iface.GetProperty("composerIcon").GetString());
        Assert.False(root.TryGetProperty("version", out _));
    }

    [Fact]
    public void The_packaged_render_differs_from_the_committed_one_only_by_the_version_line()
    {
        var committed = OpenAiPluginJsonRenderer.Render(Listing);
        var packaged = OpenAiPluginJsonRenderer.Render(Listing, "1.2.3");

        Assert.Contains("\"version\": \"1.2.3\"", packaged, StringComparison.Ordinal);
        Assert.Equal(committed, packaged.Replace("  \"version\": \"1.2.3\",\n", string.Empty, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Rejects_a_blank_version(string version)
    {
        Assert.Throws<ArgumentException>(() => OpenAiPluginJsonRenderer.Render(Listing, version));
    }

    [Fact]
    public void Rejects_a_null_listing_and_a_null_version()
    {
        Assert.Throws<ArgumentNullException>(() => OpenAiPluginJsonRenderer.Render(null!));
        Assert.Throws<ArgumentException>(() => OpenAiPluginJsonRenderer.Render(Listing, null!));
    }

    [Fact]
    public void Carries_no_hooks_apps_onboarding_executor_privacy_or_terms_key()
    {
        var json = OpenAiPluginJsonRenderer.Render(Listing, "1.2.3");

        foreach (var key in new[] { "hooks", "apps", "onboardingSkill", "requires_local_executor", "privacyPolicyURL", "termsOfServiceURL" })
        {
            Assert.DoesNotContain($"\"{key}\"", json, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Both_renders_are_byte_identical_every_time_and_end_with_one_newline()
    {
        Assert.Equal(OpenAiPluginJsonRenderer.Render(Listing), OpenAiPluginJsonRenderer.Render(Listing));
        Assert.Equal(OpenAiPluginJsonRenderer.Render(Listing, "1.2.3"), OpenAiPluginJsonRenderer.Render(Listing, "1.2.3"));
        Assert.EndsWith("}\n", OpenAiPluginJsonRenderer.Render(Listing), StringComparison.Ordinal);
        Assert.DoesNotContain('\r', OpenAiPluginJsonRenderer.Render(Listing));
    }

    [Fact]
    public void The_long_description_never_claims_more_than_what_one_run_showed()
    {
        var longDescription = Interface(OpenAiPluginJsonRenderer.Render(Listing)).GetProperty("longDescription").GetString();

        Assert.Contains("reads what one run actually did", longDescription, StringComparison.Ordinal);
        Assert.Contains(".NET SDK", longDescription, StringComparison.Ordinal);
    }

    [Fact]
    public void Escapes_every_character_json_cannot_carry_raw_and_round_trips()
    {
        var hostile = Listing with
        {
            Base = Base with { PluginDescription = "A \"quoted\" \\ name\twith\na newline.", Keywords = ["a\"b"] },
            Category = "cat\"egory",
            DefaultPrompts = ["prom\"pt"],
        };

        var root = JsonDocument.Parse(OpenAiPluginJsonRenderer.Render(hostile)).RootElement;

        Assert.Equal("A \"quoted\" \\ name\twith\na newline.", root.GetProperty("description").GetString());
        Assert.Equal("a\"b", root.GetProperty("keywords")[0].GetString());
        Assert.Equal("cat\"egory", Interface(OpenAiPluginJsonRenderer.Render(hostile)).GetProperty("category").GetString());
        Assert.Equal("prom\"pt", Interface(OpenAiPluginJsonRenderer.Render(hostile)).GetProperty("defaultPrompt")[0].GetString());
    }

    [Fact]
    public void The_real_directory_listing_renders_the_dotnet_name_and_a_display_name_inside_the_ceiling()
    {
        var json = OpenAiPluginJsonRenderer.Render(SkillCatalogue.OpenAiDirectory);

        Assert.Contains("\"name\": \"narrativetrace-dotnet\"", json, StringComparison.Ordinal);
        Assert.Contains("\"displayName\": \"NarrativeTrace for .NET\"", json, StringComparison.Ordinal);
    }
}

public sealed class OpenAiPluginJsonDriftTests
{
    [Fact]
    public void The_committed_manifest_equals_a_fresh_render_of_the_typed_catalogue()
    {
        var path = Path.Combine(TestPaths.RepoRoot(), "src", "NarrativeTrace.Skills.Catalogue", "openai-plugin", "plugin.json");

        Assert.Equal(
            OpenAiPluginJsonRenderer.Render(SkillCatalogue.OpenAiDirectory),
            File.ReadAllText(path),
            ignoreLineEndingDifferences: false);
    }
}
