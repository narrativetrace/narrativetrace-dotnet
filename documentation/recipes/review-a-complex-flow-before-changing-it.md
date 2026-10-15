# Review a complex flow before changing it

**English** | [Español](../es/recetas/revisa-un-flujo-complejo-antes-de-cambiarlo.md) | [Português](../pt-BR/receitas/revise-um-fluxo-complexo-antes-de-altera-lo.md) | [简体中文](../zh-CN/实用方案/修改复杂流程前先审查它.md)

## The problem

> Before you change the checkout, tell me what it actually does.

Reading the source tells you what the code *can* do. One run tells you what
it *does* with these inputs — in what order, with which values, and where the
flow leaves the code you are about to edit. This page deliberately never shows
the implementation: that is the point of reading a run instead.

## Minimal setup

Nothing beyond the [install](../guides/installation.md) and the xUnit
integration for the trace artifacts. The log lines need the logging bridge,
wired as in [Add logging to this service](add-logging-to-this-service.md).
The capture level is one environment variable; `Summary` is the one this page
uses beside the default `Detail`:

```bash
NARRATIVETRACE_LEVEL=Summary   # Off | Errors | Summary | Narrative | Detail
```

See the [Configuration Guide](../guides/configuration.md) for what each level
keeps.

## The scenario

One representative run of the flow — an existing test is the cheapest one to
borrow, and this is that test:

<!-- snippet: recipes/checkout/Checkout.Fixtures/CheckoutReviewFixture.cs -->
```csharp
using Checkout;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using NarrativeTrace.Core;
using NarrativeTrace.Logging;
using NarrativeTrace.Proxy;
using NarrativeTrace.TestingXunit;
using Xunit;

namespace Checkout.Fixtures;

/// <summary>
/// One successful checkout, the flow recipes/review-a-complex-flow-before-changing-it.md is read
/// before changing anything. A fixture because the recipe runs it twice under different tracing
/// levels and keeps the log lines the run wrote beside the trace artifacts. It passes either way —
/// nothing here fails by design.
/// </summary>
public sealed class CheckoutReviewFixture : IDisposable
{
    // A fixed W3C traceparent, so the page's embedded artifacts name the same trace every run.
    private const string DemoTraceparent = "00-c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7-c4e1d0a7c4e1d0a7-01";

    // One fixture per test: xUnit builds a fresh class instance for each test, so no test sees another's spans.
    private readonly NarrativeFixture _trace = new();

    public CheckoutReviewFixture()
    {
        _trace.Context.AdoptTraceparent(Traceparent.Parse(DemoTraceparent));
    }

    public void Dispose() => _trace.Dispose();

    [Fact]
    public void A_customer_buys_two_keyboards()
    {
        var log = new StringWriter { NewLine = "\n" };
        var original = Console.Out;
        Console.SetOut(log);
        try
        {
            RunCheckoutThroughALogger();
        }
        finally
        {
            Console.SetOut(original);
        }

        var outputDirectory = Environment.GetEnvironmentVariable("NARRATIVETRACE_OUTPUT_DIR");
        Assert.False(string.IsNullOrEmpty(outputDirectory), "the recipe run names its output directory");
        File.WriteAllText(Path.Combine(outputDirectory, "checkout.log"), log.ToString());
        _trace.WriteArtifacts(nameof(CheckoutReviewFixture), nameof(A_customer_buys_two_keyboards), failed: false);
    }

    private void RunCheckoutThroughALogger()
    {
        using var loggerFactory = LoggerFactory.Create(builder =>
            builder.SetMinimumLevel(LogLevel.Trace).AddSimpleConsole(
                options => options.ColorBehavior = LoggerColorBehavior.Disabled));
        var context = new LoggingNarrativeContext(_trace.Context, loggerFactory.CreateLogger("NarrativeTrace"));
        var stock = new InMemoryInventory("SKU-KB", 5);
        var repository = new InMemoryOrderRepository();
        var inventory = NarrativeTraceProxy.Create<IInventoryService>(stock, context);
        var payments = NarrativeTraceProxy.Create<IPaymentService>(new CardPaymentService(), context);
        var orders = NarrativeTraceProxy.Create<IOrderRepository>(repository, context);
        var checkout = NarrativeTraceProxy.Create<IOrderService>(
            new DefaultOrderService(inventory, payments, orders), context);

        var orderId = checkout.PlaceOrder("C-1234", "SKU-KB", 2, "4242424242424242");

        Assert.Equal("ORD-C-1234-SKU-KB-2", orderId);
        Assert.Equal(3, stock.AvailableUnits("SKU-KB"));
        Assert.Single(repository.Saved);
    }
}
```
<!-- /snippet -->

## The actual output

**At `Detail`** (the default), `traces/CheckoutReviewFixture/a_customer_buys_two_keyboards.md`
under the test output directory:

<!-- snippet: artifacts/recipes/review-a-complex-flow-before-changing-it/detail/traces/CheckoutReviewFixture/a_customer_buys_two_keyboards.md mask=duration,traceName -->
```text
---
type: trace
scenario: A customer buys two keyboards
entry_point: IOrderService.PlaceOrder
trace_id: c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7
trace_name: solid wand romps
method_count: 4
error_count: 0
result: success
duration_ms: 21
---

## Trace: solid wand romps — IOrderService.PlaceOrder

**Scenario:** A customer buys two keyboards
**Duration:** 21ms | **Result:** PASSED

### Call Flow

- **IOrderService.PlaceOrder**(customerId: `"C-1234"`, sku: `"SKU-KB"`, quantity: `2`, cardNumber: `[REDACTED]`) → `"ORD-C-1234-SKU-KB-2"` — 21ms #1
  - **IInventoryService.Reserve**(sku: `"SKU-KB"`, quantity: `2`) — 0ms #1.1
  - **IPaymentService.Charge**(customerId: `"C-1234"`, amountCents: `7998`, card: `[REDACTED]`) → `"PAY-C-1234-7998"` — 6ms #1.2
  - **IOrderRepository.Save**(order: `Order(OrderId: "ORD-C-1234-SKU-KB-2", CustomerId: "C-1234", Sku: "SKU-KB", Quantity: 2, AmountCents: 7998)`) — 0ms #1.3
```
<!-- /snippet -->

**At `Summary`**, the same run of the same test:

<!-- snippet: artifacts/recipes/review-a-complex-flow-before-changing-it/summary/traces/CheckoutReviewFixture/a_customer_buys_two_keyboards.md mask=duration,traceName -->
```text
---
type: trace
scenario: A customer buys two keyboards
entry_point: IOrderService.PlaceOrder
trace_id: c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7
trace_name: solid wand romps
method_count: 4
error_count: 0
result: success
duration_ms: 24
---

## Trace: solid wand romps — IOrderService.PlaceOrder

**Scenario:** A customer buys two keyboards
**Duration:** 24ms | **Result:** PASSED

### Call Flow

- **IOrderService.PlaceOrder**(customerId: ``, sku: ``, quantity: ``, cardNumber: `[REDACTED]`) → `"ORD-C-1234-SKU-KB-2"` — 24ms #1
  - **IInventoryService.Reserve**(sku: ``, quantity: ``) — 0ms #1.1
  - **IPaymentService.Charge**(customerId: ``, amountCents: ``, card: ``) → `"PAY-C-1234-7998"` — 10ms #1.2
  - **IOrderRepository.Save**(order: ``) — 0ms #1.3
```
<!-- /snippet -->

**The log the same flow wrote** through the project's own logger, values and
all:

<!-- snippet: artifacts/recipes/review-a-complex-flow-before-changing-it/detail/checkout.log -->
```text
trce: NarrativeTrace[1]
      → IOrderService.PlaceOrder(customerId: "C-1234", sku: "SKU-KB", quantity: 2, cardNumber: [REDACTED])
trce: NarrativeTrace[1]
      → IInventoryService.Reserve(sku: "SKU-KB", quantity: 2)
trce: NarrativeTrace[9]
      ← completed
trce: NarrativeTrace[1]
      → IPaymentService.Charge(customerId: "C-1234", amountCents: 7998, card: [REDACTED])
trce: NarrativeTrace[2]
      ← returned: "PAY-C-1234-7998"
trce: NarrativeTrace[1]
      → IOrderRepository.Save(order: Order(OrderId: "ORD-C-1234-SKU-KB-2", CustomerId: "C-1234", Sku: "SKU-KB", Quantity: 2, AmountCents: 7998))
trce: NarrativeTrace[9]
      ← completed
trce: NarrativeTrace[2]
      ← returned: "ORD-C-1234-SKU-KB-2"
```
<!-- /snippet -->

**The structural trace of that run**, `structural/CheckoutReviewFixture/a_customer_buys_two_keyboards.nt`
under the test output directory:

<!-- snippet: artifacts/recipes/review-a-complex-flow-before-changing-it/detail/structural/CheckoutReviewFixture/a_customer_buys_two_keyboards.nt -->
```text
scenario: A customer buys two keyboards

#1 - IOrderService.PlaceOrder(customerId, sku, quantity, cardNumber) → value
  #1.1 - IInventoryService.Reserve(sku, quantity)
  #1.2 - IPaymentService.Charge(customerId, amountCents, card) → value
  #1.3 - IOrderRepository.Save(order)
```
<!-- /snippet -->

## How to read it

**The flow, from the `Detail` artifact.** `#1` is the call the test made and
the only root: everything indented under it happened inside it, in the order
printed. `#1.1` reserves two units before anything is charged; `#1.2` charges
7998 — two units at 3999, so the price is derived from the quantity and not
passed in; `#1.3` saves an order that already carries the id `#1` returns.
Read as a sentence: this checkout reserves first, charges second, saves third,
and the order id is minted before the save. A change that charges before it
reserves, or saves before it charges, is a change to *this*, and now you know
it without trusting a comment.

**What each level is for.** `Detail` carries the parameter values, and it is
what you read while working on the flow. At `Summary` every parameter value is
empty — never captured, which is different from hidden at render — and only
the root and the leaf calls are kept; this flow is two levels deep, so no
frame is intermediate and the same four calls appear. A deeper flow loses its
middle. Return values survive at both levels: the return is the payload of a
narrative. (`card:` is empty rather than `[REDACTED]` here for the same
reason: a value that was never captured has nothing to redact. `cardNumber`
still reads `[REDACTED]`, because the name deny-list applies at capture time.)
Use `Summary` where the shape and the outcomes are what you need and the
values are cost — a CI run, a long soak — and `Off`/`Errors` in production
until you need more.

**Which output a human reads, and which an agent reads.** The log lines are
for the human on call: they interleave with everything else the service logs
and carry the values. The `.nt` is for the agent and the reviewer:
**value-free by construction** — compare it with the log line above and the
difference is not that the values were replaced but that they were never
written. `(customerId, sku, quantity, cardNumber)` is the parameter *names*;
`→ value` says a value was returned without saying which. That is what makes
it safe to commit, to diff in a review, and to hand to a model: it cannot leak
a customer id, and it does not change when the data changes — only when the
*calls* change. Pinning it is
[Review a refactoring: did behaviour change](review-a-refactoring-did-behaviour-change.md).

## What is not captured

- **Calls inside unwrapped classes.** Four interfaces were wrapped; a call
  between two classes behind them, or a private method, never appears. A flow
  that looks shallow may be deep.
- **What the chosen integration does not intercept.** The `DispatchProxy`
  sees calls made *through* the wrapped reference only: an inner call that a
  class makes on itself (`this.Helper()`) is invisible to it.
  [Choosing an Integration](../choosing-an-integration.md) is the trade-off
  per path.
- **Work that left the thread.** A call handed to another thread without the
  context travels no further; the trace ends where the thread ended.
- **Values the deny-list or the value-shape rule caught**, here `cardNumber`
  and `card`, are `[REDACTED]` in both the log and the `Detail` artifact, and
  absent by construction from the `.nt`.
- **One run, not the contract.** Another input takes another path — an
  out-of-stock sku never reaches `#1.2` at all.

**The skill for this:** `narrativetrace-verify` — it writes the intent down
first, runs the smallest real path, and reads the value-free trace against
that intent before anything is called done.
