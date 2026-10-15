# Review a refactoring: did behaviour change

**English** | [Español](../es/recetas/revisa-una-refactorizacion-cambio-el-comportamiento.md) | [Português](../pt-BR/receitas/revise-uma-refatoracao-o-comportamento-mudou.md) | [简体中文](../zh-CN/实用方案/审查重构行为变了吗.md)

## The problem

> I refactored the checkout. The tests are green. Did the behaviour change?

Green means no assertion noticed. A refactoring that drops a call nobody
asserts on is green and wrong, and this is the common shape of it: the
compensating call on an error path, which no happy-path test reaches.

## Minimal setup

Beyond the [install](../guides/installation.md), approval mode — one
environment variable, and a directory of committed baselines:

```bash
NARRATIVETRACE_APPROVAL=true      # compare, don't just write
NARRATIVETRACE_APPROVED_DIR=narratives   # the default, beside the test project
```

The first run of each test writes `narratives/<TestClass>/<scenario>.received.nt`
and fails; you read it, run `dotnet tool run dotnet-narrativetrace approve` to
promote every reviewed file to `.approved.nt`, and commit those. See
[What to Commit](../what-to-commit.md#approval-mode-end-to-end) and the
[Configuration Guide](../guides/configuration.md).

## The scenario

The test pins the error path a refactoring is most likely to break — a
declined card must save no order — and it passes against both versions of the
checkout, which is the whole problem:

<!-- snippet: recipes/checkout/Checkout.Fixtures/RefactoredCheckoutFixture.cs -->
```csharp
using Checkout;
using NarrativeTrace.Core;
using NarrativeTrace.Proxy;
using NarrativeTrace.TestingXunit;
using Xunit;

namespace Checkout.Fixtures;

/// <summary>
/// The test recipes/review-a-refactoring-did-behaviour-change.md pins: a declined card must save
/// no order. It passes before and after the refactoring — which is the whole point of the page —
/// so only the approved structure can object.
/// </summary>
public sealed class RefactoredCheckoutFixture : IDisposable
{
    // A fixed W3C traceparent, so the page's embedded artifacts name the same trace every run.
    private const string DemoTraceparent = "00-c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7-c4e1d0a7c4e1d0a7-01";

    // One fixture per test: xUnit builds a fresh class instance for each test, so no test sees another's spans.
    private readonly NarrativeFixture _trace = new();

    public RefactoredCheckoutFixture()
    {
        _trace.Context.AdoptTraceparent(Traceparent.Parse(DemoTraceparent));
    }

    /// <summary>
    /// Which version of the checkout this run wires: the one that releases the reservation when the
    /// charge is declined, or the refactoring that dropped that call. An environment switch,
    /// because one build has to run both — in your project the refactoring is an edit to one file
    /// and this switch does not exist.
    /// </summary>
    private static bool RefactoringDroppedTheRelease =>
        Environment.GetEnvironmentVariable("CHECKOUT_RECIPE_REFACTORING") == "dropped-release";

    public void Dispose() => _trace.Dispose();

    [Fact]
    public void A_declined_card_saves_no_order()
    {
        var repository = new InMemoryOrderRepository();
        var inventory = NarrativeTraceProxy.Create<IInventoryService>(new InMemoryInventory("SKU-KB", 5), _trace.Context);
        var payments = NarrativeTraceProxy.Create<IPaymentService>(new CardPaymentService(), _trace.Context);
        var orders = NarrativeTraceProxy.Create<IOrderRepository>(repository, _trace.Context);
        IOrderService service = RefactoringDroppedTheRelease
            ? new OrderServiceWithoutRelease(inventory, payments, orders)
            : new DefaultOrderService(inventory, payments, orders);
        var checkout = NarrativeTraceProxy.Create<IOrderService>(service, _trace.Context);

        _trace.Run("A declined card saves no order", _ =>
        {
            Assert.Throws<PaymentDeclinedException>(
                () => checkout.PlaceOrder("C-1234", "SKU-KB", 2, CardPaymentService.DecliningCard));

            Assert.Empty(repository.Saved);
        });
        _trace.WriteArtifacts(nameof(RefactoredCheckoutFixture), nameof(A_declined_card_saves_no_order), failed: false);
    }
}
```
<!-- /snippet -->

## The actual output

The committed baseline, at
`narratives/RefactoredCheckoutFixture/a_declined_card_saves_no_order.approved.nt`:

<!-- snippet: recipes/checkout/Checkout.Fixtures/narratives/RefactoredCheckoutFixture/a_declined_card_saves_no_order.approved.nt -->
```text
scenario: A declined card saves no order

#1 - IOrderService.PlaceOrder(customerId, sku, quantity, cardNumber) !! PaymentDeclinedException
  #1.1 - IInventoryService.Reserve(sku, quantity)
  #1.2 - IPaymentService.Charge(customerId, amountCents, card) !! PaymentDeclinedException
  #1.3 - IInventoryService.Release(sku, quantity)
```
<!-- /snippet -->

The refactored run's verdict — the assertions passed, the structure did not:

<!-- snippet: artifacts/recipes/review-a-refactoring-did-behaviour-change/approved-after-the-refactoring/approval-rejection.txt -->
```text
NarrativeTrace.Core.NarrativeApprovalException : Trace changed against the approved trace (-1 call IInventoryService.Release):
 scenario: A declined card saves no order
 
 #1 - IOrderService.PlaceOrder(customerId, sku, quantity, cardNumber) !! PaymentDeclinedException
   #1.1 - IInventoryService.Reserve(sku, quantity)
   #1.2 - IPaymentService.Charge(customerId, amountCents, card) !! PaymentDeclinedException
-  #1.3 - IInventoryService.Release(sku, quantity)
Received: artifacts/recipes/review-a-refactoring-did-behaviour-change/approved-after-the-refactoring/approved/RefactoredCheckoutFixture/a_declined_card_saves_no_order.received.nt
If this change is intended, promote it with: dotnet tool run dotnet-narrativetrace approve
```
<!-- /snippet -->

And the file it offers for review, which `approve` would promote:

<!-- snippet: artifacts/recipes/review-a-refactoring-did-behaviour-change/approved-after-the-refactoring/approved/RefactoredCheckoutFixture/a_declined_card_saves_no_order.received.nt -->
```text
scenario: A declined card saves no order

#1 - IOrderService.PlaceOrder(customerId, sku, quantity, cardNumber) !! PaymentDeclinedException
  #1.1 - IInventoryService.Reserve(sku, quantity)
  #1.2 - IPaymentService.Charge(customerId, amountCents, card) !! PaymentDeclinedException
```
<!-- /snippet -->

(The `Received:` path is this repository's own: the run that produces this
page points `NARRATIVETRACE_APPROVED_DIR` under `artifacts/`, on a copy of the
baseline, because the build that writes a page must not write into the source
tree. In your project it is `narratives/`, beside the baseline.)

## How to read it

- **The first line names the change, not just the fact of it:**
  `-1 call IInventoryService.Release`. One call fewer, and which one.
- **The diff body is the pinned shape with the dropped line marked.** `#1`,
  `#1.1` and `#1.2` are unchanged and unmarked; `-  #1.3 - IInventoryService.Release(sku, quantity)`
  is the behaviour that left. A reviewer who has never seen this code can tell
  what the refactoring did from four lines.
- **The assertions held.** The failure is the approval's, which is the point:
  the test asserts that no order is saved, and the refactoring did not break
  that. Nothing in the test ever looked at the release, and nothing had to.
- **Now decide.** If dropping the release is intended — the caller took it
  over, say — run `dotnet tool run dotnet-narrativetrace approve`, review the
  diff in the promoted file, and commit it: the new shape becomes the pinned
  one. If it is not intended, you have just caught the bug that
  [Help me debug and fix this bug](help-me-debug-and-fix-this-bug.md) fixed,
  on its way back in. Either way the decision is recorded in the repository,
  by a human, in a file a reviewer can read.
- **The baseline is value-free**, so approving it leaks nothing and a change
  in test data never makes it fail: `(customerId, sku, quantity, cardNumber)`
  are names, `!! PaymentDeclinedException` is an outcome.

## What is not captured

- **This is a structural comparison: the same calls, in the same order, with
  the same outcomes.** It is not a proof that every value is equal. A
  refactoring that charges 7998 where it charged 3999, with every call in
  place, passes this gate — that is what your assertions are for, and the
  `Detail` artifact is where you would see it.
- **Only the calls the integration intercepts.** A dropped private call, or
  one between two unwrapped classes, is invisible to the baseline exactly as
  it is to the trace.
- **A test that does not run is not compared.** Approval protects the
  scenarios you pinned; a flow with no test has no baseline, and deleting a
  test deletes its gate (its `.approved.nt` stays behind, a baseline
  nothing compares any more). A `.received.nt` nobody promoted is what
  `narrativetrace-doctor` reports, as `trap.approval-traces`.
- **Timings are never part of it.** The baseline carries no durations, so it
  cannot catch a change that only made the flow slower.

**The skill for this:** `narrativetrace-verify` — it reads the structural
trace against the intent of the change and pins the flow as a baseline behind
your yes.
