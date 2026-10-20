// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Skills.Catalogue;
using NarrativeTrace.Skills.Render;
using Xunit;

namespace NarrativeTrace.Skills.Tests;

/// <summary>
/// <c>narrativetrace-verify</c> (Phase 7 D1–D3, D5, D8; cross-port item 7): the contract is the loop,
/// the cost rule and the flavour table; the capture API and the approve verb are this port's own; the
/// gate is the feedback skill's wording; no allowed tools.
/// </summary>
public sealed class VerifySkillTests
{
    private static Skill Verify => SkillCatalogue.Skills.Single(s => s.CanonicalName == "narrativetrace-verify");

    private static Skill Feedback => SkillCatalogue.Skills.Single(s => s.CanonicalName == "narrativetrace-feedback");

    private static int IndexOf(string titleStart) =>
        Verify.Steps.ToList().FindIndex(step => step.Title.StartsWith(titleStart, StringComparison.Ordinal));

    [Fact]
    public void The_cost_rule_is_the_first_step_and_a_skip_is_said_out_loud()
    {
        var first = Verify.Steps[0];

        Assert.StartsWith("Decide whether to trace", first.Title, StringComparison.Ordinal);
        Assert.Contains("skipping narrativetrace-verify: <", first.Verify, StringComparison.Ordinal);
        Assert.Contains("a pure function", first.Verify, StringComparison.Ordinal);
        Assert.Contains("tracing: <the reason>", first.Verify, StringComparison.Ordinal);
    }

    [Fact]
    public void The_intent_is_written_before_anything_runs()
    {
        var intent = IndexOf("Write the intent down");

        Assert.Equal(1, intent);
        Assert.Contains("under the word Intent", Verify.Steps[intent].Verify, StringComparison.Ordinal);
        Assert.True(intent < IndexOf("Run the smallest real path"));
    }

    [Fact]
    public void The_run_embeds_the_compiled_flow_test_and_reruns_one_from_before_the_intent()
    {
        var run = Verify.Steps[IndexOf("Run the smallest real path")];

        var snippet = Assert.IsType<SnippetStep>(run.Body);
        Assert.Equal("tests/NarrativeTrace.Examples.SixtySeconds.Tests/PlaceOrderFlowTests.cs", snippet.Path);
        Assert.Equal(VerifyCommands.RunThePath, run.Verify);
        Assert.Contains("before the Intent was written does not count", run.Condition, StringComparison.Ordinal);
    }

    [Fact]
    public void The_structure_is_read_before_any_value_and_values_only_on_a_mismatch()
    {
        var structural = IndexOf("Read the structural trace first");
        var values = IndexOf("Open values on the span");
        var fix = IndexOf("Fix, re-run");

        Assert.True(structural < values && values < fix);
        Assert.Contains("span id", Verify.Steps[structural].Verify, StringComparison.Ordinal);
        Assert.Equal(NarrativeTraceVerifySkill.OpenValuesWhen, Verify.Steps[values].Condition);
        Assert.Equal(NarrativeTraceVerifySkill.FixAndRerunWhen, Verify.Steps[fix].Condition);
    }

    [Fact]
    public void Approval_mode_is_switched_on_by_the_compiled_runsettings_only_when_it_is_off()
    {
        var on = Verify.Steps[IndexOf("Turn approval mode on")];
        var reference = Verify.Steps[IndexOf("Reference the run settings")];

        Assert.Equal("tests/NarrativeTrace.Examples.SixtySeconds.Tests/approval.runsettings", Assert.IsType<SnippetStep>(on.Body).Path);
        var csproj = Assert.IsType<SnippetStep>(reference.Body);
        Assert.Equal("approval-mode", csproj.Region);
        Assert.Contains("config.approval-mode", on.Condition, StringComparison.Ordinal);
        Assert.Contains("not a change beyond the task, so do it now without asking first", on.Condition, StringComparison.Ordinal);
        Assert.Contains("**/*.received.nt", reference.Verify, StringComparison.Ordinal);
    }

    [Fact]
    public void The_pin_runs_the_whole_suite_shows_every_review_copy_and_asks_last()
    {
        var run = Verify.Steps[IndexOf("Run the suite in approval mode")];
        var ask = Verify.Steps[IndexOf("Ask once whether to pin it")];

        Assert.Equal([VerifyCommands.RunTheSuite], Assert.IsType<CommandStep>(run.Body).Commands);
        Assert.Contains("the whole text of each .received.nt that run wrote is in the reply, not a summary of it", run.Verify, StringComparison.Ordinal);
        Assert.Empty(Assert.IsType<CommandStep>(ask.Body).Commands);
        Assert.StartsWith("the reply ends with the question and nothing after it", ask.Verify, StringComparison.Ordinal);
        Assert.Contains("the question is the reply's last line, with no sentence after it", ask.Verify, StringComparison.Ordinal);
        Assert.Contains("what you will do on a yes — goes before the question", ask.Verify, StringComparison.Ordinal);
        Assert.EndsWith("end the reply with exactly the line \"Shall I pin this as the approved baseline?\" (in the user's language).", ask.Verify, StringComparison.Ordinal);
        Assert.StartsWith("this runs in the same turn as the structural read", run.Verify, StringComparison.Ordinal);
    }

    [Fact]
    public void The_pin_reuses_the_feedback_skills_gate_wording_verbatim()
    {
        var ask = Verify.Steps[IndexOf("Ask once whether to pin it")];
        var feedbackAsk = FeedbackCommands.VerifyQuestionAsked;
        var gatePrefix = feedbackAsk[..feedbackAsk.IndexOf(". ", StringComparison.Ordinal)];

        Assert.StartsWith(gatePrefix, ask.Verify, StringComparison.Ordinal);
        Assert.Contains(Verify.Never, rule => rule.Reason == Feedback.Never.Single(r => r.Rule == "Never file in the turn that asked").Reason
            && rule.Rule == "Never promote a baseline in the turn that asked");
        Assert.Contains(Verify.Never, rule => rule.Rule == "Never edit the .received.nt after showing it");
        Assert.Contains(Verify.Always, rule => rule.Rule == "Show the whole .received.nt before asking anything");
    }

    [Fact]
    public void Promotion_is_the_approve_verb_after_the_question_and_leaves_the_suite_green()
    {
        var promote = Verify.Steps[IndexOf("Promote what was shown")];

        Assert.True(IndexOf("Ask once whether to pin it") < IndexOf("Promote what was shown"));
        Assert.Equal(["dotnet tool restore", VerifyCommands.Approve], Assert.IsType<CommandStep>(promote.Body).Commands);
        Assert.Equal("dotnet tool run dotnet-narrativetrace approve", VerifyCommands.Approve);
        Assert.Contains("dotnet test passes", promote.Verify, StringComparison.Ordinal);
    }

    [Fact]
    public void The_report_comes_right_before_the_question_and_every_claim_cites_a_span_id()
    {
        var at = IndexOf("Report what the trace showed");
        var report = Verify.Steps[at];

        Assert.Equal(IndexOf("Run the suite in approval mode") + 1, at);
        Assert.Equal(IndexOf("Ask once whether to pin it") - 1, at);
        Assert.Contains("before the pin question", report.Verify, StringComparison.Ordinal);
        Assert.Contains("citing the span id", report.Verify, StringComparison.Ordinal);
        Assert.Contains(Verify.Always, rule => rule.Rule == "Cite a span id for every claim about the trace");
    }

    [Fact]
    public void It_declares_no_allowed_tools_and_the_page_carries_no_allowed_tools_line()
    {
        Assert.Empty(Verify.PreApprovedTools);
        Assert.DoesNotContain("allowed-tools", ClaudeSkillRenderer.Render(Verify, TestPaths.RepoRoot()), StringComparison.Ordinal);
    }

    [Fact]
    public void It_renders_the_shared_reading_sections_and_redaction_stays_on()
    {
        Assert.Equal(["Which flavour answers which question", "Shapes that mean something went wrong"], Verify.Sections.Select(s => s.Heading));
        Assert.Contains("narratives/<TestClass>/<scenario>.approved.nt", Verify.Sections[0].Markdown, StringComparison.Ordinal);
        Assert.Contains(Verify.Never, rule => rule.Rule == "Never turn redaction off to see more");
    }

    [Fact]
    public void The_install_skill_ends_by_handing_the_next_session_to_verify()
    {
        var install = SkillCatalogue.Skills.Single(s => s.CanonicalName == "add-narrative-tracing");

        Assert.Contains("the next session verifies with narrativetrace-verify", install.Steps[^1].Verify, StringComparison.Ordinal);
    }
}
