// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using NarrativeTrace.Runtime;
using Xunit;

namespace NarrativeTrace.Proxy.Tests;

/// <summary>
/// The checkout service calls a downstream payment API from traced code; the call carries the current
/// trace as a W3C <c>traceparent</c> header without the service formatting one by hand.
/// </summary>
public class OutboundTraceparentTests
{
    [Fact]
    public async Task An_outbound_http_call_from_traced_code_carries_the_current_trace()
    {
        var downstream = new CapturingHandler();
        var ctx = new AsyncNarrativeContext(new NarrativeTraceConfig());
        var traceId = string.Empty;

        await ctx.RunAsync(async () =>
        {
            var proxy = NarrativeTraceProxy.Create<IPaymentClient>(
                new PaymentClient(new HttpClient(new TraceparentPropagationHandler(ctx) { InnerHandler = downstream })), ctx);
            traceId = ctx.EnsureTraceId().ToString();

            await proxy.ChargeAsync("ORD-1");
        });

        var header = Assert.Single(downstream.TraceparentHeaders);
        var sent = Traceparent.Parse(header);
        Assert.NotNull(sent);
        Assert.Equal(traceId, sent!.TraceId.ToString());
    }

    public interface IPaymentClient
    {
        Task ChargeAsync(string orderId);
    }

    private sealed class PaymentClient(HttpClient http) : IPaymentClient
    {
        public async Task ChargeAsync(string orderId)
        {
            using var response = await http.PostAsync(
                new Uri("http://payments.test/charge/" + orderId), new StringContent(string.Empty));
            response.EnsureSuccessStatusCode();
        }
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public List<string> TraceparentHeaders { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Headers.TryGetValues(Traceparent.HeaderName, out var values))
            {
                TraceparentHeaders.AddRange(values);
            }

            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
        }
    }
}
