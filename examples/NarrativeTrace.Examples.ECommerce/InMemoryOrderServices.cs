// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Globalization;

namespace NarrativeTrace.Examples.ECommerce;

/// <summary>In-memory customer directory seeded with one known customer.</summary>
public sealed class InMemoryCustomerService : ICustomerService
{
    private readonly Dictionary<string, Customer> _customers = new()
    {
        ["cust-1"] = new Customer("cust-1", "Ada Lovelace", CustomerTier.Premium),
    };

    public Customer FindCustomer(string customerId)
    {
        return _customers.TryGetValue(customerId, out var customer)
            ? customer
            : throw new ArgumentException($"Unknown customer: {customerId}", nameof(customerId));
    }
}

/// <summary>In-memory price list.</summary>
public sealed class InMemoryProductCatalogService : IProductCatalogService
{
    private readonly Dictionary<string, decimal> _prices = new()
    {
        ["book-123"] = 9.99m,
        ["mechanical-keyboard"] = 149.99m,
        ["usb-hub"] = 24.99m,
    };

    public decimal LookupPrice(string productId)
    {
        return _prices.TryGetValue(productId, out var price)
            ? price
            : throw new ArgumentException($"Unknown product: {productId}", nameof(productId));
    }
}

/// <summary>In-memory stock ledger; reserving more than is available fails.</summary>
public sealed class InMemoryInventoryService(Dictionary<string, int> stock) : IInventoryService
{
    public void Reserve(string productId, int quantity)
    {
        var available = stock.TryGetValue(productId, out var count) ? count : 0;
        if (available < quantity)
        {
            throw new InvalidOperationException(
                $"Insufficient stock for {productId}: need {quantity}, have {available}");
        }

        stock[productId] = available - quantity;
    }

    public void Release(string productId, int quantity)
    {
        stock[productId] = (stock.TryGetValue(productId, out var count) ? count : 0) + quantity;
    }
}

/// <summary>Payment gateway that declines amounts over an approval limit.</summary>
public sealed class InMemoryPaymentService(decimal approvalLimit) : IPaymentService
{
    private int _counter;

    public PaymentConfirmation Charge(string customerId, decimal amount, string cardToken)
    {
        if (amount > approvalLimit)
        {
            throw new PaymentDeclinedException(string.Create(
                CultureInfo.InvariantCulture,
                $"Amount {amount} exceeds approval limit {approvalLimit}"));
        }

        var transactionId = string.Create(
            CultureInfo.InvariantCulture, $"txn-{++_counter:D5}");
        return new PaymentConfirmation(transactionId, amount);
    }
}

/// <summary>Notification sink that simply counts the confirmations it sent.</summary>
public sealed class InMemoryNotificationService : INotificationService
{
    public int SentCount { get; private set; }

    public Task<bool> NotifyOrderPlaced(string customerId, string orderId)
    {
        SentCount++;
        return Task.FromResult(true);
    }
}

/// <summary>
/// Stands in for a real HTTP notification gateway: completes on a
/// thread-pool thread, the way an awaited HTTP call would, so the deferred
/// exit shows the async completion joining the same trace.
/// </summary>
public sealed class SimulatedExternalNotificationService : INotificationService
{
    public async Task<bool> NotifyOrderPlaced(string customerId, string orderId)
    {
        await Task.Yield();
        return customerId.Length > 0 && orderId.Length > 0;
    }
}

/// <summary>
/// Decorator that succeeds until the <paramref name="failOnCall"/>th call,
/// then throws <see cref="ExternalServiceException"/> — a flaky dependency.
/// </summary>
public sealed class FlakyNotificationService(
    INotificationService inner, int failOnCall) : INotificationService
{
    private int _calls;

    public Task<bool> NotifyOrderPlaced(string customerId, string orderId)
    {
        var call = ++_calls;
        if (call >= failOnCall)
        {
            throw new ExternalServiceException(
                string.Create(CultureInfo.InvariantCulture, $"External notification service unavailable (call #{call})"));
        }

        return inner.NotifyOrderPlaced(customerId, orderId);
    }
}

/// <summary>Places an order: resolve customer, price it, reserve stock, charge.</summary>
public sealed class OrderService(
    ICustomerService customers,
    IProductCatalogService catalog,
    IInventoryService inventory,
    IPaymentService payments) : IOrderService
{
    private int _orderCounter;

    public OrderResult PlaceOrder(string customerId, string productId, int quantity)
    {
        var customer = customers.FindCustomer(customerId);
        var unitPrice = catalog.LookupPrice(productId);
        var total = unitPrice * quantity;
        inventory.Reserve(productId, quantity);
        var payment = payments.Charge(customerId, total, $"tok_{customer.Id}");
        var orderId = string.Create(
            CultureInfo.InvariantCulture, $"ORD-{++_orderCounter:D5}");
        return new OrderResult(orderId, payment.TransactionId, total, quantity);
    }
}
