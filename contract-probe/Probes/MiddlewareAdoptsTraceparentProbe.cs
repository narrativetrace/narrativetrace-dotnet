// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using Microsoft.AspNetCore.Http;
using NarrativeTrace.AspNetCore;
using NarrativeTrace.Core;

namespace NarrativeTrace.ContractProbe.Probes;

/// <summary>
/// <c>probed-default</c>, since 0.1.4: <c>NarrativeTraceMiddleware</c> adopts an inbound
/// <c>traceparent</c> request header automatically — configuration-guide.md §4, the request-header
/// sibling of <see cref="InitialTraceparentSeedsContextProbe"/>'s config-seeded path.
/// </summary>
/// <remarks>
/// No reflection needed, unlike the config-seeded probe: the BEHAVIOR is new in 0.1.4, but every
/// type this probe names — <c>NarrativeTraceMiddleware</c>, <c>DefaultHttpContext</c>,
/// <c>ITraceExporter</c>, <c>TraceTree</c> — already existed before it, so all of them compile
/// against 0.1.3 too. The header name is a literal ("traceparent"), never
/// <c>Traceparent.HeaderName</c> — that constant lives on the type this probe must NOT name at
/// compile time.
/// </remarks>
internal static class MiddlewareAdoptsTraceparentProbe
{
    private const string RemoteTraceIdValue = "4bf92f3577b34da6a3ce929d0e0e4736";
    private const string RemoteSpanIdValue = "00f067aa0ba902b7";
    private const string Header = "00-" + RemoteTraceIdValue + "-" + RemoteSpanIdValue + "-01";

    private sealed class CapturingExporter : ITraceExporter
    {
        public TraceTree? Tree { get; private set; }

        public void Export(TraceTree tree, RequestContext requestContext) => Tree = tree;
    }

    public static string Observe()
    {
        var exporter = new CapturingExporter();
        var middleware = new NarrativeTraceMiddleware(new NarrativeTraceConfig(), exporter);
        var http = new DefaultHttpContext();
        http.Request.Method = "GET";
        http.Request.Path = "/orders";
        http.Request.Headers["traceparent"] = Header;

        middleware.InvokeAsync(http, RunFixtureCall).GetAwaiter().GetResult();

        var root = exporter.Tree!.Roots[0];
        return root.SpanContext!.TraceId.Value == RemoteTraceIdValue ? "true" : "false";
    }

    private static Task RunFixtureCall(HttpContext context)
    {
        var narrativeContext = context.GetNarrativeContext();
        narrativeContext.EnterMethod("ContractProbeFixture", "Ping", []);
        narrativeContext.ExitMethodWithReturn("\"ok\"");
        return Task.CompletedTask;
    }
}
