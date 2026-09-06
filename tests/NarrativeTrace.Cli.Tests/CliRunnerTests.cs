// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Cli;
using NarrativeTrace.Clarity;
using Xunit;

namespace NarrativeTrace.Cli.Tests;

public sealed class CliRunnerTests : IDisposable
{
    private readonly string _outputDir = Path.Combine(
        Path.GetTempPath(), "nt-cli-" + Guid.NewGuid().ToString("N"));

    private readonly StringWriter _out = new();
    private readonly StringWriter _err = new();

    private static string ClarityAssemblyPath =>
        typeof(ClarityScanner).Assembly.Location;

    public void Dispose()
    {
        if (Directory.Exists(_outputDir))
        {
            Directory.Delete(_outputDir, recursive: true);
        }
    }

    [Fact]
    public void Clarity_scan_verb_writes_report_and_returns_zero()
    {
        var exit = CliRunner.Run(
            ["clarity-scan", "--assembly", ClarityAssemblyPath, "--output-dir", _outputDir],
            _out, _err);

        Assert.Equal(0, exit);
        Assert.True(File.Exists(
            Path.Combine(_outputDir, ClarityScanCommand.ResultsFileName)));
    }

    [Fact]
    public void Clarity_scan_md_format_writes_only_the_markdown_report()
    {
        var exit = CliRunner.Run(
            ["clarity-scan", "--assembly", ClarityAssemblyPath, "--output-dir", _outputDir,
             "--format", "md"],
            _out, _err);

        Assert.Equal(0, exit);
        Assert.True(File.Exists(Path.Combine(_outputDir, ClarityScanCommand.ReportFileName)));
        Assert.False(File.Exists(Path.Combine(_outputDir, ClarityScanCommand.ResultsFileName)));
    }

    [Fact]
    public void Clarity_scan_unknown_format_returns_exit_two()
    {
        var exit = CliRunner.Run(
            ["clarity-scan", "--assembly", ClarityAssemblyPath, "--output-dir", _outputDir,
             "--format", "yaml"],
            _out, _err);

        Assert.Equal(2, exit);
        Assert.Contains("--format", _err.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void No_arguments_prints_usage_and_returns_nonzero()
    {
        var exit = CliRunner.Run([], _out, _err);

        Assert.NotEqual(0, exit);
        Assert.Contains("usage", _err.ToString(), StringComparison.Ordinal);
        Assert.Contains("clarity-scan", _err.ToString(), StringComparison.Ordinal);
        Assert.Contains("--assembly", _err.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Clarity_scan_with_trailing_assembly_flag_and_no_value_errors_without_crashing()
    {
        var exit = CliRunner.Run(["clarity-scan", "--assembly"], _out, _err);

        Assert.NotEqual(0, exit);
        Assert.Contains("--assembly", _err.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Clarity_scan_without_assembly_reports_error()
    {
        var exit = CliRunner.Run(["clarity-scan", "--output-dir", _outputDir], _out, _err);

        Assert.NotEqual(0, exit);
        Assert.Contains("--assembly", _err.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Clarity_check_verb_fails_the_build_on_a_low_score()
    {
        Directory.CreateDirectory(_outputDir);
        var results = Path.Combine(_outputDir, "r.json");
        File.WriteAllText(results, """{"version":"1.0","scenarios":[{"name":"Svc","overallScore":0.10,"issues":[]}]}""");

        var exit = CliRunner.Run(
            ["clarity-check", "--results", results, "--min-score", "0.8", "--max-high-issues", "0"],
            _out, _err);

        Assert.Equal(1, exit);
    }

    [Fact]
    public void Clarity_check_verb_with_warn_only_flag_does_not_fail_the_build()
    {
        Directory.CreateDirectory(_outputDir);
        var results = Path.Combine(_outputDir, "r.json");
        File.WriteAllText(results, """{"version":"1.0","scenarios":[{"name":"Svc","overallScore":0.10,"issues":[]}]}""");

        var exit = CliRunner.Run(
            ["clarity-check", "--results", results, "--min-score", "0.8", "--warn-only"],
            _out, _err);

        Assert.Equal(0, exit);
    }

    [Fact]
    public void Clarity_check_without_results_reports_error()
    {
        var exit = CliRunner.Run(["clarity-check", "--min-score", "0.8"], _out, _err);

        Assert.NotEqual(0, exit);
        Assert.Contains("--results", _err.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Unknown_verb_reports_error_and_returns_nonzero()
    {
        var exit = CliRunner.Run(["frobnicate"], _out, _err);

        Assert.NotEqual(0, exit);
        Assert.Contains("frobnicate", _err.ToString(), StringComparison.Ordinal);
    }
}
