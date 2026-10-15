# Help me debug and fix this bug

**English** | [Español](../es/recetas/ayudame-a-depurar-y-arreglar-este-error.md) | [Português](../pt-BR/receitas/ajude-me-a-depurar-e-corrigir-este-bug.md) | [简体中文](../zh-CN/实用方案/帮我调试并修复这个缺陷.md)

## The problem

> Help me debug and fix this bug: a customer's card was declined and the
> keyboard never came back into stock.

The error the team has is `PaymentDeclinedException: card declined by the
issuer`, and that is not the bug. The bug is what the checkout did *after* it.

## Minimal setup

Nothing beyond the [install](../guides/installation.md) and the xUnit
integration: `NarrativeFixture` writes one trace per test with no
configuration. The last step on this page — pinning the fixed flow — turns on
approval mode, which is one environment variable, set for the whole test run
from a `.runsettings` file:

```xml
<RunSettings>
  <RunConfiguration>
    <EnvironmentVariables>
      <NARRATIVETRACE_APPROVAL>true</NARRATIVETRACE_APPROVAL>
    </EnvironmentVariables>
  </RunConfiguration>
</RunSettings>
```

See [What to Commit](../what-to-commit.md#approval-mode-end-to-end) and the
[Configuration Guide](../guides/configuration.md).

## The scenario

Reproduce it in the smallest test that can fail for this reason — a declined
card must leave the stock as it found it. The second test is the same
reproduction against the checkout as it stood before the fix, and it fails by
design: it is the run the first trace below comes from.

<!-- snippet: recipes/checkout/Checkout.Fixtures/DeclinedPaymentFixture.cs -->
```csharp
using Checkout;
using NarrativeTrace.Core;
using NarrativeTrace.Proxy;
using NarrativeTrace.TestingXunit;
using Xunit;

namespace Checkout.Fixtures;

/// <summary>
/// The reproduction recipes/help-me-debug-and-fix-this-bug.md is written around: a declined card
/// must leave the stock as it found it. The second test is the same reproduction against the
/// checkout as it stood before the fix, so the page's "before" trace comes from a real failing run
/// rather than from prose; it fails by design.
/// </summary>
public sealed class DeclinedPaymentFixture : IDisposable
{
    // A fixed W3C traceparent, so the page's embedded artifacts name the same trace every run.
    private const string DemoTraceparent = "00-c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7-c4e1d0a7c4e1d0a7-01";

    // One fixture per test: xUnit builds a fresh class instance for each test, so no test sees another's spans.
    private readonly NarrativeFixture _trace = new();

    public DeclinedPaymentFixture()
    {
        _trace.Context.AdoptTraceparent(Traceparent.Parse(DemoTraceparent));
    }

    public void Dispose() => _trace.Dispose();

    [Fact]
    public void The_stock_is_released_when_the_card_is_declined()
    {
        var stock = new InMemoryInventory("SKU-KB", 5);
        var repository = new InMemoryOrderRepository();
        var inventory = NarrativeTraceProxy.Create<IInventoryService>(stock, _trace.Context);
        var payments = NarrativeTraceProxy.Create<IPaymentService>(new CardPaymentService(), _trace.Context);
        var orders = NarrativeTraceProxy.Create<IOrderRepository>(repository, _trace.Context);
        var checkout = NarrativeTraceProxy.Create<IOrderService>(
            new DefaultOrderService(inventory, payments, orders), _trace.Context);

        _trace.Run("The stock is released when the card is declined", _ =>
        {
            Assert.Throws<PaymentDeclinedException>(
                () => checkout.PlaceOrder("C-1234", "SKU-KB", 2, CardPaymentService.DecliningCard));

            Assert.Empty(repository.Saved);
            Assert.Equal(5, stock.AvailableUnits("SKU-KB"));
        });
        _trace.WriteArtifacts(nameof(DeclinedPaymentFixture),
            nameof(The_stock_is_released_when_the_card_is_declined), failed: false);
    }

    [Fact]
    public void The_stock_is_released_when_the_card_is_declined_before_the_fix()
    {
        var stock = new InMemoryInventory("SKU-KB", 5);
        var repository = new InMemoryOrderRepository();
        var inventory = NarrativeTraceProxy.Create<IInventoryService>(stock, _trace.Context);
        var payments = NarrativeTraceProxy.Create<IPaymentService>(new CardPaymentService(), _trace.Context);
        var orders = NarrativeTraceProxy.Create<IOrderRepository>(repository, _trace.Context);
        var checkout = NarrativeTraceProxy.Create<IOrderService>(
            new OrderServiceWithoutRelease(inventory, payments, orders), _trace.Context);

        try
        {
            _trace.Run("The stock is released when the card is declined, before the fix", _ =>
            {
                Assert.Throws<PaymentDeclinedException>(
                    () => checkout.PlaceOrder("C-1234", "SKU-KB", 2, CardPaymentService.DecliningCard));

                Assert.Empty(repository.Saved);
                Assert.Equal(5, stock.AvailableUnits("SKU-KB"));
            });
        }
        catch
        {
            _trace.WriteArtifacts(nameof(DeclinedPaymentFixture),
                nameof(The_stock_is_released_when_the_card_is_declined_before_the_fix), failed: true);
            throw;
        }
    }
}
```
<!-- /snippet -->

The checkout the second test wires, before the fix:

<!-- snippet: recipes/checkout/Checkout/OrderServiceWithoutRelease.cs -->
```csharp
namespace Checkout;

/// <summary>
/// The same checkout with no release path: a declined charge leaves the reservation behind.
/// Two recipe pages need this code to run, not to be described — the bug before its fix and the
/// refactoring that drops the release while every test stays green. Never the implementation to
/// copy: <see cref="DefaultOrderService"/> is.
/// </summary>
public sealed class OrderServiceWithoutRelease(
    IInventoryService inventory, IPaymentService payments, IOrderRepository orders) : IOrderService
{
    private const int UnitPriceCents = 3999;

    public string PlaceOrder(string customerId, string sku, int quantity, string cardNumber)
    {
        inventory.Reserve(sku, quantity);
        var amountCents = UnitPriceCents * quantity;
        payments.Charge(customerId, amountCents, cardNumber);
        var orderId = $"ORD-{customerId}-{sku}-{quantity}";
        orders.Save(new Order(orderId, customerId, sku, quantity, amountCents));
        return orderId;
    }
}
```
<!-- /snippet -->

## The actual output

What the test runner said about the failing run:

<!-- snippet: artifacts/recipes/help-me-debug-and-fix-this-bug/runner-failure.txt -->
```text
Assert.Equal() Failure: Values differ
Expected: 5
Actual:   3
```
<!-- /snippet -->

That is the symptom — two units still held — and it does not say why. The
failing run's own narrative does, written beside it at
`traces/DeclinedPaymentFixture/the_stock_is_released_when_the_card_is_declined_before_the_fix.md`
under the test output directory:

<!-- snippet: artifacts/recipes/help-me-debug-and-fix-this-bug/traces/DeclinedPaymentFixture/the_stock_is_released_when_the_card_is_declined_before_the_fix.md mask=duration,traceName -->
```text
---
type: trace
scenario: The stock is released when the card is declined before the fix
entry_point: IOrderService.PlaceOrder
trace_id: c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7
trace_name: solid wand romps
method_count: 3
error_count: 2
result: error
duration_ms: 5
---

## Trace: solid wand romps — IOrderService.PlaceOrder

**Scenario:** The stock is released when the card is declined before the fix
**Duration:** 5ms | **Result:** FAILED

### Call Flow

- **IOrderService.PlaceOrder**(customerId: `"C-1234"`, sku: `"SKU-KB"`, quantity: `2`, cardNumber: `[REDACTED]`) — 5ms #1
  - **IInventoryService.Reserve**(sku: `"SKU-KB"`, quantity: `2`) — 1ms #1.1
  - **IPaymentService.Charge**(customerId: `"C-1234"`, amountCents: `7998`, card: `[REDACTED]`) ❌ PaymentDeclinedException: card declined by the issuer — 1ms #1.2
  - ❌ PaymentDeclinedException: card declined by the issuer
```
<!-- /snippet -->

The fix is one `catch`: release what was reserved, then let the decline
propagate as before.

<!-- snippet: recipes/checkout/Checkout/DefaultOrderService.cs -->
```csharp
namespace Checkout;

/// <summary>The checkout: reserve the stock, charge the card, save the order — and release on a decline.</summary>
public sealed class DefaultOrderService(
    IInventoryService inventory, IPaymentService payments, IOrderRepository orders) : IOrderService
{
    private const int UnitPriceCents = 3999;

    public string PlaceOrder(string customerId, string sku, int quantity, string cardNumber)
    {
        inventory.Reserve(sku, quantity);
        var amountCents = UnitPriceCents * quantity;
        try
        {
            payments.Charge(customerId, amountCents, cardNumber);
        }
        catch (PaymentDeclinedException)
        {
            inventory.Release(sku, quantity);
            throw;
        }

        var orderId = $"ORD-{customerId}-{sku}-{quantity}";
        orders.Save(new Order(orderId, customerId, sku, quantity, amountCents));
        return orderId;
    }
}
```
<!-- /snippet -->

The same reproduction, now passing, against the fixed checkout:

<!-- snippet: artifacts/recipes/help-me-debug-and-fix-this-bug/traces/DeclinedPaymentFixture/the_stock_is_released_when_the_card_is_declined.md mask=duration,traceName -->
```text
---
type: trace
scenario: The stock is released when the card is declined
entry_point: IOrderService.PlaceOrder
trace_id: c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7
trace_name: solid wand romps
method_count: 4
error_count: 2
result: error
duration_ms: 1
---

## Trace: solid wand romps — IOrderService.PlaceOrder

**Scenario:** The stock is released when the card is declined
**Duration:** 1ms | **Result:** PASSED

### Call Flow

- **IOrderService.PlaceOrder**(customerId: `"C-1234"`, sku: `"SKU-KB"`, quantity: `2`, cardNumber: `[REDACTED]`) — 1ms #1
  - **IInventoryService.Reserve**(sku: `"SKU-KB"`, quantity: `2`) — 0ms #1.1
  - **IPaymentService.Charge**(customerId: `"C-1234"`, amountCents: `7998`, card: `[REDACTED]`) ❌ PaymentDeclinedException: card declined by the issuer — 0ms #1.2
  - **IInventoryService.Release**(sku: `"SKU-KB"`, quantity: `2`) — 0ms #1.3
  - ❌ PaymentDeclinedException: card declined by the issuer
```
<!-- /snippet -->

With approval mode on, the first run of the fixed test writes its value-free
structure beside the test as a `.received.nt` and fails;
`dotnet tool run dotnet-narrativetrace approve` promotes it to the committed
baseline, which is now the shape of this flow:

<!-- snippet: recipes/checkout/Checkout.Fixtures/narratives/DeclinedPaymentFixture/the_stock_is_released_when_the_card_is_declined.approved.nt -->
```text
scenario: The stock is released when the card is declined

#1 - IOrderService.PlaceOrder(customerId, sku, quantity, cardNumber) !! PaymentDeclinedException
  #1.1 - IInventoryService.Reserve(sku, quantity)
  #1.2 - IPaymentService.Charge(customerId, amountCents, card) !! PaymentDeclinedException
  #1.3 - IInventoryService.Release(sku, quantity)
```
<!-- /snippet -->

## How to read it

In the failing trace:

- `#1 IOrderService.PlaceOrder` ends with `❌ PaymentDeclinedException` and no
  return value: the call the customer made did not complete.
- `#1.1 IInventoryService.Reserve(sku: "SKU-KB", quantity: 2)` has no `❌` and
  no value — a `void` method that completed. Two units are now held.
- `#1.2 IPaymentService.Charge` threw. Its parameters say what was attempted:
  `amountCents: 7998` for the two units, `card: [REDACTED]`.
- **Nothing follows `#1.2`.** `#1` goes straight from the charge to its own
  failure, so the two units `#1.1` reserved are still held. That absence is
  the bug, and the exception message never mentions it — the trace shows the
  whole flow and not only the line that threw, which is what makes an
  omission visible.

In the passing trace: `#1.3 IInventoryService.Release(sku: "SKU-KB", quantity: 2)`
now sits between the charge and the failure. `#1` still ends in the decline —
that is correct, the customer's card really was declined — and the stock is
back.

In the baseline: the approved `.nt` carries the same four calls with their
parameter *names* and no values at all, which is why it can be committed and
read in a review. `#1.3` is now part of the pinned shape: a later change that
drops the release makes a passing test fail with a diff naming
`IInventoryService.Release`, so this bug cannot silently return. That is
[Review a refactoring: did behaviour change](review-a-refactoring-did-behaviour-change.md).

## What is not captured

- **The trace shows calls that happened, not calls that should have.** The
  missing `Release` is an absence a reader infers from the flow; nothing in
  the artifact says "a call is missing". What the baseline adds is the
  comparison that turns an absence into a build failure — once.
- **An exception swallowed inside unwrapped code is invisible.** Only the
  wrapped interfaces appear. A `try`/`catch` inside `DefaultOrderService`
  that ate the decline and returned normally would show as a completed call.
- **The card number cannot be recovered** from the trace or the log. It is
  redacted at capture, by value here — the parameter is named `card`, so the
  name deny-list never saw it, and the number is Luhn-valid.
- **Timings are not evidence of a performance problem** in a test this small:
  the first call through a proxy in a process carries its one-off setup.

**The skill for this:** `narrativetrace-debug` — it reproduces the symptom
with the smallest input, names the span where the value or the call diverges,
fixes it there, and pins the flow as a regression test behind your approval.
