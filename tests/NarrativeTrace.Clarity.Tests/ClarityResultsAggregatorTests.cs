// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Text.Json;
using NarrativeTrace.Clarity;
using Xunit;

namespace NarrativeTrace.Clarity.Tests;

public class ClarityResultsAggregatorTests
{
    private static string Scenario(string name, double overall)
    {
        return ClarityJsonExporter.Export(
            new ClarityResult(overall, overall, overall, overall, overall, overall, []),
            name);
    }

    [Fact]
    public void Wraps_scenario_objects_in_a_versioned_envelope()
    {
        var json = ClarityResultsAggregator.Aggregate(
            [Scenario("A", 0.8), Scenario("B", 0.7)]);

        var root = JsonDocument.Parse(json).RootElement;
        Assert.Equal("1.0", root.GetProperty("version").GetString());
        var scenarios = root.GetProperty("scenarios");
        Assert.Equal(2, scenarios.GetArrayLength());
        Assert.Equal("A", scenarios[0].GetProperty("name").GetString());
        Assert.Equal("B", scenarios[1].GetProperty("name").GetString());
    }

    [Fact]
    public void Preserves_scenario_scores_and_issues()
    {
        var withIssue = ClarityJsonExporter.Export(
            new ClarityResult(0.4, 0.4, 0.4, 0.4, 0.4, 0.4,
                [new ClarityIssue(
                    "method-name", "Svc.run", "fix it", ClaritySeverity.High)]),
            "Login");

        var json = ClarityResultsAggregator.Aggregate([withIssue]);

        var scenario = JsonDocument.Parse(json).RootElement
            .GetProperty("scenarios")[0];
        Assert.Equal(0.4, scenario.GetProperty("overallScore").GetDouble());
        Assert.Equal("HIGH", scenario.GetProperty("issues")[0].GetProperty("severity").GetString());
    }

    [Fact]
    public void Empty_input_produces_an_empty_scenarios_envelope()
    {
        var json = ClarityResultsAggregator.Aggregate([]);

        var root = JsonDocument.Parse(json).RootElement;
        Assert.Equal("1.0", root.GetProperty("version").GetString());
        Assert.Equal(0, root.GetProperty("scenarios").GetArrayLength());
    }
}
