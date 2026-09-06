// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Examples.Clarity;
using NarrativeTrace.Examples.Common;
using Xunit;

namespace NarrativeTrace.Examples.Clarity.Tests;

public sealed class ClarityExampleTests
{
    private static string RunExample()
    {
        using var output = new StringWriter();
        using var run = new DemoRun(new ConsoleLoggerFactory(output, LogFormat.Bare), "clarity");
        ClarityExample.Run(run);
        return output.ToString();
    }

    [Fact]
    public void Run_walks_the_four_naming_tiers_then_prints_the_clarity_report()
    {
        var text = RunExample();

        var headers = text.Split('\n').Where(l => l.StartsWith("=== ", StringComparison.Ordinal)).ToList();
        Assert.Equal(
            [
                "=== Scenario 1: Guest Books a Room (Excellent Naming) ===",
                "=== Scenario 2: Booking via Manager (Adequate Naming) ===",
                "=== Scenario 3: Legacy Data Processing (Poor Naming) ===",
                "=== Scenario 4: Guest Repository (Cohesion Mismatch) ===",
            ],
            headers);
        Assert.Equal(4, text.Split("--- Trace tree ---").Length - 1);
        Assert.Contains("CLARITY ANALYSIS REPORT", text, StringComparison.Ordinal);
        Assert.Contains("# Clarity Suite Report", text, StringComparison.Ordinal);
    }
}
