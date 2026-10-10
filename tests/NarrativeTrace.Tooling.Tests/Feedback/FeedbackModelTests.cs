// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Tooling.Feedback;
using Xunit;

namespace NarrativeTrace.Tooling.Tests.Feedback;

public sealed class FeedbackModelTests
{
    [Fact]
    public void A_doctor_category_report_without_the_doctor_json_is_refused_at_construction()
    {
        var builder = FeedbackFixtures.ValidReportBuilder()
            .Category(FeedbackCategory.Doctor)
            .Attachments(Attachments.WithoutDoctorReport("the build is broken", ""));

        Assert.Throws<ArgumentException>(() => builder.Build());
    }

    [Fact]
    public void A_prompt_category_report_may_be_filed_without_a_doctor_json()
    {
        var report = FeedbackFixtures.ValidReportBuilder()
            .Category(FeedbackCategory.Prompt)
            .Attachments(Attachments.WithoutDoctorReport("no plugin installed", ""))
            .Build();

        Assert.True(report.Invariant());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_blank_mandatory_field_is_refused(string blank)
    {
        Assert.Throws<ArgumentException>(() => FeedbackFixtures.ValidReportBuilder().Step(blank).Build());
    }

    [Fact]
    public void The_field_map_is_exactly_the_gate_s_eight_fields_in_order()
    {
        var fields = FeedbackFixtures.ValidReport().Fields();

        Assert.Equal(
            ["install", "step", "did", "happened", "expected", "agent", ValueFreeCheck.DoctorReportField, "trace"],
            fields.Keys.ToList());
    }

    [Fact]
    public void The_doctor_report_field_key_matches_the_gate_s_exemption_constant()
    {
        Assert.Contains(ValueFreeCheck.DoctorReportField, FeedbackFixtures.ValidReport().Fields().Keys);
    }

    [Fact]
    public void Attachments_refuse_both_a_report_and_a_reason_at_once()
    {
        Assert.Throws<ArgumentException>(() => new Attachments("{}", "broken build", ""));
    }

    [Fact]
    public void Attachments_refuse_neither_a_report_nor_a_reason()
    {
        Assert.Throws<ArgumentException>(() => new Attachments("", "", ""));
    }

    [Theory]
    [InlineData("example-agent", "opus", "example-agent / opus")]
    [InlineData("example-agent", "", "example-agent")]
    [InlineData("", "", "")]
    public void An_agent_describes_itself_as_product_slash_model(string product, string model, string expected)
    {
        Assert.Equal(expected, new AgentIdentity(product, model).Describe());
    }

    [Fact]
    public void An_unknown_agent_is_empty_strings_never_null()
    {
        Assert.Throws<ArgumentNullException>(() => new AgentIdentity(null!, ""));
    }
}
