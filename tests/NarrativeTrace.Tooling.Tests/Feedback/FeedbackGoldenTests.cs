// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Tooling.Feedback;
using Xunit;

namespace NarrativeTrace.Tooling.Tests.Feedback;

/// <summary>
/// The draft a person reads before deciding to file, and the JSON an agent reads to continue, are
/// pinned byte for byte: every heading, label, separator and escape is part of what they act on.
/// </summary>
public sealed class FeedbackGoldenTests
{
    private const string Fence = "````";

    private static FeedbackReport.Builder Report() => FeedbackFixtures.ValidReportBuilder();

    [Fact]
    public void The_body_is_the_facts_the_narrative_and_both_attachments_in_that_order()
    {
        var body = FeedbackRender.Body(Report().Build());

        Assert.Equal(
            "- runtime: " + FeedbackFixtures.ValidReport().Runtime + "\n"
            + "- category: doctor\n"
            + "- install: NarrativeTrace.Core 1.0.0\n"
            + "- step: trap.redaction-proof\n"
            + "- language: en\n"
            + "- agent: " + new AgentIdentity("example-agent", "example-model").Describe() + "\n\n"
            + "## What I did\n\nran the doctor twice\n\n"
            + "## What happened\n\nthe fix did not clear the finding\n\n"
            + "## What I expected\n\nthe finding should clear once a redaction test asserts the marker\n\n"
            + "## Doctor report\n\n" + Fence + "json\n" + FeedbackFixtures.DoctorReportJson + "\n" + Fence + "\n\n"
            + "## Structural trace\n\n" + Fence + "\n" + FeedbackFixtures.StructuralTrace.Trim() + "\n" + Fence + "\n",
            body);
    }

    [Fact]
    public void A_missing_doctor_report_and_trace_say_so_instead_of_leaving_a_fence()
    {
        var report = Report().Category(FeedbackCategory.Library).Attachments(Attachments.WithoutDoctorReport("the CLI is not installed", "")).Build();

        var body = FeedbackRender.Body(report);

        Assert.EndsWith(
            "## Doctor report\n\nNo doctor report: the CLI is not installed\n\n"
            + "## Structural trace\n\nNo structural trace was attached.\n",
            body,
            StringComparison.Ordinal);
    }

    [Fact]
    public void An_agent_that_did_not_identify_itself_is_reported_as_not_reported()
    {
        var body = FeedbackRender.Body(Report().Agent(AgentIdentity.Unknown()).Build());

        Assert.Contains("- agent: not reported\n\n", body, StringComparison.Ordinal);
    }

    [Fact]
    public void The_draft_frames_the_body_with_a_title_a_rule_and_the_privacy_note()
    {
        Assert.Equal(
            "# NarrativeTrace problem report (draft — nothing has been filed)\n\nBODY\n---\n\n"
            + FeedbackRender.PrivacyNote + "\n",
            FeedbackRender.Draft("BODY"));
    }

    [Fact]
    public void The_drafted_envelope_lists_every_fact_then_the_exit_code()
    {
        var report = Report().Build();

        var json = FeedbackJson.Drafted(report, "d.md", "b.md", "no trace");

        Assert.Equal(
            "{\n  \"verb\": \"draft\",\n  \"status\": \"drafted\",\n"
            + "  \"runtime\": \"" + report.Runtime + "\",\n"
            + "  \"category\": \"doctor\",\n"
            + "  \"install\": \"NarrativeTrace.Core 1.0.0\",\n"
            + "  \"step\": \"trap.redaction-proof\",\n"
            + "  \"language\": \"en\",\n"
            + "  \"agent\": \"" + report.Agent.Describe() + "\",\n"
            + "  \"draftFile\": \"d.md\",\n  \"bodyFile\": \"b.md\",\n  \"traceNote\": \"no trace\",\n"
            + "  \"exitCode\": 0\n}\n",
            json);
    }

    [Fact]
    public void The_url_gh_and_unavailable_envelopes_carry_their_verb_status_and_exit_code()
    {
        Assert.Equal(
            "{\n  \"verb\": \"url\",\n  \"status\": \"ready\",\n  \"url\": \"https://x/y\",\n  \"bodyFile\": \"b.md\",\n  \"exitCode\": 0\n}\n",
            FeedbackJson.Url("https://x/y", "b.md"));
        Assert.Equal(
            "{\n  \"verb\": \"gh\",\n  \"status\": \"ready\",\n  \"command\": \"gh issue create\",\n  \"exitCode\": 0\n}\n",
            FeedbackJson.Gh("gh issue create"));
        Assert.Equal(
            "{\n  \"verb\": \"gh\",\n  \"status\": \"unavailable\",\n  \"reason\": \"no gh\",\n  \"exitCode\": 1\n}\n",
            FeedbackJson.GhUnavailable("no gh"));
    }

    [Fact]
    public void Quoting_escapes_quotes_backslashes_line_breaks_tabs_and_other_control_characters()
    {
        var json = FeedbackJson.Gh("a\"b\\c\nd\re\tf\u0001g\u001fh i~");

        Assert.Contains("\"command\": \"a\\\"b\\\\c\\nd\\re\\tf\\u0001g\\u001fh i~\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void The_refused_envelope_lists_each_violation_separated_by_commas_and_closes_without_one()
    {
        var report = Report()
            .Narrative(new ProblemNarrative("mail ada@example.com", "mail bob@example.com", "expected"))
            .Build();
        var refused = Assert.IsType<FeedbackDraft.Refused>(FeedbackDrafter.Draft(report));
        Assert.True(refused.Violations.Count >= 2);

        var json = FeedbackJson.Refused("draft", refused);

        var lines = json.Split('\n');
        Assert.Equal("{", lines[0]);
        Assert.Equal("  \"verb\": \"draft\",", lines[1]);
        Assert.Equal("  \"status\": \"refused\",", lines[2]);
        Assert.Equal("  \"violations\": [", lines[3]);
        for (var i = 0; i < refused.Violations.Count; i++)
        {
            var v = refused.Violations[i];
            var tail = i == refused.Violations.Count - 1 ? "" : ",";
            Assert.Equal(
                "    {\"field\": \"" + v.Field + "\", \"rule\": \"" + v.Rule.Id + "\", \"reason\": \""
                + v.Rule.Reason.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"}" + tail,
                lines[4 + i]);
        }

        Assert.Equal("  ],", lines[4 + refused.Violations.Count]);
        Assert.Equal("  \"exitCode\": 2", lines[5 + refused.Violations.Count]);
        Assert.Equal("}", lines[6 + refused.Violations.Count]);
    }
}
