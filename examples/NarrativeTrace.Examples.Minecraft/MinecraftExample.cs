// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Globalization;
using NarrativeTrace.Examples.Common;

namespace NarrativeTrace.Examples.Minecraft;

/// <summary>
/// Tutorial runner for the naming demo: the same "player joins world" call
/// graph traced twice, once through domain-rich names and once through
/// generic ones, so the two traces can be compared side by side.
/// </summary>
public static class MinecraftExample
{
    /// <summary>Runs both halves and closes with their clarity scores.</summary>
    public static void Run(DemoRun run)
    {
        ArgumentNullException.ThrowIfNull(run);
        RunRefactored(run);
        RunUnrefactored(run);
    }

    private static void RunRefactored(DemoRun run)
    {
        run.BeginScenario("Refactored: Player Joins World");
        run.Info("  Domain-specific names make the trace self-documenting.\n");

        var trace = MinecraftNamingDemo.TraceRefactored(run.Context);
        run.TraceTree(trace);
        run.Mermaid(trace);
        run.PlantUml(trace);
    }

    private static void RunUnrefactored(DemoRun run)
    {
        run.BeginScenario("Unrefactored: Player Joins World");
        run.Info("  Generic names — same behavior, but the trace tells you nothing.\n");

        var trace = MinecraftNamingDemo.TraceUnrefactored(run.Context);
        run.TraceTree(trace);

        run.Info("\n  ^ Same call graph. Same return values. Only names differ.");
        run.Info("  If your code can't tell its own story, it needs refactoring.");
        var (clean, cryptic) = MinecraftNamingDemo.CompareClarity();
        run.Info(string.Create(
            CultureInfo.InvariantCulture,
            $"  ClarityScanner agrees: refactored {clean:F2} vs unrefactored {cryptic:F2}."));
    }
}
