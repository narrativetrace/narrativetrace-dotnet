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
}
