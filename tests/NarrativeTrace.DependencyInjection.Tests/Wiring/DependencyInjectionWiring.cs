// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using Microsoft.Extensions.DependencyInjection;
using NarrativeTrace.DependencyInjection;

namespace Example.Orders;

/// <summary>
/// The framework table's dependency-injection wiring, compiled here and exercised by
/// <c>DependencyInjectionWiringTests</c>. The doctor's <c>config.di-namespaces</c> fix prints the
/// <c>wiring</c> region verbatim (carried by <c>wiring-snippets.md</c>).
/// </summary>
public static class DependencyInjectionWiring
{
    /// <summary>Registers the fixture's service and the auto-wrap, in that order.</summary>
    public static IServiceCollection Register(IServiceCollection services)
    {
        // snippet:begin wiring
        services.AddScoped<IOrderService, OrderService>();

        // Last: it wraps the interface registrations made before it whose implementation lives in a
        // listed namespace (dot-boundary match) — list this project's own service namespaces.
        services.AddNarrativeTracing(options => options.Namespaces("Example.Orders"));
        // snippet:end wiring
        return services;
    }
}

/// <summary>The fixture's one service boundary.</summary>
public interface IOrderService
{
    /// <summary>Places an order for <paramref name="customerId"/>.</summary>
    string PlaceOrder(string customerId);
}

/// <summary>The fixture's implementation of <see cref="IOrderService"/>.</summary>
public sealed class OrderService : IOrderService
{
    /// <inheritdoc />
    public string PlaceOrder(string customerId) => $"confirmed:{customerId}";
}
