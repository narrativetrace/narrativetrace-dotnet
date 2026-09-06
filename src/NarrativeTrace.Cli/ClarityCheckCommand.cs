// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Globalization;
using System.Text.Json;

namespace NarrativeTrace.Cli;

/// <summary>
/// The <c>clarity-check</c> verb: parses a <c>clarity-results.json</c> report and
/// fails (non-zero exit) when any scenario scores below <c>--min-score</c> or has
/// more than <c>--max-high-issues</c> HIGH issues. <c>--warn-only</c> downgrades a
/// failure to a warning (exit 0). A missing results file is skipped, not failed.
/// </summary>
public static class ClarityCheckCommand
{
    /// <summary>Runs the clarity gate; returns the process exit code.</summary>
    public static int Run(
        string resultsPath, double minScore, int maxHighIssues,
        bool warnOnly, TextWriter output, TextWriter error)
    {
        if (!File.Exists(resultsPath))
        {
            output.WriteLine(
                $"NarrativeTrace: no clarity results at {resultsPath} — skipping clarity check.");
            return 0;
        }

        if (!TryParse(resultsPath, error, out var scenarios))
        {
            return 2;
        }

        var failures = CollectFailures(scenarios, minScore, maxHighIssues);
        return Report(failures, warnOnly, output, error);
    }

    private static bool TryParse(
        string resultsPath, TextWriter error, out IReadOnlyList<ClarityCheckScenario> scenarios)
    {
        try
        {
            scenarios = ClarityResultsParser.Parse(File.ReadAllText(resultsPath));
            return true;
        }
        catch (JsonException ex)
        {
            error.WriteLine($"error: malformed clarity results: {ex.Message}");
            scenarios = [];
            return false;
        }
    }

    private static List<string> CollectFailures(
        IReadOnlyList<ClarityCheckScenario> scenarios, double minScore, int maxHighIssues)
    {
        var failures = new List<string>();
        foreach (var s in scenarios)
        {
            if (s.Overall < minScore)
            {
                failures.Add(string.Create(
                    CultureInfo.InvariantCulture,
                    $"  '{s.Name}': score {s.Overall:F2} < threshold {minScore:F2}"));
            }

            if (s.HighIssueCount > maxHighIssues)
            {
                failures.Add(string.Create(
                    CultureInfo.InvariantCulture,
                    $"  '{s.Name}': {s.HighIssueCount} HIGH issues (max {maxHighIssues})"));
            }
        }

        return failures;
    }

    private static int Report(
        List<string> failures, bool warnOnly, TextWriter output, TextWriter error)
    {
        if (failures.Count == 0)
        {
            output.WriteLine("Clarity check passed.");
            return 0;
        }

        var message = "Clarity check failed:" + Environment.NewLine +
            string.Join(Environment.NewLine, failures);
        if (warnOnly)
        {
            output.WriteLine("warning: " + message);
            return 0;
        }

        error.WriteLine(message);
        return 1;
    }
}
