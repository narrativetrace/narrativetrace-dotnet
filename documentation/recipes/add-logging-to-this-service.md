# Add logging to this service

**English** | [Español](../es/recetas/anade-registro-a-este-servicio.md) | [Português](../pt-BR/receitas/adicione-logs-a-este-servico.md) | [简体中文](../zh-CN/实用方案/给这个服务加日志.md)

## The problem

> Add logging to this service.

The service is a checkout. It reserves stock, charges a card and saves an
order, and it contains no log statement at all:

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

What you actually want is the thing logging is a means to: what this call
did, with the values it did it with, in the log your team already reads. So
the answer below writes no log statement either.

## Minimal setup

Beyond the [install](../guides/installation.md) — `NarrativeTrace.Proxy`
brings `Runtime` and `Core` with it — this page needs two more packages, one
for the bridge and one for the logger's console provider:

```bash
dotnet add package NarrativeTrace.Logging
dotnet add package Microsoft.Extensions.Logging.Console
```

`LoggingNarrativeContext` wraps the context the proxies record into and
mirrors every call to whatever `ILogger` it is given. Nothing in the checkout
references it. In an app that already has dependency injection,
`AddNarrativeLogging()` attaches the same bridge from the container instead;
see the [Installation Guide](../guides/installation.md).

## The scenario

Each collaborator is wrapped once, where it is constructed — the checkout
itself last, so calls through it are captured before they reach the real
implementation. Nothing else changes:

<!-- snippet: recipes/checkout/Checkout.Tests/AddLoggingRecipeTests.cs -->
```csharp
using Checkout;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using NarrativeTrace.Core;
using NarrativeTrace.Logging;
using NarrativeTrace.Proxy;
using NarrativeTrace.Runtime;
using Xunit;

namespace Checkout.Tests;

/// <summary>
/// Proves recipes/add-logging-to-this-service.md: the checkout, with no log statement anywhere in
/// it, logs every call through the project's own <c>ILogger</c> once its collaborators are wrapped
///. The page's output block is what the logger wrote, not a rendering this
/// test produces — that is the whole claim of the page, so the evidence has to come from the logger.
/// </summary>
public sealed class AddLoggingRecipeTests
{
    // A fixed W3C traceparent, seeded through NarrativeTraceConfig so this page's embedded output
    // always names the same trace. A real run generates a random trace id every time.
    private const string DemoTraceparent = "00-c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7-c4e1d0a7c4e1d0a7-01";

    [Fact]
    public void A_customer_checks_out()
    {
        var log = new StringWriter { NewLine = "\n" };
        var original = Console.Out;
        Console.SetOut(log);
        try
        {
            CheckOut();
        }
        finally
        {
            Console.SetOut(original);
        }

        Assert.Contains("→ IOrderService.PlaceOrder(customerId: \"C-1234\"", log.ToString(), StringComparison.Ordinal);
        CapturedOutput.Write("add-logging-to-this-service", "checkout.log", log.ToString());
    }

    private static void CheckOut()
    {
        using var loggerFactory = LoggerFactory.Create(builder => builder
            .SetMinimumLevel(LogLevel.Trace)
            .AddSimpleConsole(options => options.ColorBehavior = LoggerColorBehavior.Disabled));
        var context = new LoggingNarrativeContext(
            new SyncNarrativeContext(new NarrativeTraceConfig(initialTraceparent: Traceparent.Parse(DemoTraceparent))),
            loggerFactory.CreateLogger("NarrativeTrace"));

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

The test is an xUnit test because that is where a flow like this is already
exercised; the same wiring works in a `Program.cs`, a DI registration or a
request pipeline. The fixed `traceparent` is there only so this page's output
always names the same trace — your own code leaves it unset.

## The actual output

What the logger wrote, through the project's own provider:

<!-- snippet: artifacts/recipes/add-logging-to-this-service/checkout.log -->
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

## How to read it

- `→` opens a call, `←` closes the one most recently opened, so indentation
  is unnecessary: the first `→ IOrderService.PlaceOrder` and the last
  `← returned: "ORD-C-1234-SKU-KB-2"` are the same call, and everything
  between them happened inside it. In the trace artifacts the same four calls
  carry the span ids `#1`, `#1.1`, `#1.2` and `#1.3` —
  [Review a complex flow before changing it](review-a-complex-flow-before-changing-it.md)
  reads them that way.
- Every line carries the parameter *names* the method declares, which is what
  makes it a sentence rather than a tuple: `Reserve(sku: "SKU-KB", quantity: 2)`
  needs no legend.
- `← completed` is a `void` method returning; `← returned: …` carries the
  value. `Charge` returning `"PAY-C-1234-7998"` is the gateway's own id, and
  `amountCents: 7998` is where the price of two keyboards comes from. The
  number in brackets after the category is the record's event id:
  `[1]` an entry, `[2]` a return with a value, `[9]` a `void` completion.
- Two values are gone. `cardNumber: [REDACTED]` was hidden by its *name* —
  the always-on deny-list, no attribute, no configuration. `card: [REDACTED]`
  one line down was hidden by its *value*: the parameter is named `card`,
  which the deny-list does not know, and the number it held is Luhn-valid, so
  the value-shape rule caught it anyway. See
  [Privacy and Redaction](../privacy-and-redaction.md).
- The record's text is all the console provider prints by default. The span
  and trace ids travel in the logger's scope, so a provider configured to
  include scopes can group every line of one checkout.

The conclusion this supports: the narrative came from the method names, the
parameter names and the values, and not one log statement was written to get
it.

## What is not captured

- **Only wrapped code is logged.** Four interfaces were wrapped above, so
  four calls appear. A call between two unwrapped classes — or a private
  method inside `DefaultOrderService` — is invisible here. If wrapping every
  collaborator by hand is not what you want, `AddNarrativeTracing` wraps the
  interface services your container registers under a namespace, without
  touching construction code:
  [Choosing an Integration](../choosing-an-integration.md).
- **A value matching the redaction rules is replaced before it is written,**
  including in this log line. That is not configurable downward, and it means
  a redacted parameter cannot be recovered from the log.
- **The logger's own level still applies.** These records are `Trace`; with
  the category's minimum level at `Information` the output is empty and
  nothing warns you.
- **Timings are not in the log lines.** Each call's duration is in the trace
  artifacts, not here.

**The skill for this:** `add-narrative-tracing` — it installs the packages,
wraps the first class, and checks the wiring with `narrativetrace-doctor`.
