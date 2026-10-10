// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using Example.Orders;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NarrativeTrace.AspNetCore;
using NarrativeTrace.Core;
using Xunit;

namespace NarrativeTrace.AspNetCore.Tests.Wiring;

/// <summary>
/// Proves the framework table's ASP.NET Core snippet (<see cref="AspNetCoreWiring"/>) does what the
/// doctor's fix promises: a request through it exports one trace with the wrapped service's call
/// nested inside.
/// </summary>
public sealed class AspNetCoreWiringTests
{
    private sealed class RecordingExporter : ITraceExporter
    {
        public List<TraceTree> Trees { get; } = [];

        public void Export(TraceTree tree, RequestContext requestContext)
        {
            lock (Trees)
            {
                Trees.Add(tree);
            }
        }
    }

    [Fact]
    public async Task A_request_through_the_snippet_exports_a_trace_naming_the_wrapped_service()
    {
        var exporter = new RecordingExporter();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton<ITraceExporter>(exporter);
        await using var app = AspNetCoreWiring.Build(builder);
        await app.StartAsync();

        var body = await app.GetTestClient().GetStringAsync("/orders/cust-1");

        Assert.Equal("confirmed:cust-1", body);
        var tree = Assert.Single(exporter.Trees);
        var call = Assert.Single(tree.Roots);
        Assert.Equal("IOrderService", call.Signature.ClassName);
        Assert.Equal("PlaceOrder", call.Signature.MethodName);
    }
}
