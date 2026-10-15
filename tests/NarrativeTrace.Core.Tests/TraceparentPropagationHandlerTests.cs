// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using NarrativeTrace.Runtime;
using Xunit;

namespace NarrativeTrace.Core.Tests;

public class TraceparentPropagationHandlerTests
{
    private static readonly Uri Target = new("http://payments.test/charge");

    private static SyncNarrativeContext NewContext() => new(new NarrativeTraceConfig());

    private static SpanId Enter(INarrativeContext ctx) =>
        ctx.EnterMethod("Svc", "Call", Array.Empty<ParameterCapture>());

    private static async Task<HttpRequestMessage> SendThrough(
        INarrativeContext? ctx, Action<HttpRequestMessage>? tweak = null)
    {
        var capture = new CapturingHandler();
        var handler = new TraceparentPropagationHandler(() => ctx) { InnerHandler = capture };
        using var client = new HttpClient(handler);
        using var request = new HttpRequestMessage(HttpMethod.Get, Target);
        tweak?.Invoke(request);
        using var response = await client.SendAsync(request);
        return capture.Seen!;
    }

    [Fact]
    public async Task Header_names_the_active_span_as_the_callees_parent()
    {
        var ctx = NewContext();
        var span = Enter(ctx);

        var request = await SendThrough(ctx);

        var sent = Traceparent.Parse(Assert.Single(request.Headers.GetValues(Traceparent.HeaderName)));
        Assert.Equal(ctx.CurrentTraceId, sent!.TraceId);
        Assert.Equal(span, sent.ParentSpanId);
        Assert.True(sent.Sampled);
    }

    [Fact]
    public async Task Innermost_span_is_the_parent_when_calls_nest()
    {
        var ctx = NewContext();
        Enter(ctx);
        var inner = Enter(ctx);

        var request = await SendThrough(ctx);

        var sent = Traceparent.Parse(request.Headers.GetValues(Traceparent.HeaderName).Single());
        Assert.Equal(inner, sent!.ParentSpanId);
    }

    [Fact]
    public async Task No_header_is_sent_when_no_trace_has_started()
    {
        var request = await SendThrough(NewContext());

        Assert.False(request.Headers.Contains(Traceparent.HeaderName));
    }

    [Fact]
    public async Task No_header_is_sent_when_the_resolver_has_no_context()
    {
        var request = await SendThrough(null);

        Assert.False(request.Headers.Contains(Traceparent.HeaderName));
    }

    [Fact]
    public async Task A_traceparent_the_caller_set_is_never_overwritten()
    {
        var ctx = NewContext();
        Enter(ctx);
        const string callers = "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-00";

        var request = await SendThrough(
            ctx, r => r.Headers.TryAddWithoutValidation(Traceparent.HeaderName, callers));

        Assert.Equal(callers, Assert.Single(request.Headers.GetValues(Traceparent.HeaderName)));
    }

    [Fact]
    public async Task An_adopted_inbound_parent_is_forwarded_before_any_local_span_exists()
    {
        var ctx = NewContext();
        var inbound = Traceparent.Parse("00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01")!;
        ctx.AdoptTraceparent(inbound);

        var request = await SendThrough(ctx);

        var sent = Traceparent.Parse(request.Headers.GetValues(Traceparent.HeaderName).Single());
        Assert.Equal(inbound.TraceId, sent!.TraceId);
        Assert.Equal(inbound.ParentSpanId, sent.ParentSpanId);
    }

    [Fact]
    public async Task Async_context_outside_a_scope_sends_no_header_and_does_not_throw()
    {
        var request = await SendThrough(new AsyncNarrativeContext(new NarrativeTraceConfig()));

        Assert.False(request.Headers.Contains(Traceparent.HeaderName));
    }

    [Fact]
    public async Task A_handler_built_on_a_fixed_context_propagates_that_contexts_trace()
    {
        var ctx = NewContext();
        var span = Enter(ctx);
        var capture = new CapturingHandler();
        using var client = new HttpClient(new TraceparentPropagationHandler(ctx) { InnerHandler = capture });

        using var response = await client.GetAsync(Target);

        var sent = Traceparent.Parse(capture.Seen!.Headers.GetValues(Traceparent.HeaderName).Single());
        Assert.Equal(span, sent!.ParentSpanId);
    }

    [Fact]
    public void Constructors_reject_null()
    {
        Assert.Throws<ArgumentNullException>(() => new TraceparentPropagationHandler((INarrativeContext)null!));
        Assert.Throws<ArgumentNullException>(() => new TraceparentPropagationHandler((Func<INarrativeContext?>)null!));
        Assert.Throws<ArgumentNullException>(() => TraceparentHeaders.Current(null!));
    }

    [Fact]
    public void Helper_formats_the_same_value_the_handler_would_send()
    {
        var ctx = NewContext();
        var span = Enter(ctx);

        var value = TraceparentHeaders.Current(ctx);

        Assert.Equal(new Traceparent(ctx.CurrentTraceId!.Value, span, 1).Format(), value);
        Assert.Null(TraceparentHeaders.Current(NewContext()));
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public HttpRequestMessage? Seen { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Seen = request;
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
        }
    }
}
