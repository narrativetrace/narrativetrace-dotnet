// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Text.Json;
using NarrativeTrace.Core;
using NarrativeTrace.Runtime;
using Xunit;

namespace NarrativeTrace.Core.Tests;

public class ConcurrencyScenarioTests
{
    [Fact]
    public async Task ForkJoin_two_tasks_produce_correct_trace()
    {
        var ctx = new SyncNarrativeContext(
            new NarrativeTraceConfig());
        var h0 = ctx.EnterMethod(
            "OrderService", "PlaceOrder", []);

        var group = ForkJoinGroup.Create(ctx);
        group.Fork(iso =>
        {
            var h = iso.EnterMethod(
                "DiscountService", "Calculate", []);
            iso.ExitMethodWithReturn("10%", h);
            return 10;
        });
        group.Fork(iso =>
        {
            var h = iso.EnterMethod(
                "ShippingService", "Estimate", []);
            iso.ExitMethodWithReturn("5.99", h);
            return 5.99m;
        });
        await group.JoinAsync();
        ctx.ExitMethodWithReturn("ok", h0);

        var trace = ctx.CaptureTrace();
        Assert.Single(trace.Roots);
        Assert.Equal(2,
            trace.Roots[0].Children.Count);
    }

    [Fact]
    public async Task ForkJoin_merged_children_show_thread_ids()
    {
        var ctx = new SyncNarrativeContext(
            new NarrativeTraceConfig());
        var h0 = ctx.EnterMethod("Svc", "Run", []);

        var group = ForkJoinGroup.Create(ctx);
        group.Fork(iso =>
        {
            iso.EnterMethod("A", "Do", []);
            iso.ExitMethodWithReturn(null);
            return 1;
        });
        await group.JoinAsync();
        ctx.ExitMethodWithReturn(null, h0);

        var child = ctx.CaptureTrace()
            .Roots[0].Children[0];
        Assert.True(
            child.Concurrency!.ThreadId > 0);
    }

    [Fact]
    public async Task ForkJoin_join_line_shows_wall_time()
    {
        var ctx = new SyncNarrativeContext(
            new NarrativeTraceConfig());
        var h0 = ctx.EnterMethod("Svc", "Run", []);

        var group = ForkJoinGroup.Create(ctx);
        group.Fork(iso =>
        {
            iso.EnterMethod("A", "Do", []);
            iso.ExitMethodWithReturn(null);
            return 1;
        });
        await group.JoinAsync();
        ctx.ExitMethodWithReturn(null, h0);

        var md = MarkdownRenderer.Render(
            ctx.CaptureTrace());
        Assert.Contains("\u2443 join", md);
    }

    [Fact]
    public async Task FireAndForget_parent_has_launcher_node()
    {
        var ctx = new SyncNarrativeContext(
            new NarrativeTraceConfig());
        var h0 = ctx.EnterMethod("Svc", "Run", []);

        var group = FireAndForgetGroup.Create(ctx);
        var done = new TaskCompletionSource<bool>();
        group.Launch(iso =>
        {
            iso.EnterMethod("Bg", "Work", []);
            iso.ExitMethodWithReturn(null);
            done.SetResult(true);
        });
        await done.Task;
        await Task.Delay(10);

        ctx.ExitMethodWithReturn(null, h0);
        var trace = ctx.CaptureTrace();
        Assert.Single(trace.Roots);
    }

    [Fact]
    public async Task FireAndForget_childRoots_linked_by_groupId()
    {
        var ctx = new SyncNarrativeContext(
            new NarrativeTraceConfig());
        var done = new TaskCompletionSource<bool>();

        var group = FireAndForgetGroup.Create(ctx);
        group.Launch(iso =>
        {
            iso.EnterMethod("Bg", "Work", []);
            iso.ExitMethodWithReturn(null);
            done.SetResult(true);
        });
        await done.Task;
        await Task.Delay(10);

        var roots = group.ChildRoots();
        Assert.Single(roots);
        Assert.StartsWith("fanf-", group.GroupId);
    }

    [Fact]
    public void SequentialAsync_flagged_as_awaited_sequentially()
    {
        var info = MakeForkInfo("fork-1");
        var ms = TimeSpan.TicksPerMillisecond;
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Svc", "Run", []),
                new Returned(null),
                [
                    MakeConcurrentNode(
                        "A", "Do", info,
                        100 * ms, 0),
                    MakeConcurrentNode(
                        "B", "Do", info,
                        100 * ms, 100 * ms),
                ],
                0),
        ]);

        var md = MarkdownRenderer.Render(tree);

        Assert.Contains(
            "awaited sequentially", md);
    }

    [Fact]
    public void SequentialAsync_shows_optimization_hint()
    {
        var info = MakeForkInfo("fork-1");
        var ms = TimeSpan.TicksPerMillisecond;
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Svc", "Run", []),
                new Returned(null),
                [
                    MakeConcurrentNode(
                        "A", "Do", info,
                        100 * ms, 0),
                    MakeConcurrentNode(
                        "B", "Do", info,
                        200 * ms, 100 * ms),
                ],
                0),
        ]);

        var md = MarkdownRenderer.Render(tree);

        Assert.Contains(
            "could save 100ms with Task.WhenAll",
            md);
    }

    [Fact]
    public void Mixed_patterns_in_correct_order()
    {
        var forkInfo = MakeForkInfo("fork-1");
        var fanfInfo = new ConcurrencyInfo(
            "fanf-1", "Bg.Work", 1, null, true,
            ConcurrencyKind.FireAndForget);
        var sequential = new TraceNode(
            new MethodSignature("Db", "Query", []),
            new Returned(null), [], 100);
        var forkA = MakeConcurrentNode(
            "A", "Do", forkInfo);
        var forkB = MakeConcurrentNode(
            "B", "Do", forkInfo);
        var fanf = MakeConcurrentNode(
            "Bg", "Work", fanfInfo);

        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Svc", "Run", []),
                new Returned(null),
                [sequential, forkA, forkB, fanf],
                0),
        ]);

        var md = MarkdownRenderer.Render(tree);
        var dbPos = md.IndexOf(
            "Db.Query", StringComparison.Ordinal);
        var forkPos = md.IndexOf(
            "\u2442 fork", StringComparison.Ordinal);
        var fanfPos = md.IndexOf(
            "\u2933 fire-and-forget",
            StringComparison.Ordinal);

        Assert.True(dbPos < forkPos);
        Assert.True(forkPos < fanfPos);
    }

    [Fact]
    public void Mixed_JSON_includes_concurrency_fields()
    {
        var info = MakeForkInfo("fork-1");
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Svc", "Run", []),
                new Returned(null), [], 0,
                Concurrency: info),
        ]);

        var json = JsonExporter.Export(
            tree, new TraceMetadata("Mixed", ScenarioResult.Success));
        var doc = JsonDocument.Parse(json);
        var exit = doc.RootElement
            .GetProperty("events")[1];

        Assert.True(
            exit.TryGetProperty(
                "concurrency", out var c));
        Assert.Equal("fork-1",
            c.GetProperty("groupId").GetString());
    }

    [Fact]
    public void Off_level_produces_empty_trace()
    {
        var config = new NarrativeTraceConfig(
            TracingLevel.Off);
        var ctx = new SyncNarrativeContext(config);

        ctx.EnterMethod("Svc", "Run", []);
        ctx.ExitMethodWithReturn(null);

        var trace = ctx.CaptureTrace();
        Assert.True(trace.IsEmpty);
    }

    private static ConcurrencyInfo MakeForkInfo(
        string groupId)
    {
        return new ConcurrencyInfo(
            groupId, "A.Do", 1, null, true,
            ConcurrencyKind.ForkJoin);
    }

    private static TraceNode MakeConcurrentNode(
        string cls, string method,
        ConcurrencyInfo info,
        long durationTicks = 100,
        long startTimestamp = 0)
    {
        return new TraceNode(
            new MethodSignature(cls, method, []),
            new Returned(null), [], durationTicks,
            startTimestamp, info);
    }
}
