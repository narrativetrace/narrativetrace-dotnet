// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Skills.Catalogue;
using Xunit;

namespace NarrativeTrace.Skills.Tests;

/// <summary>
/// The three skill-wording gaps the OpenAI package work surfaced (design note, "The three wording
/// gaps"): a plugin install is invisible to the doctor, verify opens values on a data symptom as well
/// as on a shape mismatch, and one trace is one path.
/// </summary>
public sealed class WordingGapsTests
{
    private static Skill Named(string name) => SkillCatalogue.Skills.Single(s => s.CanonicalName == name);

    private static SkillStep VerifyStep(string titleStart) =>
        Named("narrativetrace-verify").Steps.Single(s => s.Title.StartsWith(titleStart, StringComparison.Ordinal));

    [Fact]
    public void The_doctor_description_names_the_project_scoped_skills_check_and_still_fits_the_budget()
    {
        var doctor = Named("narrativetrace-doctor");

        Assert.Contains("whether the NarrativeTrace agent skills are installed in this project and current", doctor.Description, StringComparison.Ordinal);
        Assert.DoesNotContain(doctor.CanonicalName, SkillLints.DescriptionBudgetViolations(SkillCatalogue.Skills));
    }

    [Fact]
    public void The_doctor_triggers_explain_that_a_personal_plugin_install_is_invisible_to_the_check()
    {
        var whenToUse = Named("narrativetrace-doctor").WhenToUse;

        Assert.Contains("installed these skills as a personal plugin in their agent", whenToUse, StringComparison.Ordinal);
        Assert.Contains("a plugin install is invisible to this check by design", whenToUse, StringComparison.Ordinal);
        Assert.Contains("only a project install reaches teammates and other agents", whenToUse, StringComparison.Ordinal);
    }

    [Fact]
    public void Verify_step_5_opens_the_one_span_a_data_symptom_points_at_even_when_the_shape_matches()
    {
        var condition = VerifyStep("Open values on the span").Condition;

        Assert.Equal(NarrativeTraceVerifySkill.OpenValuesWhen, condition);
        Assert.Contains("or the shape matches but a symptom points at data", condition, StringComparison.Ordinal);
        Assert.Contains("an assertion failed on a value, the output is wrong", condition, StringComparison.Ordinal);
        Assert.Contains("then open the one span the symptom points at; otherwise go on to the pin", condition, StringComparison.Ordinal);
    }

    [Fact]
    public void Verify_step_6_also_fixes_when_the_value_read_in_step_5_was_wrong()
    {
        var condition = VerifyStep("Fix, re-run").Condition;

        Assert.Equal(NarrativeTraceVerifySkill.FixAndRerunWhen, condition);
        Assert.Contains("or the value read in step 5 was wrong; otherwise go on to the pin", condition, StringComparison.Ordinal);
    }

    [Fact]
    public void The_two_verify_conditions_sit_at_steps_5_and_6_the_numbers_their_wording_cites()
    {
        var steps = Named("narrativetrace-verify").Steps.ToList();

        Assert.StartsWith("Open values on the span", steps[4].Title, StringComparison.Ordinal);
        Assert.StartsWith("Fix, re-run", steps[5].Title, StringComparison.Ordinal);
    }

    [Fact]
    public void The_verify_report_is_bound_to_the_path_this_run_took_and_names_what_it_did_not_exercise()
    {
        var verify = VerifyStep("Report what the trace showed").Verify;

        Assert.Contains("two sentences on what the trace showed on the path this run took, every claim citing the span id it rests on", verify, StringComparison.Ordinal);
        Assert.Contains("the report names the branches and inputs this run did not exercise, because one trace shows one path, not every execution", verify, StringComparison.Ordinal);
        Assert.Contains("'tests pass' alone is not the report", verify, StringComparison.Ordinal);
    }

    [Fact]
    public void The_explain_not_observed_clause_names_the_branches_this_run_did_not_take()
    {
        var verify = Named("narrativetrace-explain").Steps.Single(s => s.Title.StartsWith("Explain the run by span id", StringComparison.Ordinal)).Verify;

        Assert.EndsWith("only wrapped or intercepted code has one, and the branches this run did not take", verify, StringComparison.Ordinal);
    }

    [Fact]
    public void The_rendered_pages_of_both_flavours_carry_the_new_sentences()
    {
        var repo = TestPaths.RepoRoot();
        foreach (var flavour in new[] { ".claude", ".agents" })
        {
            var verify = File.ReadAllText(Path.Combine(repo, flavour, "skills", "narrativetrace-verify", "SKILL.md"));
            Assert.Contains("one trace shows one path, not every execution", verify, StringComparison.Ordinal);
            Assert.Contains("the shape matches but a symptom points at data", verify, StringComparison.Ordinal);
        }
    }
}
