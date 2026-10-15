// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using Checkout;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using NarrativeTrace.Core;
using NarrativeTrace.Logging;
using NarrativeTrace.Proxy;
using NarrativeTrace.TestingXunit;
using Xunit;

namespace Checkout.Fixtures;

/// <summary>
/// One successful checkout, the flow recipes/review-a-complex-flow-before-changing-it.md is read
/// before changing anything. A fixture because the recipe runs it twice under different tracing
/// levels and keeps the log lines the run wrote beside the trace artifacts. It passes either way —
/// nothing here fails by design.
/// </summary>
public sealed class CheckoutReviewFixture : IDisposable
{
    // A fixed W3C traceparent, so the page's embedded artifacts name the same trace every run.
    private const string DemoTraceparent = "00-c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7-c4e1d0a7c4e1d0a7-01";

    // One fixture per test: xUnit builds a fresh class instance for each test, so no test sees another's spans.
    private readonly NarrativeFixture _trace = new();

    public CheckoutReviewFixture()
    {
        _trace.Context.AdoptTraceparent(Traceparent.Parse(DemoTraceparent));
    }

    public void Dispose() => _trace.Dispose();

    [Fact]
    public void A_customer_buys_two_keyboards()
    {
        var log = new StringWriter { NewLine = "\n" };
        var original = Console.Out;
        Console.SetOut(log);
        try
        {
            RunCheckoutThroughALogger();
        }
        finally
        {
            Console.SetOut(original);
        }

        var outputDirectory = Environment.GetEnvironmentVariable("NARRATIVETRACE_OUTPUT_DIR");
        Assert.False(string.IsNullOrEmpty(outputDirectory), "the recipe run names its output directory");
        File.WriteAllText(Path.Combine(outputDirectory, "checkout.log"), log.ToString());
        _trace.WriteArtifacts(nameof(CheckoutReviewFixture), nameof(A_customer_buys_two_keyboards), failed: false);
    }

    private void RunCheckoutThroughALogger()
    {
        using var loggerFactory = LoggerFactory.Create(builder =>
            builder.SetMinimumLevel(LogLevel.Trace).AddSimpleConsole(
                options => options.ColorBehavior = LoggerColorBehavior.Disabled));
        var context = new LoggingNarrativeContext(_trace.Context, loggerFactory.CreateLogger("NarrativeTrace"));
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
