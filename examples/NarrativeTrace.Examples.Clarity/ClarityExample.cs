// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Clarity;
using NarrativeTrace.Examples.Common;

namespace NarrativeTrace.Examples.Clarity;

/// <summary>
/// Tutorial runner for the clarity example: four hotel-booking scenarios at
/// different naming-quality tiers, wired identically, then scored by
/// <see cref="ClarityAnalyzer"/> and reported by
/// <see cref="ClarityReportRenderer"/> — so each score connects back to the
/// naming choice that caused it.
/// </summary>
public static class ClarityExample
{
    private const string Banner = "========================================";

    /// <summary>Runs every scenario and prints the clarity report.</summary>
    public static void Run(DemoRun run)
    {
        ArgumentNullException.ThrowIfNull(run);
        var results = new List<ScenarioClarity>
        {
            Scenario(run, "Scenario 1: Guest Books a Room (Excellent Naming)", "Guest books a room", HotelClarityDemo.ReservationTrace),
            Scenario(run, "Scenario 2: Booking via Manager (Adequate Naming)", "Booking via manager", HotelClarityDemo.BookingTrace),
            Scenario(run, "Scenario 3: Legacy Data Processing (Poor Naming)", "Legacy data processing", HotelClarityDemo.ProcessingTrace),
            Scenario(run, "Scenario 4: Guest Repository (Cohesion Mismatch)", "Guest repository operations", HotelClarityDemo.RepositoryTrace),
        };

        run.Info("\n\n" + Banner);
        run.Info("         CLARITY ANALYSIS REPORT");
        run.Info(Banner + "\n");
        run.Info("\n" + ClarityReportRenderer.Render(results));
    }

    private static ScenarioClarity Scenario(
        DemoRun run, string title, string name, Func<Core.INarrativeContext, Core.TraceTree> scenario)
    {
        run.BeginScenario(title);
        var trace = scenario(run.Context);
        run.TraceTree(trace);
        return new ScenarioClarity(name, ClarityAnalyzer.Analyze(trace));
    }
}
