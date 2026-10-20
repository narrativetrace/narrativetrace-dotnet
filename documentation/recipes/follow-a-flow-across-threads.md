# Follow a flow across threads

**English** | [Español](../es/recetas/sigue-un-flujo-entre-hilos.md) | [Português](../pt-BR/receitas/acompanhe-um-fluxo-entre-threads.md) | [简体中文](../zh-CN/实用方案/跨线程追踪一个流程.md)

## The problem

> The checkout hands the slow parts to other threads. The trace stops at the
> submit and I cannot see what the background work did.

A thread knows nothing about the span another thread is in, so handing work
to a worker hands over the work and nothing else. The trace has to be handed
over too — explicitly, at submission time. Below is one run that does, and one
that does not.

## Minimal setup

Nothing beyond the [install](../guides/installation.md): `ForkJoinGroup` and
`FireAndForgetGroup` are in `NarrativeTrace.Runtime`, beside the contexts.
Which one depends on what the caller does next:

| The launching call | The helper | The ending step |
|---|---|---|
| Waits for the background work (this page) | `ForkJoinGroup` | `await group.JoinAsync()` — it grafts what the branches recorded under the launching span |
| Returns without waiting | `FireAndForgetGroup` | None. The children are a separate record, read through `ChildRoots()` |

A branch is handed an **isolated child context** and must trace through that
argument: a collaborator wrapped with `NarrativeTraceProxy.Create(service, branch)`
inside the branch is what records the work. If your code stays on one logical
flow across `await`, `AsyncNarrativeContext.RunAsync` carries the ambient trace
for you and you write neither of the above — the
[Feature Guide](../feature-guide.md) is the row-by-row version.

The log lines use the same logger as
[Follow one request across services](follow-one-request-across-services.md),
with the raw trace id in brackets after the category — on this page the empty
bracket is as telling as the filled ones.

## The scenario

**The checkout, with the two slow steps off its own thread.** The handover is
`group.Fork(branch => …)` around each task and `await group.JoinAsync()` after
both are started. The inventory and the fraud check are passed in unwrapped, so
the class decides which context each branch traces through:

<!-- snippet: recipes/checkout/Checkout/ParallelCheckout.cs -->
```csharp
using NarrativeTrace.Core;
using NarrativeTrace.Proxy;
using NarrativeTrace.Runtime;

namespace Checkout;

/// <summary>
/// The same checkout with the two slow steps off the caller's thread: reserve the stock and run the
/// fraud check as two branches of a <see cref="ForkJoinGroup"/>, wait for both, then charge and save.
/// A branch runs on a pool thread that knows nothing about the caller's trace, so each branch is
/// handed an isolated child context of the group and traces through it; <c>JoinAsync</c> grafts what
/// the branches recorded back under the span that launched them. Skip the join and the branches stay
/// out of the caller's trace. The inventory and the fraud check are passed unwrapped for that reason:
/// this class decides which context each call is traced through.
/// </summary>
public sealed class ParallelCheckout(
    INarrativeContext context,
    IInventoryService inventory,
    IFraudCheckService fraud,
    IPaymentService payments,
    IOrderRepository orders) : IOrderService
{
    private const int UnitPriceCents = 3999;

    public string PlaceOrder(string customerId, string sku, int quantity, string cardNumber)
    {
        var amountCents = UnitPriceCents * quantity;
        var group = ForkJoinGroup.Create(context);
        var reserving = group.Fork(branch =>
        {
            NarrativeTraceProxy.Create(inventory, branch).Reserve(sku, quantity);
            return true;
        });
        var scoring = group.Fork(branch => NarrativeTraceProxy.Create(fraud, branch).Approve(customerId, amountCents));
        group.JoinAsync().GetAwaiter().GetResult();

        if (!scoring.Result)
        {
            NarrativeTraceProxy.Create(inventory, context).Release(sku, quantity);
            throw new PaymentDeclinedException("the fraud check refused the order");
        }

        _ = reserving.Result;
        payments.Charge(customerId, amountCents, cardNumber);
        var orderId = $"ORD-{customerId}-{sku}-{quantity}";
        orders.Save(new Order(orderId, customerId, sku, quantity, amountCents));
        return orderId;
    }
}
```
<!-- /snippet -->

**The run — and the same work without the handover.** The first test runs that
checkout. The second hands a traced call to a worker thread with no group,
which is what the code looked like before the handover was added: the worker
is a thread that was already running, and it is held until the launching call
has returned, so the order of events is the same on every run:

<!-- snippet: recipes/checkout/Checkout.Tests/AcrossThreadsRecipeTests.cs -->
```csharp
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
```
<!-- /snippet -->

## The actual output

**The log of the run that keeps the trace:**

<!-- snippet: artifacts/recipes/follow-a-flow-across-threads/checkout.log -->
```text
TRACE [NarrativeTrace] [c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7] - → IOrderService.PlaceOrder(customerId: "C-1234", sku: "SKU-KB", quantity: 2, cardNumber: [REDACTED])
TRACE [NarrativeTrace] [] - ⑂ fork group created [groupId: fork-1]
TRACE [NarrativeTrace] [] - Graft IInventoryService.Reserve
TRACE [NarrativeTrace] [] - Graft IFraudCheckService.Approve
TRACE [NarrativeTrace] [] - ⑃ fork joined [groupId: fork-1, members: 2]
TRACE [NarrativeTrace] [c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7] - → IPaymentService.Charge(customerId: "C-1234", amountCents: 7998, card: [REDACTED])
TRACE [NarrativeTrace] [c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7] - ← returned: "PAY-C-1234-7998"
TRACE [NarrativeTrace] [c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7] - → IOrderRepository.Save(order: Order(OrderId: "ORD-C-1234-SKU-KB-2", CustomerId: "C-1234", Sku: "SKU-KB", Quantity: 2, AmountCents: 7998))
TRACE [NarrativeTrace] [c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7] - ← completed
TRACE [NarrativeTrace] [c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7] - ← returned: "ORD-C-1234-SKU-KB-2"
```
<!-- /snippet -->

**The structural trace of that run:**

<!-- snippet: artifacts/recipes/follow-a-flow-across-threads/with-handover.nt -->
```text
scenario: The background tasks stay in the customers trace

#1 - IOrderService.PlaceOrder(customerId, sku, quantity, cardNumber) → value
  ~ fork [2]
    #1.1 - IFraudCheckService.Approve(customerId, amountCents) → value
    #1.2 - IInventoryService.Reserve(sku, quantity)
  #1.3 - IPaymentService.Charge(customerId, amountCents, card) → value
  #1.4 - IOrderRepository.Save(order)
```
<!-- /snippet -->

**And the structural trace of the run without the handover:**

<!-- snippet: artifacts/recipes/follow-a-flow-across-threads/without-handover.nt -->
```text
scenario: A task handed to a worker without the handover

#1 - IOrderService.PlaceOrder(customerId, sku, quantity, cardNumber) → value
#2 - IInventoryService.Reserve(sku, quantity)
```
<!-- /snippet -->

The Markdown artifact of the first run carries three things more — the thread
each branch ran on, the join's wall time, and a "sequential async" hint where
the branches did not overlap — and is not embedded here because the thread ids
and the timings differ on every run.

## How to read it

- **The handover is visible, twice.**
  `⑂ fork group created [groupId: fork-1]` is the group being created, before
  either branch starts; `⑃ fork joined [groupId: fork-1, members: 2]` is
  `JoinAsync` reporting what came back. Between them is background work that
  belongs to this checkout. Those lines carry an empty trace id — the last
  bracket is `[]` — because they are not span events: they are the group's
  own lifecycle, published by the context rather than by a traced call.
- **The two `Graft` lines are the branches coming home.** A branch traces
  through its isolated child context, so it writes no log line of its own
  while it runs; at the join, each branch's recorded call is grafted under the
  launching span and the bridge logs that — `Graft IInventoryService.Reserve`,
  `Graft IFraudCheckService.Approve` — before `Charge` starts. Read the log as
  the order things *happened* in: the group, the grafts, the join, then the
  sequential calls that depend on them.
- **The children are in the customer's trace, in the tree, once.** The
  structural trace holds the two branches inside `~ fork [2]`, `#1.1` and
  `#1.2`, under `#1`, and then `Charge` and `Save` as `#1.3` and `#1.4`: the
  fork sits where the join ran, ahead of the calls that depend on it, so the
  tree reads in the order things happened. The members of a group are listed
  in a fixed order, not the order the threads finished in, which is what
  keeps the file byte-stable.
- **What the test asserts that a log cannot show.** A span id is generated per
  span and differs on every run, and a thread id likewise, so
  `AssertTheChildrenCameHome` asserts instead what they would have shown:
  each branch carries the root's trace id and names `#1` as its parent span,
  each branch's recorded thread is not the caller's, and the two share one
  group id. That assertion is this page's proof. The parent is set when the
  branch's context is created, so a consumer that follows parent span ids —
  the live OpenTelemetry listener, for one — sees each branch under `#1` from
  its first event, not as a root.
- **How the second run differs.** The same traced call, handed to a worker
  with no group, did happen: the stock is reserved. It is not under the
  checkout. The structural trace has two roots — `#1 PlaceOrder` with no
  children, and `#2 Reserve` beside it — because the worker ran the call
  after the launching span had closed, and nothing joined it back. The second
  test asserts exactly that: `PlaceOrder` has no children and `Reserve` is a
  root with no concurrency group. The trace id is the same, because this
  scenario shares one context; what is missing is the *place* in the story.
- **Why the worker is held.** Release the worker while `PlaceOrder` is still
  open and the call lands under it by accident of timing, and a second worker
  doing the same on the same shared context interleaves its spans with the
  first's. The group exists so that neither is left to timing: each branch
  gets its own context, and the join is the one place they come back.
- **Why one worker.** The log and the structural trace are real runs' bytes,
  and two threads interleave differently every time. The handover code is
  the same for any number of branches; what more threads add is
  nondeterministic output, not a second mechanism.

## What is not captured

- **Work started without the handover is not in the launching span.** That is
  the second half of this page: no group, no join, no place under `#1`.
  Nothing warns you, because from the library's point of view a traced call on
  another thread is an ordinary call.
- **A branch that does not trace through its own context is not isolated.**
  Capturing through the outer context from a branch defeats the isolation and
  interleaves spans; the argument the branch is handed is the one to use.
- **A group never joined collects nothing into the caller's trace.** The
  branches ran, and then nothing grafted them: the stock is reserved and the
  caller's tree does not say so. [Troubleshooting](../troubleshooting.md)
  has the entry.
- **A branch that throws does not come home.** The join surfaces the failure to
  the caller and the failed branch's spans are not grafted; observing it is
  the caller's `try`/`catch`.
- **Work that outlives the call is a different helper.** `FireAndForgetGroup`
  has no join by design — the launching method has already returned, so there
  is nobody left to merge into — and swallows a launched task's failure.
- **Interleaving.** With a real pool the branches finish in whatever order
  the threads get scheduled, which is why a log is read by trace id and not by
  position. The grafted structure is unaffected: it follows the group, not the
  clock.

**The skill for this:** `narrativetrace-verify` — "runs async" and "crosses
threads" are two of the cases it exists for, and it reads the structural trace
against the intent before anything is called done.
