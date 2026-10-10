// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Tooling.Doctor;
using NarrativeTrace.Tooling.Feedback;
using NarrativeTrace.Tooling.Tests.Doctor;
using Xunit;

namespace NarrativeTrace.Tooling.Tests.Feedback;

public sealed class FeedbackGathererTests
{
    private const string CleanTrace =
        "scenario: order is placed\n- OrderService.placeOrder(customerId, total) → value\n";

    [Fact]
    public void The_install_coordinate_lists_our_packages_sorted()
    {
        var snapshot = DoctorSnapshotFixtures.Empty() with
        {
            InstalledPackages = new Dictionary<string, string>
            {
                ["NarrativeTrace.Runtime"] = "1.2.0",
                ["NarrativeTrace.Core"] = "1.2.0",
                ["Newtonsoft.Json"] = "13.0.0",
            },
        };

        Assert.Equal("NarrativeTrace.Core 1.2.0, NarrativeTrace.Runtime 1.2.0", FeedbackGatherer.InstallCoordinate(snapshot));
    }

    [Fact]
    public void A_project_with_no_family_package_states_the_unknown()
    {
        var snapshot = DoctorSnapshotFixtures.WithPackages(("Newtonsoft.Json", "13.0.0"));

        Assert.Equal(FeedbackGatherer.InstallUnknown, FeedbackGatherer.InstallCoordinate(snapshot));
    }

    [Fact]
    public void A_clean_structural_trace_is_chosen()
    {
        var snapshot = DoctorSnapshotFixtures.Empty() with
        {
            OutputFiles = new Dictionary<string, string> { ["out/order.nt"] = CleanTrace },
        };

        var choice = FeedbackGatherer.ChooseTrace(snapshot, "");

        Assert.Equal(CleanTrace, choice.Content);
        Assert.Equal("", choice.Reason);
    }

    [Fact]
    public void A_trace_that_leaks_a_value_is_refused_with_the_rule_named()
    {
        // Structurally valid by grammar (a header plus a clean call line), but the header text itself
        // carries a card-shaped value — so the trace looks structural yet a vf rule still refuses it.
        var leaky = "scenario: charge 4111111111111111 settled\n- OrderService.placeOrder(customerId, total) → value\n";
        var snapshot = DoctorSnapshotFixtures.Empty() with
        {
            OutputFiles = new Dictionary<string, string> { ["out/order.nt"] = leaky },
        };

        var choice = FeedbackGatherer.ChooseTrace(snapshot, "");

        Assert.Equal("", choice.Content);
        Assert.Contains("breaks vf.value-shape", choice.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void No_trace_at_all_says_so_rather_than_attaching_nothing_silently()
    {
        var choice = FeedbackGatherer.ChooseTrace(DoctorSnapshotFixtures.Empty(), "");

        Assert.Equal("", choice.Content);
        Assert.Contains("no structural trace", choice.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Without_a_doctor_json_the_attachments_record_why()
    {
        var attachments = FeedbackGatherer.Attachments(DoctorSnapshotFixtures.Empty(), "", "");

        Assert.False(attachments.HasDoctorReport());
        Assert.Contains(FeedbackGatherer.DoctorReportPath, attachments.DoctorUnavailable, StringComparison.Ordinal);
    }

    [Fact]
    public void A_trace_choice_is_content_xor_a_reason()
    {
        Assert.Throws<ArgumentException>(() => new FeedbackGatherer.TraceChoice("content", "reason"));
        Assert.Throws<ArgumentException>(() => new FeedbackGatherer.TraceChoice("", ""));
    }
}
