// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using NarrativeTrace.AspNetCore;
using NarrativeTrace.Core;
using NarrativeTrace.DependencyInjection;
using Xunit;

namespace NarrativeTrace.AspNetCore.Tests;

public interface IGreetingService
{
    string Greet(string name);
}

public sealed class GreetingService : IGreetingService
{
    public string Greet(string name) => $"Hello {name}";
}

public class SharedContextCompositionTests
{
    private const string ThisNamespace = "NarrativeTrace.AspNetCore.Tests";

    private sealed class CapturingExporter : ITraceExporter
    {
        public TraceTree? Tree { get; private set; }

        public void Export(TraceTree tree, RequestContext requestContext)
            => Tree = tree;
    }

    private static (IServiceScope scope, NarrativeTraceMiddleware middleware,
        CapturingExporter exporter) Compose()
    {
        var services = new ServiceCollection();
        services.AddScoped<IGreetingService, GreetingService>();
        services.AddNarrativeTrace();
        services.AddNarrativeTracing(o => o.Namespaces(ThisNamespace));
        var provider = services.BuildServiceProvider();
        var scope = provider.CreateScope();
        var exporter = new CapturingExporter();
        var middleware = new NarrativeTraceMiddleware(
            scope.ServiceProvider.GetRequiredService<NarrativeTraceConfig>(),
            exporter);
        return (scope, middleware, exporter);
    }

    private static DefaultHttpContext RequestIn(IServiceScope scope)
    {
        var http = new DefaultHttpContext
        {
            RequestServices = scope.ServiceProvider,
        };
        http.Request.Method = "GET";
        http.Request.Path = "/greet";
        return http;
    }

    [Fact]
    public async Task Wrapped_service_span_appears_in_the_exported_tree()
    {
        var (scope, middleware, exporter) = Compose();
        using (scope)
        {
            var http = RequestIn(scope);

            await middleware.InvokeAsync(http, ctx =>
            {
                ctx.RequestServices
                    .GetRequiredService<IGreetingService>().Greet("Ada");
                return Task.CompletedTask;
            });

            Assert.NotNull(exporter.Tree);
            Assert.Contains(
                exporter.Tree!.Roots,
                r => r.Signature.MethodName == "Greet");
        }
    }

    [Fact]
    public async Task Http_context_shares_the_scoped_context_with_proxies()
    {
        var (scope, middleware, _) = Compose();
        using (scope)
        {
            var http = RequestIn(scope);
            var scoped = scope.ServiceProvider
                .GetRequiredService<INarrativeContext>();

            await middleware.InvokeAsync(http, _ => Task.CompletedTask);

            Assert.Same(scoped, http.GetNarrativeContext());
        }
    }

    public interface IPolicyService
    {
        void Renew(Policy policy);
    }

    public sealed record Policy(string PolicyId, string HolderName);

    private sealed class PolicyService : IPolicyService
    {
        public void Renew(Policy policy)
        {
        }
    }

    // Row 17b: a RedactionPolicy configured once through
    // services.AddNarrativeTrace(o => o.Redaction = …) — the ASP.NET Core
    // integration's own options — reaches a service auto-wrapped by
    // NarrativeTrace.DependencyInjection's AddNarrativeTracing (no
    // Redaction of its own set) when that service is invoked from inside a
    // real request through the middleware. Same study shape as
    // NarrativeTrace.Proxy.Tests.ParameterNameRedactionTests: a
    // plain-string PII property two levels down (Policy.HolderName) that
    // RedactionPolicy.Default does not recognize by name.
    [Fact]
    public async Task Redaction_configured_via_AddNarrativeTrace_reaches_an_auto_wrapped_proxy_through_the_middleware()
    {
        var services = new ServiceCollection();
        services.AddScoped<IPolicyService, PolicyService>();
        services.AddNarrativeTrace(
            o => o.Redaction = RedactionPolicy.OfPatterns(["holdername"]));
        services.AddNarrativeTracing(o => o.Namespaces(ThisNamespace));
        var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var exporter = new CapturingExporter();
        var middleware = new NarrativeTraceMiddleware(
            scope.ServiceProvider.GetRequiredService<NarrativeTraceConfig>(),
            exporter);
        var http = RequestIn(scope);

        await middleware.InvokeAsync(http, ctx =>
        {
            ctx.RequestServices.GetRequiredService<IPolicyService>()
                .Renew(new Policy("POL-123", "Ada Lovelace"));
            return Task.CompletedTask;
        });

        var parameter = exporter.Tree!.Roots
            .Single(r => r.Signature.MethodName == "Renew")
            .Signature.Parameters[0];
        Assert.DoesNotContain(
            "Ada Lovelace", parameter.RenderedValue, StringComparison.Ordinal);
        Assert.Contains(
            RedactionPolicy.Marker, parameter.RenderedValue, StringComparison.Ordinal);
    }
}
