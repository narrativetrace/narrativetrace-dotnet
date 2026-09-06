// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using Microsoft.Extensions.DependencyInjection;
using NarrativeTrace.Core;
using NarrativeTrace.Examples.Common;
using NarrativeTrace.Proxy;

namespace NarrativeTrace.Examples.ECommerce;

/// <summary>
/// Tutorial runner for the e-commerce example — a guided tour, not a
/// production <c>Main</c>. Six scenarios show what NarrativeTrace looks like
/// in a realistic service graph: the happy path fanning out into async work,
/// a payment failure whose trace exposes an inventory-release bug, a flaky
/// external dependency, two validation failures, and explicit async capture.
/// </summary>
/// <remarks>
/// Read the indented tree first, then compare the prose and diagram
/// renderings of the same scenario; open <see cref="ECommerceExample"/> to
/// see how the container wires the tracing, and <see cref="OrderService"/>
/// to connect the orchestration code to the trace.
/// </remarks>
public static class ECommerceDemo
{
    /// <summary>Runs all six scenarios against <paramref name="run"/>.</summary>
    public static async Task RunAsync(DemoRun run)
    {
        ArgumentNullException.ThrowIfNull(run);
        using var container = ECommerceExample.BuildContainer(run.Context);
        var orders = container.GetRequiredService<IOrderService>();
        var notifications = container.GetRequiredService<INotificationService>();
        var catalog = container.GetRequiredService<IProductCatalogService>();

        await RunSuccessfulOrder(run, orders, notifications).ConfigureAwait(false);
        RunPaymentFailure(run, orders);
        RunFlakyService(run);
        RunUnknownCustomer(run, orders);
        RunOutOfStock(run, orders);
        await RunExplicitAsync(run, catalog).ConfigureAwait(false);
    }

    private static async Task RunSuccessfulOrder(
        DemoRun run, IOrderService orders, INotificationService notifications)
    {
        run.BeginScenario("Scenario 1: Successful Order + Async Notification");
        var result = orders.PlaceOrder("cust-1", "book-123", 2);
        await notifications.NotifyOrderPlaced("cust-1", result.OrderId).ConfigureAwait(false);

        var trace = run.Context.CaptureTrace();
        run.TraceTree(trace);
        run.Prose(trace);
        run.Mermaid(trace);
    }

    private static void RunPaymentFailure(DemoRun run, IOrderService orders)
    {
        run.BeginScenario("Scenario 2: Payment Failure — Inventory Leak Bug");
        Expect<PaymentDeclinedException>(() => orders.PlaceOrder("cust-1", "mechanical-keyboard", 3));

        var trace = run.Context.CaptureTrace();
        run.TraceTree(trace);
        run.Info("\n  ^ Notice: IInventoryService.Reserve was called but IInventoryService.Release is missing from the trace.");
        run.Prose(trace);
        run.Mermaid(trace);
    }

    private static void RunFlakyService(DemoRun run)
    {
        run.BeginScenario("Scenario 3: Flaky External Service");
        var flaky = NarrativeTraceProxy.Create<INotificationService>(
            new FlakyNotificationService(new SimulatedExternalNotificationService(), failOnCall: 2),
            run.Context);
        flaky.NotifyOrderPlaced("cust-1", "ORD-00001").GetAwaiter().GetResult();
        Expect<ExternalServiceException>(() => flaky.NotifyOrderPlaced("cust-1", "ORD-00002"));

        var trace = run.Context.CaptureTrace();
        run.TraceTree(trace);
        run.Prose(trace);
    }

    private static void RunUnknownCustomer(DemoRun run, IOrderService orders)
    {
        run.BeginScenario("Scenario 4: Unknown Customer");
        Expect<ArgumentException>(() => orders.PlaceOrder("cust-unknown", "book-123", 1));

        var trace = run.Context.CaptureTrace();
        run.TraceTree(trace);
        run.Prose(trace);
    }

    private static void RunOutOfStock(DemoRun run, IOrderService orders)
    {
        run.BeginScenario("Scenario 5: Out of Stock");
        Expect<InvalidOperationException>(() => orders.PlaceOrder("cust-1", "usb-hub", 9999));

        var trace = run.Context.CaptureTrace();
        run.TraceTree(trace);
        run.Prose(trace);
        run.PlantUml(trace);
    }

    private static async Task RunExplicitAsync(DemoRun run, IProductCatalogService catalog)
    {
        run.BeginScenario("Scenario 6: Explicit Async Trace Capture");
        run.Info("  Traces are context-scoped, not thread-scoped: the context flows with AsyncLocal, so a");
        run.Info("  worker thread sharing it adds to the same story. Fork-join and fire-and-forget groups give");
        run.Info("  concurrent work isolated child contexts that are merged back into the parent on join.\n");

        catalog.LookupPrice("book-123");
        await Task.Run(() => catalog.LookupPrice("mechanical-keyboard")).ConfigureAwait(false);
        var shared = run.Context.CaptureTrace();
        run.Info("Shared context — main thread and worker thread, one story:\n"
            + IndentedTextRenderer.Render(shared));

        run.Context.Reset();
        var concurrent = await ConcurrencyScenario.RunAsync(run.Context).ConfigureAwait(false);
        run.Info("\nFork-join pricing + fire-and-forget notification, merged into the parent:\n"
            + IndentedTextRenderer.Render(concurrent.Trace));

        // This scenario renders its trees through Info rather than TraceTree,
        // so the launcher's translated capture point is not on their path;
        // they are handed over explicitly, under sub-titles of their own.
        DemoTraces.Capture(
            "Scenario 6: Explicit Async Trace Capture (shared context)", shared);
        DemoTraces.Capture(
            "Scenario 6: Explicit Async Trace Capture (fork-join and fire-and-forget)",
            concurrent.Trace);
    }

    /// <summary>Runs an action whose failure is the point of the scenario.</summary>
    private static void Expect<TException>(Action action)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            // expected: the trace records it
        }
    }
}
