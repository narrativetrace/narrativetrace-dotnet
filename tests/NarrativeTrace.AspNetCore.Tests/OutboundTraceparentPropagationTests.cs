// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using NarrativeTrace.AspNetCore;
using NarrativeTrace.Core;
using Xunit;

namespace NarrativeTrace.AspNetCore.Tests;

/// <summary>An HttpClient from IHttpClientFactory sends the serving request's trace downstream.</summary>
public class OutboundTraceparentPropagationTests
{
    private const string ClientName = "payments";

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public List<string> Headers { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Headers.TryGetValues(Traceparent.HeaderName, out var values))
            {
                Headers.AddRange(values);
            }

            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
        }
    }

    private static ServiceProvider Build(CapturingHandler downstream)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddNarrativeTrace();
        services.AddHttpClient(ClientName)
            .AddNarrativeTraceTraceparentPropagation()
            .ConfigurePrimaryHttpMessageHandler(() => downstream);
        return services.BuildServiceProvider();
    }

    private static async Task Call(ServiceProvider root, IServiceProvider? requestServices)
    {
        var accessor = root.GetRequiredService<IHttpContextAccessor>();
        accessor.HttpContext = requestServices is null
            ? null
            : new DefaultHttpContext { RequestServices = requestServices };
        using var client = root.GetRequiredService<IHttpClientFactory>().CreateClient(ClientName);
        using var response = await client.GetAsync(new Uri("http://payments.test/charge"));
    }

    [Fact]
    public async Task Client_sends_the_serving_requests_trace_and_active_span()
    {
        var downstream = new CapturingHandler();
        await using var root = Build(downstream);
        using var scope = root.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<INarrativeContext>();
        var span = ctx.EnterMethod("Checkout", "Pay", Array.Empty<ParameterCapture>());

        await Call(root, scope.ServiceProvider);

        var sent = Traceparent.Parse(Assert.Single(downstream.Headers));
        Assert.Equal(ctx.CurrentTraceId, sent!.TraceId);
        Assert.Equal(span, sent.ParentSpanId);
    }

    [Fact]
    public async Task Client_sends_no_header_outside_a_request()
    {
        var downstream = new CapturingHandler();
        await using var root = Build(downstream);

        await Call(root, null);

        Assert.Empty(downstream.Headers);
    }

    [Fact]
    public void Registration_rejects_a_null_builder()
    {
        Assert.Throws<ArgumentNullException>(
            () => HttpClientBuilderExtensions.AddNarrativeTraceTraceparentPropagation(null!));
    }
}
