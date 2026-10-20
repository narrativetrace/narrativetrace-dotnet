// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using Checkout;
using Microsoft.Extensions.Logging;
using NarrativeTrace.Core;
using NarrativeTrace.Logging;
using NarrativeTrace.Proxy;
using NarrativeTrace.Runtime;
using Xunit;

namespace Checkout.Tests;

/// <summary>
/// Proves recipes/follow-one-request-across-services.md: the checkout charges a card through a
/// second service over HTTP, the W3C <c>traceparent</c> goes with the request, and the callee's own
/// trace carries the caller's trace id and names the caller's span as its parent (rule 8, docs as tests).
/// The callee's narrative is captured here, not by the caller's context: the second service has a
/// context — and a narrative — of its own, which is the point of the page.
/// </summary>
public sealed class AcrossServicesRecipeTests
{
    private const string Slug = "follow-one-request-across-services";

    // A fixed W3C traceparent, so the page's embedded artifacts name the same trace every run.
    private const string DemoTraceparent = "00-c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7-c4e1d0a7c4e1d0a7-01";

    [Fact]
    public async Task A_customer_checks_out_against_the_payment_service()
    {
        var log = new StringWriter { NewLine = "\n" };
        await using var paymentService = await PaymentServiceHost.StartAsync(log);
        var stock = new InMemoryInventory("SKU-KB", 5);
        var repository = new InMemoryOrderRepository();
        var context = CallerContext(log);
        using var client = new HttpClient(new TraceparentPropagationHandler(context) { InnerHandler = new HttpClientHandler() });

        var orderId = CheckoutCalling(client, paymentService, context, stock, repository)
            .PlaceOrder("C-1234", "SKU-KB", 2, "4242424242424242");

        Assert.Equal("ORD-C-1234-SKU-KB-2", orderId);
        Assert.Single(repository.Saved);
        AssertTheTraceCrossedTheWire(paymentService, context.CaptureTrace());
        CapturedOutput.Write(Slug, "checkout.log", log.ToString());
        CapturedOutput.Write(Slug, "caller.nt", StructuralTraceRenderer.RenderDocument(context.CaptureTrace(), "A customer checks out against the payment service"));
        CapturedOutput.Write(Slug, "payment-service.md", MarkdownRenderer.Render(paymentService.LastTrace));
    }

    private static LoggingNarrativeContext CallerContext(TextWriter log)
    {
        var loggerFactory = LoggerFactory.Create(builder => builder
            .SetMinimumLevel(LogLevel.Trace)
            .AddProvider(new CorrelationLoggerProvider(log)));
        var traceparent = Traceparent.Parse(DemoTraceparent);
        return new LoggingNarrativeContext(
            new SyncNarrativeContext(new NarrativeTraceConfig(initialTraceparent: traceparent)),
            loggerFactory.CreateLogger("NarrativeTrace"));
    }

    /// <summary>The checkout, with the HTTP payment client where the local one used to be.</summary>
    private static IOrderService CheckoutCalling(
        HttpClient client, PaymentServiceHost paymentService, INarrativeContext context,
        InMemoryInventory stock, InMemoryOrderRepository repository)
    {
        var inventory = NarrativeTraceProxy.Create<IInventoryService>(stock, context);
        var payments = NarrativeTraceProxy.Create<IPaymentService>(
            new HttpPaymentService(client, paymentService.Endpoint), context);
        var orders = NarrativeTraceProxy.Create<IOrderRepository>(repository, context);
        return NarrativeTraceProxy.Create<IOrderService>(
            new DefaultOrderService(inventory, payments, orders), context);
    }

    /// <summary>
    /// One trace, two services: the header named the caller's own span, and the callee's root adopted
    /// it as its parent instead of starting a trace of its own.
    /// </summary>
    private static void AssertTheTraceCrossedTheWire(PaymentServiceHost paymentService, TraceTree callerTrace)
    {
        var sent = Traceparent.Parse(paymentService.InboundTraceparent);
        Assert.NotNull(sent);
        Assert.Equal(Traceparent.Parse(DemoTraceparent)!.TraceId, sent.TraceId);

        var calleeRoot = Assert.Single(paymentService.LastTrace.Roots);
        Assert.Equal("Charge", calleeRoot.Signature.MethodName);
        Assert.Equal(sent.TraceId, calleeRoot.SpanContext!.TraceId);
        Assert.Equal(sent.ParentSpanId, calleeRoot.SpanContext.ParentSpanId);
        var exportedEnter = System.Text.Json.JsonDocument.Parse(
                JsonExporter.Export(paymentService.LastTrace, new TraceMetadata("callee", ScenarioResult.Success)))
            .RootElement.GetProperty("events")[0];
        Assert.Equal(sent.ParentSpanId.Value, exportedEnter.GetProperty("parentSpanId").GetString());
        var callersCharge = callerTrace.Roots[0].Children.Single(n => n.Signature.MethodName == "Charge");
        Assert.Equal(callersCharge.SpanContext!.SpanId, sent.ParentSpanId);
    }
}
