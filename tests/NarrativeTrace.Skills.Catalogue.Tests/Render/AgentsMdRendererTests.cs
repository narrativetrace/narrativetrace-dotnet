// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Skills.Render;
using Xunit;

namespace NarrativeTrace.Skills.Tests.Render;

public sealed class AgentsMdRendererTests
{
    private static readonly Skill Skill = new(
        "narrativetrace-example", SkillClass.Mechanical, "Does a thing.", null,
        "examples/NarrativeTrace.Examples.SixtySeconds",
        [new SkillStep("Run it", new CommandStep(["dotnet run"]))],
        [new ReasonedRule("Always", "reason")], [new ReasonedRule("Never", "reason")]);

    private static readonly ProListing Listing = new(
        "narrativetrace-pro-example", "prompt", "does a pro thing", null, null, "shipped", "status text");

    [Fact]
    public void Section_lists_every_skill_and_pro_listing()
    {
        var section = AgentsMdRenderer.RenderSection([Skill], [Listing]);

        Assert.Contains("- `narrativetrace-example` — Does a thing.", section, StringComparison.Ordinal);
        Assert.Contains(
            "- `narrativetrace-pro-example` (Pro, shipped) — does a pro thing",
            section, StringComparison.Ordinal);
    }

    [Fact]
    public void Splice_appends_a_new_managed_section_when_none_exists()
    {
        var result = AgentsMdRenderer.Splice("# AGENTS\n\nSome content.\n", "body\n");

        Assert.Contains(AgentsMdRenderer.BeginMarker, result, StringComparison.Ordinal);
        Assert.Contains(AgentsMdRenderer.EndMarker, result, StringComparison.Ordinal);
        Assert.Contains("Some content.", result, StringComparison.Ordinal);
        Assert.Contains("body\n", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Splice_replaces_an_existing_managed_section_in_place()
    {
        var original =
            $"# AGENTS\n\nbefore\n\n{AgentsMdRenderer.BeginMarker}\nold body\n{AgentsMdRenderer.EndMarker}\n\nafter\n";

        var result = AgentsMdRenderer.Splice(original, "new body\n");

        Assert.Contains("before", result, StringComparison.Ordinal);
        Assert.Contains("after", result, StringComparison.Ordinal);
        Assert.Contains("new body", result, StringComparison.Ordinal);
        Assert.DoesNotContain("old body", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Splice_is_idempotent_when_applied_twice_with_the_same_section()
    {
        var once = AgentsMdRenderer.Splice("# AGENTS\n", "body\n");

        var twice = AgentsMdRenderer.Splice(once, "body\n");

        Assert.Equal(once, twice);
    }
}
