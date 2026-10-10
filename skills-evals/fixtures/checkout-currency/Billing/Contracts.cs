// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace Billing;

/// <summary>A customer's order at checkout; its amount is in euro cents.</summary>
public sealed record Order(string OrderId, string CustomerId, long AmountCents);

/// <summary>Checks a customer's order out, charging the card in its own currency.</summary>
public interface ICheckoutService
{
    /// <summary>Checks the order out and returns the id of the invoice it paid.</summary>
    string Checkout(Order order);
}

/// <summary>Issues the invoice a checkout pays, in euro.</summary>
public interface IInvoiceService
{
    string IssueInvoice(string orderId, long euroCents);
}

/// <summary>Knows each customer's card.</summary>
public interface ICustomerDirectory
{
    /// <summary>The ISO currency the customer's card is charged in.</summary>
    string CardCurrencyOf(string customerId);
}

/// <summary>Today's exchange rates from the euro.</summary>
public interface IExchangeRates
{
    /// <summary>How many units of <paramref name="currency"/> one euro buys today.</summary>
    decimal RateFor(string currency);
}

/// <summary>Converts a euro amount into the card's currency.</summary>
public interface ICurrencyConverter
{
    /// <summary>The amount, in minor units of <paramref name="currency"/>, that <paramref name="euroCents"/> is worth.</summary>
    long Convert(long euroCents, string currency);
}

/// <summary>Takes a card payment in two steps: authorize, then confirm.</summary>
public interface IPaymentGateway
{
    string Authorize(string customerId, long amount, string currency);

    void Confirm(string authorizationId);
}
