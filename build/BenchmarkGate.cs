// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

static class BenchmarkGate
{
    public static Dictionary<string, (double MeanNs, long AllocBytes)> LoadResults(string artifactsDir)
    {
        var results = new Dictionary<string, (double, long)>();
        var reportDir = Path.Combine(artifactsDir, "results");
        if (!Directory.Exists(reportDir))
            return results;

        foreach (var file in Directory.EnumerateFiles(reportDir, "*-report-full-compressed.json"))
            ParseReport(file, results);

        return results;
    }

    public static void CheckRegressions(
        Dictionary<string, (double MeanNs, long AllocBytes)> current,
        string baselineFile)
    {
        var baseline = LoadBaseline(baselineFile);
        var regressions = FindRegressions(baseline, current);

        if (regressions.Count > 0)
        {
            foreach (var r in regressions)
                Console.WriteLine($"  REGRESSION: {r}");
            throw new InvalidOperationException(
                $"Benchmark regressions: {regressions.Count}. Run ./build.sh BenchmarkBaseline to update.");
        }

        Console.WriteLine($"Benchmark gate passed ({baseline.Count} checks, 15% time / 0% alloc threshold)");
    }

    /// <summary>
    /// The same comparison <see cref="CheckRegressions"/> makes, split into its two independent
    /// kinds instead of one merged pass/fail — <c>./build.sh VerifyAll</c>'s <c>benchmarks</c> and
    /// <c>allocation</c> report rows both read this <em>one</em> BenchmarkDotNet
    /// <c>MemoryDiagnoser</c> run (it captures mean time and bytes-allocated-per-operation
    /// together), rather than paying for two invocations the way JMH's separate GC-profiler pass
    /// does for the Java port. Never throws — this is a read, not a gate.
    /// </summary>
    public static (int Checked, int TimeRegressions, int AllocRegressions) CountRegressionsByKind(
        Dictionary<string, (double MeanNs, long AllocBytes)> current, string baselineFile)
    {
        var baseline = LoadBaseline(baselineFile);
        var timeRegressions = 0;
        var allocRegressions = 0;

        foreach (var entry in baseline)
        {
            if (!current.TryGetValue(entry.Key, out var cur))
                continue;

            var (baseMean, baseAlloc) = entry.Value;
            if (baseMean > 0 && cur.MeanNs / baseMean > 1.15)
                timeRegressions++;
            if (cur.AllocBytes > baseAlloc)
                allocRegressions++;
        }

        return (baseline.Count, timeRegressions, allocRegressions);
    }

    public static void SaveBaseline(
        Dictionary<string, (double MeanNs, long AllocBytes)> results,
        string baselineFile)
    {
        using var stream = File.Create(baselineFile);
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });

        writer.WriteStartObject();
        foreach (var entry in results.OrderBy(x => x.Key))
        {
            writer.WriteStartObject(entry.Key);
            writer.WriteNumber("meanNs", Math.Round(entry.Value.MeanNs, 2));
            writer.WriteNumber("allocatedBytes", entry.Value.AllocBytes);
            writer.WriteEndObject();
        }
        writer.WriteEndObject();

        Console.WriteLine($"Baseline saved: {baselineFile} ({results.Count} benchmarks)");
    }

    static void ParseReport(string file, Dictionary<string, (double MeanNs, long AllocBytes)> results)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(file));
        foreach (var bench in doc.RootElement.GetProperty("Benchmarks").EnumerateArray())
        {
            var key = BenchmarkKey(bench);
            var mean = bench.GetProperty("Statistics").GetProperty("Mean").GetDouble();
            var alloc = bench.GetProperty("Memory").GetProperty("BytesAllocatedPerOperation").GetInt64();
            results[key] = (mean, alloc);
        }
    }

    static string BenchmarkKey(JsonElement bench)
    {
        var type = bench.GetProperty("Type").GetString()!;
        var method = bench.GetProperty("Method").GetString()!;
        var parameters = bench.GetProperty("Parameters").GetString()!;
        return string.IsNullOrEmpty(parameters) ? $"{type}.{method}" : $"{type}.{method}({parameters})";
    }

    static Dictionary<string, (double MeanNs, long AllocBytes)> LoadBaseline(string baselineFile)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(baselineFile));
        var results = new Dictionary<string, (double, long)>();

        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            var mean = prop.Value.GetProperty("meanNs").GetDouble();
            var alloc = prop.Value.GetProperty("allocatedBytes").GetInt64();
            results[prop.Name] = (mean, alloc);
        }

        return results;
    }

    static List<string> FindRegressions(
        Dictionary<string, (double MeanNs, long AllocBytes)> baseline,
        Dictionary<string, (double MeanNs, long AllocBytes)> current)
    {
        var regressions = new List<string>();

        foreach (var entry in baseline)
        {
            if (!current.TryGetValue(entry.Key, out var cur))
                continue;

            var (baseMean, baseAlloc) = entry.Value;
            if (baseMean > 0 && cur.MeanNs / baseMean > 1.15)
                regressions.Add($"{entry.Key}: time {baseMean:F0}ns -> {cur.MeanNs:F0}ns (+{cur.MeanNs / baseMean - 1:P0})");

            if (cur.AllocBytes > baseAlloc)
                regressions.Add($"{entry.Key}: alloc {baseAlloc}B -> {cur.AllocBytes}B");
        }

        return regressions;
    }
}
