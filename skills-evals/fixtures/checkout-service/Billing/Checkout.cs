// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using Contoso.Checkout;

namespace Billing;

/// <summary>Wraps a service as it is wired — tests pass one that traces it; production passes none.</summary>
public interface IServiceWrapper
{
    T Wrap<T>(T service)
        where T : class;
}

/// <summary>Wires every service as production does.</summary>
public sealed class NoWrapping : IServiceWrapper
{
    public T Wrap<T>(T service)
        where T : class => service;
}

/// <summary>The checkout, wired: the service to call, and the collaborators behind it.</summary>
public sealed record CheckoutApp(
    ICheckoutService Checkout, CardGateway Payments, NotificationService Notifications, AuditLog Audit);

/// <summary>The composition root: how the checkout and its collaborators are put together.</summary>
public static class Checkout
{
    public static CheckoutApp Compose(IServiceWrapper wrap)
    {
        var payments = new CardGateway();
        var notifications = new NotificationService();
        var audit = new AuditLog();
        var auditLog = wrap.Wrap<IAuditLog>(audit);
        var notifier = wrap.Wrap<INotificationService>(notifications);

        // Side effects of a checkout (the audit entry, emails, analytics) are ICheckoutListeners:
        // the pipeline calls each one once the checkout is complete.
        ICheckoutListener[] listeners = [wrap.Wrap<ICheckoutListener>(new AuditListener(auditLog))];

        var pipeline = new CheckoutPipeline(
            wrap.Wrap<IInvoiceService>(new InvoiceService()), wrap.Wrap<IPaymentGateway>(payments), listeners);
        return new CheckoutApp(wrap.Wrap<ICheckoutService>(new CheckoutService(pipeline)), payments, notifications, audit);
    }
}
