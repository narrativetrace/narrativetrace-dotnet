// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using BenchmarkDotNet.Attributes;
using NarrativeTrace.Core;
using NarrativeTrace.Runtime;

namespace NarrativeTrace.Benchmarks;

[MemoryDiagnoser]
public class CoreBenchmarks
{
    private readonly NarrativeTraceConfig _config =
        new();

    [Benchmark]
    public TraceTree EnterExitCycle()
    {
        var ctx = new SyncNarrativeContext(_config);
        ctx.EnterMethod("Svc", "run", []);
        ctx.ExitMethodWithReturn(null);
        return ctx.CaptureTrace();
    }

    [Benchmark]
    public string RenderPrimitive()
    {
        return ValueRenderer.Render(42);
    }

    [Benchmark]
    public string RenderObject()
    {
        return ValueRenderer.Render(
            new { Name = "test", Value = 123 });
    }

    [Params(10, 100, 1000)]
    public int NodeCount { get; set; }

    [Benchmark]
    public TraceTree BuildTree()
    {
        var ctx = new SyncNarrativeContext(_config);
        for (var i = 0; i < NodeCount; i++)
        {
            ctx.EnterMethod("Svc", "method", []);
            ctx.ExitMethodWithReturn(null);
        }

        return ctx.CaptureTrace();
    }
}
