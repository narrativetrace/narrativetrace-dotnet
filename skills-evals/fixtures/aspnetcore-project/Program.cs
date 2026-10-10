// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
// An ASP.NET Core app that already exists — one real service boundary behind one endpoint, no
// NarrativeTrace anywhere. The init prompt's "existing project" branch tells the agent to trace THIS
// boundary; the doctor it installs is what tells it how ASP.NET Core is wired.

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<IOrderService, OrderService>();

var app = builder.Build();

app.MapGet("/orders/{customerId}", (string customerId, IOrderService orders) =>
    orders.PlaceOrder(customerId, "book-123", 2));

app.Run();

/// <summary>The boundary the agent is asked to trace.</summary>
public interface IOrderService
{
    string PlaceOrder(string customerId, string productId, int quantity);
}

/// <summary>A plain, untraced implementation.</summary>
public sealed class OrderService : IOrderService
{
    public string PlaceOrder(string customerId, string productId, int quantity)
        => $"confirmed:{customerId}:{productId}:{quantity}";
}
