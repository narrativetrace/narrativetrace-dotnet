// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NarrativeTrace.AspNetCore;
using NarrativeTrace.Core;
using Xunit;

namespace NarrativeTrace.AspNetCore.Tests;

public class ConcurrencyIsolationTests
{
    private sealed class RecordingExporter : ITraceExporter
    {
        private readonly List<TraceTree> _trees = new();

        public TraceTree[] Snapshot()
        {
            lock (_trees)
            {
                return _trees.ToArray();
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

    private static IHost BuildHost(RecordingExporter exporter)
    {
        return new HostBuilder()
            .ConfigureWebHost(web => web
                .UseTestServer()
                .ConfigureServices(services =>
                {
                    services.AddSingleton<ITraceExporter>(exporter);
                    services.AddNarrativeTrace();
                })
                .Configure(app => app.UseNarrativeTrace().Run(Handle)))
            .Build();
    }

    // Names the span after this request's own path so a tree whose root
    // method != root route would prove cross-request contamination.
    private static async Task Handle(HttpContext ctx)
    {
        var id = ctx.Request.Path.ToString();
        var nc = ctx.GetNarrativeContext();
        nc.EnterMethod("H", id, []);
        await Task.Yield();
        nc.ExitMethodWithReturn(null);
    }

    [Fact]
    public async Task Parallel_requests_never_cross_contaminate()
    {
        const int count = 50;
        var exporter = new RecordingExporter();
        using var host = BuildHost(exporter);
        await host.StartAsync();
        var client = host.GetTestClient();

        await Task.WhenAll(Enumerable.Range(0, count)
            .Select(i => client.GetAsync($"/r/{i}")));

        var trees = exporter.Snapshot();
        Assert.Equal(count, trees.Length);
        var routes = new HashSet<string>();
        foreach (var tree in trees)
        {
            var root = Assert.Single(tree.Roots);
            Assert.Equal(
                root.SpanContext!.HttpRoute!.Value,
                root.Signature.MethodName);
            Assert.True(routes.Add(root.SpanContext.HttpRoute.Value));
        }

        Assert.Equal(count, routes.Count);
    }
}
