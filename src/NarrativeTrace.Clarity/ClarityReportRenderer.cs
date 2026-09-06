// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Globalization;
using System.Text;

namespace NarrativeTrace.Clarity;

/// <summary>
/// Markdown renderer for a clarity suite report, mirroring Java's
/// <c>renderSuiteReport</c> (CLARITY-4): a <c># Clarity Suite Report</c> title,
/// a <c>## Scenarios</c> table sorted ascending by overall score, then a
/// weighted per-scenario detail block (with the 0.30/0.20/0.25/0.15/0.10 dimension
/// weights and issues) for every scenario scoring below the low-score threshold.
/// </summary>
public static class ClarityReportRenderer
{
    private const double LowScoreThreshold = 0.7;

    /// <summary>Renders suite clarity results as a human-readable Markdown report.</summary>
    /// <param name="results">
    /// One entry per analyzed scenario, in the order they should appear. An
    /// empty list still renders a well-formed report rather than an empty string.
    /// </param>
    /// <returns>The Markdown report.</returns>
    public static string Render(IReadOnlyList<ScenarioClarity> results)
    {
        var sorted = results
            .OrderBy(r => r.Result.Overall)
            .ToList();

        var sb = new StringBuilder();
        sb.AppendLine("# Clarity Suite Report").AppendLine();
        AppendScenariosTable(sb, sorted);
        AppendDetails(sb, sorted);
        return sb.ToString().TrimEnd();
    }

    private static void AppendScenariosTable(
        StringBuilder sb, IReadOnlyList<ScenarioClarity> sorted)
    {
        sb.AppendLine("## Scenarios").AppendLine();
        sb.AppendLine("| Scenario | Score |");
        sb.AppendLine("|----------|-------|");
        for (var i = 0; i < sorted.Count; i++)
        {
            sb.AppendLine(Format(
                "| {0} | {1} |", sorted[i].Scenario, Score(sorted[i].Result.Overall)));
        }
    }

    private static void AppendDetails(
        StringBuilder sb, IReadOnlyList<ScenarioClarity> sorted)
    {
        for (var i = 0; i < sorted.Count; i++)
        {
            if (NeedsDetail(sorted[i]))
            {
                AppendScenarioDetail(sb, sorted[i]);
            }
        }
    }

    private static bool NeedsDetail(ScenarioClarity sr)
    {
        return sr.Result.Overall < LowScoreThreshold
            && sr.Result.Issues.Count > 0;
    }

    private static void AppendScenarioDetail(StringBuilder sb, ScenarioClarity sr)
    {
        sb.AppendLine().AppendLine(Format("### {0}", sr.Scenario)).AppendLine();
        AppendScoresTable(sb, sr.Result);
        sb.AppendLine();
        AppendIssuesTable(sb, sr.Result.Issues);
    }

    private static void AppendScoresTable(StringBuilder sb, ClarityResult r)
    {
        sb.AppendLine("## Scores").AppendLine();
        sb.AppendLine("| Category | Score | Weight | Weighted |");
        sb.AppendLine("|----------|-------|--------|----------|");
        AppendScoreRow(sb, "Method Names", r.Method, 0.30);
        AppendScoreRow(sb, "Class Names", r.Class, 0.20);
        AppendScoreRow(sb, "Parameter Names", r.Parameter, 0.25);
        AppendScoreRow(sb, "Structural", r.Structural, 0.15);
        AppendScoreRow(sb, "Cohesion", r.Cohesion, 0.10);
        sb.AppendLine(Format("| **Overall** | **{0}** | | |", Score(r.Overall)));
    }

    private static void AppendScoreRow(
        StringBuilder sb, string label, double score, double weight)
    {
        sb.AppendLine(Format(
            "| {0} | {1} | {2} | {3} |",
            label, Score(score), Score(weight), Score(score * weight)));
    }

    private static void AppendIssuesTable(
        StringBuilder sb, IReadOnlyList<ClarityIssue> issues)
    {
        sb.AppendLine(
            "| Severity | Category | Element | Occurrences | Suggestion |");
        sb.AppendLine(
            "|----------|----------|---------|-------------|------------|");
        for (var i = 0; i < issues.Count; i++)
        {
            var issue = issues[i];
            sb.AppendLine(Format(
                "| {0} | {1} | {2} | {3} | {4} |",
                issue.Severity.JsonName(), issue.Category, issue.Element,
                issue.Occurrences, issue.Suggestion));
        }
    }

    private static string Score(double value)
    {
        return value.ToString("F2", CultureInfo.InvariantCulture);
    }

    private static string Format(string template, params object[] args)
    {
        return string.Format(CultureInfo.InvariantCulture, template, args);
    }
}
