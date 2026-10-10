// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Tooling.Feedback;
using Xunit;

namespace NarrativeTrace.Tooling.Tests.Feedback;

public sealed class FeedbackJsonTests
{
    [Fact]
    public void A_drafted_envelope_names_the_verb_and_exits_zero()
    {
        var json = FeedbackJson.Drafted(FeedbackFixtures.ValidReport(), "d.md", "b.md", "");

        Assert.Contains("\"verb\": \"draft\"", json, StringComparison.Ordinal);
        Assert.Contains("\"status\": \"drafted\"", json, StringComparison.Ordinal);
        Assert.Contains("\"exitCode\": 0", json, StringComparison.Ordinal);
    }

    [Fact]
    public void A_refused_envelope_lists_every_violation_and_exits_two()
    {
        var report = FeedbackFixtures.ValidReportBuilder()
            .Narrative(new ProblemNarrative("did", "leaked ada@example.com", "expected"))
            .Build();
        var refused = Assert.IsType<FeedbackDraft.Refused>(FeedbackDrafter.Draft(report));

        var json = FeedbackJson.Refused("draft", refused);

        Assert.Contains("\"status\": \"refused\"", json, StringComparison.Ordinal);
        Assert.Contains("\"rule\": \"vf.email\"", json, StringComparison.Ordinal);
        Assert.Contains("\"exitCode\": 2", json, StringComparison.Ordinal);
    }

    [Fact]
    public void A_url_envelope_carries_the_url_and_the_body_file()
    {
        var json = FeedbackJson.Url("https://github.com/x", "b.md");

        Assert.Contains("\"verb\": \"url\"", json, StringComparison.Ordinal);
        Assert.Contains("\"url\": \"https://github.com/x\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void A_gh_unavailable_envelope_exits_one()
    {
        Assert.Contains("\"exitCode\": 1", FeedbackJson.GhUnavailable("no gh"), StringComparison.Ordinal);
    }

    [Fact]
    public void A_control_character_in_a_value_is_escaped()
    {
        var json = FeedbackJson.Gh("gh\tissue");

        Assert.Contains("gh\\tissue", json, StringComparison.Ordinal);
    }
}
