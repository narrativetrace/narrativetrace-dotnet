// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Skills.Catalogue;
using NarrativeTrace.Skills.Render;
using Xunit;

namespace NarrativeTrace.Skills.Tests.Render;

public sealed class CatalogueJsonRendererTests
{
    private static readonly Skill First = new(
        CanonicalName: "narrativetrace-example",
        SkillClass: SkillClass.Mechanical,
        Description: "Does a thing.",
        WhenToUse: "When a thing needs doing.",
        Fixture: "examples/NarrativeTrace.Examples.SixtySeconds",
        Steps: [],
        Always: [],
        Never: []);

    private static readonly Skill Second = new(
        CanonicalName: "narrativetrace-other",
        SkillClass: SkillClass.Guided,
        Description: "Does another thing.",
        WhenToUse: null,
        Fixture: "examples/NarrativeTrace.Examples.SixtySeconds",
        Steps: [],
        Always: [],
        Never: []);

    [Fact]
    public void Carries_the_dotnet_runtime_literal()
    {
        var rendered = CatalogueJsonRenderer.Render([First]);

        Assert.Contains("\"runtime\": \"dotnet\"", rendered, StringComparison.Ordinal);
    }

    [Fact]
    public void Lists_every_skill_with_name_and_description()
    {
        var rendered = CatalogueJsonRenderer.Render([First, Second]);

        Assert.Contains("\"name\": \"narrativetrace-example\"", rendered, StringComparison.Ordinal);
        Assert.Contains("\"description\": \"Does a thing.\"", rendered, StringComparison.Ordinal);
        Assert.Contains("\"name\": \"narrativetrace-other\"", rendered, StringComparison.Ordinal);
        Assert.Contains("\"description\": \"Does another thing.\"", rendered, StringComparison.Ordinal);
    }

    [Fact]
    public void Points_each_skill_at_its_agents_and_claude_carrier_relative_paths()
    {
        var rendered = CatalogueJsonRenderer.Render([First]);

        Assert.Contains(
            "\"agents\": \"agents/narrativetrace-example/SKILL.md\"", rendered, StringComparison.Ordinal);
        Assert.Contains(
            "\"claude\": \"claude/narrativetrace-example/SKILL.md\"", rendered, StringComparison.Ordinal);
    }

    [Fact]
    public void Carries_no_version_literal()
    {
        var rendered = CatalogueJsonRenderer.Render([First, Second]);

        Assert.DoesNotMatch(@"\d+\.\d+\.\d+", rendered);
    }

    [Fact]
    public void Uses_lf_line_endings_and_ends_with_exactly_one_trailing_newline()
    {
        var rendered = CatalogueJsonRenderer.Render([First]);

        Assert.DoesNotContain("\r", rendered, StringComparison.Ordinal);
        Assert.EndsWith("}\n", rendered, StringComparison.Ordinal);
        Assert.False(rendered.EndsWith("}\n\n", StringComparison.Ordinal));
    }

    [Fact]
    public void Rendering_the_same_catalogue_twice_is_byte_identical()
    {
        var first = CatalogueJsonRenderer.Render([First, Second]);
        var second = CatalogueJsonRenderer.Render([First, Second]);

        Assert.Equal(first, second, StringComparer.Ordinal);
    }
}
