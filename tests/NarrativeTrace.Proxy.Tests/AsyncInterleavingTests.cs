// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using NarrativeTrace.Runtime;
using Xunit;

namespace NarrativeTrace.Proxy.Tests;

public class AsyncInterleavingTests
{
    public interface IAsyncService
    {
        Task<string> Fast();
        Task<string> Slow();
    }

    public interface ISyncService
    {
        int Add(int a, int b);
    }

    public interface INestedAsyncService
    {
        Task<string> Outer();
    }

    private sealed class ControlledService : IAsyncService
    {
        internal readonly TaskCompletionSource<string>
            FastTcs = new();

        internal readonly TaskCompletionSource<string>
            SlowTcs = new();

        public Task<string> Fast() => FastTcs.Task;
        public Task<string> Slow() => SlowTcs.Task;
    }

    private sealed class SyncCalculator : ISyncService
    {
        public int Add(int a, int b) => a + b;
    }

    private sealed class NestedAsyncService
        : INestedAsyncService
    {
        private readonly IAsyncService _inner;

        public NestedAsyncService(IAsyncService inner)
        {
            _inner = inner;
        }

        public async Task<string> Outer()
        {
            return await _inner.Fast();
        }
    }

    [Fact]
    public async Task Concurrent_async_calls_produce_sibling_roots()
    {
        var ctx = new SyncNarrativeContext(
            new NarrativeTraceConfig());
        var svc = new ControlledService();
        var proxy = NarrativeTraceProxy
            .Create<IAsyncService>(svc, ctx);

        // Both enter synchronously on this thread
        var fast = proxy.Fast();
        var slow = proxy.Slow();

        // Fast completes first, then slow
        svc.FastTcs.SetResult("fast-result");
        svc.SlowTcs.SetResult("slow-result");
        await Task.WhenAll(fast, slow);

        var trace = ctx.CaptureTrace();

        Assert.Equal(2, trace.Roots.Count);

        var names = trace.Roots
            .Select(r => r.Signature.MethodName)
            .ToList();
        Assert.Contains("Fast", names);
        Assert.Contains("Slow", names);

        Assert.Empty(trace.Roots[0].Children);
        Assert.Empty(trace.Roots[1].Children);
    }

    [Fact]
    public async Task Concurrent_async_calls_capture_correct_return_values()
    {
        var ctx = new SyncNarrativeContext(
            new NarrativeTraceConfig());
        var svc = new ControlledService();
        var proxy = NarrativeTraceProxy
            .Create<IAsyncService>(svc, ctx);

        var fast = proxy.Fast();
        var slow = proxy.Slow();

        svc.FastTcs.SetResult("fast-result");
        svc.SlowTcs.SetResult("slow-result");
        await Task.WhenAll(fast, slow);

        var trace = ctx.CaptureTrace();
        var fastNode = trace.Roots
            .First(r => r.Signature.MethodName == "Fast");
        var slowNode = trace.Roots
            .First(r => r.Signature.MethodName == "Slow");

        var fastOutcome = Assert.IsType<Returned>(
            fastNode.Outcome);
        var slowOutcome = Assert.IsType<Returned>(
            slowNode.Outcome);

        Assert.Equal(
            "\"fast-result\"", fastOutcome.RenderedValue);
        Assert.Equal(
            "\"slow-result\"", slowOutcome.RenderedValue);
    }

    [Fact]
    public async Task Concurrent_async_calls_return_correct_values_to_caller()
    {
        var ctx = new SyncNarrativeContext(
            new NarrativeTraceConfig());
        var svc = new ControlledService();
        var proxy = NarrativeTraceProxy
            .Create<IAsyncService>(svc, ctx);

        var fast = proxy.Fast();
        var slow = proxy.Slow();

        svc.FastTcs.SetResult("fast-result");
        svc.SlowTcs.SetResult("slow-result");
        await Task.WhenAll(fast, slow);

        Assert.Equal("fast-result", await fast);
        Assert.Equal("slow-result", await slow);
    }

    [Fact]
    public async Task Nested_async_through_proxy_produces_parent_child()
    {
        var ctx = new SyncNarrativeContext(
            new NarrativeTraceConfig());
        var svc = new ControlledService();
        var innerProxy = NarrativeTraceProxy
            .Create<IAsyncService>(svc, ctx);
        var outerSvc = new NestedAsyncService(innerProxy);
        var outerProxy = NarrativeTraceProxy
            .Create<INestedAsyncService>(outerSvc, ctx);

        var task = outerProxy.Outer();
        svc.FastTcs.SetResult("nested-result");
        var result = await task;

        Assert.Equal("nested-result", result);
        var trace = ctx.CaptureTrace();
        Assert.Single(trace.Roots);
        Assert.Equal("Outer",
            trace.Roots[0].Signature.MethodName);
    }

    [Fact]
    public void Sync_proxy_methods_still_work_unchanged()
    {
        var ctx = new SyncNarrativeContext(
            new NarrativeTraceConfig());
        var proxy = NarrativeTraceProxy
            .Create<ISyncService>(new SyncCalculator(), ctx);

        var result = proxy.Add(2, 3);

        Assert.Equal(5, result);
        var trace = ctx.CaptureTrace();
        Assert.Single(trace.Roots);
        Assert.Equal("Add",
            trace.Roots[0].Signature.MethodName);
        var returned = Assert.IsType<Returned>(
            trace.Roots[0].Outcome);
        Assert.Equal("5", returned.RenderedValue);
    }

    [Fact]
    public async Task One_async_faults_while_another_succeeds()
    {
        var ctx = new SyncNarrativeContext(
            new NarrativeTraceConfig());
        var svc = new ControlledService();
        var proxy = NarrativeTraceProxy
            .Create<IAsyncService>(svc, ctx);

        var fast = proxy.Fast();
        var slow = proxy.Slow();

        svc.FastTcs.SetException(
            new InvalidOperationException("boom"));
        svc.SlowTcs.SetResult("ok");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => fast);
        Assert.Equal("ok", await slow);

        var trace = ctx.CaptureTrace();
        Assert.Equal(2, trace.Roots.Count);

        var fastNode = trace.Roots
            .First(r => r.Signature.MethodName == "Fast");
        var slowNode = trace.Roots
            .First(r => r.Signature.MethodName == "Slow");

        Assert.IsType<Threw>(fastNode.Outcome);
        Assert.IsType<Returned>(slowNode.Outcome);
    }

    [Fact]
    public async Task Reverse_completion_order_produces_correct_trace()
    {
        var ctx = new SyncNarrativeContext(
            new NarrativeTraceConfig());
        var svc = new ControlledService();
        var proxy = NarrativeTraceProxy
            .Create<IAsyncService>(svc, ctx);

        var fast = proxy.Fast();
        var slow = proxy.Slow();

        svc.SlowTcs.SetResult("slow-first");
        svc.FastTcs.SetResult("fast-second");
        await Task.WhenAll(fast, slow);

        var trace = ctx.CaptureTrace();
        Assert.Equal(2, trace.Roots.Count);

        var fastNode = trace.Roots
            .First(r => r.Signature.MethodName == "Fast");
        var slowNode = trace.Roots
            .First(r => r.Signature.MethodName == "Slow");

        var fastVal = Assert.IsType<Returned>(
            fastNode.Outcome);
        var slowVal = Assert.IsType<Returned>(
            slowNode.Outcome);
        Assert.Equal("\"fast-second\"",
            fastVal.RenderedValue);
        Assert.Equal("\"slow-first\"",
            slowVal.RenderedValue);
    }
}
