// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using Xunit;

namespace Build.Tests;

/// <summary>
/// Covers <see cref="BenchmarkGate.CountRegressionsByKind"/> — the split <c>VerifyAll</c>'s
/// <c>benchmarks</c> and <c>allocation</c> rows read from the same BenchmarkDotNet
/// <c>MemoryDiagnoser</c> run, instead of re-running it twice the way the JMH-based java port does.
/// </summary>
public sealed class BenchmarkGateTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("nt-benchmark-gate").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string WriteBaseline(params (string Key, double MeanNs, long AllocBytes)[] entries)
    {
        var path = Path.Combine(_dir, "baseline.json");
        BenchmarkGate.SaveBaseline(entries.ToDictionary(e => e.Key, e => (e.MeanNs, e.AllocBytes)), path);
        return path;
    }

    [Fact]
    public void A_benchmark_over_115_percent_of_baseline_mean_is_a_time_regression_only()
    {
        var baseline = WriteBaseline(("A", 1000.0, 100));
        var current = new Dictionary<string, (double MeanNs, long AllocBytes)> { ["A"] = (1200.0, 100) };

        var (checkedCount, timeRegressions, allocRegressions) = BenchmarkGate.CountRegressionsByKind(current, baseline);

        Assert.Equal(1, checkedCount);
        Assert.Equal(1, timeRegressions);
        Assert.Equal(0, allocRegressions);
    }

    [Fact]
    public void A_benchmark_allocating_more_than_baseline_is_an_allocation_regression_only()
    {
        var baseline = WriteBaseline(("A", 1000.0, 100));
        var current = new Dictionary<string, (double MeanNs, long AllocBytes)> { ["A"] = (1010.0, 200) };

        var (_, timeRegressions, allocRegressions) = BenchmarkGate.CountRegressionsByKind(current, baseline);

        Assert.Equal(0, timeRegressions);
        Assert.Equal(1, allocRegressions);
    }

    [Fact]
    public void A_benchmark_within_threshold_on_both_axes_regresses_on_neither()
    {
        var baseline = WriteBaseline(("A", 1000.0, 100));
        var current = new Dictionary<string, (double MeanNs, long AllocBytes)> { ["A"] = (1100.0, 100) };

        var (_, timeRegressions, allocRegressions) = BenchmarkGate.CountRegressionsByKind(current, baseline);

        Assert.Equal(0, timeRegressions);
        Assert.Equal(0, allocRegressions);
    }

    [Fact]
    public void A_benchmark_missing_from_the_current_run_is_not_counted_as_a_regression_here()
    {
        // The actual gate has its own opinion about a benchmark missing from the current run;
        // this split only classifies the ones both runs actually share.
        var baseline = WriteBaseline(("A", 1000.0, 100), ("B", 500.0, 50));
        var current = new Dictionary<string, (double MeanNs, long AllocBytes)> { ["A"] = (1000.0, 100) };

        var (checkedCount, timeRegressions, allocRegressions) = BenchmarkGate.CountRegressionsByKind(current, baseline);

        Assert.Equal(2, checkedCount);
        Assert.Equal(0, timeRegressions);
        Assert.Equal(0, allocRegressions);
    }

    // ── Determinism: the process configuration the gate actually launches ──────────────────────
    //
    // A controlled bisection (2026-09-19: ten fresh single-shot processes each, default vs.
    // DOTNET_TieredPGO=0, on RendererBenchmarks.JsonExportSmall) found no measurable difference —
    // the real source of that benchmark's run-to-run allocation spread is content-length jitter
    // from a live Stopwatch reading serialized into the exported text, not JIT tiering. These
    // tests cover what the gate can actually promise: the process it launches carries the
    // determinism setting regardless (BuildProcessStartInfo is the single source both the gate and
    // this test read from — no hand-copied second copy of the configuration to drift out of sync),
    // and the floor-of-two-runs mechanism never lets a single noisy sample decide the comparison.

    [Fact]
    public void The_benchmark_process_configuration_disables_dynamic_PGO()
    {
        var psi = BenchmarkGate.BuildProcessStartInfo(
            "benchmarks/NarrativeTrace.Benchmarks/NarrativeTrace.Benchmarks.csproj", "artifacts/x");

        Assert.Equal("0", psi.Environment["DOTNET_TieredPGO"]);
    }

    [Fact]
    public void The_gate_and_the_process_it_launches_read_the_determinism_setting_from_one_place()
    {
        // Not a second, hand-copied assertion of the same fact as the test above — this one
        // fails if BuildProcessStartInfo ever stops consulting DeterminismEnvironment (the one
        // dictionary Build.cs's RunBenchmarks and this file both build the process from) and
        // starts hard-coding the setting instead, which would let the two quietly diverge again.
        var psi = BenchmarkGate.BuildProcessStartInfo("proj.csproj", "artifacts/x");

        foreach (var (key, value) in BenchmarkGate.DeterminismEnvironment)
            Assert.Equal(value, psi.Environment[key]);
    }

    [Fact]
    public void Flooring_two_runs_reports_the_lower_allocation_reading_never_a_single_sample()
    {
        // The two real BenchmarkDotNet readings the 2026-09-19 reproduction captured for the same
        // commit's RendererBenchmarks.JsonExportSmall — recorded baseline-era 17264 B, a
        // reproduction run 17328 B. Red before MergeRuns existed (there was nothing here to floor
        // a single sample with — the gate compared whichever one run it happened to see against
        // the baseline and could regress on the higher reading alone); green with it.
        var first = new Dictionary<string, (double MeanNs, long AllocBytes)>
        {
            ["RendererBenchmarks.JsonExportSmall"] = (3810.93, 17328),
        };
        var second = new Dictionary<string, (double MeanNs, long AllocBytes)>
        {
            ["RendererBenchmarks.JsonExportSmall"] = (3901.20, 17264),
        };

        var merged = BenchmarkGate.MergeRuns(first, second);

        Assert.Equal(17264, merged["RendererBenchmarks.JsonExportSmall"].AllocBytes);
    }

    [Fact]
    public void Flooring_two_runs_averages_mean_time_rather_than_flooring_or_ceiling_it()
    {
        var first = new Dictionary<string, (double MeanNs, long AllocBytes)> { ["A"] = (1000.0, 100) };
        var second = new Dictionary<string, (double MeanNs, long AllocBytes)> { ["A"] = (1200.0, 100) };

        var merged = BenchmarkGate.MergeRuns(first, second);

        Assert.Equal(1100.0, merged["A"].MeanNs);
    }

    // ── Time regressions: unreliable under host load, never counted, never silently dropped ────

    private string WriteLoadAvg(double oneMinute, double fiveMinute, double fifteenMinute)
    {
        var path = Path.Combine(_dir, "loadavg");
        File.WriteAllText(path, $"{oneMinute} {fiveMinute} {fifteenMinute} 1/200 12345\n");
        return path;
    }

    [Fact]
    public void A_time_regression_measured_under_high_5_minute_load_does_not_fail_the_gate()
    {
        var baseline = WriteBaseline(("A", 1000.0, 100));
        var current = new Dictionary<string, (double MeanNs, long AllocBytes)> { ["A"] = (1300.0, 100) };
        var loadPath = WriteLoadAvg(oneMinute: 1.0, fiveMinute: 9.0, fifteenMinute: 2.0);

        // Under the documented threshold (6.0) the same regression still throws — proven by the
        // absence of an exception here only because the load line places it above threshold.
        var exception = Record.Exception(() =>
            BenchmarkGate.CheckRegressions(current, baseline, loadThreshold: 6.0, loadAveragePath: loadPath));

        Assert.Null(exception);
    }

    [Fact]
    public void A_time_regression_measured_under_low_5_minute_load_still_fails_the_gate()
    {
        var baseline = WriteBaseline(("A", 1000.0, 100));
        var current = new Dictionary<string, (double MeanNs, long AllocBytes)> { ["A"] = (1300.0, 100) };
        var loadPath = WriteLoadAvg(oneMinute: 1.0, fiveMinute: 1.5, fifteenMinute: 1.0);

        Assert.Throws<InvalidOperationException>(() =>
            BenchmarkGate.CheckRegressions(current, baseline, loadThreshold: 6.0, loadAveragePath: loadPath));
    }

    [Fact]
    public void An_allocation_regression_fails_the_gate_regardless_of_host_load()
    {
        // Allocation gets no load exception — it is a floor over two runs, never a single noisy
        // sample, so there is nothing left for load to explain away.
        var baseline = WriteBaseline(("A", 1000.0, 100));
        var current = new Dictionary<string, (double MeanNs, long AllocBytes)> { ["A"] = (1000.0, 200) };
        var loadPath = WriteLoadAvg(oneMinute: 1.0, fiveMinute: 9.0, fifteenMinute: 2.0);

        Assert.Throws<InvalidOperationException>(() =>
            BenchmarkGate.CheckRegressions(current, baseline, loadThreshold: 6.0, loadAveragePath: loadPath));
    }
}
