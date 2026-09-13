// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using Xunit;

namespace NarrativeTrace.Core.Tests;

public sealed class NarrativeFailureReportDeltaTests
{
    private static TraceTree TreeWithNode()
    {
        return new TraceTree([
            new TraceNode(new MethodSignature("Svc", "Run", []), new Returned(null), [], 0),
        ]);
    }

    [Fact]
    public void Changed_delta_reports_the_summary_and_diff_instead_of_the_whole_trace()
    {
        var delta = new ScenarioDelta("Run", ScenarioDeltaKind.Changed, "+1 call Svc.Extra", " a\n+b\n");

        var report = NarrativeFailureReport.Build("Run", TreeWithNode(), delta);

        Assert.Contains("Changed since last green (+1 call Svc.Extra):", report, StringComparison.Ordinal);
        Assert.Contains(" a\n+b\n", report, StringComparison.Ordinal);
        Assert.DoesNotContain("Svc.Run", report, StringComparison.Ordinal);
    }

    [Fact]
    public void Unchanged_delta_reports_that_the_flow_held_and_still_shows_the_trace()
    {
        var delta = new ScenarioDelta("Run", ScenarioDeltaKind.Unchanged, string.Empty, string.Empty);

        var report = NarrativeFailureReport.Build("Run", TreeWithNode(), delta);

        Assert.Contains("Structure unchanged since last green", report, StringComparison.Ordinal);
        Assert.Contains("Svc.Run", report, StringComparison.Ordinal);
    }

    [Fact]
    public void New_delta_falls_back_to_the_whole_trace_dump()
    {
        var delta = new ScenarioDelta("Run", ScenarioDeltaKind.New, string.Empty, string.Empty);

        var report = NarrativeFailureReport.Build("Run", TreeWithNode(), delta);

        Assert.Equal(NarrativeFailureReport.Build("Run", TreeWithNode()), report);
    }

    [Fact]
    public void Empty_trace_reports_nothing_regardless_of_delta()
    {
        var delta = new ScenarioDelta("Run", ScenarioDeltaKind.Changed, "+1 call X.y", "diff");

        var report = NarrativeFailureReport.Build("Run", new TraceTree([]), delta);

        Assert.Equal(string.Empty, report);
    }
}
