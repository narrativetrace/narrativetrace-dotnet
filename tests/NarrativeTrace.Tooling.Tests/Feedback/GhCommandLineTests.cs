// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Tooling.Feedback;
using Xunit;

namespace NarrativeTrace.Tooling.Tests.Feedback;

public sealed class GhCommandLineTests
{
    [Fact]
    public void The_line_files_the_body_from_a_file_into_the_dotnet_repository()
    {
        var line = GhCommandLine.Of(FeedbackFixtures.ValidReport(), "TestResults/narrativetrace/feedback/feedback-body.md");

        Assert.StartsWith("gh issue create", line, StringComparison.Ordinal);
        Assert.Contains("--repo narrativetrace/narrativetrace-dotnet", line, StringComparison.Ordinal);
        Assert.Contains("--body-file TestResults/narrativetrace/feedback/feedback-body.md", line, StringComparison.Ordinal);
        Assert.Contains("--label from-agent", line, StringComparison.Ordinal);
    }

    [Fact]
    public void The_title_is_single_quoted_so_a_shell_metacharacter_in_the_step_is_inert()
    {
        var report = FeedbackFixtures.ValidReportBuilder().Step("x; rm -rf /").Build();

        var line = GhCommandLine.Of(report, "b.md");

        Assert.Contains("--title 'doctor: x; rm -rf /'", line, StringComparison.Ordinal);
    }

    [Fact]
    public void A_single_quote_in_the_title_is_escaped()
    {
        var report = FeedbackFixtures.ValidReportBuilder().Step("ada's step").Build();

        var line = GhCommandLine.Of(report, "b.md");

        Assert.Contains("'\\''", line, StringComparison.Ordinal);
    }

    [Fact]
    public void A_blank_body_file_is_refused()
    {
        Assert.Throws<ArgumentException>(() => GhCommandLine.Of(FeedbackFixtures.ValidReport(), "  "));
    }

    [Fact]
    public void A_report_from_another_runtime_is_refused()
    {
        var report = FeedbackFixtures.ValidReportBuilder().Runtime("java").Build();

        Assert.Throws<ArgumentException>(() => GhCommandLine.Of(report, "b.md"));
    }
}
