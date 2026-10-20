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
    public void The_allocation_floor_run_disables_dynamic_PGO()
    {
        var psi = BenchmarkGate.BuildProcessStartInfo(
            "benchmarks/NarrativeTrace.Benchmarks/NarrativeTrace.Benchmarks.csproj", "artifacts/x",
            BenchmarkGate.DeterminismEnvironment);

        Assert.Equal("0", psi.Environment["DOTNET_TieredPGO"]);
    }

    [Fact]
    public void The_timing_run_enables_dynamic_PGO_explicitly_as_production_does()
    {
        var psi = BenchmarkGate.BuildProcessStartInfo(
            "benchmarks/NarrativeTrace.Benchmarks/NarrativeTrace.Benchmarks.csproj", "artifacts/x",
            BenchmarkGate.ProductionEnvironment);

        Assert.Equal("1", psi.Environment["DOTNET_TieredPGO"]);
    }

    [Fact]
    public void Both_runs_are_built_from_one_argument_list_and_differ_only_in_environment()
    {
        var floor = BenchmarkGate.BuildProcessStartInfo("proj.csproj", "artifacts/x", BenchmarkGate.DeterminismEnvironment);
        var timing = BenchmarkGate.BuildProcessStartInfo("proj.csproj", "artifacts/x", BenchmarkGate.ProductionEnvironment);

        Assert.Equal(floor.ArgumentList, timing.ArgumentList);
        Assert.NotEqual(floor.Environment["DOTNET_TieredPGO"], timing.Environment["DOTNET_TieredPGO"]);
    }

    [Fact]
    public void The_process_environment_comes_from_the_dictionary_it_is_given()
    {
        // Fails if BuildProcessStartInfo hard-codes a setting instead of consulting the
        // environment it was handed, which would let the two runs quietly diverge again.
        var environment = new Dictionary<string, string> { ["NT_PROBE"] = "x" };
        var psi = BenchmarkGate.BuildProcessStartInfo("proj.csproj", "artifacts/x", environment);

        Assert.Equal("x", psi.Environment["NT_PROBE"]);
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
    public void Merging_two_runs_takes_mean_time_from_the_timing_run_alone()
    {
        var floorRun = new Dictionary<string, (double MeanNs, long AllocBytes)> { ["A"] = (5950.0, 100) };
        var timingRun = new Dictionary<string, (double MeanNs, long AllocBytes)> { ["A"] = (850.0, 100) };

        var merged = BenchmarkGate.MergeRuns(floorRun, timingRun);

        Assert.Equal(850.0, merged["A"].MeanNs);
    }

    [Fact]
    public void Merging_two_runs_keeps_the_allocation_minimum_whichever_run_holds_it()
    {
        var floorRun = new Dictionary<string, (double MeanNs, long AllocBytes)> { ["A"] = (5950.0, 100), ["B"] = (1.0, 300) };
        var timingRun = new Dictionary<string, (double MeanNs, long AllocBytes)> { ["A"] = (850.0, 120), ["B"] = (2.0, 280) };

        var merged = BenchmarkGate.MergeRuns(floorRun, timingRun);

        Assert.Equal((850.0, 100L), merged["A"]);
        Assert.Equal((2.0, 280L), merged["B"]);
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
    // ── Environment shift: every row moved together, allocation flat — a slower host, never a
    // code regression (release rule: bisect, never attribute; allocation is the axis a real
    // regression would move and a slower host never does).

    private static string Capture(Action action)
    {
        var original = Console.Out;
        using var captured = new StringWriter();
        Console.SetOut(captured);
        try
        {
            action();
        }
        finally
        {
            Console.SetOut(original);
        }
        return captured.ToString();
    }

    [Fact]
    public void Every_row_moving_together_with_allocation_flat_is_an_environment_shift_not_a_failure()
    {
        // The 2026-09-20 nightly's 20 rows verbatim: every one a time regression, no allocation
        // regression, host load 1.22/1.30/1.84 — well under the 6.0 load-unreliable threshold, so
        // the existing load exception does not explain this case; the uniform-shift classifier is
        // the only thing that can.
        var baseline = WriteBaseline(
            ("ConcurrencyBenchmarks.DetectOverlapping", 29.0, 100),
            ("ConcurrencyBenchmarks.DetectSequential", 87.0, 100),
            ("ConcurrencyBenchmarks.ExportForkJoinJson", 8844.0, 100),
            ("ConcurrencyBenchmarks.FireAndForget_Launch", 1271.0, 100),
            ("ConcurrencyBenchmarks.ForkJoin_TwoTasks", 3311.0, 100),
            ("ConcurrencyBenchmarks.RenderForkJoinIndented", 1095.0, 100),
            ("ConcurrencyBenchmarks.RenderForkJoinMarkdown", 2056.0, 100),
            ("CoreBenchmarks.BuildTree(NodeCount=10)", 6110.0, 100),
            ("CoreBenchmarks.BuildTree(NodeCount=100)", 66596.0, 100),
            ("CoreBenchmarks.BuildTree(NodeCount=1000)", 3225152.0, 100),
            ("CoreBenchmarks.EnterExitCycle(NodeCount=10)", 1131.0, 100),
            ("CoreBenchmarks.EnterExitCycle(NodeCount=100)", 1128.0, 100),
            ("CoreBenchmarks.EnterExitCycle(NodeCount=1000)", 1135.0, 100),
            ("CoreBenchmarks.RenderPrimitive(NodeCount=10)", 28.0, 100),
            ("CoreBenchmarks.RenderPrimitive(NodeCount=100)", 28.0, 100),
            ("CoreBenchmarks.RenderPrimitive(NodeCount=1000)", 30.0, 100),
            ("RendererBenchmarks.ClaritySmall", 5210.0, 100),
            ("RendererBenchmarks.JsonExportSmall", 3811.0, 100),
            ("RendererBenchmarks.MarkdownMedium", 6234.0, 100),
            ("RendererBenchmarks.MarkdownSmall", 1195.0, 100));
        var current = new Dictionary<string, (double MeanNs, long AllocBytes)>
        {
            ["ConcurrencyBenchmarks.DetectOverlapping"] = (68.0, 100),
            ["ConcurrencyBenchmarks.DetectSequential"] = (307.0, 100),
            ["ConcurrencyBenchmarks.ExportForkJoinJson"] = (11160.0, 100),
            ["ConcurrencyBenchmarks.FireAndForget_Launch"] = (1804.0, 100),
            ["ConcurrencyBenchmarks.ForkJoin_TwoTasks"] = (3826.0, 100),
            ["ConcurrencyBenchmarks.RenderForkJoinIndented"] = (1641.0, 100),
            ["ConcurrencyBenchmarks.RenderForkJoinMarkdown"] = (3404.0, 100),
            ["CoreBenchmarks.BuildTree(NodeCount=10)"] = (16412.0, 100),
            ["CoreBenchmarks.BuildTree(NodeCount=100)"] = (178114.0, 100),
            ["CoreBenchmarks.BuildTree(NodeCount=1000)"] = (5280004.0, 100),
            ["CoreBenchmarks.EnterExitCycle(NodeCount=10)"] = (2869.0, 100),
            ["CoreBenchmarks.EnterExitCycle(NodeCount=100)"] = (2690.0, 100),
            ["CoreBenchmarks.EnterExitCycle(NodeCount=1000)"] = (2654.0, 100),
            ["CoreBenchmarks.RenderPrimitive(NodeCount=10)"] = (73.0, 100),
            ["CoreBenchmarks.RenderPrimitive(NodeCount=100)"] = (73.0, 100),
            ["CoreBenchmarks.RenderPrimitive(NodeCount=1000)"] = (73.0, 100),
            ["RendererBenchmarks.ClaritySmall"] = (7392.0, 100),
            ["RendererBenchmarks.JsonExportSmall"] = (4538.0, 100),
            ["RendererBenchmarks.MarkdownMedium"] = (11609.0, 100),
            ["RendererBenchmarks.MarkdownSmall"] = (1864.0, 100),
        };
        var loadPath = WriteLoadAvg(oneMinute: 1.22, fiveMinute: 1.30, fifteenMinute: 1.84);

        string output = "";
        var exception = Record.Exception(() => output = Capture(() =>
            BenchmarkGate.CheckRegressions(current, baseline, loadThreshold: 6.0, loadAveragePath: loadPath)));

        Assert.Null(exception);
        Assert.Contains("ENVIRONMENT_SHIFT", output, StringComparison.Ordinal);
    }

    [Fact]
    public void One_row_regressing_while_its_peers_hold_steady_still_fails_the_gate()
    {
        // The opposite shape: one benchmark slower, four others unchanged. Not every row moved,
        // so this is a code regression report, not an environment shift, however large the one
        // row's factor — red stays red.
        var baseline = WriteBaseline(
            ("A", 1000.0, 100),
            ("B", 1000.0, 100),
            ("C", 1000.0, 100),
            ("D", 1000.0, 100),
            ("E", 1000.0, 100));
        var current = new Dictionary<string, (double MeanNs, long AllocBytes)>
        {
            ["A"] = (2500.0, 100),
            ["B"] = (1000.0, 100),
            ["C"] = (1000.0, 100),
            ["D"] = (1000.0, 100),
            ["E"] = (1000.0, 100),
        };
        var loadPath = WriteLoadAvg(oneMinute: 1.0, fiveMinute: 1.0, fifteenMinute: 1.0);

        Assert.Throws<InvalidOperationException>(() =>
            BenchmarkGate.CheckRegressions(current, baseline, loadThreshold: 6.0, loadAveragePath: loadPath));
    }

    private static string RealBaselinePath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "benchmarks", "benchmark-baseline.json")))
            dir = dir.Parent;
        return Path.Combine(dir!.FullName, "benchmarks", "benchmark-baseline.json");
    }

    private static Dictionary<string, (double MeanNs, long AllocBytes)> RealBaselineRows()
    {
        using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(RealBaselinePath()));
        return doc.RootElement.EnumerateObject().ToDictionary(
            p => p.Name,
            p => (p.Value.GetProperty("meanNs").GetDouble(), p.Value.GetProperty("allocatedBytes").GetInt64()));
    }

    private string CheckVerdict(
        Dictionary<string, (double MeanNs, long AllocBytes)> current,
        Dictionary<string, (double MeanNs, long AllocBytes)> baselineRows)
    {
        var baseline = WriteBaseline(baselineRows.Select(r => (r.Key, r.Value.MeanNs, r.Value.AllocBytes)).ToArray());
        var loadPath = WriteLoadAvg(oneMinute: 1.15, fiveMinute: 1.15, fifteenMinute: 1.15);
        return Capture(() =>
            BenchmarkGate.CheckRegressions(current, baseline, loadThreshold: 6.0, loadAveragePath: loadPath));
    }

    private static Dictionary<string, (double MeanNs, long AllocBytes)> Rows(
        int count, Func<int, (double MeanNs, long AllocBytes)> row) =>
        Enumerable.Range(0, count).ToDictionary(i => $"B{i:D2}", row);

    [Fact]
    public void The_2026_10_18_nightly_against_the_real_baseline_is_an_environment_shift()
    {
        // 21 rows slower by a factor (the nightly's REGRESSION lines), three RenderObject rows
        // within noise, and ForkJoin_TwoTasks allocating 6411 B against 6409 B.
        var baseline = RealBaselineRows();
        var factors = new Dictionary<string, double>
        {
            ["ConcurrencyBenchmarks.DetectOverlapping"] = 68,
            ["ConcurrencyBenchmarks.DetectSequential"] = 307,
            ["ConcurrencyBenchmarks.ExportForkJoinJson"] = 11142,
            ["ConcurrencyBenchmarks.FireAndForget_Launch"] = 1797,
            ["ConcurrencyBenchmarks.ForkJoin_TwoTasks"] = 4008,
            ["ConcurrencyBenchmarks.ForkJoin_WhenAll"] = 4174,
            ["ConcurrencyBenchmarks.RenderForkJoinIndented"] = 1779,
            ["ConcurrencyBenchmarks.RenderForkJoinMarkdown"] = 3524,
            ["CoreBenchmarks.BuildTree(NodeCount=10)"] = 16415,
            ["CoreBenchmarks.BuildTree(NodeCount=100)"] = 176797,
            ["CoreBenchmarks.BuildTree(NodeCount=1000)"] = 5311801,
            ["CoreBenchmarks.EnterExitCycle(NodeCount=10)"] = 2799,
            ["CoreBenchmarks.EnterExitCycle(NodeCount=100)"] = 2699,
            ["CoreBenchmarks.EnterExitCycle(NodeCount=1000)"] = 2639,
            ["CoreBenchmarks.RenderPrimitive(NodeCount=10)"] = 74,
            ["CoreBenchmarks.RenderPrimitive(NodeCount=100)"] = 74,
            ["CoreBenchmarks.RenderPrimitive(NodeCount=1000)"] = 77,
            ["RendererBenchmarks.ClaritySmall"] = 7268,
            ["RendererBenchmarks.JsonExportSmall"] = 4414,
            ["RendererBenchmarks.MarkdownMedium"] = 12547,
            ["RendererBenchmarks.MarkdownSmall"] = 1921,
        };
        var current = baseline.ToDictionary(
            b => b.Key,
            b => (factors.TryGetValue(b.Key, out var ns) ? ns : b.Value.MeanNs, b.Value.AllocBytes));
        current["ConcurrencyBenchmarks.ForkJoin_TwoTasks"] = (4008, 6411);

        var output = CheckVerdict(current, baseline);

        Assert.Contains("ENVIRONMENT_SHIFT", output, StringComparison.Ordinal);
    }

    [Fact]
    public void A_shift_with_a_two_byte_allocation_jitter_on_one_row_is_still_an_environment_shift()
    {
        var baseline = Rows(24, _ => (1000.0, 6409));
        var current = Rows(24, i => (2500.0, i == 0 ? 6411 : 6409));

        Assert.Contains("ENVIRONMENT_SHIFT", CheckVerdict(current, baseline), StringComparison.Ordinal);
    }

    [Fact]
    public void A_shift_where_three_rows_slow_by_less_than_the_threshold_is_still_an_environment_shift()
    {
        var baseline = Rows(24, _ => (1000.0, 100));
        var current = Rows(24, i => (i < 3 ? 1050.0 : 2500.0, 100));

        Assert.Contains("ENVIRONMENT_SHIFT", CheckVerdict(current, baseline), StringComparison.Ordinal);
    }

    [Fact]
    public void A_row_faster_past_the_threshold_among_slower_peers_is_not_a_shift()
    {
        var baseline = Rows(24, _ => (1000.0, 100));
        var current = Rows(24, i => (i == 0 ? 500.0 : 2500.0, 100));

        Assert.Throws<InvalidOperationException>(() => CheckVerdict(current, baseline));
    }

    [Fact]
    public void Real_allocation_growth_on_one_row_among_slower_peers_fails_on_that_row()
    {
        var baseline = Rows(24, _ => (1000.0, 6409));
        var current = Rows(24, i => (2500.0, i == 0 ? 6409 + 128 : 6409));
        var ex = Assert.Throws<InvalidOperationException>(() => CheckVerdict(current, baseline));

        Assert.Contains("Benchmark regressions", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Allocation_growth_counts_only_at_sixteen_bytes_and_one_percent_together()
    {
        var baseline = WriteBaseline(("A", 1000.0, 10_000), ("B", 1000.0, 10_000));
        var within = new Dictionary<string, (double MeanNs, long AllocBytes)>
        { ["A"] = (1000.0, 10_099), ["B"] = (1000.0, 10_000) };
        var past = new Dictionary<string, (double MeanNs, long AllocBytes)>
        { ["A"] = (1000.0, 10_100), ["B"] = (1000.0, 10_000) };

        Assert.Equal(0, BenchmarkGate.CountRegressionsByKind(within, baseline).AllocRegressions);
        Assert.Equal(1, BenchmarkGate.CountRegressionsByKind(past, baseline).AllocRegressions);
        Assert.False(BenchmarkGate.AllocationGrew(1000, 1015));
        Assert.True(BenchmarkGate.AllocationGrew(1000, 1016));
    }

    [Fact]
    public void A_lone_slower_benchmark_is_never_an_environment_shift()
    {
        var baseline = Rows(1, _ => (1000.0, 100));
        var current = Rows(1, _ => (2500.0, 100));

        Assert.Throws<InvalidOperationException>(() => CheckVerdict(current, baseline));
    }
}
