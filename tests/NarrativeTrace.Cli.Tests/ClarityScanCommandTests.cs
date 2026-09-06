// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Cli;
using NarrativeTrace.Clarity;
using Xunit;

namespace NarrativeTrace.Cli.Tests;

public sealed class ClarityScanCommandTests : IDisposable
{
    private readonly string _outputDir = Path.Combine(
        Path.GetTempPath(), "nt-clarity-" + Guid.NewGuid().ToString("N"));

    private readonly StringWriter _out = new();
    private readonly StringWriter _err = new();

    private static string ClarityAssemblyPath =>
        typeof(ClarityScanner).Assembly.Location;

    private string JsonPath => Path.Combine(_outputDir, ClarityScanCommand.ResultsFileName);

    private string MarkdownPath => Path.Combine(_outputDir, ClarityScanCommand.ReportFileName);

    public void Dispose()
    {
        if (Directory.Exists(_outputDir))
        {
            Directory.Delete(_outputDir, recursive: true);
        }
    }

    [Fact]
    public void Default_both_format_writes_json_and_markdown()
    {
        var exit = ClarityScanCommand.Run(
            ClarityAssemblyPath, _outputDir, "both", _out, _err);

        Assert.Equal(0, exit);
        Assert.True(File.Exists(JsonPath));
        Assert.True(File.Exists(MarkdownPath));
        var json = File.ReadAllText(JsonPath);
        Assert.Contains("\"version\": \"1.0\"", json, StringComparison.Ordinal);
        Assert.Contains("# Clarity Suite Report", File.ReadAllText(MarkdownPath), StringComparison.Ordinal);
    }

    [Fact]
    public void Scan_writes_scan_specific_filenames()
    {
        var exit = ClarityScanCommand.Run(
            ClarityAssemblyPath, _outputDir, "both", _out, _err);

        Assert.Equal(0, exit);
        Assert.True(File.Exists(Path.Combine(_outputDir, "clarity-scan-results.json")));
        Assert.True(File.Exists(Path.Combine(_outputDir, "clarity-scan-report.md")));
    }

    [Fact]
    public void Scan_never_overwrites_the_test_run_artifacts_the_gate_reads()
    {
        // Literals on purpose: the whole point is that these two names belong
        // to the test run (SuiteReportWriter) and the aggregate verb, and that
        // a scan must never land on them. Renaming both constants together
        // would keep a constant-based assertion green.
        Directory.CreateDirectory(_outputDir);
        var runResults = Path.Combine(_outputDir, "clarity-results.json");
        var runReport = Path.Combine(_outputDir, "clarity-report.md");
        File.WriteAllText(runResults, "{\"from\":\"test-run\"}");
        File.WriteAllText(runReport, "# from test run");

        var exit = ClarityScanCommand.Run(
            ClarityAssemblyPath, _outputDir, "both", _out, _err);

        Assert.Equal(0, exit);
        Assert.Equal("{\"from\":\"test-run\"}", File.ReadAllText(runResults));
        Assert.Equal("# from test run", File.ReadAllText(runReport));
    }

    [Fact]
    public void Json_format_writes_only_json()
    {
        var exit = ClarityScanCommand.Run(
            ClarityAssemblyPath, _outputDir, "json", _out, _err);

        Assert.Equal(0, exit);
        Assert.True(File.Exists(JsonPath));
        Assert.False(File.Exists(MarkdownPath));
    }

    [Fact]
    public void Md_format_writes_only_markdown()
    {
        var exit = ClarityScanCommand.Run(
            ClarityAssemblyPath, _outputDir, "md", _out, _err);

        Assert.Equal(0, exit);
        Assert.True(File.Exists(MarkdownPath));
        Assert.False(File.Exists(JsonPath));
    }

    [Fact]
    public void Unknown_format_is_rejected_with_exit_two()
    {
        var exit = ClarityScanCommand.Run(
            ClarityAssemblyPath, _outputDir, "yaml", _out, _err);

        Assert.Equal(2, exit);
        Assert.Contains("--format", _err.ToString(), StringComparison.Ordinal);
        Assert.False(Directory.Exists(_outputDir));
    }

    [Fact]
    public void Run_reports_error_and_nonzero_exit_when_assembly_is_missing()
    {
        var exit = ClarityScanCommand.Run(
            Path.Combine(_outputDir, "does-not-exist.dll"), _outputDir, "both", _out, _err);

        Assert.Equal(1, exit);
        Assert.Contains("not found", _err.ToString(), StringComparison.Ordinal);
        Assert.False(File.Exists(JsonPath));
    }
}
