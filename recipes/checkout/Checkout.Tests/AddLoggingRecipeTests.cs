// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using Checkout;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using NarrativeTrace.Core;
using NarrativeTrace.Logging;
using NarrativeTrace.Proxy;
using NarrativeTrace.Runtime;
using Xunit;

namespace Checkout.Tests;

/// <summary>
/// Proves recipes/add-logging-to-this-service.md: the checkout, with no log statement anywhere in
/// it, logs every call through the project's own <c>ILogger</c> once its collaborators are wrapped
///. The page's output block is what the logger wrote, not a rendering this
/// test produces — that is the whole claim of the page, so the evidence has to come from the logger.
/// </summary>
public sealed class AddLoggingRecipeTests
{
    // A fixed W3C traceparent, seeded through NarrativeTraceConfig so this page's embedded output
    // always names the same trace. A real run generates a random trace id every time.
    private const string DemoTraceparent = "00-c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7-c4e1d0a7c4e1d0a7-01";

    [Fact]
    public void A_customer_checks_out()
    {
        var log = new StringWriter { NewLine = "\n" };
        var original = Console.Out;
        Console.SetOut(log);
        try
        {
            CheckOut();
        }
        finally
        {
            Console.SetOut(original);
        }

        Assert.Contains("→ IOrderService.PlaceOrder(customerId: \"C-1234\"", log.ToString(), StringComparison.Ordinal);
        CapturedOutput.Write("add-logging-to-this-service", "checkout.log", log.ToString());
    }

    private static void CheckOut()
    {
        using var loggerFactory = LoggerFactory.Create(builder => builder
            .SetMinimumLevel(LogLevel.Trace)
            .AddSimpleConsole(options => options.ColorBehavior = LoggerColorBehavior.Disabled));
        var context = new LoggingNarrativeContext(
            new SyncNarrativeContext(new NarrativeTraceConfig(initialTraceparent: Traceparent.Parse(DemoTraceparent))),
            loggerFactory.CreateLogger("NarrativeTrace"));

        var stock = new InMemoryInventory("SKU-KB", 5);
        var repository = new InMemoryOrderRepository();
        var inventory = NarrativeTraceProxy.Create<IInventoryService>(stock, context);
        var payments = NarrativeTraceProxy.Create<IPaymentService>(new CardPaymentService(), context);
        var orders = NarrativeTraceProxy.Create<IOrderRepository>(repository, context);
        var checkout = NarrativeTraceProxy.Create<IOrderService>(
            new DefaultOrderService(inventory, payments, orders), context);

        var orderId = checkout.PlaceOrder("C-1234", "SKU-KB", 2, "4242424242424242");

        Assert.Equal("ORD-C-1234-SKU-KB-2", orderId);
        Assert.Equal(3, stock.AvailableUnits("SKU-KB"));
        Assert.Single(repository.Saved);
    }
}
