// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using Xunit;

namespace NarrativeTrace.Core.Tests;

public sealed class NarrativeFailureReportTests
{
    [Fact]
    public void Build_frames_the_scenario_and_includes_the_rendered_trace()
    {
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("OrderService", "PlaceOrder", []),
                new Returned(null), [], 0),
        ]);

        var report = NarrativeFailureReport.Build("Places_an_order", tree);

        Assert.Contains(ScenarioFramer.Frame("Places_an_order"), report, StringComparison.Ordinal);
        Assert.Contains("OrderService", report, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_of_an_empty_trace_is_empty()
    {
        var report = NarrativeFailureReport.Build("Nothing_ran", new TraceTree([]));

        Assert.Equal(string.Empty, report);
    }
}
