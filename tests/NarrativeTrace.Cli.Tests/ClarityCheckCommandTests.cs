// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Cli;
using Xunit;

namespace NarrativeTrace.Cli.Tests;

public sealed class ClarityCheckCommandTests : IDisposable
{
    private readonly string _resultsPath = Path.Combine(
        Path.GetTempPath(), "nt-check-" + Guid.NewGuid().ToString("N") + ".json");

    private readonly StringWriter _out = new();
    private readonly StringWriter _err = new();

    public void Dispose()
    {
        if (File.Exists(_resultsPath))
        {
            File.Delete(_resultsPath);
        }
    }

    private void WriteResults(string json)
    {
        File.WriteAllText(_resultsPath, json);
    }

    [Fact]
    public void Score_equal_to_min_passes()
    {
        WriteResults("""{"version":"1.0","scenarios":[{"name":"Svc","overallScore":0.80,"issues":[]}]}""");

        var exit = ClarityCheckCommand.Run(
            _resultsPath, minScore: 0.80, maxHighIssues: 0, warnOnly: false, _out, _err);

        Assert.Equal(0, exit);
    }

    [Fact]
    public void Score_just_below_min_fails_with_exit_one_and_names_the_scenario()
    {
        WriteResults("""{"version":"1.0","scenarios":[{"name":"Svc","overallScore":0.79,"issues":[]}]}""");

        var exit = ClarityCheckCommand.Run(
            _resultsPath, minScore: 0.80, maxHighIssues: 0, warnOnly: false, _out, _err);

        Assert.Equal(1, exit);
        Assert.Contains("Svc", _err.ToString(), StringComparison.Ordinal);
        Assert.Contains("0.79", _err.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Warn_only_downgrades_a_failure_to_a_warning_with_exit_zero()
    {
        WriteResults("""{"version":"1.0","scenarios":[{"name":"Svc","overallScore":0.10,"issues":[]}]}""");

        var exit = ClarityCheckCommand.Run(
            _resultsPath, minScore: 0.80, maxHighIssues: 0, warnOnly: true, _out, _err);

        Assert.Equal(0, exit);
        Assert.Contains("warning", _out.ToString(), StringComparison.Ordinal);
        Assert.Empty(_err.ToString());
    }

    [Fact]
    public void High_issue_count_over_max_fails()
    {
        WriteResults(
            """{"version":"1.0","scenarios":[{"name":"Svc","overallScore":0.95,"issues":[{"severity":"HIGH","description":"d","suggestion":"s"},{"severity":"LOW","description":"d","suggestion":"s"},{"severity":"HIGH","description":"d","suggestion":"s"}]}]}""");

        var exit = ClarityCheckCommand.Run(
            _resultsPath, minScore: 0.0, maxHighIssues: 1, warnOnly: false, _out, _err);

        Assert.Equal(1, exit);
        Assert.Contains("2 HIGH issues", _err.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Lowercase_high_severity_still_trips_the_gate()
    {
        // A hand-authored or legacy file may carry lowercase "high"; the gate
        // matches severity case-insensitively, not only the uppercase "HIGH".
        WriteResults(
            """{"version":"1.0","scenarios":[{"name":"Svc","overallScore":0.95,"issues":[{"severity":"high","description":"d","suggestion":"s"}]}]}""");

        var exit = ClarityCheckCommand.Run(
            _resultsPath, minScore: 0.0, maxHighIssues: 0, warnOnly: false, _out, _err);

        Assert.Equal(1, exit);
        Assert.Contains("1 HIGH issues", _err.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Missing_results_file_is_skipped_not_failed()
    {
        var exit = ClarityCheckCommand.Run(
            _resultsPath, minScore: 0.9, maxHighIssues: 0, warnOnly: false, _out, _err);

        Assert.Equal(0, exit);
        Assert.Contains("skipping", _out.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Malformed_results_file_reports_error_with_exit_two()
    {
        WriteResults("{ this is not valid json");

        var exit = ClarityCheckCommand.Run(
            _resultsPath, minScore: 0.9, maxHighIssues: 0, warnOnly: false, _out, _err);

        Assert.Equal(2, exit);
        Assert.Contains("malformed", _err.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void All_scenarios_passing_returns_zero_with_a_pass_message()
    {
        WriteResults(
            """{"version":"1.0","scenarios":[{"name":"A","overallScore":0.9,"issues":[]},{"name":"B","overallScore":0.85,"issues":[]}]}""");

        var exit = ClarityCheckCommand.Run(
            _resultsPath, minScore: 0.8, maxHighIssues: 0, warnOnly: false, _out, _err);

        Assert.Equal(0, exit);
        Assert.Contains("passed", _out.ToString(), StringComparison.Ordinal);
    }
}
