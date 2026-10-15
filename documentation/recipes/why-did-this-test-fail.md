# Why did this test fail

**English** | [Español](../es/recetas/por-que-fallo-esta-prueba.md) | [Português](../pt-BR/receitas/por-que-este-teste-falhou.md) | [简体中文](../zh-CN/实用方案/这个测试为什么失败了.md)

## The problem

> Why did this test fail?

One checkout test is red. The assertion says what did not match; it does not
say what the code did on the way there, which is the part you need in order
to know whether the code or the expectation is wrong.

## Minimal setup

Nothing beyond the [install](../guides/installation.md) and the xUnit
integration. `NarrativeFixture` writes one narrative per test with no
configuration — for a failing test too, which is the case this page is about.
Call `WriteArtifacts(..., failed: true)` from the failure path and the files
land in `TestResults/narrativetrace/traces/<TestClass>/<scenario>.md`;
`manifest.json` beside them lists every artifact of every scenario when you
know the test's name but not its file's. See [What to Commit](../what-to-commit.md)
for the full set.

## The scenario

The failing test:

<!-- snippet: recipes/checkout/Checkout.Fixtures/StaleExpectationFixture.cs -->
```csharp
using Checkout;
using NarrativeTrace.Core;
using NarrativeTrace.Proxy;
using NarrativeTrace.TestingXunit;
using Xunit;

namespace Checkout.Fixtures;

/// <summary>
/// The failing checkout test recipes/why-did-this-test-fail.md is read through. It fails on an
/// assertion, by design. Nothing here says which side is wrong — that is the reader's exercise,
/// and the narrative the run writes is what settles it.
/// </summary>
public sealed class StaleExpectationFixture : IDisposable
{
    // A fixed W3C traceparent, so the page's embedded artifact names the same trace every run.
    private const string DemoTraceparent = "00-c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7-c4e1d0a7c4e1d0a7-01";

    // One fixture per test: xUnit builds a fresh class instance for each test, so no test sees another's spans.
    private readonly NarrativeFixture _trace = new();

    public StaleExpectationFixture()
    {
        _trace.Context.AdoptTraceparent(Traceparent.Parse(DemoTraceparent));
    }

    public void Dispose() => _trace.Dispose();

    [Fact]
    public void The_customer_gets_an_order_id_for_their_keyboards()
    {
        var stock = new InMemoryInventory("SKU-KB", 5);
        var inventory = NarrativeTraceProxy.Create<IInventoryService>(stock, _trace.Context);
        var payments = NarrativeTraceProxy.Create<IPaymentService>(new CardPaymentService(), _trace.Context);
        var orders = NarrativeTraceProxy.Create<IOrderRepository>(new InMemoryOrderRepository(), _trace.Context);
        var checkout = NarrativeTraceProxy.Create<IOrderService>(
            new DefaultOrderService(inventory, payments, orders), _trace.Context);

        var failed = true;
        try
        {
            _trace.Run("The customer gets an order id for their keyboards", _ =>
            {
                var orderId = checkout.PlaceOrder("C-1234", "SKU-KB", 2, "4242424242424242");

                Assert.Equal("ORD-C-1234-SKU-KB-1", orderId);
            });
            failed = false;
        }
        finally
        {
            _trace.WriteArtifacts(nameof(StaleExpectationFixture),
                nameof(The_customer_gets_an_order_id_for_their_keyboards), failed);
        }
    }
}
```
<!-- /snippet -->

## The actual output

What the runner reported:

<!-- snippet: artifacts/recipes/why-did-this-test-fail/assertion-failure.txt -->
```text
Assert.Equal() Failure: Strings differ
                             ↓ (pos 18)
Expected: "ORD-C-1234-SKU-KB-1"
Actual:   "ORD-C-1234-SKU-KB-2"
                             ↑ (pos 18)
```
<!-- /snippet -->

And what the same run wrote beside it, at
`traces/StaleExpectationFixture/the_customer_gets_an_order_id_for_their_keyboards.md`:

<!-- snippet: artifacts/recipes/why-did-this-test-fail/traces/StaleExpectationFixture/the_customer_gets_an_order_id_for_their_keyboards.md mask=duration,traceName -->
```text
---
type: trace
scenario: The customer gets an order id for their keyboards
entry_point: IOrderService.PlaceOrder
trace_id: c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7
trace_name: solid wand romps
method_count: 4
error_count: 0
result: success
duration_ms: 10
---

## Trace: solid wand romps — IOrderService.PlaceOrder

**Scenario:** The customer gets an order id for their keyboards
**Duration:** 10ms | **Result:** FAILED

### Call Flow

- **IOrderService.PlaceOrder**(customerId: `"C-1234"`, sku: `"SKU-KB"`, quantity: `2`, cardNumber: `[REDACTED]`) → `"ORD-C-1234-SKU-KB-2"` — 10ms #1
  - **IInventoryService.Reserve**(sku: `"SKU-KB"`, quantity: `2`) — 0ms #1.1
  - **IPaymentService.Charge**(customerId: `"C-1234"`, amountCents: `7998`, card: `[REDACTED]`) → `"PAY-C-1234-7998"` — 6ms #1.2
  - **IOrderRepository.Save**(order: `Order(OrderId: "ORD-C-1234-SKU-KB-2", CustomerId: "C-1234", Sku: "SKU-KB", Quantity: 2, AmountCents: 7998)`) — 0ms #1.3
```
<!-- /snippet -->

## How to read it

Read the narrative top-down against the assertion, one span at a time:

- `#1 IOrderService.PlaceOrder(... quantity: 2 ...) → "ORD-C-1234-SKU-KB-2"`.
  The value the assertion rejected is right there, and so is the input that
  produced it: the call asked for **two** keyboards.
- `#1.1 IInventoryService.Reserve(sku: "SKU-KB", quantity: 2)` — two units
  reserved, consistent with the call.
- `#1.2 IPaymentService.Charge(..., amountCents: 7998, ...)` — 7998 is two
  units at 3999, so the price agrees with the quantity too.
- `#1.3 IOrderRepository.Save(order: Order(... Quantity: 2 ...))` — what was
  stored agrees with what was asked for.
- `**Result:** FAILED` in the header is the *test's* verdict, not the code's:
  `error_count: 0` and every span completed. Nothing threw.

Every span agrees with every other span and with the call that started them.
The expectation — `ORD-C-1234-SKU-KB-1` — is the only thing in the run that
says one keyboard, so it is the thing that is out of date; the scenario it
was written against has since changed to order two. The fix is in the test.

The opposite reading is the other recipe: when a span *disagrees* with the
intent — a quantity that changes between two calls, a call that should have
followed and did not — the code is wrong, and
[Help me debug and fix this bug](help-me-debug-and-fix-this-bug.md) is that
walk-through.

## What is not captured

- **Only that test's run.** The narrative is written per test method, per run.
  A failure that happens once in twenty runs needs one of those runs to be
  the one that wrote the file; each run overwrites the last one's artifact,
  and a failing run does not advance the last-green `.nt`.
- **The assertion is not in the trace.** The two outputs above come from two
  places — the runner reported the mismatch, the fixture wrote the flow — and
  nothing correlates them automatically beyond the scenario name.
- **A value a test computed but never passed to a traced call is invisible.**
  The trace carries parameters and return values of wrapped calls, not local
  variables.
- **Timings in a failing run mean nothing.** The run stopped being
  representative the moment the assertion threw.

**The skill for this:** `narrativetrace-debug` — it starts from the symptom,
reads the first span where a value diverges, and names it by span id.
