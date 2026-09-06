// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using Xunit;

namespace NarrativeTrace.Core.Tests;

public sealed class ConsoleSummaryReporterTests
{
    [Fact]
    public void Suite_footer_reports_scenario_count_clarity_buckets_and_output_path()
    {
        var footer = ConsoleSummaryReporter.FormatSuiteFooter(
            scenarioCount: 4,
            outputPath: "out/dir",
            clarityScores: [0.9, 0.75, 0.5, 0.2]);

        Assert.Contains("4 scenarios recorded", footer, StringComparison.Ordinal);
        Assert.Contains("50% high", footer, StringComparison.Ordinal);
        Assert.Contains("25% moderate", footer, StringComparison.Ordinal);
        Assert.Contains("25% low", footer, StringComparison.Ordinal);
        Assert.Contains("out/dir", footer, StringComparison.Ordinal);
    }

    [Fact]
    public void Suite_footer_with_no_scores_reports_zero_percentages_without_dividing_by_zero()
    {
        var footer = ConsoleSummaryReporter.FormatSuiteFooter(0, "out", []);

        Assert.Contains("0 scenarios recorded", footer, StringComparison.Ordinal);
        Assert.Contains("0% high", footer, StringComparison.Ordinal);
        Assert.Contains("0% low", footer, StringComparison.Ordinal);
    }

    [Fact]
    public void Test_result_reports_a_check_name_and_duration()
    {
        Assert.Equal(
            "    ✓ places_order (12ms)",
            ConsoleSummaryReporter.FormatTestResult("places_order", 12));
    }

    [Fact]
    public void Test_result_with_clarity_appends_two_decimal_invariant_score()
    {
        Assert.Equal(
            "    ✓ t (12ms, clarity: 0.50)",
            ConsoleSummaryReporter.FormatTestResult("t", 12, 0.5));
    }

    [Fact]
    public void Test_failure_renders_a_three_line_block_with_trace_pointer()
    {
        Assert.Equal(
            "    ✗ t (5ms)\n"
            + "      > IOException at Foo.bar:10\n"
            + "      > Full trace: /p/t.md",
            ConsoleSummaryReporter.FormatTestFailure(
                "t", 5, "IOException", "Foo.bar:10", "/p/t.md"));
    }

    [Fact]
    public void Suite_header_announces_recording()
    {
        Assert.Equal(
            "NarrativeTrace — Recording test narratives\n",
            ConsoleSummaryReporter.FormatSuiteHeader);
    }

    [Fact]
    public void Suite_footer_without_clarity_omits_the_clarity_line()
    {
        var footer = ConsoleSummaryReporter.FormatSuiteFooter(2, "out/dir");

        Assert.Contains("2 scenarios recorded", footer, StringComparison.Ordinal);
        Assert.Contains("Reports: out/dir", footer, StringComparison.Ordinal);
        Assert.DoesNotContain("Clarity:", footer, StringComparison.Ordinal);
    }

    [Fact]
    public void Boundary_scores_land_in_the_expected_buckets()
    {
        // 0.7 -> high, 0.4 -> moderate, just under 0.4 -> low.
        var footer = ConsoleSummaryReporter.FormatSuiteFooter(3, "o", [0.7, 0.4, 0.39]);

        Assert.Contains("33% high", footer, StringComparison.Ordinal);
        Assert.Contains("33% moderate", footer, StringComparison.Ordinal);
        Assert.Contains("33% low", footer, StringComparison.Ordinal);
    }
}
