// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using NarrativeTrace.Proxy;
using NarrativeTrace.Runtime;

namespace NarrativeTrace.Examples.ECommerce;

/// <summary>
/// The result of running <see cref="ConcurrencyScenario"/>: the captured trace
/// plus the group ids the scenario stitched its concurrent work under, so a
/// characterization test can assert the merged children and launcher node
/// carry them.
/// </summary>
public sealed record ConcurrencyScenarioResult(
    TraceTree Trace, string ForkGroupId, string NotificationGroupId);

/// <summary>
/// Demonstrates the shipped concurrency API on the e-commerce domain: pricing
/// and stock reservation run in parallel under a <see cref="ForkJoinGroup"/>,
/// then their traces are merged back into one narrative.
/// </summary>
public static class ConcurrencyScenario
{
    /// <summary>Runs the fork-join then fire-and-forget scenario.</summary>
    public static async Task<ConcurrencyScenarioResult> RunAsync(INarrativeContext context)
    {
        var handle = context.EnterMethod("OrderService", "PlaceOrder", []);

        var forkGroupId = await ForkPricingAndInventoryAsync(context);
        var notificationGroupId = await FireOffNotificationAsync(context);

        context.ExitMethodWithReturn("\"ORD-00001\"", handle);
        return new ConcurrencyScenarioResult(
            context.CaptureTrace(), forkGroupId, notificationGroupId);
    }

    private static async Task<string> ForkPricingAndInventoryAsync(INarrativeContext context)
    {
        var catalog = new InMemoryProductCatalogService();
        var inventory = new InMemoryInventoryService(
            new Dictionary<string, int> { ["book-123"] = 10 });

        var fork = ForkJoinGroup.Create(context);
        _ = fork.Fork(iso =>
            NarrativeTraceProxy.Create<IProductCatalogService>(catalog, iso)
                .LookupPrice("book-123"));
        _ = fork.Fork(iso =>
        {
            NarrativeTraceProxy.Create<IInventoryService>(inventory, iso)
                .Reserve("book-123", 2);
            return true;
        });
        await fork.JoinAsync();
        return fork.GroupId;
    }

    private static async Task<string> FireOffNotificationAsync(INarrativeContext context)
    {
        var notifier = new InMemoryNotificationService();
        var fanout = FireAndForgetGroup.Create(context, "OrderService");
        var sent = new TaskCompletionSource<bool>();
        fanout.Launch(iso =>
        {
            _ = NarrativeTraceProxy.Create<INotificationService>(notifier, iso)
                .NotifyOrderPlaced("cust-1", "ORD-00001");
            sent.SetResult(true);
        });
        await sent.Task;
        return fanout.GroupId;
    }
}
