// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Reflection;
using Xunit;

namespace NarrativeTrace.Examples.Clarity.Tests;

/// <summary>
/// Runs the example's real entry point (top-level <c>Program</c>), which
/// <see cref="ClarityExampleTests"/> deliberately bypasses by constructing a
/// <c>DemoRun</c> by hand. This is the only path that exercises the shipped
/// <c>Program.cs</c> — argument parsing plus <c>DemoRun.Create</c> — so a
/// broken entry point (a bad default option, a renamed factory) is caught
/// here rather than only when a user runs the published example.
/// </summary>
public sealed class ProgramTests
{
    [Fact]
    public void The_shipped_entry_point_runs_the_demo_end_to_end()
    {
        var main = EntryPoint();
        var original = Console.Out;
        using var captured = new StringWriter();
        Console.SetOut(captured);
        try
        {
            main.Invoke(null, [Array.Empty<string>()]);
        }
        finally
        {
            Console.SetOut(original);
        }

        Assert.Contains(
            "CLARITY ANALYSIS REPORT", captured.ToString(), StringComparison.Ordinal);
    }

    private static MethodInfo EntryPoint()
    {
        var programType = typeof(ClarityExample).Assembly.GetType("Program", throwOnError: true)!;
        return programType.GetMethod(
                   "<Main>$", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
               ?? throw new InvalidOperationException("no top-level entry point on Program");
    }
}
