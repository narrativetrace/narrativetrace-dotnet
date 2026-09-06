// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using Xunit;

namespace NarrativeTrace.Core.Tests;

public sealed class SuiteReportWriterTests : IDisposable
{
    private readonly string _dir = Path.Combine(
        Path.GetTempPath(), "nt-suite-" + Guid.NewGuid().ToString("N"));

    private static readonly Func<TraceTree, double> HighScore = _ => 0.9;

    private static string JoinNames(
        IReadOnlyList<KeyValuePair<string, TraceTree>> entries)
    {
        return string.Join(",", entries.Select(e => e.Key));
    }

    private static IReadOnlyList<KeyValuePair<string, TraceTree>> Entries(
        params string[] names)
    {
        return names
            .Select(n => new KeyValuePair<string, TraceTree>(n, new TraceTree([])))
            .ToArray();
    }

    [Fact]
    public void Writes_results_json_with_every_entry_including_duplicates()
    {
        var entries = Entries("places order", "places order", "ships order");

        SuiteReportWriter.Write(
            entries, _dir, TextWriter.Null, HighScore, JoinNames, JoinNames);

        var json = File.ReadAllText(
            Path.Combine(_dir, "clarity-results.json"));
        Assert.Equal("places order,places order,ships order", json);
    }

    [Fact]
    public void Writes_the_markdown_report_beside_the_results_json()
    {
        SuiteReportWriter.Write(
            Entries("places order"), _dir, TextWriter.Null,
            HighScore, JoinNames, _ => "# report body");

        Assert.Equal(
            "# report body",
            File.ReadAllText(
                Path.Combine(_dir, SuiteReportWriter.ReportFileName)));
    }

    [Fact]
    public void Prints_the_footer_once_with_the_scenario_count()
    {
        var console = new StringWriter();

        SuiteReportWriter.Write(
            Entries("a", "b", "c"), _dir, console, HighScore, JoinNames, JoinNames);

        var text = console.ToString();
        Assert.Contains("3 scenarios recorded", text, StringComparison.Ordinal);
        Assert.Equal(
            1,
            text.Split("Suite complete").Length - 1);
    }

    [Fact]
    public void A_lossy_run_names_what_it_lost_in_the_footer()
    {
        var console = new StringWriter();

        SuiteReportWriter.Write(
            Entries("a"), _dir, console, HighScore, JoinNames, JoinNames,
            new TraceLoss(0, 3, 4100));

        Assert.Contains(
            "Incomplete: 3 async scopes not adopted (cap, 4,100 spans)",
            console.ToString(),
            StringComparison.Ordinal);
    }

    [Fact]
    public void A_clean_run_prints_no_loss_line_at_all()
    {
        var console = new StringWriter();

        SuiteReportWriter.Write(
            Entries("a"), _dir, console, HighScore, JoinNames, JoinNames,
            TraceLoss.None);

        Assert.DoesNotContain(
            "Incomplete", console.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void A_caller_that_tracks_no_loss_prints_no_loss_line()
    {
        var console = new StringWriter();

        SuiteReportWriter.Write(
            Entries("a"), _dir, console, HighScore, JoinNames, JoinNames);

        Assert.DoesNotContain(
            "Incomplete", console.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Empty_suite_writes_no_files_and_prints_no_footer()
    {
        var console = new StringWriter();

        SuiteReportWriter.Write(
            Entries(), _dir, console, HighScore, JoinNames, JoinNames);

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
