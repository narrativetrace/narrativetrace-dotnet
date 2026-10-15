// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace Checkout;

/// <summary>
/// The same checkout with no release path: a declined charge leaves the reservation behind.
/// Two recipe pages need this code to run, not to be described — the bug before its fix and the
/// refactoring that drops the release while every test stays green. Never the implementation to
/// copy: <see cref="DefaultOrderService"/> is.
/// </summary>
public sealed class OrderServiceWithoutRelease(
    IInventoryService inventory, IPaymentService payments, IOrderRepository orders) : IOrderService
{
    private const int UnitPriceCents = 3999;

    public string PlaceOrder(string customerId, string sku, int quantity, string cardNumber)
    {
        inventory.Reserve(sku, quantity);
        var amountCents = UnitPriceCents * quantity;
        payments.Charge(customerId, amountCents, cardNumber);
        var orderId = $"ORD-{customerId}-{sku}-{quantity}";
        orders.Save(new Order(orderId, customerId, sku, quantity, amountCents));
        return orderId;
    }
}
