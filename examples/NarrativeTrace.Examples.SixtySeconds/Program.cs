// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using NarrativeTrace.Proxy;
using NarrativeTrace.Runtime;

// snippet:begin fixedTraceparent
// A fixed W3C traceparent, seeded through NarrativeTraceConfig so this page's embedded output
// always names the same trace. A real run adopts nothing here (or a real inbound request header,
// via NarrativeTraceMiddleware) and gets a fresh, randomly generated trace id every time.
const string DemoTraceparent = "00-a1b2c3d4a1b2c3d4a1b2c3d4a1b2c3d4-a1b2c3d4a1b2c3d4-01";
// snippet:end fixedTraceparent

var context = new SyncNarrativeContext(
    new NarrativeTraceConfig(initialTraceparent: Traceparent.Parse(DemoTraceparent)));
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
