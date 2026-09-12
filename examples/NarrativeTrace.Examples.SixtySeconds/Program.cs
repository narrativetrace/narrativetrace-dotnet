// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using NarrativeTrace.Proxy;
using NarrativeTrace.Runtime;

var context = new SyncNarrativeContext(new NarrativeTraceConfig());
var orders = NarrativeTraceProxy.Create<IOrderService>(new OrderService(), context);

orders.PlaceOrder("cust-1", "book-123", 2);

Console.WriteLine(IndentedTextRenderer.Render(context.CaptureTrace()));

public interface IOrderService
{
    string PlaceOrder(string customerId, string productId, int quantity);
}

public sealed class OrderService : IOrderService
{
    public string PlaceOrder(string customerId, string productId, int quantity)
        => $"confirmed:{customerId}:{productId}:{quantity}";
}
