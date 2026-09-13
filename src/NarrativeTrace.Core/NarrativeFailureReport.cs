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

    /// <summary>
    /// Builds a failure report that localizes change instead of dumping the
    /// trace: when the failing scenario's structure changed since last
    /// green, the report is the delta — summary plus readable diff —
    /// because assertion output already covers detection and the trace's
    /// job here is saying <em>where</em> behavior moved.
    /// </summary>
    /// <returns>
    /// The delta-aware report for <see cref="ScenarioDeltaKind.Changed"/> or
    /// <see cref="ScenarioDeltaKind.Unchanged"/>; falls back to
    /// <see cref="Build(string, TraceTree)"/> for
    /// <see cref="ScenarioDeltaKind.New"/> (nothing to localize against yet)
    /// and for an empty trace.
    /// </returns>
    public static string Build(string testName, TraceTree trace, ScenarioDelta delta)
    {
        if (trace.IsEmpty)
        {
            return string.Empty;
        }

        var scenario = ScenarioFramer.Frame(testName);
        return delta.Kind switch
        {
            ScenarioDeltaKind.Changed =>
                $"Narrative for failed test — {scenario}{Environment.NewLine}"
                + $"Changed since last green ({delta.Summary}):{Environment.NewLine}{delta.Diff}",
            ScenarioDeltaKind.Unchanged =>
                $"Narrative for failed test — {scenario}{Environment.NewLine}"
                + "Structure unchanged since last green — the flow held; check values and assertions."
                + $"{Environment.NewLine}{Environment.NewLine}{IndentedTextRenderer.Render(trace)}",
            _ => Build(testName, trace),
        };
    }
}
