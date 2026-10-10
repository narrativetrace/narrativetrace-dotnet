// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
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

/// <summary>The checkout, wired: the service to call, and the gateway that recorded the charge.</summary>
public sealed record CheckoutApp(ICheckoutService Checkout, CardGateway Payments);

/// <summary>The composition root: how the checkout and its collaborators are put together.</summary>
public static class Checkout
{
    public static CheckoutApp Compose(IServiceWrapper wrap)
    {
        var payments = new CardGateway();
        var converter = new RateTableConverter(wrap.Wrap<IExchangeRates>(new DailyRates()));
        var checkout = new CheckoutService(
            wrap.Wrap<IInvoiceService>(new InvoiceService()),
            wrap.Wrap<ICustomerDirectory>(new CustomerDirectory()),
            wrap.Wrap<ICurrencyConverter>(converter),
            wrap.Wrap<IPaymentGateway>(payments));
        return new CheckoutApp(wrap.Wrap<ICheckoutService>(checkout), payments);
    }
}
