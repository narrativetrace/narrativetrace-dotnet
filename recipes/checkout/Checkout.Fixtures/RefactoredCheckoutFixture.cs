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
/// The test recipes/review-a-refactoring-did-behaviour-change.md pins: a declined card must save
/// no order. It passes before and after the refactoring — which is the whole point of the page —
/// so only the approved structure can object.
/// </summary>
public sealed class RefactoredCheckoutFixture : IDisposable
{
    // A fixed W3C traceparent, so the page's embedded artifacts name the same trace every run.
    private const string DemoTraceparent = "00-c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7-c4e1d0a7c4e1d0a7-01";

    // One fixture per test: xUnit builds a fresh class instance for each test, so no test sees another's spans.
    private readonly NarrativeFixture _trace = new();

    public RefactoredCheckoutFixture()
    {
        _trace.Context.AdoptTraceparent(Traceparent.Parse(DemoTraceparent));
    }

    /// <summary>
    /// Which version of the checkout this run wires: the one that releases the reservation when the
    /// charge is declined, or the refactoring that dropped that call. An environment switch,
    /// because one build has to run both — in your project the refactoring is an edit to one file
    /// and this switch does not exist.
    /// </summary>
    private static bool RefactoringDroppedTheRelease =>
        Environment.GetEnvironmentVariable("CHECKOUT_RECIPE_REFACTORING") == "dropped-release";

    public void Dispose() => _trace.Dispose();

    [Fact]
    public void A_declined_card_saves_no_order()
    {
        var repository = new InMemoryOrderRepository();
        var inventory = NarrativeTraceProxy.Create<IInventoryService>(new InMemoryInventory("SKU-KB", 5), _trace.Context);
        var payments = NarrativeTraceProxy.Create<IPaymentService>(new CardPaymentService(), _trace.Context);
        var orders = NarrativeTraceProxy.Create<IOrderRepository>(repository, _trace.Context);
        IOrderService service = RefactoringDroppedTheRelease
            ? new OrderServiceWithoutRelease(inventory, payments, orders)
            : new DefaultOrderService(inventory, payments, orders);
        var checkout = NarrativeTraceProxy.Create<IOrderService>(service, _trace.Context);

        _trace.Run("A declined card saves no order", _ =>
        {
            Assert.Throws<PaymentDeclinedException>(
                () => checkout.PlaceOrder("C-1234", "SKU-KB", 2, CardPaymentService.DecliningCard));

            Assert.Empty(repository.Saved);
        });
        _trace.WriteArtifacts(nameof(RefactoredCheckoutFixture), nameof(A_declined_card_saves_no_order), failed: false);
    }
}
