// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace Billing;

public sealed class CheckoutService(
    IInvoiceService invoices, ICustomerDirectory customers, ICurrencyConverter converter, IPaymentGateway payments)
    : ICheckoutService
{
    public string Checkout(Order order)
    {
        var invoiceId = invoices.IssueInvoice(order.OrderId, order.AmountCents);
        var currency = customers.CardCurrencyOf(order.CustomerId);
        var amount = converter.Convert(order.AmountCents, currency);
        var authorization = payments.Authorize(order.CustomerId, amount, currency);
        payments.Confirm(authorization);
        return invoiceId;
    }
}

public sealed class InvoiceService : IInvoiceService
{
    public string IssueInvoice(string orderId, long euroCents) => "INV-" + orderId;
}

public sealed class CustomerDirectory : ICustomerDirectory
{
    private static readonly Dictionary<string, string> Cards = new()
    {
        ["C-2041"] = "CHF",
        ["C-3307"] = "GBP",
    };

    public string CardCurrencyOf(string customerId) => Cards.GetValueOrDefault(customerId, "EUR");
}

public sealed class DailyRates : IExchangeRates
{
    private static readonly Dictionary<string, decimal> Rates = new()
    {
        ["EUR"] = 1m,
        ["CHF"] = 0.93m,
        ["GBP"] = 0.86m,
    };

    public decimal RateFor(string currency) =>
        Rates.TryGetValue(currency, out var rate) ? rate : throw new ArgumentException("no rate for " + currency, nameof(currency));
}

public sealed class CardGateway : IPaymentGateway
{
    public List<(string CustomerId, long Amount, string Currency)> Charged { get; } = [];

    public string Authorize(string customerId, long amount, string currency)
    {
        Charged.Add((customerId, amount, currency));
        return "AUTH-" + customerId;
    }

    public void Confirm(string authorizationId)
    {
    }
}
