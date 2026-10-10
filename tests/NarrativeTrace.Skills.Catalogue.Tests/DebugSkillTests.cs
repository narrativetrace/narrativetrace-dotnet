// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Skills.Catalogue;
using NarrativeTrace.Skills.Render;
using Xunit;

namespace NarrativeTrace.Skills.Tests;

/// <summary>
/// <c>narrativetrace-debug</c> (Phase 7 D4, R2; Java milestone 2 cross-port items 1–4, 7): starts from a
/// symptom, localizes by reading values span by span, fixes in the diverging span, checks nothing else
/// moved, pins the regression behind the shared gate, reports by span id.
/// </summary>
public sealed class DebugSkillTests
{
    private static Skill Debug => SkillCatalogue.Skills.Single(s => s.CanonicalName == "narrativetrace-debug");

    private static Skill Verify => SkillCatalogue.Skills.Single(s => s.CanonicalName == "narrativetrace-verify");

    private static int IndexOf(string titleStart) =>
        Debug.Steps.ToList().FindIndex(step => step.Title.StartsWith(titleStart, StringComparison.Ordinal));

    private static SkillStep Step(string titleStart) => Debug.Steps[IndexOf(titleStart)];

    [Fact]
    public void It_reproduces_first_with_the_same_compiled_listing_verify_uses()
    {
        var reproduce = Debug.Steps[0];

        Assert.StartsWith("Reproduce the symptom", reproduce.Title, StringComparison.Ordinal);
        Assert.Equal(
            Assert.IsType<SnippetStep>(Verify.Steps[2].Body).Path,
            Assert.IsType<SnippetStep>(reproduce.Body).Path);
        Assert.Equal(DebugCommands.RunTheReproduction, reproduce.Verify);
    }

    [Fact]
    public void The_diverging_span_is_named_by_id_before_any_code_changes()
    {
        var localize = Step("Localize by reading");

        Assert.Contains("names that span by its id", localize.Verify, StringComparison.Ordinal);
        Assert.Contains("before any code is changed", localize.Verify, StringComparison.Ordinal);
        Assert.True(IndexOf("Localize by reading") < IndexOf("Fix it in the diverging span"));
        Assert.Contains(Debug.Never, rule => rule.Rule == "Never change code before the diverging span is named");
    }

    [Fact]
    public void The_sequence_diagram_comes_first_only_across_threads()
    {
        var diagram = Step("Across threads, read the sequence diagram first");

        Assert.True(IndexOf("Across threads") < IndexOf("Localize by reading"));
        Assert.Contains("fork, async or fire-and-forget", diagram.Condition, StringComparison.Ordinal);
        Assert.Contains(".mmd", diagram.Verify, StringComparison.Ordinal);
    }

    [Fact]
    public void Bisecting_never_narrows_with_not_traced_because_it_redacts_here()
    {
        var bisect = Step("Bisect by span");

        Assert.Contains("never [NotTraced] to narrow", bisect.Verify, StringComparison.Ordinal);
        Assert.Contains("NarrativeTraceProxy.Create", bisect.Verify, StringComparison.Ordinal);
        Assert.NotNull(bisect.Condition);
    }

    [Fact]
    public void A_defect_in_narrativetrace_itself_is_handed_to_the_feedback_skill_before_any_fix()
    {
        var handOff = Step("Hand a defect in NarrativeTrace itself");

        Assert.True(IndexOf("Hand a defect") < IndexOf("Fix it in the diverging span"));
        Assert.Contains("narrativetrace-feedback", handOff.Verify, StringComparison.Ordinal);
        Assert.Contains(SkillCatalogue.Skills, s => s.CanonicalName == "narrativetrace-feedback");
    }

    [Fact]
    public void Nothing_else_moved_is_read_from_the_red_runs_narrative_since_a_red_run_writes_no_nt()
    {
        var check = Step("Check that nothing else moved");

        Assert.Contains("a red run writes no .nt", check.Verify, StringComparison.Ordinal);
        Assert.Contains("the .md's calls and ids without their values", check.Verify, StringComparison.Ordinal);
    }

    [Fact]
    public void The_regression_is_kept_and_pinned_with_the_shared_pin_steps()
    {
        var keep = IndexOf("Keep the reproduction as the regression test");
        var report = Debug.Steps.Single(s => s.Title.StartsWith("Report the root cause", StringComparison.Ordinal));
        var pin = BaselinePin.Steps(report);

        Assert.True(keep > IndexOf("Check that nothing else moved"));
        Assert.Equal(pin.Select(s => s.Title), Debug.Steps.Skip(keep + 1).Select(s => s.Title));
        Assert.Equal(pin.Select(s => s.Verify), Debug.Steps.Skip(keep + 1).Select(s => s.Verify));
    }

    [Fact]
    public void The_report_names_the_span_id_before_the_question_and_again_after_the_promotion()
    {
        var report = Debug.Steps[IndexOf("Report the root cause")];

        Assert.Equal(IndexOf("Ask once whether to pin it") - 1, IndexOf("Report the root cause"));
        Assert.Contains("before the pin question", report.Verify, StringComparison.Ordinal);
        Assert.Contains("names the span id again", report.Verify, StringComparison.Ordinal);
    }

    [Fact]
    public void It_shares_the_reading_sections_with_verify_and_declares_no_allowed_tools()
    {
        Assert.Equal(Verify.Sections, Debug.Sections);
        Assert.Empty(Debug.PreApprovedTools);
        Assert.DoesNotContain("allowed-tools", ClaudeSkillRenderer.Render(Debug, TestPaths.RepoRoot()), StringComparison.Ordinal);
        Assert.Contains(Debug.Never, rule => rule.Rule == "Never turn redaction off to see more");
    }
}
