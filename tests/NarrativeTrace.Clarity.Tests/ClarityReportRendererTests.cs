// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Clarity;
using Xunit;

namespace NarrativeTrace.Clarity.Tests;

public class ClarityReportRendererTests
{
    private static ScenarioClarity Scenario(
        string name, double overall, params ClarityIssue[] issues)
    {
        return new ScenarioClarity(
            name,
            new ClarityResult(overall, overall, overall, overall, overall, overall, issues));
    }

    [Fact]
    public void Renders_suite_title_and_scenarios_table()
    {
        var output = ClarityReportRenderer.Render([Scenario("Place order", 0.85)]);

        Assert.Contains("# Clarity Suite Report", output, StringComparison.Ordinal);
        Assert.Contains("## Scenarios", output, StringComparison.Ordinal);
        Assert.Contains("| Scenario | Score |", output, StringComparison.Ordinal);
        Assert.Contains("Place order", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Scenarios_are_sorted_ascending_by_overall_score()
    {
        var output = ClarityReportRenderer.Render(
        [
            Scenario("High", 0.90),
            Scenario("Low", 0.20),
            Scenario("Mid", 0.55),
        ]);

        var low = output.IndexOf("Low", StringComparison.Ordinal);
        var mid = output.IndexOf("Mid", StringComparison.Ordinal);
        var high = output.IndexOf("High", StringComparison.Ordinal);
        Assert.True(low < mid && mid < high);
    }

    [Fact]
    public void Low_scoring_scenario_renders_a_weighted_scores_table()
    {
        var output = ClarityReportRenderer.Render(
        [
            Scenario("Login", 0.40,
                new ClarityIssue("high", "Generic verb 'handle'", "Use domain verb")),
        ]);

        Assert.Contains("### Login", output, StringComparison.Ordinal);
        Assert.Contains("| Category | Score | Weight | Weighted |", output, StringComparison.Ordinal);
        Assert.Contains("| Method Names | 0.40 | 0.30 |", output, StringComparison.Ordinal);
        Assert.Contains("| Cohesion | 0.40 | 0.10 |", output, StringComparison.Ordinal);
        Assert.Contains("Generic verb 'handle'", output, StringComparison.Ordinal);
    }

    [Fact]
    public void High_scoring_scenario_has_no_detail_section()
    {
        var output = ClarityReportRenderer.Render(
        [
            Scenario("Clean", 0.95,
                new ClarityIssue("high", "noise", "fix")),
        ]);

        Assert.DoesNotContain("### Clean", output, StringComparison.Ordinal);
        Assert.DoesNotContain("| Category | Score | Weight | Weighted |", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Low_scoring_scenario_without_issues_has_no_detail_section()
    {
        var output = ClarityReportRenderer.Render([Scenario("Quiet", 0.40)]);

        Assert.DoesNotContain("### Quiet", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Scores_render_culture_invariantly()
    {
        using var _ = new CultureScope("de-DE");

        var output = ClarityReportRenderer.Render(
        [
            Scenario("Login", 0.40,
                new ClarityIssue("high", "noise", "fix")),
        ]);

        Assert.Contains("0.40", output, StringComparison.Ordinal);
        Assert.DoesNotContain("0,40", output, StringComparison.Ordinal);
    }
}
