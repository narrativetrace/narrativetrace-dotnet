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
/// The reproduction recipes/help-me-debug-and-fix-this-bug.md is written around: a declined card
/// must leave the stock as it found it. The second test is the same reproduction against the
/// checkout as it stood before the fix, so the page's "before" trace comes from a real failing run
/// rather than from prose; it fails by design.
/// </summary>
public sealed class DeclinedPaymentFixture : IDisposable
{
    // A fixed W3C traceparent, so the page's embedded artifacts name the same trace every run.
    private const string DemoTraceparent = "00-c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7-c4e1d0a7c4e1d0a7-01";

    // One fixture per test: xUnit builds a fresh class instance for each test, so no test sees another's spans.
    private readonly NarrativeFixture _trace = new();

    public DeclinedPaymentFixture()
    {
        _trace.Context.AdoptTraceparent(Traceparent.Parse(DemoTraceparent));
    }

    public void Dispose() => _trace.Dispose();

    [Fact]
    public void The_stock_is_released_when_the_card_is_declined()
    {
        var stock = new InMemoryInventory("SKU-KB", 5);
        var repository = new InMemoryOrderRepository();
        var inventory = NarrativeTraceProxy.Create<IInventoryService>(stock, _trace.Context);
        var payments = NarrativeTraceProxy.Create<IPaymentService>(new CardPaymentService(), _trace.Context);
        var orders = NarrativeTraceProxy.Create<IOrderRepository>(repository, _trace.Context);
        var checkout = NarrativeTraceProxy.Create<IOrderService>(
            new DefaultOrderService(inventory, payments, orders), _trace.Context);

        _trace.Run("The stock is released when the card is declined", _ =>
        {
            Assert.Throws<PaymentDeclinedException>(
                () => checkout.PlaceOrder("C-1234", "SKU-KB", 2, CardPaymentService.DecliningCard));

            Assert.Empty(repository.Saved);
            Assert.Equal(5, stock.AvailableUnits("SKU-KB"));
        });
        _trace.WriteArtifacts(nameof(DeclinedPaymentFixture),
            nameof(The_stock_is_released_when_the_card_is_declined), failed: false);
    }

    [Fact]
    public void The_stock_is_released_when_the_card_is_declined_before_the_fix()
    {
        var stock = new InMemoryInventory("SKU-KB", 5);
        var repository = new InMemoryOrderRepository();
        var inventory = NarrativeTraceProxy.Create<IInventoryService>(stock, _trace.Context);
        var payments = NarrativeTraceProxy.Create<IPaymentService>(new CardPaymentService(), _trace.Context);
        var orders = NarrativeTraceProxy.Create<IOrderRepository>(repository, _trace.Context);
        var checkout = NarrativeTraceProxy.Create<IOrderService>(
            new OrderServiceWithoutRelease(inventory, payments, orders), _trace.Context);

        try
        {
            _trace.Run("The stock is released when the card is declined, before the fix", _ =>
            {
                Assert.Throws<PaymentDeclinedException>(
                    () => checkout.PlaceOrder("C-1234", "SKU-KB", 2, CardPaymentService.DecliningCard));

                Assert.Empty(repository.Saved);
                Assert.Equal(5, stock.AvailableUnits("SKU-KB"));
            });
        }
        catch
        {
            _trace.WriteArtifacts(nameof(DeclinedPaymentFixture),
                nameof(The_stock_is_released_when_the_card_is_declined_before_the_fix), failed: true);
            throw;
        }
    }
}
