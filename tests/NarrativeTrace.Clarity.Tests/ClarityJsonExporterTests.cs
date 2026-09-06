// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Text.Json;
using NarrativeTrace.Clarity;
using Xunit;

namespace NarrativeTrace.Clarity.Tests;

public class ClarityJsonExporterTests
{
    [Fact]
    public void Export_produces_java_keyed_scenario_object()
    {
        var result = new ClarityResult(
            0.85, 0.9, 0.8, 0.85, 0.75, 0.9, []);

        var json = ClarityJsonExporter.Export(
            result, "Place order");

        var root = JsonDocument.Parse(json).RootElement;
        Assert.Equal("Place order",
            root.GetProperty("name").GetString());
        Assert.Equal(0.85,
            root.GetProperty("overallScore").GetDouble());
    }

    [Fact]
    public void Export_includes_all_dimensions_with_java_keys()
    {
        var result = new ClarityResult(
            0.85, 0.9, 0.8, 0.85, 0.75, 0.9, []);

        var json = ClarityJsonExporter.Export(result, "test");

        var root = JsonDocument.Parse(json).RootElement;
        Assert.Equal(0.9, root.GetProperty("methodNameScore").GetDouble());
        Assert.Equal(0.8, root.GetProperty("classNameScore").GetDouble());
        Assert.Equal(0.85, root.GetProperty("parameterNameScore").GetDouble());
        Assert.Equal(0.75, root.GetProperty("structuralScore").GetDouble());
        Assert.Equal(0.9, root.GetProperty("cohesionScore").GetDouble());
    }

    [Fact]
    public void Scores_are_formatted_to_two_decimal_places()
    {
        var result = new ClarityResult(
            0.8, 0.8, 0.8, 0.8, 0.8, 0.8, []);

        var json = ClarityJsonExporter.Export(result, "test");

        Assert.Contains("\"overallScore\": 0.80", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Issue_severity_is_upper_cased()
    {
        var result = new ClarityResult(
            0.4, 0.3, 0.5, 0.4, 0.5, 0.3,
            [
                new ClarityIssue(
                    "method-name", "Svc.run", "Use domain verb",
                    ClaritySeverity.High, 2),
            ]);

        var json = ClarityJsonExporter.Export(result, "test");

        var issue = JsonDocument.Parse(json).RootElement
            .GetProperty("issues")[0];
        Assert.Equal("HIGH", issue.GetProperty("severity").GetString());
    }

    [Fact]
    public void ExportReport_wraps_scenarios_in_versioned_envelope()
    {
        var results = new[]
        {
            new ScenarioClarity("Order",
                new ClarityResult(0.8, 0.9, 0.8, 0.7, 0.8, 0.9, [])),
            new ScenarioClarity("Payment",
                new ClarityResult(0.7, 0.6, 0.8, 0.7, 0.8, 0.8, [])),
        };

        var json = ClarityJsonExporter.ExportReport(results);

        var root = JsonDocument.Parse(json).RootElement;
        Assert.Equal("1.0", root.GetProperty("version").GetString());
        var scenarios = root.GetProperty("scenarios");
        Assert.Equal(2, scenarios.GetArrayLength());
        Assert.Equal("Order", scenarios[0].GetProperty("name").GetString());
        Assert.Equal("Payment", scenarios[1].GetProperty("name").GetString());
    }
}
