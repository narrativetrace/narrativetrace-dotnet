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

public class TestServerIntegrationTests
{
    private sealed class RecordingExporter : ITraceExporter
    {
        private readonly List<TraceTree> _trees = new();

        public TraceTree[] Trees
        {
            get
            {
                lock (_trees)
                {
                    return _trees.ToArray();
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
                .Configure(app =>
                {
                    app.UseNarrativeTrace();
                    app.Run(ctx =>
                    {
                        var nc = ctx.GetNarrativeContext();
                        nc.EnterMethod("Handler", "Index", []);
                        nc.ExitMethodWithReturn(null);
                        return Task.CompletedTask;
                    });
                }))
            .Build();
    }

    [Fact]
    public async Task UseNarrativeTrace_round_trips_a_request_and_exports()
    {
        var exporter = new RecordingExporter();
        using var host = BuildHost(exporter);
        await host.StartAsync();

        var response = await host.GetTestClient().GetAsync("/orders");

        Assert.True(response.IsSuccessStatusCode);
        Assert.Single(exporter.Trees);
        Assert.Single(exporter.Trees[0].Roots);
    }
}
