// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Tooling.Feedback;
using Xunit;

namespace NarrativeTrace.Tooling.Tests.Feedback;

public sealed class IssueFormUrlTests
{
    [Fact]
    public void The_url_carries_exactly_the_contracted_field_ids()
    {
        // A URL parameter naming a field id the form does not have is SILENTLY ignored by GitHub, so
        // the set of ids the URL emits is a contract the issue form (shipped in milestone 3) must
        // satisfy exactly. This test pins the emitting side; the form-vs-URL mutual check lands with
        // the form.
        var url = IssueFormUrl.Of(FeedbackFixtures.ValidReport());

        var query = url[(url.IndexOf('?') + 1)..];
        var ids = query.Split('&').Select(pair => pair[..pair.IndexOf('=')]).ToList();

        Assert.Equal(
            ["template", "title", "labels", "runtime", "category", "install", "step", "language", "agent"],
            ids);
    }

    [Fact]
    public void The_url_targets_the_dotnet_repository_and_form()
    {
        var url = IssueFormUrl.Of(FeedbackFixtures.ValidReport());

        Assert.StartsWith("https://github.com/narrativetrace/narrativetrace-dotnet/issues/new?", url, StringComparison.Ordinal);
        Assert.Contains("template=narrativetrace-report.yml", url, StringComparison.Ordinal);
    }

    [Fact]
    public void A_space_is_percent_encoded_not_a_plus()
    {
        var report = FeedbackFixtures.ValidReportBuilder().Step("a step with spaces").Build();

        var url = IssueFormUrl.Of(report);

        Assert.Contains("%20", url, StringComparison.Ordinal);
        Assert.DoesNotContain("step=a+step", url, StringComparison.Ordinal);
    }

    [Fact]
    public void A_pathological_field_is_clipped_so_the_url_stays_under_budget()
    {
        var report = FeedbackFixtures.ValidReportBuilder()
            .Step(new string('x', 20_000))
            .Build();

        var url = IssueFormUrl.Of(report);

        Assert.True(IssueFormUrl.StaysUnderBudget(url), $"URL was {url.Length} chars");
    }

    [Fact]
    public void A_report_from_another_runtime_is_refused()
    {
        var report = FeedbackFixtures.ValidReportBuilder().Runtime("java").Build();

        Assert.Throws<ArgumentException>(() => IssueFormUrl.Of(report));
    }
}
