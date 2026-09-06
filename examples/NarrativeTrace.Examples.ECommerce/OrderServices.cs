// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core.Annotation;

namespace NarrativeTrace.Examples.ECommerce;

/// <summary>Resolves a customer by id.</summary>
public interface ICustomerService
{
    [OnError("Customer {customerId} not found", ExceptionType = typeof(ArgumentException))]
    Customer FindCustomer(string customerId);
}

/// <summary>Looks up product pricing.</summary>
public interface IProductCatalogService
{
    decimal LookupPrice(string productId);
}

/// <summary>Reserves and releases stock for an order.</summary>
public interface IInventoryService
{
    [OnError("Insufficient stock for {productId}, requested {quantity}", ExceptionType = typeof(InvalidOperationException))]
    void Reserve(string productId, int quantity);

    void Release(string productId, int quantity);
}

/// <summary>Charges a customer's payment method; the card token never enters the trace.</summary>
public interface IPaymentService
{
    [OnError("Payment declined for customer {customerId}, amount was {amount}", ExceptionType = typeof(PaymentDeclinedException))]
    PaymentConfirmation Charge(string customerId, decimal amount, [NotTraced] string cardToken);
}

/// <summary>Orchestrates placing an order across the other services.</summary>
public interface IOrderService
{
    [Narrated("Placing order of {quantity} {productId} for customer {customerId}")]
    OrderResult PlaceOrder(string customerId, string productId, int quantity);
}

/// <summary>Sends the customer a confirmation once an order is placed — asynchronously.</summary>
public interface INotificationService
{
    [OnError("Failed to notify customer {customerId} about order {orderId}", ExceptionType = typeof(ExternalServiceException))]
    Task<bool> NotifyOrderPlaced(string customerId, string orderId);
}
