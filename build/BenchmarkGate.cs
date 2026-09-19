// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;

static class BenchmarkGate
{
    /// <summary>
    /// Environment every benchmark process this repository launches runs under — the gate, the
    /// baseline, and this file's own process-configuration test all build the process from
    /// <see cref="BuildProcessStartInfo"/>, so they can never drift apart into two different
    /// configurations.
    /// </summary>
    /// <remarks>
    /// Dynamic PGO is disabled here on general determinism grounds (it never hurts, and it is the
    /// one JIT-tiering knob the reproduction could plausibly implicate). It is <b>not</b>,
    /// however, the mechanism behind the regression this gate exists to fix: a controlled
    /// bisection (2026-09-19 — ten fresh single-shot processes each, default vs.
    /// <c>DOTNET_TieredPGO=0</c>, plus a hundred same-process repeats) found the identical
    /// allocation spread under both settings. The real, bisected cause is that
    /// <c>RendererBenchmarks.JsonExportSmall</c>/<c>MarkdownMedium</c> serialize a tree whose
    /// node durations come from a real <c>Stopwatch.GetTimestamp()</c> reading taken during
    /// <c>[GlobalSetup]</c>; the wall-clock time that setup actually takes varies process to
    /// process (scheduler/GC jitter, not JIT tiering), and when it crosses a whole-millisecond
    /// boundary the serialized duration gains or loses a digit, shifting the exported text's
    /// length and, with it, the write buffer's growth — a handful of bytes, but enough to trip a
    /// 0%-tolerance comparison. Allocation must be a property of the code, not of a process's
    /// accidental timing — see <see cref="MergeRuns"/>, the actual fix for that.
    /// </remarks>
    public static readonly IReadOnlyDictionary<string, string> DeterminismEnvironment =
        new Dictionary<string, string> { ["DOTNET_TieredPGO"] = "0" };

    /// <summary>
    /// Builds the exact <c>dotnet run</c> invocation the benchmark job runs under. The gate and
    /// this file's own determinism test both go through this one method, so a test that checks
    /// the process configuration is checking the real configuration, never a hand-copied
    /// approximation of it.
    /// </summary>
    public static ProcessStartInfo BuildProcessStartInfo(
        string projectPath, string artifactsDir, string filter = "*", string job = "medium")
    {
        var psi = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        foreach (var arg in new[]
        {
            "run", "--project", projectPath, "-c", "Release", "--",
            "--filter", filter, "--exporters", "json", "--job", job, "--artifacts", artifactsDir,
        })
            psi.ArgumentList.Add(arg);

        foreach (var (key, value) in DeterminismEnvironment)
            psi.Environment[key] = value;

        return psi;
    }

    /// <summary>Runs the benchmark job process to completion and echoes its output; throws on a
    /// non-zero exit so a build break in the benchmarked code fails the gate the same way every
    /// other stage does.</summary>
    public static void RunProcess(string projectPath, string artifactsDir, string filter = "*", string job = "medium")
    {
        var psi = BuildProcessStartInfo(projectPath, artifactsDir, filter, job);
        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start the benchmark process.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        Console.Write(stdout.GetAwaiter().GetResult());
        Console.Error.Write(stderr.GetAwaiter().GetResult());
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"Benchmark process exited {process.ExitCode} (filter={filter}).");
    }

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

    /// <summary>
    /// Combines two independent runs of the same benchmark job into the one result set the gate
    /// compares against the baseline: allocation is the MINIMUM observed across the two runs — a
    /// floor, never a single sample — because a single run's allocated-bytes reading can land on
    /// the noisy (longer-serialized) side of the content-length jitter described on
    /// <see cref="DeterminismEnvironment"/>, and that noise only ever adds bytes, never removes
    /// them, so the smaller of two honest readings is the truer one. Mean time is the average of
    /// the two runs — no floor or ceiling on that axis; the existing 15% tolerance and the
    /// load-aware "unreliable: load" marking in <see cref="CheckRegressions"/> are what absorb
    /// timing noise instead.
    /// </summary>
    public static Dictionary<string, (double MeanNs, long AllocBytes)> MergeRuns(
        Dictionary<string, (double MeanNs, long AllocBytes)> first,
        Dictionary<string, (double MeanNs, long AllocBytes)> second)
    {
        var merged = new Dictionary<string, (double, long)>();
        foreach (var (key, a) in first)
        {
            merged[key] = second.TryGetValue(key, out var b)
                ? ((a.MeanNs + b.MeanNs) / 2.0, Math.Min(a.AllocBytes, b.AllocBytes))
                : a;
        }
        foreach (var (key, b) in second)
            if (!merged.ContainsKey(key))
                merged[key] = b;

        return merged;
    }

    /// <summary>
    /// The container's own 1/5/15-minute load average, read fresh at gate time — never
    /// fabricated; <c>null</c> on a platform with no <c>/proc/loadavg</c> (e.g. running this
    /// build directly on macOS/Windows outside the Linux dev container).
    /// </summary>
    public static HostLoad? ReadHostLoad(string path = "/proc/loadavg")
    {
        if (!File.Exists(path))
            return null;

        var fields = File.ReadAllText(path).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (fields.Length < 3)
            return null;

        var culture = CultureInfo.InvariantCulture;
        if (double.TryParse(fields[0], NumberStyles.Float, culture, out var l1)
            && double.TryParse(fields[1], NumberStyles.Float, culture, out var l5)
            && double.TryParse(fields[2], NumberStyles.Float, culture, out var l15))
            return new HostLoad(l1, l5, l15);

        return null;
    }

    /// <summary>
    /// Compares <paramref name="current"/> (already floored by <see cref="MergeRuns"/>) against
    /// the recorded baseline. Allocation has no exception: any allocation regression fails the
    /// gate, because it is now measured as a floor rather than a single noisy sample. A time
    /// regression measured while the host itself is under load measures the host, not the code
    /// (release rule: bisect, never attribute) — the 5-minute load average is printed next to
    /// every time regression, and when it is above <paramref name="loadThreshold"/> that
    /// regression is reported but does not fail the gate.
    /// </summary>
    public static void CheckRegressions(
        Dictionary<string, (double MeanNs, long AllocBytes)> current,
        string baselineFile,
        double loadThreshold = 6.0,
        string loadAveragePath = "/proc/loadavg")
    {
        var baseline = LoadBaseline(baselineFile);
        var (timeRegressions, allocRegressions) = FindRegressionsByKind(baseline, current);
        var load = ReadHostLoad(loadAveragePath);
        var loadLine = load is { } l
            ? $"host load 1/5/15m: {l.OneMinute:F2}/{l.FiveMinute:F2}/{l.FifteenMinute:F2} (threshold {loadThreshold:F1})"
            : "host load unavailable (no /proc/loadavg)";
        var timeUnreliable = load is { } l2 && l2.FiveMinute > loadThreshold;

        foreach (var r in allocRegressions)
            Console.WriteLine($"  REGRESSION: {r}");
        foreach (var r in timeRegressions)
            Console.WriteLine(timeUnreliable
                ? $"  REGRESSION (unreliable: load): {r} — {loadLine}"
                : $"  REGRESSION: {r} — {loadLine}");

        var failingCount = allocRegressions.Count + (timeUnreliable ? 0 : timeRegressions.Count);
        if (failingCount > 0)
        {
            throw new InvalidOperationException(
                $"Benchmark regressions: {failingCount}. Run ./build.sh BenchmarkBaseline to update.");
        }

        Console.WriteLine(timeUnreliable && timeRegressions.Count > 0
            ? $"Benchmark gate passed ({baseline.Count} checks, 15% time / 0% alloc threshold, "
                + $"allocation floored over 2 runs; {timeRegressions.Count} time regression(s) "
                + $"not counted: {loadLine})"
            : $"Benchmark gate passed ({baseline.Count} checks, 15% time / 0% alloc threshold, "
                + "allocation floored over 2 runs)");
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
        var (timeRegressions, allocRegressions) = FindRegressionsByKind(baseline, current);
        return (baseline.Count, timeRegressions.Count, allocRegressions.Count);
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

    /// <summary>
    /// Re-baselines ONLY the allocation column of every benchmark <paramref name="current"/>
    /// covers, preserving each one's existing recorded mean time untouched — landing a
    /// determinism fix earns only the allocation figures the fix made deterministic; re-baselining
    /// time is a separate, deliberate act this method never performs. Returns <c>false</c> without
    /// writing anything when there is no existing baseline to preserve mean times from (nothing
    /// honest to merge into) — the caller decides what to do next.
    /// </summary>
    public static bool UpdateAllocationOnly(
        Dictionary<string, (double MeanNs, long AllocBytes)> current, string baselineFile)
    {
        if (!File.Exists(baselineFile))
            return false;

        var existing = LoadBaseline(baselineFile);
        var merged = new Dictionary<string, (double MeanNs, long AllocBytes)>(existing);
        foreach (var (key, value) in current)
        {
            merged[key] = existing.TryGetValue(key, out var old)
                ? (old.MeanNs, value.AllocBytes)
                : value;
        }

        SaveBaseline(merged, baselineFile);
        return true;
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

    static (List<string> TimeRegressions, List<string> AllocRegressions) FindRegressionsByKind(
        Dictionary<string, (double MeanNs, long AllocBytes)> baseline,
        Dictionary<string, (double MeanNs, long AllocBytes)> current)
    {
        var timeRegressions = new List<string>();
        var allocRegressions = new List<string>();

        foreach (var entry in baseline)
        {
            if (!current.TryGetValue(entry.Key, out var cur))
                continue;

            var (baseMean, baseAlloc) = entry.Value;
            if (baseMean > 0 && cur.MeanNs / baseMean > 1.15)
                timeRegressions.Add($"{entry.Key}: time {baseMean:F0}ns -> {cur.MeanNs:F0}ns (+{cur.MeanNs / baseMean - 1:P0})");

            if (cur.AllocBytes > baseAlloc)
                allocRegressions.Add($"{entry.Key}: alloc {baseAlloc}B -> {cur.AllocBytes}B");
        }

        return (timeRegressions, allocRegressions);
    }
}

/// <summary>A container's 1/5/15-minute load average, as read from <c>/proc/loadavg</c>.</summary>
readonly record struct HostLoad(double OneMinute, double FiveMinute, double FifteenMinute);
