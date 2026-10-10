// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using NarrativeTrace.AspNetCore;
using NarrativeTrace.Proxy;

namespace Example.Orders;

/// <summary>
/// The framework table's ASP.NET Core wiring, compiled here and exercised by
/// <c>AspNetCoreWiringTests</c>. The doctor's <c>config.registration-pair</c> fix prints the
/// <c>wiring</c> region verbatim (carried by <c>wiring-snippets.md</c>), so these are the lines a
/// project adds — never retyped anywhere.
/// </summary>
public static class AspNetCoreWiring
{
    /// <summary>Applies the wiring to <paramref name="builder"/> and returns the built app.</summary>
    public static WebApplication Build(WebApplicationBuilder builder)
    {
        // snippet:begin wiring
        builder.Services.AddNarrativeTrace(builder.Configuration);

        var app = builder.Build();
        app.UseNarrativeTrace(); // early: it wraps the whole request

        // Wrap a service against the request's own context, so its calls nest into the request's trace.
        app.MapGet("/orders/{customerId}", (string customerId, HttpContext http) =>
            NarrativeTraceProxy.Create<IOrderService>(new OrderService(), http.GetNarrativeContext())
                .PlaceOrder(customerId));
        // snippet:end wiring
        return app;
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
