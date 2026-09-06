// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Core;

/// <summary>
/// Builds the human-readable report printed when a test fails: the framed
/// scenario name followed by the indented narrative of what the code did. Shared
/// by the xUnit and NUnit integrations so a failing test reads the same way in
/// either framework.
/// </summary>
public static class NarrativeFailureReport
{
    /// <summary>
    /// Builds a failure report for <paramref name="testName"/> from its captured
    /// <paramref name="trace"/>, or the empty string when nothing was traced.
    /// </summary>
    public static string Build(string testName, TraceTree trace)
    {
        if (trace.IsEmpty)
        {
            return string.Empty;
        }

        var scenario = ScenarioFramer.Frame(testName);
        var rendered = IndentedTextRenderer.Render(trace);
        return $"Narrative for failed test — {scenario}{Environment.NewLine}{rendered}";
    }
}
