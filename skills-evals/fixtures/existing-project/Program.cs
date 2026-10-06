// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
// A project that already exists — one real service boundary, no NarrativeTrace anywhere.
// The init prompt's "existing project" branch tells the agent to trace THIS boundary.

var service = new OrderService();
Console.WriteLine(service.PlaceOrder("cust-1", "book-123", 2));

/// <summary>A plain, untraced service — the boundary the agent is asked to trace.</summary>
public sealed class OrderService
{
    public string PlaceOrder(string customerId, string productId, int quantity)
        => $"confirmed:{customerId}:{productId}:{quantity}";
}
