// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using Microsoft.Extensions.DependencyInjection;
using NarrativeTrace.Core;
using NarrativeTrace.DependencyInjection;
using NarrativeTrace.Proxy;

namespace NarrativeTrace.Examples.ECommerce;

/// <summary>
/// Wires the e-commerce services together, each behind a NarrativeTrace proxy
/// that shares one context — so a call from <see cref="OrderService"/> into its
/// collaborators is recorded as a nested child in the same trace.
/// </summary>
public static class ECommerceExample
{
    /// <summary>The namespace <c>AddNarrativeTracing</c> auto-wraps.</summary>
    public const string ServicesNamespace = "NarrativeTrace.Examples.ECommerce";

    /// <summary>
    /// Builds a fully-traced <see cref="IOrderService"/> against an in-memory
    /// backend with the given stock level and payment approval limit, wrapping
    /// each service by hand with <see cref="NarrativeTraceProxy"/>.
    /// </summary>
    public static IOrderService BuildTracedOrderService(
        INarrativeContext context, int stock, decimal approvalLimit)
    {
        var customers = Trace<ICustomerService>(new InMemoryCustomerService(), context);
        var catalog = Trace<IProductCatalogService>(
            new InMemoryProductCatalogService(), context);
        var inventory = Trace<IInventoryService>(
            new InMemoryInventoryService(new Dictionary<string, int> { ["book-123"] = stock }),
            context);
        var payments = Trace<IPaymentService>(
            new InMemoryPaymentService(approvalLimit), context);

        var orderService = new OrderService(customers, catalog, inventory, payments);
        return Trace<IOrderService>(orderService, context);
    }

    /// <summary>
    /// The container the tutorial runs on: ordinary registrations, then
    /// <c>AddNarrativeTracing</c> wraps every interface service in
    /// <see cref="ServicesNamespace"/> in a tracing proxy — no service knows
    /// it is traced. Registering <paramref name="context"/> first makes the
    /// container use it (and its live event stream) instead of creating its own.
    /// </summary>
    public static ServiceProvider BuildContainer(INarrativeContext context)
    {
        var services = new ServiceCollection();
        services.AddSingleton(context);
        services.AddSingleton<ICustomerService, InMemoryCustomerService>();
        services.AddSingleton<IProductCatalogService, InMemoryProductCatalogService>();
        services.AddSingleton<IInventoryService>(new InMemoryInventoryService(
            new Dictionary<string, int> { ["book-123"] = 5, ["mechanical-keyboard"] = 3, ["usb-hub"] = 0 }));
        services.AddSingleton<IPaymentService>(new InMemoryPaymentService(approvalLimit: 100m));
        services.AddSingleton<INotificationService, SimulatedExternalNotificationService>();
        services.AddSingleton<IOrderService, OrderService>();
        services.AddNarrativeTracing(o => o.Namespaces(ServicesNamespace));
        return services.BuildServiceProvider();
    }

    private static T Trace<T>(T target, INarrativeContext context)
        where T : class
    {
        return NarrativeTraceProxy.Create(target, context);
    }
}
