// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Text.Json;
using System.Text.RegularExpressions;
using NarrativeTrace.Skills.Catalogue;
using NarrativeTrace.Skills.Render;
using Xunit;

namespace NarrativeTrace.Skills.Tests.Render;

/// <summary>
/// Checks the rendered <c>plugin.json</c> against the vendor's own plugin schema shape. CI never
/// fetches the schema itself (the portal's own validation on upload is the by-hand check), so this is
/// the offline stand-in: every top-level key is one the schema allows, the two required keys are
/// present, <c>name</c> matches the schema's own pattern, the asset paths are relative to the ZIP root,
/// and the skills-only rules (no <c>version</c> in the committed file, no <c>hooks</c>/<c>apps</c>) hold.
/// </summary>
public sealed class OpenAiPluginShapeTests
{
    /// <summary>The schema's ten top-level keys (agent-plugins.org/schemas/1.0.0/plugin.schema.json).</summary>
    private static readonly string[] SchemaTopLevelKeys =
    [
        "$schema", "name", "version", "description", "author", "homepage", "repository", "license", "keywords", "extensions",
    ];

    /// <summary>
    /// Copied from the schema's <c>name</c> pattern (agent-plugins.org/schemas/1.0.0/plugin.schema.json):
    /// lower-case alphanumerics, <c>.</c> and <c>-</c>, never starting or ending with a separator and never
    /// carrying <c>--</c> or <c>..</c>.
    /// </summary>
    private static readonly Regex NamePattern = new(
        @"^(?!.*(?:--|\.\.))[a-z0-9](?:[a-z0-9.-]*[a-z0-9])?$", RegexOptions.None, TimeSpan.FromSeconds(1));

    private static JsonElement Committed() =>
        JsonDocument.Parse(OpenAiPluginJsonRenderer.Render(SkillCatalogue.OpenAiDirectory)).RootElement;

    [Fact]
    public void Every_top_level_key_is_one_the_schema_allows()
    {
        Assert.All(Committed().EnumerateObject().Select(p => p.Name), key => Assert.Contains(key, SchemaTopLevelKeys));
    }

    [Fact]
    public void The_two_required_keys_are_present()
    {
        var root = Committed();

        Assert.True(root.TryGetProperty("$schema", out _));
        Assert.True(root.TryGetProperty("name", out _));
    }

    [Theory]
    [InlineData("narrativetrace-dotnet", true)]
    [InlineData("a", true)]
    [InlineData("a.b-c", true)]
    [InlineData("-a", false)]
    [InlineData("a-", false)]
    [InlineData("a--b", false)]
    [InlineData("a..b", false)]
    [InlineData("A", false)]
    [InlineData("a b", false)]
    public void The_schemas_name_pattern_accepts_and_refuses_what_the_vendor_does(string name, bool valid)
    {
        Assert.Equal(valid, NamePattern.IsMatch(name));
    }

    [Fact]
    public void The_listings_name_matches_the_schemas_pattern()
    {
        Assert.Matches(NamePattern, Committed().GetProperty("name").GetString()!);
    }

    [Fact]
    public void The_committed_file_carries_no_version_key()
    {
        Assert.False(Committed().TryGetProperty("version", out _));
    }

    [Fact]
    public void Carries_no_hooks_or_apps_key_at_any_depth()
    {
        var json = OpenAiPluginJsonRenderer.Render(SkillCatalogue.OpenAiDirectory);

        Assert.DoesNotContain("\"hooks\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"apps\"", json, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("logo", "./assets/logo.png")]
    [InlineData("composerIcon", "./assets/composer-icon.png")]
    public void The_two_asset_paths_are_dot_slash_relative_and_stay_inside_the_zip_root(string key, string expected)
    {
        var path = Committed().GetProperty("extensions").GetProperty("com.openai").GetProperty("interface").GetProperty(key).GetString()!;

        Assert.Equal(expected, path);
        Assert.StartsWith("./assets/", path, StringComparison.Ordinal);
        Assert.DoesNotContain("..", path, StringComparison.Ordinal);
    }
}
