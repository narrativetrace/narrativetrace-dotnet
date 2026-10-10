// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Tooling.Feedback;
using Xunit;

namespace NarrativeTrace.Tooling.Tests.Feedback;

public sealed class FeedbackDrafterTests
{
    [Fact]
    public void A_clean_report_drafts_and_the_draft_contains_the_body_verbatim()
    {
        var draft = FeedbackDrafter.Draft(FeedbackFixtures.ValidReport());

        var drafted = Assert.IsType<FeedbackDraft.Drafted>(draft);
        Assert.Contains(drafted.Body, drafted.Draft, StringComparison.Ordinal);
        Assert.Contains(FeedbackRender.PrivacyNote, drafted.Draft, StringComparison.Ordinal);
    }

    [Fact]
    public void A_doctor_report_that_names_the_marker_still_drafts()
    {
        // The real doctor's JSON quotes "[REDACTED]"; the field-scoped exemption is what lets the verb
        // draft any report at all for a project that has NarrativeTrace installed.
        var draft = FeedbackDrafter.Draft(FeedbackFixtures.ValidReport());

        Assert.IsType<FeedbackDraft.Drafted>(draft);
    }

    [Fact]
    public void A_leaked_value_in_the_narrative_refuses_the_whole_draft()
    {
        var report = FeedbackFixtures.ValidReportBuilder()
            .Narrative(new ProblemNarrative(
                "ran the doctor",
                "the token ghp_0123456789abcdefghij leaked into the message",
                "it should have been redacted"))
            .Build();

        var refused = Assert.IsType<FeedbackDraft.Refused>(FeedbackDrafter.Draft(report));
        Assert.Contains(refused.Violations, v => v.Rule.Id == "vf.value-shape");
    }

    [Fact]
    public void A_home_path_is_rewritten_to_tilde_before_the_gate_reads_it()
    {
        var report = FeedbackFixtures.ValidReportBuilder()
            .Narrative(new ProblemNarrative(
                "ran the doctor in /Users/ada/work/orders",
                "the fix did not clear the finding",
                "it should clear"))
            .Build();

        var drafted = Assert.IsType<FeedbackDraft.Drafted>(FeedbackDrafter.Draft(report));
        Assert.Contains("~/work/orders", drafted.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("/Users/ada", drafted.Body, StringComparison.Ordinal);
    }

    [Fact]
    public void The_body_carries_four_backtick_fences_around_attachments()
    {
        var drafted = Assert.IsType<FeedbackDraft.Drafted>(FeedbackDrafter.Draft(FeedbackFixtures.ValidReport()));

        Assert.Contains("````json", drafted.Body, StringComparison.Ordinal);
    }

    [Fact]
    public void Drafting_from_null_is_a_programming_error()
    {
        Assert.Throws<ArgumentNullException>(() => FeedbackDrafter.Draft(null!));
    }
}
