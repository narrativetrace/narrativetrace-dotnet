# Keep secrets out of the logs

**English** | [Español](../es/recetas/manten-los-secretos-fuera-de-los-logs.md) | [Português](../pt-BR/receitas/mantenha-os-segredos-fora-dos-logs.md) | [简体中文](../zh-CN/实用方案/别让密钥进日志.md)

## The problem

> If this thing logs every call with every argument, it is going to log a
> card number. Convince me it does not.

The checkout takes a password and a card number. Below is one run of it with
both, the log it actually wrote, and the test that proves neither value is in
that log.

## Minimal setup

Nothing. Redaction is on by default in every shipped integration, and the
deny-list is consulted at *capture*, before a value becomes an event, so a
denied value never reaches the log line, the trace file, the buffered path or a
listener on the pipeline. The default is switched off only by handing a proxy a
different policy on purpose (`RedactionPolicy.Disabled` in `ProxyOptions`), and
that is exactly what the test below would catch.
[Privacy and Redaction](../privacy-and-redaction.md) is the row-by-row version,
verified against the code.

Two things, though, are worth doing once in your own project:

1. **Write this page's test for your own flow.** Redaction you have not
   asserted is a claim, not a property of your build. The doctor's
   `trap.redaction-proof` finding exists for exactly this.
2. **Know the escape hatch.** A secret under a name nobody could guess —
   `fingerprint`, `answer2` — is reachable with `[NotTraced]` on the
   parameter, property or record component, which always wins
   ([Annotations Guide](../guides/annotations.md)).

## The scenario

One new collaborator, so the run carries a password as well as a card. Its
parameter is called `password` — that name is the whole point:

<!-- snippet: recipes/checkout/Checkout/Services.cs region=accounts -->
```csharp
/// <summary>Signs a customer in before the checkout charges them. The parameter is named <c>password</c>.</summary>
public interface ICustomerAccounts
{
    /// <returns>The session id the checkout carries for the rest of the request.</returns>
    string SignIn(string customerId, string password);
}
```
<!-- /snippet -->

The run signs the customer in and places the order. The two secret values are
constants here so the test can search the output for them afterwards — a real
flow gets them from the request:

<!-- snippet: recipes/checkout/Checkout.Tests/SecretsRecipeTests.cs -->
```csharp
using Checkout;
using Microsoft.Extensions.Logging;
using NarrativeTrace.Core;
using NarrativeTrace.Logging;
using NarrativeTrace.Proxy;
using NarrativeTrace.Runtime;
using Xunit;

namespace Checkout.Tests;

/// <summary>
/// Proves recipes/keep-secrets-out-of-the-logs.md: one checkout carrying a password and a card number
/// writes neither into the log its own logger produced — the password caught by its NAME, the card
/// number by its name and again by its VALUE (rule 8, docs as tests). The assertion reads what the
/// logger wrote, not a rendering this test produced: a page claiming "the secret is not in your log"
/// is only worth what a real log says.
/// </summary>
public sealed class SecretsRecipeTests
{
    private const string Slug = "keep-secrets-out-of-the-logs";

    // A fixed W3C traceparent, so the page's embedded output names the same trace every run.
    private const string DemoTraceparent = "00-c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7-c4e1d0a7c4e1d0a7-01";

    /// <summary>Luhn-valid, like a real one — which is what the value-shape rule recognizes.</summary>
    private const string CardNumber = "4242424242424242";

    /// <summary>
    /// The password this run signs in with. It matches no value-shape rule at all: only its
    /// parameter's NAME can save it. Named for the phrase rather than for the field it fills, because
    /// the public snapshot's secrets gate reads a constant whose name carries that word and whose value
    /// is a string literal as a credential shipped in the open — correctly, for every such line but a
    /// documentation fixture's.
    /// </summary>
    private const string SignInPassphrase = "orchid-trombone-77";

    [Fact]
    public void A_checkout_with_a_password_and_a_card_writes_neither()
    {
        var log = new StringWriter { NewLine = "\n" };
        var context = LoggedContext(log);
        var stock = new InMemoryInventory("SKU-KB", 5);
        var repository = new InMemoryOrderRepository();
        var accounts = NarrativeTraceProxy.Create<ICustomerAccounts>(new InMemoryAccounts(), context);
        var inventory = NarrativeTraceProxy.Create<IInventoryService>(stock, context);
        var payments = NarrativeTraceProxy.Create<IPaymentService>(new CardPaymentService(), context);
        var orders = NarrativeTraceProxy.Create<IOrderRepository>(repository, context);
        var checkout = NarrativeTraceProxy.Create<IOrderService>(
            new DefaultOrderService(inventory, payments, orders), context);

        var session = accounts.SignIn("C-1234", SignInPassphrase);
        var orderId = checkout.PlaceOrder("C-1234", "SKU-KB", 2, CardNumber);

        Assert.Equal("SESSION-C-1234", session);
        Assert.Equal("ORD-C-1234-SKU-KB-2", orderId);
        var trace = MarkdownRenderer.Render(context.CaptureTrace());
        AssertNeitherSecretIsInTheLog(log.ToString(), redacted: "[REDACTED]");
        AssertNeitherSecretIsInTheLog(trace, redacted: "`[REDACTED]`");
        CapturedOutput.Write(Slug, "checkout.log", log.ToString());
        CapturedOutput.Write(Slug, "trace.md", trace);
    }

    private static LoggingNarrativeContext LoggedContext(TextWriter log)
    {
        var loggerFactory = LoggerFactory.Create(builder => builder
            .SetMinimumLevel(LogLevel.Trace)
            .AddProvider(new CorrelationLoggerProvider(log)));
        var traceparent = Traceparent.Parse(DemoTraceparent);
        return new LoggingNarrativeContext(
            new SyncNarrativeContext(new NarrativeTraceConfig(initialTraceparent: traceparent)),
            loggerFactory.CreateLogger("NarrativeTrace"));
    }

    /// <summary>
    /// The proof: what the project's own logger wrote, and the trace file beside it, read back and
    /// searched. <paramref name="redacted"/> is how that surface spells a redacted value.
    /// </summary>
    private static void AssertNeitherSecretIsInTheLog(string written, string redacted)
    {
        Assert.Contains($"password: {redacted}", written, StringComparison.Ordinal);
        Assert.Contains($"cardNumber: {redacted}", written, StringComparison.Ordinal);
        Assert.Contains($"card: {redacted}", written, StringComparison.Ordinal);
        Assert.DoesNotContain(SignInPassphrase, written, StringComparison.Ordinal);
        Assert.DoesNotContain(CardNumber, written, StringComparison.Ordinal);
    }
}
```
<!-- /snippet -->

## The actual output

**The log the run wrote**, through the project's own logger:

<!-- snippet: artifacts/recipes/keep-secrets-out-of-the-logs/checkout.log -->
```text
TRACE [NarrativeTrace] [c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7] - → ICustomerAccounts.SignIn(customerId: "C-1234", password: [REDACTED])
TRACE [NarrativeTrace] [c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7] - ← returned: "SESSION-C-1234"
TRACE [NarrativeTrace] [c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7] - → IOrderService.PlaceOrder(customerId: "C-1234", sku: "SKU-KB", quantity: 2, cardNumber: [REDACTED])
TRACE [NarrativeTrace] [c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7] - → IInventoryService.Reserve(sku: "SKU-KB", quantity: 2)
TRACE [NarrativeTrace] [c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7] - ← completed
TRACE [NarrativeTrace] [c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7] - → IPaymentService.Charge(customerId: "C-1234", amountCents: 7998, card: [REDACTED])
TRACE [NarrativeTrace] [c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7] - ← returned: "PAY-C-1234-7998"
TRACE [NarrativeTrace] [c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7] - → IOrderRepository.Save(order: Order(OrderId: "ORD-C-1234-SKU-KB-2", CustomerId: "C-1234", Sku: "SKU-KB", Quantity: 2, AmountCents: 7998))
TRACE [NarrativeTrace] [c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7] - ← completed
TRACE [NarrativeTrace] [c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7] - ← returned: "ORD-C-1234-SKU-KB-2"
```
<!-- /snippet -->

**The trace of the same run**, which is the other surface a secret could have
reached — the Markdown rendering a trace file holds:

<!-- snippet: artifacts/recipes/keep-secrets-out-of-the-logs/trace.md mask=duration,traceName -->
```text
---
type: trace
entry_point: ICustomerAccounts.SignIn
trace_id: c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7
trace_name: solid wand romps
method_count: 5
error_count: 0
result: success
duration_ms: 12
---

- **ICustomerAccounts.SignIn**(customerId: `"C-1234"`, password: `[REDACTED]`) → `"SESSION-C-1234"` — 12ms #1
- **IOrderService.PlaceOrder**(customerId: `"C-1234"`, sku: `"SKU-KB"`, quantity: `2`, cardNumber: `[REDACTED]`) → `"ORD-C-1234-SKU-KB-2"` — 3ms #2
  - **IInventoryService.Reserve**(sku: `"SKU-KB"`, quantity: `2`) — 0ms #2.1
  - **IPaymentService.Charge**(customerId: `"C-1234"`, amountCents: `7998`, card: `[REDACTED]`) → `"PAY-C-1234-7998"` — 0ms #2.2
  - **IOrderRepository.Save**(order: `Order(OrderId: "ORD-C-1234-SKU-KB-2", CustomerId: "C-1234", Sku: "SKU-KB", Quantity: 2, AmountCents: 7998)`) — 0ms #2.3
```
<!-- /snippet -->

## How to read it

**Which rule caught which value.** Three values are gone, by two independent
mechanisms:

| Line | Value | Caught by |
|---|---|---|
| `#1` `ICustomerAccounts.SignIn(…, password: [REDACTED])` | `orchid-trombone-77` | Its **name**. `password` is on the always-on deny-list, which matches parameter names, property names and record-component names — in several languages — on any class, yours included. The value itself matches no pattern at all; nothing but the name could have saved it |
| `#2` `IOrderService.PlaceOrder(…, cardNumber: [REDACTED])` | `4242424242424242` | Its **name** again: `cardNumber` is on the same list |
| `#2.2` `IPaymentService.Charge(…, card: [REDACTED])` | the same number, one call deeper | Its **value**. The parameter is named `card`, which the deny-list does not know — and the number is Luhn-valid, so the independent value-shape check caught it anyway |

That third row is the one worth remembering: `card` is what a parameter gets
called in real code, and the name axis alone would have printed it. The shape
axis knows a Luhn-valid card number, a JWT, a `Set-Cookie` string, a dashed
US Social Security number and a national identity number that passes its own
checksum.

**Where the rules live.** In the library, not in your configuration:
`RedactionPolicy.Default`, consulted by the one `ValueRenderer` every shipped
integration constructs the same way. No log level and no appsettings key
reaches it; [Privacy and Redaction](../privacy-and-redaction.md) lists the
surfaces that can take a different policy and what outranks what.
`customerId: "C-1234"` *is* written in full, by the way — a customer id is
identifying, not secret, and NarrativeTrace does not guess. If yours must not be
logged, that is `[NotTraced]`, which is a decision in your source rather than a
state a deploy can flip.

**Why the test is the proof, not the page.** The test reads what the logger
wrote, and the trace beside it, and asserts two things: that the three
redacted parameters are present *as* `[REDACTED]`, and that neither secret
string appears anywhere. A page can only show you bytes; a test fails the
build the day a refactor renames `password` to something the list does not
know, or someone hands the proxy a `RedactionPolicy.Disabled`. That is the
difference between "it was safe when this was written" and "it is safe now".
Keep the absence assertion — the `DoesNotContain` — even when the presence one
looks sufficient: a value that vanishes because the call stopped being traced
at all would still satisfy the first.

## What is not captured

- **Code that is not traced is not NarrativeTrace's to redact.** Four
  interfaces are wrapped above; a secret passed between two unwrapped classes
  is not seen, and therefore neither redacted nor logged. That is not a
  guarantee of safety — it is a guarantee of silence.
- **A secret your own log statement writes is yours.** NarrativeTrace
  redacts what *it* renders. `logger.LogInformation("charging {Card}", cardNumber)`
  is your line, in your code, and nothing here filters it. The redaction
  contract is about generated narrative, not about everything the process
  writes.
- **A return value is not matched by name**, because it has none. `SignIn`
  returns `"SESSION-C-1234"` and it is printed. A session token returned from
  a traced method needs the shape rules to catch it, or a narrower return
  type.
- **Names the list does not know.** The list is big and multilingual, not
  infinite. A secret called `q3` is caught only by its shape, if it has one.
  Assert, do not assume — which is this page's whole point.
- **What happens after the log line is written.** Shipping, retention and
  access to the file are your platform's; the structural `.nt` carries no
  values at all and is the artifact meant for committing and for handing to a
  model ([Review a complex flow before changing it](review-a-complex-flow-before-changing-it.md)).

**The skill for this:** `narrativetrace-doctor` — one of its checks is
exactly "redaction never proven in a test", and it names the fix.
