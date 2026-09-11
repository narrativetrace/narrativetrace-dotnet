// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Build;
using Xunit;

namespace Build.Tests;

/// <summary>
/// Covers <see cref="StrykerReportSupport"/> against the real mutation-testing-elements shape
/// (confirmed field-for-field against a real run of this repo's own
/// <c>stryker-config.valuerefs.json</c>: Stryker's own console summary — Killed 111, Survived 3,
/// Timeout 2 — reproduced exactly from its <c>mutation-report.json</c>).
/// </summary>
public sealed class StrykerReportSupportTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("nt-stryker-report").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private static string Mutant(string status) =>
        "{\"id\": \"1\", \"mutatorName\": \"x\", \"status\": \"" + status + "\"}";

    private string WriteReport(params string[] statuses)
    {
        var mutants = string.Join(",", statuses.Select(Mutant));
        var json = "{\"files\": {\"a.cs\": {\"language\": \"cs\", \"mutants\": [" + mutants + "]}}}";
        var path = Path.Combine(_dir, "mutation-report.json");
        File.WriteAllText(path, json);
        return path;
    }

    [Fact]
    public void Read_returns_null_when_the_report_file_is_absent()
    {
        Assert.Null(StrykerReportSupport.Read(Path.Combine(_dir, "does-not-exist.json")));
    }

    [Fact]
    public void Read_counts_each_real_status_into_its_own_bucket()
    {
        var counts = StrykerReportSupport.Read(WriteReport("Killed", "Killed", "Survived", "NoCoverage", "Timeout"));

        Assert.NotNull(counts);
        Assert.Equal(2, counts!.Killed);
        Assert.Equal(1, counts.Survived);
        Assert.Equal(1, counts.NoCoverage);
        Assert.Equal(1, counts.Timeout);
    }

    [Fact]
    public void Read_excludes_Ignored_and_CompileError_from_every_count_including_the_score_denominator()
    {
        var counts = StrykerReportSupport.Read(
            WriteReport("Killed", "Killed", "Killed", "Ignored", "Ignored", "CompileError", "RuntimeError"));

        Assert.NotNull(counts);
        Assert.Equal(3, counts!.Scored);
        Assert.Equal(100.0, counts.Score, precision: 3);
    }

    [Fact]
    public void Score_reproduces_Strykers_own_console_summary_for_a_real_run()
    {
        // From a real ./build.sh Mutation run of stryker-config.valuerefs.json on this repo's own
        // dev container: Stryker's own console printed "Killed: 111, Survived: 3, Timeout: 2,
        // Errors: 0" (NoCoverage: 1, from the report's own per-file breakdown).
        var counts = new MutationCounts(Killed: 111, Survived: 3, NoCoverage: 1, Timeout: 2);

        Assert.Equal(117, counts.Scored);
        Assert.Equal(94.87, counts.Score, precision: 2);
    }

    [Fact]
    public void Score_is_zero_not_NaN_when_nothing_was_scored()
    {
        Assert.Equal(0.0, MutationCounts.Empty.Score);
    }

    [Fact]
    public void Plus_sums_every_bucket_across_modules()
    {
        var core = new MutationCounts(10, 2, 1, 0);
        var proxy = new MutationCounts(5, 1, 0, 1);

        var total = core + proxy;

        Assert.Equal(15, total.Killed);
        Assert.Equal(3, total.Survived);
        Assert.Equal(1, total.NoCoverage);
        Assert.Equal(1, total.Timeout);
    }

    [Fact]
    public void ReadAll_reports_a_module_with_no_report_file_as_missing_not_zero()
    {
        var present = WriteReport("Killed", "Survived");
        var byModule = new Dictionary<string, string>
        {
            ["present"] = present,
            ["dropped"] = Path.Combine(_dir, "never-written.json"),
        };

        var totals = StrykerReportSupport.ReadAll(byModule, out var missing);

        Assert.Equal(["dropped"], missing);
        Assert.Equal(1, totals.Killed);
        Assert.Equal(1, totals.Survived);
    }
}
