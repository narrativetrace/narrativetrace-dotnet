// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace Checkout;

public interface IInventoryService
{
    void Reserve(string sku, int quantity);

    void Release(string sku, int quantity);
}

public interface IPaymentService
{
    string Charge(string customerId, int amountCents, string card);
}

public interface IOrderRepository
{
    void Save(Order order);
}

public interface IFraudCheckService
{
    bool Approve(string customerId, int amountCents);
}

// snippet:begin accounts
/// <summary>Signs a customer in before the checkout charges them. The parameter is named <c>password</c>.</summary>
public interface ICustomerAccounts
{
    /// <returns>The session id the checkout carries for the rest of the request.</returns>
    string SignIn(string customerId, string password);
}
// snippet:end accounts

public interface IOrderService
{
    string PlaceOrder(string customerId, string sku, int quantity, string cardNumber);
}

public sealed record Order(string OrderId, string CustomerId, string Sku, int Quantity, int AmountCents);

public sealed class PaymentDeclinedException(string message) : Exception(message);
