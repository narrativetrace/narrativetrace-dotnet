// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Text.Json;

namespace NarrativeTrace.Cli;

/// <summary>One scanned scenario's gate-relevant facts: name, overall score, HIGH-issue count.</summary>
internal sealed record ClarityCheckScenario(string Name, double Overall, int HighIssueCount);

/// <summary>
/// Parses the Java-compatible <c>clarity-results.json</c> envelope produced by
/// <c>clarity-scan</c> (and the test-framework integrations) into the minimal
/// shape the gate needs. The envelope is <c>{"version","scenarios":[…]}</c> with
/// <c>name</c>/<c>overallScore</c>-keyed scenarios; a missing <c>version</c> or
/// <c>scenarios</c> field is rejected, mirroring the Java parser. See CLARITY-3.
/// </summary>
internal static class ClarityResultsParser
{
    private const string HighSeverity = "HIGH";

    public static IReadOnlyList<ClarityCheckScenario> Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        Require(root, "version");
        var scenarios = new List<ClarityCheckScenario>();
        foreach (var element in Require(root, "scenarios").EnumerateArray())
        {
            scenarios.Add(new ClarityCheckScenario(
                element.GetProperty("name").GetString() ?? "",
                element.GetProperty("overallScore").GetDouble(),
                CountHighIssues(element)));
        }

        return scenarios;
    }

    private static JsonElement Require(JsonElement root, string field)
    {
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty(field, out var value))
        {
            throw new JsonException(
                $"clarity results missing '{field}' field");
        }

        return value;
    }

    private static int CountHighIssues(JsonElement element)
    {
        if (!element.TryGetProperty("issues", out var issues))
        {
            return 0;
        }

        var count = 0;
        foreach (var issue in issues.EnumerateArray())
        {
            if (issue.TryGetProperty("severity", out var severity)
                && string.Equals(
                    severity.GetString(), HighSeverity,
                    StringComparison.OrdinalIgnoreCase))
            {
                count++;
            }
        }

        return count;
    }
}
