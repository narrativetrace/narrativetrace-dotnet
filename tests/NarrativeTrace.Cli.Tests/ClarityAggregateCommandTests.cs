// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Cli;
using Xunit;

namespace NarrativeTrace.Cli.Tests;

public sealed class ClarityAggregateCommandTests : IDisposable
{
    private readonly string _inputDir = Path.Combine(
        Path.GetTempPath(), "nt-agg-" + Guid.NewGuid().ToString("N"));

    private readonly StringWriter _out = new();
    private readonly StringWriter _err = new();

    public ClarityAggregateCommandTests()
    {
        Directory.CreateDirectory(_inputDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_inputDir))
        {
            Directory.Delete(_inputDir, recursive: true);
        }
    }

    private void WritePerTest(string name, double overall)
    {
        var json = $$"""
            {"name":"{{name}}","overallScore":{{overall.ToString(System.Globalization.CultureInfo.InvariantCulture)}},"issues":[]}
            """;
        File.WriteAllText(Path.Combine(_inputDir, name + ".clarity.json"), json);
    }

    [Fact]
    public void Aggregates_per_test_clarity_files_into_the_gate_results_envelope()
    {
        WritePerTest("Alpha", 0.90);
        WritePerTest("Beta", 0.40);

        var exit = ClarityAggregateCommand.Run(_inputDir, _inputDir, _out, _err);

        Assert.Equal(0, exit);
        var results = Path.Combine(_inputDir, ClarityAggregateCommand.ResultsFileName);
        var json = File.ReadAllText(results);
        Assert.Contains("\"version\": \"1.0\"", json, StringComparison.Ordinal);
        Assert.Contains("Alpha", json, StringComparison.Ordinal);
        Assert.Contains("Beta", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Aggregated_envelope_is_readable_by_the_gate()
    {
        WritePerTest("Weak", 0.10);

        var exit = ClarityAggregateCommand.Run(_inputDir, _inputDir, _out, _err);
        Assert.Equal(0, exit);

        var results = Path.Combine(_inputDir, ClarityAggregateCommand.ResultsFileName);
        var gate = ClarityCheckCommand.Run(
            results, minScore: 0.5, maxHighIssues: 0, warnOnly: false, _out, _err);

        Assert.Equal(1, gate);
        Assert.Contains("Weak", _err.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Missing_input_directory_reports_error_with_exit_one()
    {
        var missing = Path.Combine(_inputDir, "nope");

        var exit = ClarityAggregateCommand.Run(missing, missing, _out, _err);

        Assert.Equal(1, exit);
        Assert.Contains("not found", _err.ToString(), StringComparison.Ordinal);
    }
}
