// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Skills.Catalogue;
using NarrativeTrace.Skills;
using Xunit;

namespace NarrativeTrace.Skills.Tests;

public sealed class SkillCatalogueTests
{
    [Fact]
    public void Ships_exactly_the_design_skills()
    {
        Assert.Equal(
            ["narrativetrace-doctor", "add-narrative-tracing", "narrativetrace-feedback", "add-narrativetrace-clarity",
                "narrativetrace-verify", "narrativetrace-debug", "narrativetrace-explain"],
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
    public void The_gated_skills_pre_approve_no_tool_and_every_other_skill_keeps_the_vocabulary()
    {
        string[] gated = ["narrativetrace-feedback", "narrativetrace-verify", "narrativetrace-debug", "narrativetrace-explain"];
        Assert.All(SkillCatalogue.Skills.Where(s => gated.Contains(s.CanonicalName)), skill => Assert.Empty(skill.PreApprovedTools));
        Assert.All(
            SkillCatalogue.Skills.Where(s => !gated.Contains(s.CanonicalName)),
            skill => Assert.Equal(CommandVocabulary.AllowedTools, skill.PreApprovedTools));
    }

    [Fact]
    public void The_feedback_skill_asks_before_it_prints_the_way_to_file_and_never_sends_in_a_turn_that_asked()
    {
        var skill = SkillCatalogue.Skills.Single(s => s.CanonicalName == "narrativetrace-feedback");

        var titles = skill.Steps.Select(step => step.Title).ToList();

        Assert.True(
            titles.FindIndex(t => t.StartsWith("Show the whole draft", StringComparison.Ordinal))
            < titles.FindIndex(t => t.StartsWith("Ask once", StringComparison.Ordinal)));
        Assert.True(
            titles.FindIndex(t => t.StartsWith("Ask once", StringComparison.Ordinal))
            < titles.FindIndex(t => t.StartsWith("Print the way to file", StringComparison.Ordinal)));
        Assert.Contains(skill.Never, rule => rule.Rule.StartsWith("Never file in the turn that asked", StringComparison.Ordinal));
        Assert.Equal(
            [FeedbackCommands.PrintUrl],
            skill.Steps.SelectMany(step => step.Body is CommandStep c ? c.Commands : [])
                .Where(command => command.Contains("feedback url", StringComparison.Ordinal)));
    }

    [Fact]
    public void The_feedback_approval_step_says_that_showing_the_url_is_the_filing()
    {
        var skill = SkillCatalogue.Skills.Single(s => s.CanonicalName == "narrativetrace-feedback");

        var approval = skill.Steps.Single(step => step.Title.StartsWith("Ask once", StringComparison.Ordinal));

        Assert.Contains(
            "Do not print the issue URL or run gh before the user says yes — showing the URL is the filing.",
            approval.Verify,
            StringComparison.Ordinal);
    }

    [Fact]
    public void The_doctor_skill_points_at_the_feedback_skill_and_the_target_exists()
    {
        var doctor = SkillCatalogue.Skills.Single(s => s.CanonicalName == "narrativetrace-doctor");

        var pointer = Assert.Single(doctor.Always, rule => rule.Rule.Contains("narrativetrace-feedback", StringComparison.Ordinal));

        Assert.StartsWith("If a check is wrong", pointer.Rule, StringComparison.Ordinal);
        Assert.Contains(SkillCatalogue.Skills, skill => skill.CanonicalName == "narrativetrace-feedback");
    }

    [Fact]
    public void Add_narrative_tracing_ends_by_previewing_the_skills_install_never_applying_it()
    {
        var skill = SkillCatalogue.Skills.Single(s => s.CanonicalName == "add-narrative-tracing");

        var lastStep = skill.Steps[^1];
        Assert.Equal("Install the skills for next time", lastStep.Title);
        var body = Assert.IsType<CommandStep>(lastStep.Body);
        Assert.All(body.Commands, command => Assert.Contains("--dry-run", command));

        Assert.All(
            skill.Steps.SelectMany(step => step.Body is CommandStep commands ? commands.Commands : []),
            command =>
            {
                if (command.Contains("narrativetrace init", StringComparison.Ordinal))
                {
                    Assert.Contains("--dry-run", command, StringComparison.Ordinal);
                }
            });
    }

    [Fact]
    public void The_clarity_skill_runs_the_gate_then_renames_then_runs_it_again_until_it_is_clean()
    {
        var skill = SkillCatalogue.Skills.Single(s => s.CanonicalName == "add-narrativetrace-clarity");

        var commands = skill.Steps.SelectMany(step => step.Body is CommandStep c ? c.Commands : []).ToList();

        Assert.Contains(ClarityCommands.Scan, commands);
        Assert.Contains(ClarityCommands.Check, commands);
        var closing = skill.Steps.Single(step => step.Title.StartsWith("Apply the report's renames", StringComparison.Ordinal));
        Assert.Equal(
            [ClarityCommands.Build, ClarityCommands.Scan, ClarityCommands.Check],
            Assert.IsType<CommandStep>(closing.Body).Commands);
    }

    [Fact]
    public void The_clarity_skill_forbids_passing_the_gate_by_removing_the_evidence()
    {
        var skill = SkillCatalogue.Skills.Single(s => s.CanonicalName == "add-narrativetrace-clarity");

        Assert.Contains(skill.Never, rule => rule.Rule.Contains("delete", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(skill.Never, rule => rule.Rule.Contains("threshold", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(skill.Never, rule => rule.Rule.Contains("published", StringComparison.OrdinalIgnoreCase));
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
