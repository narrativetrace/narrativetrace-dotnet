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
        var repoDocs = new HashSet<string> { "README.md" };

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

}
