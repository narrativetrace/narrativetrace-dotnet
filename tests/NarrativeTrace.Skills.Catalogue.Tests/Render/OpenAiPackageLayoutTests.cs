// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Skills.Catalogue;
using NarrativeTrace.Skills.Render;
using Xunit;

namespace NarrativeTrace.Skills.Tests.Render;

public sealed class OpenAiPackageLayoutTests
{
    [Fact]
    public void Lists_the_manifest_every_skill_page_and_the_two_assets_and_nothing_else()
    {
        var entries = OpenAiPackageLayout.Entries(SkillCatalogue.Skills, SkillCatalogue.OpenAiDirectory);

        Assert.Equal(SkillCatalogue.Skills.Count + 3, entries.Count);
        Assert.Contains("plugin.json", entries);
        Assert.Contains("assets/logo.png", entries);
        Assert.Contains("assets/composer-icon.png", entries);
        Assert.All(SkillCatalogue.Skills, skill => Assert.Contains($"skills/{skill.CanonicalName}/SKILL.md", entries));
    }

    [Fact]
    public void Entries_are_sorted_ordinally()
    {
        var entries = OpenAiPackageLayout.Entries(SkillCatalogue.Skills, SkillCatalogue.OpenAiDirectory);

        Assert.Equal(entries.OrderBy(e => e, StringComparer.Ordinal), entries);
    }

    [Fact]
    public void Rejects_a_null_listing_and_a_null_skill_list()
    {
        Assert.Throws<ArgumentNullException>(() => OpenAiPackageLayout.Entries(SkillCatalogue.Skills, null!));
        Assert.Throws<ArgumentNullException>(() => OpenAiPackageLayout.Entries(null!, SkillCatalogue.OpenAiDirectory));
    }

    [Fact]
    public void An_empty_skill_list_still_carries_the_plugin_and_the_assets()
    {
        var entries = OpenAiPackageLayout.Entries([], SkillCatalogue.OpenAiDirectory);

        Assert.Equal(["assets/composer-icon.png", "assets/logo.png", "plugin.json"], entries);
    }

    [Fact]
    public void The_asset_entries_are_the_manifests_own_paths_without_the_dot_slash()
    {
        Assert.Equal("assets/logo.png", OpenAiPackageLayout.LogoEntry);
        Assert.Equal("assets/composer-icon.png", OpenAiPackageLayout.ComposerIconEntry);
    }
}
