// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using BenchmarkDotNet.Attributes;
using NarrativeTrace.Core;
using NarrativeTrace.Runtime;

namespace NarrativeTrace.Benchmarks;

[MemoryDiagnoser]
public class ConcurrencyBenchmarks
{
    private TraceTree _forkJoinTree = null!;
    private IReadOnlyList<TraceNode> _sequentialMembers = null!;
    private IReadOnlyList<TraceNode> _overlappingMembers = null!;

    [GlobalSetup]
    public void Setup()
    {
        _forkJoinTree = BuildForkJoinTree(10);
        _sequentialMembers = BuildSequentialMembers(10);
        _overlappingMembers = BuildOverlappingMembers(10);
    }

    [Benchmark]
    public async Task ForkJoin_TwoTasks()
    {
        var ctx = new SyncNarrativeContext(
            new NarrativeTraceConfig());
        var group = ForkJoinGroup.Create(ctx);
        group.Fork(_ => 1);
        group.Fork(_ => 2);
        await group.JoinAsync();
    }

    [Benchmark]
    public async Task<(int, int)> ForkJoin_WhenAll()
    {
        var ctx = new SyncNarrativeContext(
            new NarrativeTraceConfig());
        return await ForkJoinGroup.WhenAll(
            ctx, _ => 1, _ => 2);
    }

    [Benchmark]
    public void FireAndForget_Launch()
    {
        var ctx = new SyncNarrativeContext(
            new NarrativeTraceConfig());
        var group = FireAndForgetGroup.Create(ctx);
        group.Launch(_ => { });
    }

    [Benchmark]
    public SequentialAsyncResult DetectSequential()
    {
        return SequentialAsyncDetector.Analyze(
            _sequentialMembers);
    }

    [Benchmark]
    public SequentialAsyncResult DetectOverlapping()
    {
        return SequentialAsyncDetector.Analyze(
            _overlappingMembers);
    }

    [Benchmark]
    public string RenderForkJoinMarkdown()
    {
        return MarkdownRenderer.Render(_forkJoinTree);
    }

    [Benchmark]
    public string RenderForkJoinIndented()
    {
        return IndentedTextRenderer.Render(
            _forkJoinTree);
    }

    [Benchmark]
    public string ExportForkJoinJson()
    {
        var meta = new TraceMetadata("bench", ScenarioResult.Success);
        return JsonExporter.Export(
            _forkJoinTree, meta);
    }

    private static TraceTree BuildForkJoinTree(
        int memberCount)
    {
        var info = new ConcurrencyInfo(
            "fork-1", "A.Do", 1, null, true,
            ConcurrencyKind.ForkJoin);
        var members = new List<TraceNode>();
        for (var i = 0; i < memberCount; i++)
        {
            members.Add(new TraceNode(
                new MethodSignature(
                    "Svc", $"Task{i}", []),
                new Returned(null), [], 1000,
                i * 100, info));
        }

        return new TraceTree([
            new TraceNode(
                new MethodSignature(
                    "Parent", "Run", []),
                new Returned(null), members, 0),
        ]);
    }

    private static List<TraceNode> BuildSequentialMembers(
        int count)
    {
        var ms = TimeSpan.TicksPerMillisecond;
        var members = new List<TraceNode>();
        for (var i = 0; i < count; i++)
        {
            members.Add(new TraceNode(
                new MethodSignature("S", $"M{i}", []),
                new Returned(null), [],
                100 * ms, i * 100 * ms));
        }

        return members;
    }

    private static List<TraceNode> BuildOverlappingMembers(
        int count)
    {
        var ms = TimeSpan.TicksPerMillisecond;
        var members = new List<TraceNode>();
        for (var i = 0; i < count; i++)
        {
            members.Add(new TraceNode(
                new MethodSignature("S", $"M{i}", []),
                new Returned(null), [],
                200 * ms, i * 50 * ms));
        }

        return members;
    }
}
