// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Build;
using Xunit;

namespace Build.Tests;

/// <summary>
/// Covers <see cref="VerificationReportSupport"/> — the one writer and one renderer for
/// <c>reports/verification/&lt;date&gt;.json</c>/<c>.md</c>, the family-wide contract
/// <c>reports/verification/SCHEMA.md</c> (golden Java repo) defines.
/// </summary>
public sealed class VerificationReportSupportTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("nt-verification-report").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private static readonly DateTimeOffset Started = new(2026, 9, 9, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Ended = new(2026, 9, 9, 10, 0, 0, TimeSpan.Zero);

    private static CategoryResult Row(string category, string status) =>
        new(category, "some-tool 1.0", status, new Dictionary<string, object> { ["findings"] = 0 }, 1.5, null);

    [Fact]
    public void OverallStatus_is_failed_iff_at_least_one_row_failed()
    {
        var allPassing = new VerificationRun("dotnet", "0.1.1", "abc123", "host-arch", Started, Ended,
            [Row("format", "passed"), Row("secrets", "skipped"), Row("types", "not-implemented")]);
        var oneFailing = allPassing with { Categories = [Row("format", "passed"), Row("lint", "failed")] };

        Assert.Equal("passed", allPassing.OverallStatus);
        Assert.Equal("failed", oneFailing.OverallStatus);
    }

    [Fact]
    public void WriteJson_creates_parent_directories_and_round_trips_every_field()
    {
        var run = new VerificationRun(
            "dotnet", "0.1.1", "abc123", "host-arch", Started, Ended,
            [new CategoryResult(
                "mutation", "Stryker.NET 4.16.0", "passed",
                new Dictionary<string, object> { ["mutants_killed"] = 111, ["mutation_score"] = 94.87 },
                721.3, "some note")]);
        var path = Path.Combine(_dir, "nested", "2026-09-09.json");

        VerificationReportSupport.WriteJson(run, path);

        Assert.True(File.Exists(path));
        var text = File.ReadAllText(path);
        Assert.Contains("\"runtime\": \"dotnet\"", text, StringComparison.Ordinal);
        Assert.Contains("\"overall_status\": \"passed\"", text, StringComparison.Ordinal);
        Assert.Contains("\"mutants_killed\": 111", text, StringComparison.Ordinal);
        Assert.Contains("\"duration_seconds\": 721.3", text, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteJson_never_omits_a_field_even_when_metrics_or_note_are_empty()
    {
        var run = new VerificationRun(
            "dotnet", "0.1.1", "abc123", "host-arch", Started, Ended,
            [new CategoryResult("clarity", "none", "not-implemented", new Dictionary<string, object>(), 0.0, null)]);
        var path = Path.Combine(_dir, "2026-09-09.json");

        VerificationReportSupport.WriteJson(run, path);

        var text = File.ReadAllText(path);
        Assert.Contains("\"metrics\": {}", text, StringComparison.Ordinal);
        Assert.Contains("\"note\": null", text, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderMarkdown_reads_the_file_back_rather_than_a_second_in_memory_account()
    {
        // Writes the JSON with one run, then hand-edits the FILE before rendering — proving the
        // renderer is provably a rendering of what is on disk, never the VerificationRun the
        // caller already had in memory (mirrors ts's verify-all-schema.ts test intent).
        var run = new VerificationRun("dotnet", "0.1.1", "abc123", "host-arch", Started, Ended, [Row("format", "passed")]);
        var path = Path.Combine(_dir, "2026-09-09.json");
        VerificationReportSupport.WriteJson(run, path);
        var edited = File.ReadAllText(path).Replace("\"passed\"", "\"failed\"", StringComparison.Ordinal);
        File.WriteAllText(path, edited);

        var markdown = VerificationReportSupport.RenderMarkdown(path);

        Assert.Contains("**FAILED**", markdown, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderMarkdown_bolds_only_failed_rows()
    {
        var run = new VerificationRun(
            "dotnet", "0.1.1", "abc123", "host-arch", Started, Ended,
            [Row("format", "passed"), Row("lint", "failed"), Row("secrets", "skipped"), Row("types", "not-implemented")]);
        var path = Path.Combine(_dir, "2026-09-09.json");
        VerificationReportSupport.WriteJson(run, path);

        var markdown = VerificationReportSupport.RenderMarkdown(path);

        Assert.Contains("| lint | some-tool 1.0 | **FAILED** |", markdown, StringComparison.Ordinal);
        Assert.Contains("| format | some-tool 1.0 | passed |", markdown, StringComparison.Ordinal);
        Assert.Contains("| secrets | some-tool 1.0 | skipped |", markdown, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderMarkdown_renders_a_dash_for_empty_metrics_and_joins_multiple_with_semicolons()
    {
        var run = new VerificationRun(
            "dotnet", "0.1.1", "abc123", "host-arch", Started, Ended,
            [
                new CategoryResult("format", "t", "passed", new Dictionary<string, object>(), 1.0, null),
                new CategoryResult(
                    "coverage", "t", "passed",
                    new Dictionary<string, object> { ["coverage_pct"] = 94.5, ["lines_covered"] = 100 },
                    1.0, null),
            ]);
        var path = Path.Combine(_dir, "2026-09-09.json");
        VerificationReportSupport.WriteJson(run, path);

        var markdown = VerificationReportSupport.RenderMarkdown(path);

        Assert.Contains("| format | t | passed | 1.0 | — |", markdown, StringComparison.Ordinal);
        Assert.Contains("coverage_pct=94.5; lines_covered=100", markdown, StringComparison.Ordinal);
    }
}
