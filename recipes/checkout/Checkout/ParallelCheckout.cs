// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using NarrativeTrace.Proxy;
using NarrativeTrace.Runtime;

namespace Checkout;

/// <summary>
/// The same checkout with the two slow steps off the caller's thread: reserve the stock and run the
/// fraud check as two branches of a <see cref="ForkJoinGroup"/>, wait for both, then charge and save.
/// A branch runs on a pool thread that knows nothing about the caller's trace, so each branch is
/// handed an isolated child context of the group and traces through it; <c>JoinAsync</c> grafts what
/// the branches recorded back under the span that launched them. Skip the join and the branches stay
/// out of the caller's trace. The inventory and the fraud check are passed unwrapped for that reason:
/// this class decides which context each call is traced through.
/// </summary>
public sealed class ParallelCheckout(
    INarrativeContext context,
    IInventoryService inventory,
    IFraudCheckService fraud,
    IPaymentService payments,
    IOrderRepository orders) : IOrderService
{
    private const int UnitPriceCents = 3999;

    public string PlaceOrder(string customerId, string sku, int quantity, string cardNumber)
    {
        var amountCents = UnitPriceCents * quantity;
        var group = ForkJoinGroup.Create(context);
        var reserving = group.Fork(branch =>
        {
            NarrativeTraceProxy.Create(inventory, branch).Reserve(sku, quantity);
            return true;
        });
        var scoring = group.Fork(branch => NarrativeTraceProxy.Create(fraud, branch).Approve(customerId, amountCents));
        group.JoinAsync().GetAwaiter().GetResult();

        if (!scoring.Result)
        {
            NarrativeTraceProxy.Create(inventory, context).Release(sku, quantity);
            throw new PaymentDeclinedException("the fraud check refused the order");
        }

        _ = reserving.Result;
        payments.Charge(customerId, amountCents, cardNumber);
        var orderId = $"ORD-{customerId}-{sku}-{quantity}";
        orders.Save(new Order(orderId, customerId, sku, quantity, amountCents));
        return orderId;
    }
}
