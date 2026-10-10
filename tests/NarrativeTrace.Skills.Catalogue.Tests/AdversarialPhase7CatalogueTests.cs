// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Skills;
using NarrativeTrace.Skills.Render;
using Xunit;

namespace NarrativeTrace.Skills.Catalogue.Tests;

/// <summary>
/// Adversarial probes of <see cref="SkillSection"/> validation, the section block's place in a rendered
/// page, and the promotion lint's command matching.
/// </summary>
public sealed class AdversarialPhase7CatalogueTests
{
    private static Skill SkillWith(string command, IReadOnlyList<string>? allowedTools = null) =>
        new(
            "narrativetrace-probe",
            SkillClass.Mechanical,
            "Probe the promotion lint. Use when asked to probe.",
            null,
            "fixtures/probe",
            [new SkillStep("Promote the reviewed copy", new CommandStep([command]), "the approved file exists")],
            [new ReasonedRule("Show the diff first.", "a reviewed copy is the contract")],
            [new ReasonedRule("Never promote unseen.", "an unseen promotion hides a change")],
            allowedTools);

    private static Skill PageWithSection(params SkillSection[] sections)
    {
        var skill = SkillWith("dotnet tool run dotnet-narrativetrace doctor", []);
        return skill with { Sections = sections };
    }

    [Theory]
    [InlineData("  ## Reading a trace")]
    [InlineData(" # Reading a trace")]
    public void A_section_heading_with_leading_space_before_a_heading_marker_is_refused(string heading)
    {
        Assert.Throws<ArgumentException>(() => new SkillSection(heading, "body text"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Reading\ntrace")]
    [InlineData("Reading\rtrace")]
    public void A_section_heading_that_is_blank_or_spans_lines_is_refused(string heading)
    {
        Assert.Throws<ArgumentException>(() => new SkillSection(heading, "body text"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("\n\t ")]
    public void A_section_body_that_is_null_or_blank_is_refused(string? markdown)
    {
        Assert.Throws<ArgumentException>(() => new SkillSection("Reading a trace", markdown!));
    }

    [Fact]
    public void A_section_body_with_crlf_trailing_newlines_renders_with_one_blank_line_before_the_next_block()
    {
        var page = ClaudeSkillRenderer.Render(
            PageWithSection(new SkillSection("Reading a trace", "The body.\r\n\r\n")), ".");

        Assert.Contains("## Reading a trace\n\nThe body.\n\n", page);
    }

    [Fact]
    public void Sections_render_after_the_steps_and_before_the_always_rules()
    {
        var page = ClaudeSkillRenderer.Render(PageWithSection(new SkillSection("Reading a trace", "Ids are positions.")), ".");

        var section = page.IndexOf("## Reading a trace", StringComparison.Ordinal);
        Assert.True(section > page.IndexOf("Promote the reviewed copy", StringComparison.Ordinal));
        Assert.True(section < page.IndexOf("## Always", StringComparison.Ordinal));
    }

    [Fact]
    public void Sections_render_in_declaration_order()
    {
        var page = ClaudeSkillRenderer.Render(
            PageWithSection(new SkillSection("First block", "one"), new SkillSection("Second block", "two")), ".");

        Assert.True(page.IndexOf("## First block", StringComparison.Ordinal)
            < page.IndexOf("## Second block", StringComparison.Ordinal));
    }

    [Fact]
    public void A_page_without_sections_has_no_extra_heading_between_steps_and_always()
    {
        var page = ClaudeSkillRenderer.Render(SkillWith("dotnet tool run dotnet-narrativetrace doctor", []), ".");

        var steps = page.IndexOf("Promote the reviewed copy", StringComparison.Ordinal);
        var always = page.IndexOf("## Always", StringComparison.Ordinal);
        Assert.DoesNotContain("\n## ", page[steps..always]);
    }

    [Theory]
    [InlineData("dotnet-narrativetrace approve --dir x")]
    [InlineData("dotnet tool run dotnet-narrativetrace approve")]
    [InlineData("dotnet-narrativetrace\tapprove")]
    [InlineData("echo ok && dotnet-narrativetrace approve")]
    public void The_promotion_lint_flags_every_spelling_of_the_approve_verb_that_writes_a_baseline(string command)
    {
        var violations = SkillLints.PromotionNotPreApprovedViolations([SkillWith(command)]);

        Assert.NotEmpty(violations);
    }

    [Theory]
    [InlineData("dotnet-narrativetrace approve-all")]
    [InlineData("dotnet-narrativetrace doctor approve")]
    [InlineData("dotnet-narrativetrace approved --dir x")]
    public void The_promotion_lint_ignores_commands_that_merely_contain_the_word_approve(string command)
    {
        var violations = SkillLints.PromotionNotPreApprovedViolations([SkillWith(command)]);

        Assert.Empty(violations);
    }

    [Fact]
    public void The_promotion_lint_flags_the_windows_executable_spelling_of_the_approve_verb()
    {
        var violations = SkillLints.PromotionNotPreApprovedViolations(
            [SkillWith("dotnet-narrativetrace.exe approve --dir x")]);

        Assert.NotEmpty(violations);
    }

    [Fact]
    public void A_promoting_skill_that_declares_no_pre_approved_tools_is_not_flagged()
    {
        var violations = SkillLints.PromotionNotPreApprovedViolations(
            [SkillWith("dotnet-narrativetrace approve --dir x", [])]);

        Assert.Empty(violations);
    }

    [Fact]
    public void A_promoting_skill_with_the_default_tool_list_is_flagged_once_per_pre_approved_tool()
    {
        var violations = SkillLints.PromotionNotPreApprovedViolations(
            [SkillWith("dotnet-narrativetrace approve --dir x")]);

        Assert.Equal(CommandVocabulary.AllowedTools.Count, violations.Count);
    }
}
