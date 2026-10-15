// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using Checkout;
using NarrativeTrace.Core;
using NarrativeTrace.Proxy;
using NarrativeTrace.TestingXunit;
using Xunit;

namespace Checkout.Fixtures;

/// <summary>
/// The failing checkout test recipes/why-did-this-test-fail.md is read through. It fails on an
/// assertion, by design. Nothing here says which side is wrong — that is the reader's exercise,
/// and the narrative the run writes is what settles it.
/// </summary>
public sealed class StaleExpectationFixture : IDisposable
{
    // A fixed W3C traceparent, so the page's embedded artifact names the same trace every run.
    private const string DemoTraceparent = "00-c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7-c4e1d0a7c4e1d0a7-01";

    // One fixture per test: xUnit builds a fresh class instance for each test, so no test sees another's spans.
    private readonly NarrativeFixture _trace = new();

    public StaleExpectationFixture()
    {
        _trace.Context.AdoptTraceparent(Traceparent.Parse(DemoTraceparent));
    }

    public void Dispose() => _trace.Dispose();

    [Fact]
    public void The_customer_gets_an_order_id_for_their_keyboards()
    {
        var stock = new InMemoryInventory("SKU-KB", 5);
        var inventory = NarrativeTraceProxy.Create<IInventoryService>(stock, _trace.Context);
        var payments = NarrativeTraceProxy.Create<IPaymentService>(new CardPaymentService(), _trace.Context);
        var orders = NarrativeTraceProxy.Create<IOrderRepository>(new InMemoryOrderRepository(), _trace.Context);
        var checkout = NarrativeTraceProxy.Create<IOrderService>(
            new DefaultOrderService(inventory, payments, orders), _trace.Context);

        var failed = true;
        try
        {
            _trace.Run("The customer gets an order id for their keyboards", _ =>
            {
                var orderId = checkout.PlaceOrder("C-1234", "SKU-KB", 2, "4242424242424242");

                Assert.Equal("ORD-C-1234-SKU-KB-1", orderId);
            });
            failed = false;
        }
        finally
        {
            _trace.WriteArtifacts(nameof(StaleExpectationFixture),
                nameof(The_customer_gets_an_order_id_for_their_keyboards), failed);
        }
    }
}
