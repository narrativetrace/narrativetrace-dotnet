// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Clarity;
using NarrativeTrace.Core;
using Xunit;

namespace NarrativeTrace.Clarity.Tests;

public sealed class ClaritySuiteReporterTests : IDisposable
{
    private readonly string _dir = Path.Combine(
        Path.GetTempPath(), "nt-clarity-suite-" + Guid.NewGuid().ToString("N"));

    private static IReadOnlyList<KeyValuePair<string, TraceTree>> Entries(
        params string[] names)
    {
        return names
            .Select(n => new KeyValuePair<string, TraceTree>(
                n,
                new TraceTree([
                    new TraceNode(
                        new MethodSignature("Svc", "PlaceOrder", []),
                        new Returned(null), [], 0),
                ])))
            .ToArray();
    }

    [Fact]
    public void Writes_a_results_json_scoring_every_scenario()
    {
        var console = new StringWriter();

        ClaritySuiteReporter.Write(Entries("a", "b"), _dir, console);

        var json = File.ReadAllText(
            Path.Combine(_dir, "clarity-results.json"));
        Assert.Contains("\"name\": \"a\"", json, StringComparison.Ordinal);
        Assert.Contains("\"name\": \"b\"", json, StringComparison.Ordinal);
        Assert.Contains("2 scenarios recorded", console.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Writes_a_markdown_report_beside_the_results_json()
    {
        var console = new StringWriter();

        ClaritySuiteReporter.Write(Entries("a", "b"), _dir, console);

        var report = File.ReadAllText(
            Path.Combine(_dir, SuiteReportWriter.ReportFileName));
        Assert.Contains("# Clarity Suite Report", report, StringComparison.Ordinal);
        Assert.Contains("a", report, StringComparison.Ordinal);
        Assert.Contains("b", report, StringComparison.Ordinal);
    }

    [Fact]
    public void Report_lists_the_issues_found_in_the_suite()
    {
        var console = new StringWriter();
        var weak = new[]
        {
            new KeyValuePair<string, TraceTree>(
                "weak",
                new TraceTree([
                    new TraceNode(
                        new MethodSignature("Manager", "run", []),
                        new Returned(null), [], 0),
                ])),
        };

        ClaritySuiteReporter.Write(weak, _dir, console);

        var report = File.ReadAllText(
            Path.Combine(_dir, SuiteReportWriter.ReportFileName));
        Assert.Contains("method-name", report, StringComparison.Ordinal);
        Assert.Contains("Manager.run", report, StringComparison.Ordinal);
        Assert.Contains("HIGH", report, StringComparison.Ordinal);
    }

    [Fact]
    public void Empty_suite_writes_nothing()
    {
        var console = new StringWriter();

        ClaritySuiteReporter.Write(Entries(), _dir, console);

        Assert.False(Directory.Exists(_dir));
        Assert.Equal(string.Empty, console.ToString());
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }
}
