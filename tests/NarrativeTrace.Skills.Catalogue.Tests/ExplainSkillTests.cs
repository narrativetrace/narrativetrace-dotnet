// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Skills.Catalogue;
using NarrativeTrace.Skills.Render;
using Xunit;

namespace NarrativeTrace.Skills.Tests;

/// <summary>
/// <c>narrativetrace-explain</c>: answers "what does my application actually do" by running one scenario
/// and reading its trace — or, when the source already answers, says so and stops.
/// </summary>
public sealed class ExplainSkillTests
{
    private static Skill Explain => SkillCatalogue.Skills.Single(s => s.CanonicalName == "narrativetrace-explain");

    private static Skill Debug => SkillCatalogue.Skills.Single(s => s.CanonicalName == "narrativetrace-debug");

    private static IReadOnlyList<string> Titles => [.. Explain.Steps.Select(s => s.Title)];

    private static SkillStep Step(string title) => Explain.Steps.Single(s => s.Title == title);

    [Fact]
    public void It_ships_in_the_catalogue_right_after_the_debug_skill()
    {
        var names = SkillCatalogue.Skills.Select(s => s.CanonicalName).ToList();

        Assert.Equal(names.IndexOf("narrativetrace-debug") + 1, names.IndexOf("narrativetrace-explain"));
        Assert.Equal(SkillClass.Guided, Explain.SkillClass);
    }

    [Fact]
    public void The_first_step_decides_whether_the_source_already_answers_the_question()
    {
        var first = Explain.Steps[0];

        Assert.Equal("Decide whether the source already answers it, and say so", first.Title);
        Assert.Equal(new CommandStep([]), first.Body);
        Assert.Contains("before anything is installed or run", first.Verify, StringComparison.Ordinal);
        Assert.Contains("'reading the source:", first.Verify, StringComparison.Ordinal);
        Assert.Contains("'tracing:", first.Verify, StringComparison.Ordinal);
        Assert.Contains("no install and no run", first.Verify, StringComparison.Ordinal);
        Assert.Contains("the skill ends there", first.Verify, StringComparison.Ordinal);
    }

    [Fact]
    public void The_cost_rule_is_an_always_rule_that_carries_its_own_reason()
    {
        var rule = Assert.Single(Explain.Always, r => r == NarrativeTraceExplainSkill.FromTheSource);

        Assert.Equal("Answer from the source when ten lines of source answer the question", rule.Rule);
        Assert.Contains("cost the user time and tokens", rule.Reason, StringComparison.Ordinal);
        Assert.Contains("where the path is not obvious", rule.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void One_scenario_is_chosen_and_a_new_one_written_only_where_there_is_none()
    {
        var scenario = Step("Choose the one scenario that already exercises the flow");

        Assert.Equal(1, Titles.ToList().IndexOf(scenario.Title));
        Assert.Contains("names ONE scenario", scenario.Verify, StringComparison.Ordinal);
        Assert.Contains("an existing test", scenario.Verify, StringComparison.Ordinal);
        Assert.Contains("written only where", scenario.Verify, StringComparison.Ordinal);
        Assert.Contains("Two scenarios is two traces", scenario.Verify, StringComparison.Ordinal);
    }

    [Fact]
    public void A_project_without_tracing_is_routed_through_the_install_skill_before_the_run()
    {
        var install = Step("Install tracing first when the project has none");

        Assert.Contains("only when the project has no NarrativeTrace at all", install.Condition, StringComparison.Ordinal);
        Assert.Contains("go straight to the run", install.Condition, StringComparison.Ordinal);
        Assert.Contains("add-narrative-tracing", install.Verify, StringComparison.Ordinal);
        Assert.Contains("resumed at the run below", install.Verify, StringComparison.Ordinal);
        Assert.Contains("does not refactor", install.Verify, StringComparison.Ordinal);
        Assert.True(Titles.ToList().IndexOf(install.Title) < Titles.ToList().IndexOf("Run that one scenario with tracing on"));
    }

    [Fact]
    public void The_run_drives_the_scenario_as_found_and_sends_a_silent_run_to_the_doctor()
    {
        var run = Step("Run that one scenario with tracing on");

        Assert.Equal([ExplainCommands.RunTheScenario], Assert.IsType<CommandStep>(run.Body).Commands);
        Assert.Contains("wrote a trace of its own", run.Verify, StringComparison.Ordinal);
        Assert.Contains("a run that writes nothing is the failure below", run.Verify, StringComparison.Ordinal);
        Assert.Contains("ran as it was found", run.Verify, StringComparison.Ordinal);
        Assert.Contains("run that one, as it is", run.Condition, StringComparison.Ordinal);
        Assert.Contains("add-narrative-tracing's own first-trace step", run.Condition, StringComparison.Ordinal);
        Assert.Contains("narrativetrace-doctor", Assert.Single(run.FailureNotes).Fix, StringComparison.Ordinal);
    }

    [Fact]
    public void The_structural_trace_is_read_whole_and_in_order_before_any_value_is_opened()
    {
        var shape = Step("Read the structural trace first, in the order it ran");
        var values = Step("Open values only where the question needs one");

        Assert.Contains("read whole before any value", shape.Verify, StringComparison.Ordinal);
        Assert.Contains("by span id", shape.Verify, StringComparison.Ordinal);
        Assert.Contains("which branch", shape.Verify, StringComparison.Ordinal);
        Assert.Equal(Titles.ToList().IndexOf(shape.Title) + 1, Titles.ToList().IndexOf(values.Title));
        Assert.Equal(NarrativeTraceExplainSkill.OnlyWhatTheQuestionNeeds, values.Condition);
        Assert.Contains("not the whole file", values.Verify, StringComparison.Ordinal);
        Assert.Contains("[REDACTED]", values.Verify, StringComparison.Ordinal);
    }

    [Fact]
    public void A_defect_or_a_change_in_progress_is_handed_off_instead_of_fixed_here()
    {
        var handOff = Step("Hand off when it stops being an explanation");
        var titles = Titles.ToList();

        Assert.Contains("only when the run shows something wrong", handOff.Condition, StringComparison.Ordinal);
        Assert.Contains("a change of this session's own", handOff.Condition, StringComparison.Ordinal);
        Assert.Contains("narrativetrace-debug", handOff.Verify, StringComparison.Ordinal);
        Assert.Contains("narrativetrace-verify", handOff.Verify, StringComparison.Ordinal);
        Assert.Contains("fixes nothing itself", handOff.Verify, StringComparison.Ordinal);
        Assert.True(titles.IndexOf(handOff.Title) > titles.IndexOf("Read the structural trace first, in the order it ran"));
        Assert.True(titles.IndexOf(handOff.Title) < titles.IndexOf("Explain the run by span id, and mark what you inferred"));
    }

    [Fact]
    public void The_last_step_explains_by_span_id_and_names_what_was_not_observed()
    {
        var last = Explain.Steps[^1];

        Assert.Equal("Explain the run by span id, and mark what you inferred", last.Title);
        Assert.Contains("in the user's own words", last.Verify, StringComparison.Ordinal);
        Assert.Contains("citing the span id it rests on", last.Verify, StringComparison.Ordinal);
        Assert.Contains("a claim without an id is not a claim", last.Verify, StringComparison.Ordinal);
        Assert.Contains("marked as an inference", last.Verify, StringComparison.Ordinal);
        Assert.Contains("what was NOT observed", last.Verify, StringComparison.Ordinal);
    }

    [Fact]
    public void It_shares_the_reading_sections_with_debug_and_declares_no_allowed_tools()
    {
        Assert.Equal(Debug.Sections, Explain.Sections);
        Assert.Empty(Explain.PreApprovedTools);
        Assert.DoesNotContain("allowed-tools", ClaudeSkillRenderer.Render(Explain, TestPaths.RepoRoot()), StringComparison.Ordinal);
    }

    [Fact]
    public void The_rules_forbid_inventing_observations_and_changing_the_code_to_explain_it()
    {
        Assert.Contains(Explain.Always, r => r.Rule == "Cite a span id for every claim about the trace");
        Assert.Contains(Explain.Always, r => r.Rule == "Separate what the trace showed from what you inferred");
        Assert.Equal(
            [
                "Never present an unobserved call as something the trace showed",
                "Never change the code in order to explain it",
                "Never turn redaction off to see more",
            ],
            Explain.Never.Select(r => r.Rule));
    }

    [Fact]
    public void The_description_carries_the_questions_a_user_asks()
    {
        Assert.Contains("'show me what my application actually does'", Explain.Description, StringComparison.Ordinal);
        Assert.Contains("'walk me through what happens when a user checks out'", Explain.Description, StringComparison.Ordinal);
        Assert.Contains("'what does this endpoint call'", Explain.Description, StringComparison.Ordinal);
        Assert.Contains("nothing broken and no change needing verification", Explain.Description, StringComparison.Ordinal);
        Assert.Contains("BEFORE reading source files", Explain.Description, StringComparison.Ordinal);
        Assert.Contains("add-narrative-tracing", Explain.Description, StringComparison.Ordinal);
        Assert.Contains("narrativetrace-debug", Explain.Description, StringComparison.Ordinal);
        Assert.Contains("narrativetrace-verify", Explain.Description, StringComparison.Ordinal);
        Assert.Contains("Non-obvious triggers", Explain.WhenToUse, StringComparison.Ordinal);
    }

    [Fact]
    public void It_is_a_reading_loop_so_it_names_no_command_that_writes_anything()
    {
        var commands = Explain.Steps.SelectMany(s => s.Body is CommandStep c ? c.Commands : []).ToList();

        Assert.Equal([ExplainCommands.RunTheScenario], commands);
        Assert.DoesNotContain(commands, c => c.Contains("approve", StringComparison.Ordinal) || c.Contains("init", StringComparison.Ordinal));
    }

    [Fact]
    public void Every_step_carries_its_own_verify()
    {
        Assert.All(Explain.Steps, step => Assert.False(string.IsNullOrWhiteSpace(step.Verify)));
    }
}
