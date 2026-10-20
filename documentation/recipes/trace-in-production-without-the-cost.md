# Trace in production without the cost

**English** | [Español](../es/recetas/traza-en-produccion-sin-el-coste.md) | [Português](../pt-BR/receitas/trace-em-producao-sem-o-custo.md) | [简体中文](../zh-CN/实用方案/在生产环境追踪而不付出代价.md)

## The problem

> I want this in production for the requests that go wrong. I am not paying
> for a full trace of every request that goes right.

That is one setting — `TracingLevel` — and this page is the same run seen
through it twice: the declined-payment checkout at `Detail`, where every value
is captured, and at `Errors`, where only runs that failed survive at all.

## Minimal setup

One level, set wherever your deployment is configured. All three of these say
the same thing:

```bash
# an environment variable, read by the test fixtures and by NarrativeTraceConfig resolution
NARRATIVETRACE_LEVEL=Errors   # Off | Errors | Summary | Narrative | Detail
```

```csharp
// in code, for a context you build yourself
var config = new NarrativeTraceConfig(TracingLevel.Errors);
```

```json
// appsettings.json — for an ASP.NET Core application using NarrativeTrace.AspNetCore
{ "NarrativeTrace": { "Level": "Errors" } }
```

See the [Configuration Guide](../guides/configuration.md) for what each level
keeps and which source wins. The level is NarrativeTrace's own dial and it
sits at the front: a call it filters out never becomes part of the trace, for
any consumer. Your logger's level is a second, independent dial that decides
what is *printed* — [Configuration Guide §8, "Two dials, two paths"](../guides/configuration.md#8-two-dials-two-paths)
is the long version, and the distinction matters here because raising a
logger level saves you log volume while changing nothing about capture.

## The scenario

The run is a declined-payment checkout — `Reserve`, then `Charge` throwing,
then `Release`, the one [Help me debug and fix this bug](help-me-debug-and-fix-this-bug.md)
reproduces — reused, because the subject of this page is the dial and not
another flow. The fixture below runs it, and runs a *successful* checkout as
well, under whichever level it is started with; the build starts it once at
`Detail` and once at `Errors`:

<!-- snippet: recipes/checkout/Checkout.Fixtures/CostFixture.cs -->
```csharp
using Checkout;
using NarrativeTrace.Core;
using NarrativeTrace.Proxy;
using NarrativeTrace.TestingXunit;
using Xunit;

namespace Checkout.Fixtures;

/// <summary>
/// The runs recipes/trace-in-production-without-the-cost.md reads at two tracing levels: the declined
/// checkout, whose artifact the page compares at <c>Detail</c> and at <c>Errors</c>, and a checkout
/// that succeeds, which must leave an artifact at <c>Detail</c> and none at <c>Errors</c>. The level
/// is the <c>NARRATIVETRACE_LEVEL</c> the run is started under, so one class serves both columns of
/// the page; the second test asserts both halves of the absence claim, not just the absence.
/// </summary>
public sealed class CostFixture : IDisposable
{
    // A fixed W3C traceparent, so the page's embedded artifacts name the same trace every run.
    private const string DemoTraceparent = "00-c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7-c4e1d0a7c4e1d0a7-01";

    // One fixture per test: xUnit builds a fresh class instance for each test, so no test sees another's spans.
    private readonly NarrativeFixture _trace = new();
    private readonly InMemoryInventory _stock = new("SKU-KB", 5);
    private readonly IOrderService _checkout;

    public CostFixture()
    {
        _trace.Context.AdoptTraceparent(Traceparent.Parse(DemoTraceparent));
        var inventory = NarrativeTraceProxy.Create<IInventoryService>(_stock, _trace.Context);
        var payments = NarrativeTraceProxy.Create<IPaymentService>(new CardPaymentService(), _trace.Context);
        var orders = NarrativeTraceProxy.Create<IOrderRepository>(new InMemoryOrderRepository(), _trace.Context);
        _checkout = NarrativeTraceProxy.Create<IOrderService>(
            new DefaultOrderService(inventory, payments, orders), _trace.Context);
    }

    public void Dispose() => _trace.Dispose();

    private static bool AtErrors =>
        string.Equals(Environment.GetEnvironmentVariable("NARRATIVETRACE_LEVEL"), "Errors", StringComparison.OrdinalIgnoreCase);

    [Fact]
    public void A_declined_card_at_the_configured_level()
    {
        _trace.Run("A declined card at the configured level", _ =>
        {
            Assert.Throws<PaymentDeclinedException>(
                () => _checkout.PlaceOrder("C-1234", "SKU-KB", 2, CardPaymentService.DecliningCard));
            Assert.Equal(5, _stock.AvailableUnits("SKU-KB"));
        });
        _trace.WriteArtifacts(nameof(CostFixture), nameof(A_declined_card_at_the_configured_level), failed: false);

        var artifact = ArtifactOf(nameof(A_declined_card_at_the_configured_level));
        Assert.True(File.Exists(artifact), "the failed run's artifact exists at every level");
        var written = File.ReadAllText(artifact);
        Assert.Contains("[REDACTED]", written, StringComparison.Ordinal);
        Assert.DoesNotContain(CardPaymentService.DecliningCard, written, StringComparison.Ordinal);
    }

    [Fact]
    public void A_successful_checkout_at_the_configured_level()
    {
        _trace.Run("A successful checkout at the configured level", _ =>
            Assert.Equal("ORD-C-1234-SKU-KB-2", _checkout.PlaceOrder("C-1234", "SKU-KB", 2, "4242424242424242")));
        _trace.WriteArtifacts(nameof(CostFixture), nameof(A_successful_checkout_at_the_configured_level), failed: false);

        // Both halves of the claim: the path IS written when the level keeps successful runs, so the
        // absence at Errors is the level's doing and not a path that was never right.
        Assert.Equal(!AtErrors, File.Exists(ArtifactOf(nameof(A_successful_checkout_at_the_configured_level))));
    }

    private static string ArtifactOf(string method)
    {
        var directory = Environment.GetEnvironmentVariable("NARRATIVETRACE_OUTPUT_DIR");
        Assert.False(string.IsNullOrEmpty(directory), "the recipe run names its output directory");
        return Path.Combine(directory, "traces", nameof(CostFixture), method.ToLowerInvariant() + ".md");
    }
}
```
<!-- /snippet -->

## The actual output

**At `Detail`**, the declined run, every value captured:

<!-- snippet: artifacts/recipes/trace-in-production-without-the-cost/detail/traces/CostFixture/a_declined_card_at_the_configured_level.md mask=duration,traceName -->
```text
---
type: trace
scenario: A declined card at the configured level
entry_point: IOrderService.PlaceOrder
trace_id: c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7
trace_name: solid wand romps
method_count: 4
error_count: 2
result: error
duration_ms: 4
---

## Trace: solid wand romps — IOrderService.PlaceOrder

**Scenario:** A declined card at the configured level
**Duration:** 4ms | **Result:** PASSED

### Call Flow

- **IOrderService.PlaceOrder**(customerId: `"C-1234"`, sku: `"SKU-KB"`, quantity: `2`, cardNumber: `[REDACTED]`) — 4ms #1
  - **IInventoryService.Reserve**(sku: `"SKU-KB"`, quantity: `2`) — 0ms #1.1
  - **IPaymentService.Charge**(customerId: `"C-1234"`, amountCents: `7998`, card: `[REDACTED]`) ❌ PaymentDeclinedException: card declined by the issuer — 0ms #1.2
  - **IInventoryService.Release**(sku: `"SKU-KB"`, quantity: `2`) — 0ms #1.3
  - ❌ PaymentDeclinedException: card declined by the issuer
```
<!-- /snippet -->

**At `Errors`**, the same run of the same code:

<!-- snippet: artifacts/recipes/trace-in-production-without-the-cost/errors/traces/CostFixture/a_declined_card_at_the_configured_level.md mask=duration,traceName -->
```text
---
type: trace
scenario: A declined card at the configured level
entry_point: IOrderService.PlaceOrder
trace_id: c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7
trace_name: solid wand romps
method_count: 4
error_count: 2
result: error
duration_ms: 4
---

## Trace: solid wand romps — IOrderService.PlaceOrder

**Scenario:** A declined card at the configured level
**Duration:** 4ms | **Result:** PASSED

### Call Flow

- **IOrderService.PlaceOrder**(customerId: ``, sku: ``, quantity: ``, cardNumber: `[REDACTED]`) — 4ms #1
  - **IInventoryService.Reserve**(sku: ``, quantity: ``) — 0ms #1.1
  - **IPaymentService.Charge**(customerId: ``, amountCents: ``, card: ``) ❌ PaymentDeclinedException: card declined by the issuer — 0ms #1.2
  - **IInventoryService.Release**(sku: ``, quantity: ``) — 0ms #1.3
  - ❌ PaymentDeclinedException: card declined by the issuer
```
<!-- /snippet -->

**And at `Errors`, a checkout that succeeded**: no artifact at all. There is
nothing to embed, which is the whole point — the fixture asserts both halves
of that claim, the file's absence at `Errors` and its presence at `Detail`,
so the emptiness is the level's doing and not a path that was never right.

## How to read it

- **What `Errors` keeps.** The failed run, whole. `#1` threw, and an error
  node's subtree is kept intact — so `Reserve` (`#1.1`), `Charge` (`#1.2`) and
  `Release` (`#1.3`) are all still there, including the two calls that
  returned normally. That is deliberate: the calls around a failure are what
  you need to diagnose it, and dropping the successful sibling would leave you
  with an exception and no story. A call that returned *and* leads to no
  failure anywhere below it is dropped, and a root with no failure in its
  whole subtree is dropped entirely — which is why the successful checkout
  wrote nothing.
- **What `Errors` drops.** Every parameter value: `customerId` and `quantity`
  are rendered as an empty pair of backticks. The emptiness is suppression at
  *capture* — the value was never rendered, so it cannot be recovered from these events
  afterwards, at any later level. What survives is the shape, the outcomes and
  the exception, and `cardNumber: [REDACTED]` is still redacted because
  redaction is a different decision from suppression.
- **The pruning happens after capture, not instead of it.** Every call was
  intercepted and every event was published; the tree builder then built the
  tree and removed what the level does not keep. That is the honest shape of
  the cost: `Errors` is cheaper than `Detail` because values are not
  rendered — the expensive part — not because calls go unintercepted.
- **Only `Off` skips interception.** At `Off` the proxy's `IsActive` gate
  returns before any argument array or event exists, and nothing downstream
  can bring the call back. `Errors`, `Summary` and `Narrative` all still
  intercept every call. If the question is "what does tracing cost in this hot
  loop", the answer is a different level for that scope, or `Off`.
- **The numbers.** The measured figures — the cost of an enter/exit cycle, the
  allocation per call, the regression gate that holds them — are in the
  [README's Performance section](../../README.md#performance), beside the
  committed baseline they come from. They are not repeated here, because one
  copy that moves with the measurements is worth more than two that disagree.

## What is not captured

- **The published figures are measurements, not a promise.** They were taken
  with BenchmarkDotNet on the host the README names, one benchmark operation
  per call. Your numbers depend on your runtime, your hardware, your parameter
  shapes (a deep object graph renders for longer than an `int`) and how much of
  your code is traced. Measure the level you intend to run.
- **A level is not a privacy control.** `Errors` happens to write fewer values
  because it suppresses them, but suppression and redaction are different
  mechanisms with different guarantees:
  [Keep secrets out of the logs](keep-secrets-out-of-the-logs.md) is the one
  that holds at every level.
- **Nothing here changes how much your logger writes.** Each captured event is
  still offered to the logging bridge, so the `NarrativeTrace` category's own
  level is what decides log volume. Two dials.
- **A level cannot be raised retroactively.** Values not captured are gone;
  reproducing the run at `Detail` is the only way back, which is what the
  debugging recipe does.
- **Per-scope levels are not shown here.** This page sets one level for a
  whole run. Narrowing the traced scope instead — which classes are wrapped at
  all — is the other half of the cost question, and belongs to
  [Choosing an Integration](../choosing-an-integration.md).

**The skill for this:** `narrativetrace-doctor` — it reports the level a
project is actually running at, which is the first thing to check when a
trace is thinner than expected.
