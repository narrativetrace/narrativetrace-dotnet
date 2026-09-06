// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using BenchmarkDotNet.Attributes;
using NarrativeTrace.Clarity;
using NarrativeTrace.Core;
using NarrativeTrace.Runtime;

namespace NarrativeTrace.Benchmarks;

[MemoryDiagnoser]
public class RendererBenchmarks
{
    private TraceTree _smallTree = null!;
    private TraceTree _mediumTree = null!;

    [GlobalSetup]
    public void Setup()
    {
        _smallTree = BuildTree(5);
        _mediumTree = BuildTree(50);
    }

    [Benchmark]
    public string MarkdownSmall()
    {
        return MarkdownRenderer.Render(_smallTree);
    }

    [Benchmark]
    public string MarkdownMedium()
    {
        return MarkdownRenderer.Render(_mediumTree);
    }

    [Benchmark]
    public string JsonExportSmall()
    {
        var meta = new TraceMetadata(
            "bench", ScenarioResult.Success);
        return JsonExporter.Export(_smallTree, meta);
    }

    [Benchmark]
    public ClarityResult ClaritySmall()
    {
        return ClarityAnalyzer.Analyze(_smallTree);
    }

    private static TraceTree BuildTree(int count)
    {
        var config = new NarrativeTraceConfig();
        var ctx = new SyncNarrativeContext(config);
        for (var i = 0; i < count; i++)
        {
            ctx.EnterMethod(
                "Service", $"method{i}",
                [new ParameterCapture(
                    "id",
                    i.ToString(
                        System.Globalization.CultureInfo
                            .InvariantCulture),
                    false)]);
            ctx.ExitMethodWithReturn("ok");
        }

        return ctx.CaptureTrace();
    }
}
