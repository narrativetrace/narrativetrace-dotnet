// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Skills.Catalogue;
using Xunit;

namespace NarrativeTrace.Skills.Tests;

public sealed class SkillCatalogueTests
{
    [Fact]
    public void Ships_exactly_the_two_frozen_design_skills()
    {
        Assert.Equal(
            ["narrativetrace-doctor", "add-narrative-tracing"],
            SkillCatalogue.Skills.Select(s => s.CanonicalName));
    }

    [Fact]
    public void Every_skill_has_a_unique_canonical_name()
    {
        // The canonical name is the single source for both the rendered directory (on every
        // platform) and the frontmatter name — no separate "claude segment" to keep unique
        // alongside it (skills-design ruling, 2026-09-04, reaffirmed 2026-09-13).
        Assert.Equal(
            SkillCatalogue.Skills.Count,
            SkillCatalogue.Skills.Select(s => s.CanonicalName).Distinct().Count());
    }

    [Fact]
    public void Every_skill_has_at_least_one_step_and_one_always_and_never_rule()
    {
        Assert.All(SkillCatalogue.Skills, skill =>
        {
            Assert.NotEmpty(skill.Steps);
            Assert.NotEmpty(skill.Always);
            Assert.NotEmpty(skill.Never);
        });
    }

    [Fact]
    public void Doctor_skill_flags_the_approval_flow_step_as_unstudied()
    {
        var doctor = SkillCatalogue.Skills.Single(s => s.CanonicalName == "narrativetrace-doctor");

        var flagged = doctor.Steps.Single(step => step.Flag is not null);

        Assert.Equal("unstudied — eval cell pending", flagged.Flag);
    }

    [Fact]
    public void Pro_listings_never_mention_price_or_the_word_paid()
    {
        Assert.All(SkillCatalogue.Pro, listing =>
        {
            Assert.DoesNotContain("paid", listing.Delivers, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("price", listing.Delivers, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("$", listing.Delivers, StringComparison.Ordinal);
        });
    }
}
