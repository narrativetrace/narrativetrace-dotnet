// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Tooling.Doctor;
using NarrativeTrace.Tooling.Feedback;
using NarrativeTrace.Tooling.Tests.Doctor;
using Xunit;

namespace NarrativeTrace.Tooling.Tests.Feedback;

/// <summary>
/// Which structural trace is attached when the user names one, which when they do not, and what is
/// said in each refusal — the choice decides what leaves the machine, so every branch is pinned.
/// </summary>
public sealed class FeedbackGathererNamedTraceTests
{
    private const string Clean = "scenario: order is placed\n- OrderService.placeOrder(customerId, total) → value\n";
    private const string Other = "scenario: refund is issued\n- RefundService.refund(orderId) → value\n";
    private const string Leaky = "scenario: charge 4111111111111111 settled\n- OrderService.placeOrder(customerId, total) → value\n";

    private static DoctorSnapshot Snapshot(
        IReadOnlyDictionary<string, string>? output = null, IReadOnlyDictionary<string, string>? approved = null) =>
        DoctorSnapshotFixtures.Empty() with
        {
            OutputFiles = output ?? new Dictionary<string, string>(),
            ApprovedDirFiles = approved ?? new Dictionary<string, string>(),
        };

    [Fact]
    public void A_named_trace_is_chosen_by_its_path_suffix_among_several()
    {
        var snapshot = Snapshot(new Dictionary<string, string> { ["out/a.nt"] = Clean, ["out/refund.nt"] = Other });

        var choice = FeedbackGatherer.ChooseTrace(snapshot, "refund.nt");

        Assert.Equal(Other, choice.Content);
        Assert.Equal("", choice.Reason);
    }

    [Fact]
    public void A_named_trace_that_is_not_there_says_so_and_attaches_nothing()
    {
        var choice = FeedbackGatherer.ChooseTrace(Snapshot(new Dictionary<string, string> { ["out/a.nt"] = Clean }), "missing.nt");

        Assert.Equal("", choice.Content);
        Assert.Equal("missing.nt was not found under this project's output", choice.Reason);
    }

    [Fact]
    public void A_named_trace_that_is_not_structural_says_why()
    {
        var snapshot = Snapshot(new Dictionary<string, string> { ["out/a.nt"] = "just some text\n" });

        Assert.Equal("a.nt is not a structural trace", FeedbackGatherer.ChooseTrace(snapshot, "a.nt").Reason);
    }

    [Fact]
    public void A_named_trace_that_leaks_a_value_names_the_rule_it_breaks()
    {
        var snapshot = Snapshot(new Dictionary<string, string> { ["out/a.nt"] = Leaky });

        Assert.Equal("a.nt breaks vf.value-shape", FeedbackGatherer.ChooseTrace(snapshot, "a.nt").Reason);
    }

    [Fact]
    public void A_name_of_only_whitespace_lets_the_verb_choose()
    {
        var snapshot = Snapshot(new Dictionary<string, string> { ["out/a.nt"] = Clean });

        Assert.Equal(Clean, FeedbackGatherer.ChooseTrace(snapshot, "   ").Content);
    }

    [Fact]
    public void Traces_in_the_approved_directory_are_candidates_too()
    {
        var snapshot = Snapshot(approved: new Dictionary<string, string> { ["approved/x.approved.nt"] = Clean });

        Assert.Equal(Clean, FeedbackGatherer.ChooseTrace(snapshot, "").Content);
    }

    [Fact]
    public void Only_files_ending_in_nt_are_candidates()
    {
        var snapshot = Snapshot(new Dictionary<string, string> { ["out/a.nt.txt"] = Clean, ["out/b.md"] = Clean });

        Assert.Equal("no structural trace was found under this project's output", FeedbackGatherer.ChooseTrace(snapshot, "").Reason);
    }

    [Fact]
    public void The_first_candidate_in_path_order_is_the_one_the_refusal_names()
    {
        var snapshot = Snapshot(new Dictionary<string, string> { ["out/z.nt"] = Leaky, ["out/b.nt"] = "plain\n" });

        Assert.Equal(
            "no attachable structural trace: out/b.nt is not a structural trace",
            FeedbackGatherer.ChooseTrace(snapshot, "").Reason);
    }

    [Fact]
    public void The_first_attachable_candidate_wins_over_an_earlier_refused_one()
    {
        var snapshot = Snapshot(new Dictionary<string, string> { ["out/a.nt"] = Leaky, ["out/b.nt"] = Clean });

        Assert.Equal(Clean, FeedbackGatherer.ChooseTrace(snapshot, "").Content);
    }

    [Fact]
    public void A_missing_doctor_report_is_explained_with_the_path_to_run_it_for()
    {
        var attachments = FeedbackGatherer.Attachments(Snapshot(), " ", "");

        Assert.Equal(
            $"no report at {FeedbackGatherer.DoctorReportPath} — run the doctor first",
            attachments.DoctorUnavailable);
    }

    [Fact]
    public void A_doctor_report_and_a_named_trace_are_attached_together()
    {
        var snapshot = Snapshot(new Dictionary<string, string> { ["out/a.nt"] = Clean });

        var attachments = FeedbackGatherer.Attachments(snapshot, FeedbackFixtures.DoctorReportJson, "a.nt");

        Assert.Equal(FeedbackFixtures.DoctorReportJson, attachments.DoctorReport);
        Assert.Equal(Clean, attachments.StructuralTrace);
    }

    [Fact]
    public void A_trace_choice_refuses_null_and_both_or_neither_with_their_own_words()
    {
        Assert.Equal(
            "a trace choice is content or a reason, never null",
            Assert.Throws<ArgumentException>(() => new FeedbackGatherer.TraceChoice(null!, "r")).Message);
        Assert.Equal(
            "a trace choice is content or a reason, never null",
            Assert.Throws<ArgumentException>(() => new FeedbackGatherer.TraceChoice("c", null!)).Message);
        Assert.Equal(
            "a choice attached a trace or says why it did not, never both and never neither",
            Assert.Throws<ArgumentException>(() => new FeedbackGatherer.TraceChoice("c", "r")).Message);
    }
}
