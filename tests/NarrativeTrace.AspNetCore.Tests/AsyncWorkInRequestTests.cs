// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NarrativeTrace.Core;
using Xunit;

namespace NarrativeTrace.AspNetCore.Tests;

/// <summary>
/// The integration proof for bidirectional snapshot propagation: a request that
/// starts a <see cref="Task"/> exports that work as part of the request's own
/// trace — exactly once, under the request's trace id.
/// </summary>
/// <remarks>
/// The .NET counterpart of Java's
/// <c>SpringIntegrationTest.asyncCallAppearsInTheCallersTrace</c>. Both cases
/// matter: the ordinary one where the request awaits its task, and the racy one
/// where the request finishes (and the middleware exports) while the worker's
/// scope is <i>still open</i> — which is the ordering a framework actually
/// produces, and the one that used to lose the call.
/// </remarks>
public sealed class AsyncWorkInRequestTests
{
    private const string Handler = "PlaceOrder";
    private const string AsyncCall = "NotifyOrderPlaced";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task An_awaited_task_appears_once_in_the_requests_exported_trace()
    {
        var exporter = new RecordingExporter();
        using var host = BuildHost(exporter, AwaitedAsyncWork);
        await host.StartAsync();

        var response = await host.GetTestClient().GetAsync("/orders");

        Assert.True(response.IsSuccessStatusCode);
        var tree = Assert.Single(exporter.Trees);
        Assert.Equal(1, Occurrences(tree, AsyncCall));
        Assert.Equal([Handler, AsyncCall], MethodNames(tree));
    }

    [Fact]
    public async Task The_async_call_carries_the_requests_trace_id()
    {
        var exporter = new RecordingExporter();
        using var host = BuildHost(exporter, AwaitedAsyncWork);
        await host.StartAsync();

        await host.GetTestClient().GetAsync("/orders");

        var tree = Assert.Single(exporter.Trees);
        var traceIds = TraceIds(tree);
        Assert.Equal(2, traceIds.Count);
        Assert.All(traceIds, id => Assert.Equal(traceIds[0], id));
    }

    [Fact]
    public async Task A_task_still_inside_its_scope_when_the_request_ends_is_exported_once()
    {
        var exporter = new RecordingExporter();
        using var worker = new HeldWorker();
        using var host = BuildHost(exporter, worker.Start);
        await host.StartAsync();
        try
        {
            await host.GetTestClient().GetAsync("/orders");

            // Exported while the worker still holds its scope open: published
            // is reportable, and joined is not the condition.
            var tree = Assert.Single(exporter.Trees);
            Assert.Equal(1, Occurrences(tree, AsyncCall));
        }
        finally
        {
            worker.ReleaseAndJoin();
        }
    }

    /// <summary>Starts the task, waits for it, and closes its scope — the plain case.</summary>
    private static async Task AwaitedAsyncWork(INarrativeContext trace)
    {
        var snapshot = trace.Snapshot();
        await Task.Run(() =>
        {
            using var scope = snapshot.Activate();
            trace.EnterMethod("NotificationService", AsyncCall, []);
            trace.ExitMethodWithReturn("true");
        });
    }

    private static IHost BuildHost(
        RecordingExporter exporter, Func<INarrativeContext, Task> asyncWork)
    {
        return new HostBuilder()
            .ConfigureWebHost(web => web
                .UseTestServer()
                .ConfigureServices(services =>
                {
                    services.AddSingleton<ITraceExporter>(exporter);
                    services.AddNarrativeTrace();
                })
                .Configure(app =>
                {
                    app.UseNarrativeTrace();
                    app.Run(ctx => HandleRequest(ctx, asyncWork));
                }))
            .Build();
    }

    private static async Task HandleRequest(
        HttpContext ctx, Func<INarrativeContext, Task> asyncWork)
    {
        var trace = ctx.GetNarrativeContext();
        trace.EnterMethod("OrderController", Handler, []);
        await asyncWork(trace);
        trace.ExitMethodWithReturn("\"ORD-1\"");
    }

    /// <summary>
    /// A worker that publishes its call, tells the request it may return, and
    /// only then blocks — so the middleware exports while its scope is open.
    /// </summary>
    private sealed class HeldWorker : IDisposable
    {
        private readonly ManualResetEventSlim _published = new(false);
        private readonly ManualResetEventSlim _release = new(false);
        private Task? _task;

        internal Task Start(INarrativeContext trace)
        {
            var snapshot = trace.Snapshot();
            _task = Task.Run(() =>
            {
                using var scope = snapshot.Activate();
                trace.EnterMethod("NotificationService", AsyncCall, []);
                trace.ExitMethodWithReturn("true");
                _published.Set();
                _release.Wait(Timeout);
            });
            Assert.True(_published.Wait(Timeout));
            return Task.CompletedTask;
        }

        internal void ReleaseAndJoin()
        {
            _release.Set();
            _task?.Wait(Timeout);
        }

        public void Dispose()
        {
            _release.Set();
            _published.Dispose();
            _release.Dispose();
        }
    }

    private sealed class RecordingExporter : ITraceExporter
    {
        private readonly List<TraceTree> _trees = [];

        internal TraceTree[] Trees
        {
            get
            {
                lock (_trees)
                {
                    return [.. _trees];
                }
            }
        }

        public void Export(TraceTree tree, RequestContext requestContext)
        {
            lock (_trees)
            {
                _trees.Add(tree);
            }
        }
    }

    private static int Occurrences(TraceTree tree, string methodName)
    {
        var count = 0;
        foreach (var name in MethodNames(tree))
        {
            if (string.Equals(name, methodName, StringComparison.Ordinal))
            {
                count++;
            }
        }

        return count;
    }

    private static List<string> MethodNames(TraceTree tree)
    {
        var names = new List<string>();
        Collect(tree.Roots, node => node.Signature.MethodName, names);
        return names;
    }

    private static List<string> TraceIds(TraceTree tree)
    {
        var ids = new List<string>();
        Collect(tree.Roots, node => node.SpanContext?.TraceId.ToString(), ids);
        return ids;
    }

    private static void Collect(
        IReadOnlyList<TraceNode> nodes,
        Func<TraceNode, string?> select,
        List<string> into)
    {
        for (var i = 0; i < nodes.Count; i++)
        {
            if (select(nodes[i]) is { } value)
            {
                into.Add(value);
            }

            Collect(nodes[i].Children, select, into);
        }
    }
}
