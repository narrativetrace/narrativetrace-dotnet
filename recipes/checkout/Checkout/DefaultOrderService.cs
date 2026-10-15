// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace Checkout;

/// <summary>The checkout: reserve the stock, charge the card, save the order — and release on a decline.</summary>
public sealed class DefaultOrderService(
    IInventoryService inventory, IPaymentService payments, IOrderRepository orders) : IOrderService
{
    private const int UnitPriceCents = 3999;

    public string PlaceOrder(string customerId, string sku, int quantity, string cardNumber)
    {
        inventory.Reserve(sku, quantity);
        var amountCents = UnitPriceCents * quantity;
        try
        {
            payments.Charge(customerId, amountCents, cardNumber);
        }
        catch (PaymentDeclinedException)
        {
            inventory.Release(sku, quantity);
            throw;
        }

        var orderId = $"ORD-{customerId}-{sku}-{quantity}";
        orders.Save(new Order(orderId, customerId, sku, quantity, amountCents));
        return orderId;
    }
}
