// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Skills.Catalogue;
using Xunit;

namespace NarrativeTrace.Skills.Tests;

public sealed class SkillLintsTests
{
    private static readonly Skill CleanSkill = new(
        CanonicalName: "narrativetrace-example",
        SkillClass: SkillClass.Mechanical,
        Description: "Does a thing.",
        WhenToUse: null,
        Fixture: "examples/NarrativeTrace.Examples.SixtySeconds",
        Steps: [new SkillStep("Run it", new CommandStep(["dotnet run"]), Verify: "it runs")],
        Always: [new ReasonedRule("Always something", "because reasons")],
        Never: [new ReasonedRule("Never something", "because reasons")]);

    [Fact]
    public void The_real_catalogue_has_no_description_budget_violations()
    {
        Assert.Empty(SkillLints.DescriptionBudgetViolations(SkillCatalogue.Skills));
    }

    [Fact]
    public void Flags_a_description_over_the_per_skill_budget()
    {
        var oversized = CleanSkill with { Description = new string('x', SkillLints.DescriptionBudget + 1) };

        Assert.Equal(
            ["narrativetrace-example"], SkillLints.DescriptionBudgetViolations([oversized]));
    }

    [Fact]
    public void The_real_catalogue_stays_under_the_catalogue_wide_budget()
    {
        Assert.True(
            SkillLints.CatalogueDescriptionChars(SkillCatalogue.Skills) < SkillLints.CatalogueCharBudget);
    }

    [Fact]
    public void The_real_catalogue_has_no_vocabulary_violations()
    {
        Assert.Empty(SkillLints.VocabularyViolations(SkillCatalogue.Skills));
    }

    [Fact]
    public void The_real_vocabulary_has_no_allowed_tools_violations()
    {
        Assert.Empty(SkillLints.AllowedToolsViolations(CommandVocabulary.AllowedTools));
    }

    [Fact]
    public void Flags_a_vocabulary_entry_already_written_as_a_rendered_platform_pattern()
    {
        var violations = SkillLints.AllowedToolsViolations(["git", "Bash(git *)"]);

        Assert.Equal(["Bash(git *)"], violations);
    }

    [Fact]
    public void Flags_a_command_outside_the_dotnet_git_vocabulary()
    {
        var offending = CleanSkill with
        {
            Steps = [new SkillStep("Do it", new CommandStep(["npm install"]))],
        };

        var violations = SkillLints.VocabularyViolations([offending]);

        Assert.Single(violations);
        Assert.Contains("npm install", violations[0], StringComparison.Ordinal);
    }

    [Fact]
    public void Steps_without_verify_are_reported_by_skill_and_title()
    {
        var noVerify = CleanSkill with
        {
            Steps = [new SkillStep("Read it", new CommandStep(["dotnet run"]))],
        };

        var reported = SkillLints.StepsWithoutVerify([noVerify]);

        Assert.Equal(["narrativetrace-example: Read it"], reported);
    }

    [Fact]
    public void The_real_catalogue_has_no_citation_violations()
    {
        // AGENTS.md is a real, stable file at the repo root, the installer's own write target —
        // the production gate scans the real tree and already allows citing it.
        var repoDocs = new HashSet<string> { "README.md", "AGENTS.md" };

        Assert.Empty(SkillLints.CitationViolations(SkillCatalogue.Skills, repoDocs));
    }

    [Fact]
    public void Flags_a_section_mark_citation()
    {
        var offending = CleanSkill with { Description = "See §4.5 for details." };

        var violations = SkillLints.CitationViolations([offending], new HashSet<string>());

        Assert.Contains(violations, v => v.Contains("section-mark", StringComparison.Ordinal));
    }

    [Fact]
    public void Flags_an_external_markdown_filename_not_in_the_repo_set()
    {
        var offending = CleanSkill with { Description = "See private-plan.md for context." };

        var violations = SkillLints.CitationViolations([offending], new HashSet<string>());

        Assert.Contains(violations, v => v.Contains("private-plan.md", StringComparison.Ordinal));
    }

    [Fact]
    public void Allows_a_markdown_filename_present_in_the_repo_set()
    {
        var clean = CleanSkill with { Description = "See README.md for context." };

        Assert.Empty(SkillLints.CitationViolations([clean], new HashSet<string> { "README.md" }));
    }

    [Fact]
    public void The_real_pro_listings_agree_with_the_feature_guide()
    {
        var featureGuide = File.ReadAllText(
            Path.Combine(TestPaths.RepoRoot(), "documentation", "feature-guide.md"));

        Assert.Empty(SkillLints.ListingsDisagreeingWithFeatureGuide(SkillCatalogue.Pro, featureGuide));
    }

    [Fact]
    public void Flags_a_listing_whose_status_text_is_absent_from_the_feature_guide()
    {
        var listing = new ProListing(
            "narrativetrace-example", "prompt", "delivers", null, null, "planned", "not-in-the-guide");

        var violations = SkillLints.ListingsDisagreeingWithFeatureGuide([listing], "some other text");

        Assert.Equal(["narrativetrace-example"], violations);
    }


    [Fact]
    public void A_step_condition_is_prose_the_citation_lint_reads()
    {
        var offending = CleanSkill with
        {
            Steps = [CleanSkill.Steps[0] with { Condition = "see §4 for the rule" }],
        };

        var violations = SkillLints.CitationViolations([offending], new HashSet<string>());

        Assert.Contains(violations, v => v.Contains("section-mark", StringComparison.Ordinal) && v.Contains("condition", StringComparison.Ordinal));
    }

    [Fact]
    public void A_reference_section_is_prose_the_citation_lint_reads()
    {
        var offending = CleanSkill with { Sections = [new SkillSection("Shapes", "see §4 for the rule")] };

        var violations = SkillLints.CitationViolations([offending], new HashSet<string>());

        Assert.Contains(violations, v => v.Contains("section-mark", StringComparison.Ordinal) && v.Contains("section 'Shapes'", StringComparison.Ordinal));
    }

    private static Skill ReportingSkill(IReadOnlyList<string>? allowedTools) => CleanSkill with
    {
        Steps =
        [
            new SkillStep(
                "Report it",
                new CommandStep(["dotnet tool run dotnet-narrativetrace feedback url --category library"])),
        ],
        AllowedTools = allowedTools,
    };

    [Fact]
    public void Flags_a_skill_that_pre_approves_its_own_reporting_command()
    {
        var violations = SkillLints.PublishingNotPreApprovedViolations([ReportingSkill(["dotnet"])]);

        var violation = Assert.Single(violations);
        Assert.Contains("pre-approves its own publishing command", violation, StringComparison.Ordinal);
        Assert.Contains("let the harness ask", violation, StringComparison.Ordinal);
        Assert.StartsWith("narrativetrace-example:", violation, StringComparison.Ordinal);
    }

    [Fact]
    public void A_skill_that_inherits_the_vocabulary_pre_approves_its_reporting_command()
    {
        // Omitting the declaration means "the whole vocabulary", which includes dotnet — the
        // dangerous default is exactly what the lint exists to catch.
        Assert.Single(SkillLints.PublishingNotPreApprovedViolations([ReportingSkill(null)]));
    }

    [Fact]
    public void Accepts_the_same_skill_declaring_no_pre_approved_tool()
    {
        Assert.Empty(SkillLints.PublishingNotPreApprovedViolations([ReportingSkill([])]));
    }

    [Fact]
    public void Ignores_a_skill_whose_commands_publish_nothing()
    {
        Assert.Empty(SkillLints.PublishingNotPreApprovedViolations([CleanSkill]));
    }

    [Fact]
    public void The_publishing_match_is_a_word_so_a_near_miss_is_not_a_report()
    {
        var nearMiss = CleanSkill with
        {
            Steps = [new SkillStep("Run it", new CommandStep(["dotnet run --project feedbackless"]))],
        };

        Assert.Empty(SkillLints.PublishingNotPreApprovedViolations([nearMiss]));
    }

    [Fact]
    public void The_real_catalogue_pre_approves_no_publishing_command()
    {
        Assert.Empty(SkillLints.PublishingNotPreApprovedViolations(SkillCatalogue.Skills));
    }

    private static Skill PromotingSkill(IReadOnlyList<string>? allowedTools, string command = "dotnet tool run dotnet-narrativetrace approve") =>
        CleanSkill with
        {
            Steps = [new SkillStep("Promote what was shown", new CommandStep([command]))],
            AllowedTools = allowedTools,
        };

    [Theory]
    [InlineData("dotnet")]
    [InlineData("git")]
    public void Flags_a_promoting_skill_that_pre_approves_any_tool(string tool)
    {
        var violation = Assert.Single(SkillLints.PromotionNotPreApprovedViolations([PromotingSkill([tool])]));

        Assert.StartsWith("narrativetrace-example:", violation, StringComparison.Ordinal);
        Assert.Contains("promotes an approval baseline", violation, StringComparison.Ordinal);
        Assert.Contains($"\"{tool}\"", violation, StringComparison.Ordinal);
    }

    [Fact]
    public void A_promoting_skill_that_inherits_the_vocabulary_is_flagged_once_per_tool()
    {
        Assert.Equal(2, SkillLints.PromotionNotPreApprovedViolations([PromotingSkill(null)]).Count);
    }

    [Fact]
    public void Accepts_a_promoting_skill_that_pre_approves_nothing()
    {
        Assert.Empty(SkillLints.PromotionNotPreApprovedViolations([PromotingSkill([])]));
    }

    [Theory]
    [InlineData("dotnet tool run dotnet-narrativetrace approved")]
    [InlineData("dotnet tool run dotnet-narrativetrace doctor --json")]
    [InlineData("dotnet test --filter Approve")]
    public void A_command_that_does_not_run_the_approve_verb_is_not_a_promotion(string command)
    {
        Assert.Empty(SkillLints.PromotionNotPreApprovedViolations([PromotingSkill(["dotnet"], command)]));
    }

    [Fact]
    public void The_real_catalogue_pre_approves_no_promotion()
    {
        Assert.Empty(SkillLints.PromotionNotPreApprovedViolations(SkillCatalogue.Skills));
    }
}
