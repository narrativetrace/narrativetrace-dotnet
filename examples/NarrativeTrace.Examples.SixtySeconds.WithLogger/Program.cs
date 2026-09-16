// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using Microsoft.Extensions.Logging; // new: send the trace to an ILogger, not only the console
using Microsoft.Extensions.Logging.Console; // new: for LoggerColorBehavior below
using NarrativeTrace.Core;
using NarrativeTrace.Logging; // new: TraceLogExporter replays a captured trace onto an ILogger
using NarrativeTrace.Proxy;
using NarrativeTrace.Runtime;

// A fixed W3C traceparent, seeded through NarrativeTraceConfig so this page's embedded output
// always names the same trace. A real run adopts nothing here (or a real inbound request header,
// via NarrativeTraceMiddleware) and gets a fresh, randomly generated trace id every time.
const string DemoTraceparent = "00-a1b2c3d4a1b2c3d4a1b2c3d4a1b2c3d4-a1b2c3d4a1b2c3d4-01";

var context = new SyncNarrativeContext(
    new NarrativeTraceConfig(initialTraceparent: Traceparent.Parse(DemoTraceparent)));
var orders = NarrativeTraceProxy.Create<IOrderService>(new OrderService(), context);

orders.PlaceOrder("cust-1", "book-123", 2);

var tree = context.CaptureTrace(); // new: capture once, render it to both destinations below
Console.WriteLine(IndentedTextRenderer.Render(tree));

using var loggerFactory = LoggerFactory.Create(builder => // new: a second destination for the same trace
    builder.AddSimpleConsole(options => options.ColorBehavior = LoggerColorBehavior.Disabled)); // new: disable ANSI color codes so captured/piped output stays plain text
TraceLogExporter.ExportToLogger(tree, loggerFactory.CreateLogger("NarrativeTrace")); // new: replay the same tree onto the logger

public interface IOrderService
{
    string PlaceOrder(string customerId, string productId, int quantity);
}

public sealed class OrderService : IOrderService
{
    public string PlaceOrder(string customerId, string productId, int quantity)
        => $"confirmed:{customerId}:{productId}:{quantity}";
}
