// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using Contoso.Checkout;

namespace Billing;

/// <summary>Checks a customer's order out.</summary>
public interface ICheckoutService
{
    /// <summary>Checks the order out and returns the id of the invoice it paid.</summary>
    string Checkout(Order order);
}

/// <summary>Sends a customer a short message.</summary>
public interface INotificationService
{
    /// <summary>Sends <paramref name="message"/> to the customer.</summary>
    void Send(string customerId, string message);
}

/// <summary>Records what happened, for the auditors.</summary>
public interface IAuditLog
{
    /// <summary>Records one entry.</summary>
    void Record(string entry);
}

public sealed class CheckoutService(CheckoutPipeline pipeline) : ICheckoutService
{
    public string Checkout(Order order) => pipeline.Run(order);
}

public sealed class InvoiceService : IInvoiceService
{
    public string IssueInvoice(string orderId, long amountCents) => "INV-" + orderId;
}

public sealed class CardGateway : IPaymentGateway
{
    public List<string> Confirmed { get; } = [];

    public string Authorize(string customerId, long amountCents) => "AUTH-" + customerId;

    public void Confirm(string authorizationId) => Confirmed.Add(authorizationId);
}

public sealed class NotificationService : INotificationService
{
    public List<(string CustomerId, string Message)> Sent { get; } = [];

    public void Send(string customerId, string message) => Sent.Add((customerId, message));
}

public sealed class AuditLog : IAuditLog
{
    public List<string> Entries { get; } = [];

    public void Record(string entry) => Entries.Add(entry);
}

/// <summary>Records every completed checkout in the audit log.</summary>
public sealed class AuditListener(IAuditLog audit) : ICheckoutListener
{
    public void OnCheckout(string invoiceId, Order order) => audit.Record("checked out " + order.OrderId + " as " + invoiceId);
}
