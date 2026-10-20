// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Collections.Concurrent;
using Checkout;
using Microsoft.Extensions.Logging;
using NarrativeTrace.Core;
using NarrativeTrace.Logging;
using NarrativeTrace.Proxy;
using NarrativeTrace.Runtime;
using Xunit;

namespace Checkout.Tests;

/// <summary>
/// Proves recipes/follow-a-flow-across-threads.md: the two branches the checkout hands to a
/// <see cref="ForkJoinGroup"/> come home inside the caller's trace — same trace id, grafted under the
/// span that launched them, recorded on other threads — and a task handed to a worker without the
/// group is not under that span at all (rule 8, docs as tests).
/// </summary>
public sealed class AcrossThreadsRecipeTests
{
    private const string Slug = "follow-a-flow-across-threads";

    // A fixed W3C traceparent, so the page's embedded artifacts name the same trace every run.
    private const string DemoTraceparent = "00-c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7-c4e1d0a7c4e1d0a7-01";

    [Fact]
    public void The_background_tasks_stay_in_the_customers_trace()
    {
        var log = new StringWriter { NewLine = "\n" };
        var context = CallerContext(log);
        var stock = new InMemoryInventory("SKU-KB", 5);
        var repository = new InMemoryOrderRepository();
        var checkout = ParallelCheckoutOver(context, stock, repository);

        var orderId = checkout.PlaceOrder("C-1234", "SKU-KB", 2, "4242424242424242");

        Assert.Equal("ORD-C-1234-SKU-KB-2", orderId);
        Assert.Equal(3, stock.AvailableUnits("SKU-KB"));
        Assert.Single(repository.Saved);
        var trace = context.CaptureTrace();
        AssertTheChildrenCameHome(trace);
        CapturedOutput.Write(Slug, "checkout.log", log.ToString());
        CapturedOutput.Write(Slug, "with-handover.nt", StructuralTraceRenderer.RenderDocument(trace, "The background tasks stay in the customers trace"));
    }

    [Fact]
    public async Task A_task_handed_to_a_worker_without_the_handover_is_not_under_the_checkout()
    {
        var context = new SyncNarrativeContext(new NarrativeTraceConfig(initialTraceparent: Traceparent.Parse(DemoTraceparent)));
        var stock = new InMemoryInventory("SKU-KB", 5);
        var jobs = new BlockingCollection<Action>();
        var launched = new ManualResetEventSlim();
        var worker = StartWorker(jobs, launched);
        var checkout = NarrativeTraceProxy.Create<IOrderService>(
            new HandsOverWithoutTheGroup(NarrativeTraceProxy.Create<IInventoryService>(stock, context), jobs), context);

        checkout.PlaceOrder("C-1234", "SKU-KB", 2, "4242424242424242");
        launched.Set();
        await worker;

        Assert.Equal(3, stock.AvailableUnits("SKU-KB"));
        var trace = context.CaptureTrace();
        AssertTheWorkIsADetachedRoot(trace);
        CapturedOutput.Write(Slug, "without-handover.nt", StructuralTraceRenderer.RenderDocument(trace, "A task handed to a worker without the handover"));
    }

    private static LoggingNarrativeContext CallerContext(TextWriter log)
    {
        var loggerFactory = LoggerFactory.Create(builder => builder
            .SetMinimumLevel(LogLevel.Trace)
            .AddProvider(new CorrelationLoggerProvider(log)));
        return new LoggingNarrativeContext(
            new SyncNarrativeContext(new NarrativeTraceConfig(initialTraceparent: Traceparent.Parse(DemoTraceparent))),
            loggerFactory.CreateLogger("NarrativeTrace"));
    }

    /// <summary>The checkout with the stock and the fraud check unwrapped: the group decides what each branch traces through.</summary>
    private static IOrderService ParallelCheckoutOver(
        INarrativeContext context, InMemoryInventory stock, InMemoryOrderRepository repository)
    {
        var payments = NarrativeTraceProxy.Create<IPaymentService>(new CardPaymentService(), context);
        var orders = NarrativeTraceProxy.Create<IOrderRepository>(repository, context);
        return NarrativeTraceProxy.Create<IOrderService>(
            new ParallelCheckout(context, stock, new RuleBasedFraudCheck(), payments, orders), context);
    }

    /// <summary>
    /// The branches are in the caller's tree, in the customer's trace, name the launching span as their
    /// parent, and ran on other threads.
    /// </summary>
    private static void AssertTheChildrenCameHome(TraceTree trace)
    {
        var root = Assert.Single(trace.Roots);
        Assert.Equal(
            ["IInventoryService.Reserve", "IFraudCheckService.Approve", "IPaymentService.Charge", "IOrderRepository.Save"],
            root.Children.Select(n => $"{n.Signature.ClassName}.{n.Signature.MethodName}"));
        var branches = root.Children.Where(n => n.Concurrency is not null).ToList();
        Assert.Equal(2, branches.Count);
        Assert.All(branches, branch => Assert.Equal(root.SpanContext!.TraceId, branch.SpanContext!.TraceId));
        Assert.All(branches, branch => Assert.Equal(root.SpanContext!.SpanId, branch.SpanContext!.ParentSpanId));
        Assert.All(branches, branch => Assert.NotEqual(Environment.CurrentManagedThreadId, branch.Concurrency!.ThreadId));
        Assert.Single(branches.Select(branch => branch.Concurrency!.GroupId).Distinct());
    }

    /// <summary>The work happened, but as a second root: nothing joined it to the call that launched it.</summary>
    private static void AssertTheWorkIsADetachedRoot(TraceTree trace)
    {
        Assert.Equal(
            ["IOrderService.PlaceOrder", "IInventoryService.Reserve"],
            trace.Roots.Select(n => $"{n.Signature.ClassName}.{n.Signature.MethodName}"));
        Assert.Empty(trace.Roots[0].Children);
        Assert.Null(trace.Roots[1].Concurrency);
    }

    /// <summary>
    /// A long-lived worker thread, named so a failure names it. It takes one job, then waits for the
    /// launching call to have returned before running it: the order is the point of the page, and a
    /// race would make the output differ from run to run.
    /// </summary>
    private static Task StartWorker(BlockingCollection<Action> jobs, ManualResetEventSlim launched)
    {
        var finished = new TaskCompletionSource();
        var worker = new Thread(() =>
        {
            var job = jobs.Take();
            launched.Wait();
            job();
            finished.SetResult();
        })
        { IsBackground = true, Name = "checkout-worker-1" };
        worker.Start();
        return finished.Task;
    }

    /// <summary>The checkout as it looked before the handover was added: the reservation goes to a worker, the trace does not.</summary>
    private sealed class HandsOverWithoutTheGroup(IInventoryService inventory, BlockingCollection<Action> jobs) : IOrderService
    {
        public string PlaceOrder(string customerId, string sku, int quantity, string cardNumber)
        {
            jobs.Add(() => inventory.Reserve(sku, quantity));
            return $"ORD-{customerId}-{sku}-{quantity}";
        }
    }
}
