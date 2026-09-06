# NarrativeTrace .NET — Annotations Guide

**English** | [Español](es/guia-de-atributos.md) | [Português](pt-BR/guia-de-atributos.md) | [简体中文](zh-CN/特性指南.md)

NarrativeTrace follows a **Code is the Log** philosophy: method names,
parameter names, and return values should already tell the runtime story.
Keep business logic clean and expressive first, then reach for attributes
*exceptionally* — for targeted narration, error context, redaction, or
custom value rendering.

The four narrative attributes live in `NarrativeTrace.Core.Annotation` — one
`using` for all of them, and the same package your trace model already comes
from. They are pure metadata: the `DispatchProxy` interceptor
(`NarrativeTrace.Proxy`) reads `[Narrated]`, `[OnError]` and `[NotTraced]` on
calls it intercepts, while `[NarrativeSummary]` and `[NotTraced]` are honored
wherever values are rendered. `[Traced]` is the one exception — a
`DispatchProxy`-specific marker with no JVM counterpart, so it stays in
`NarrativeTrace.Proxy`.

```csharp
using NarrativeTrace.Core.Annotation;
```

## Inventory

| Attribute | Namespace | Target | Purpose |
|---|---|---|---|
| `[NarrativeSummary]` | `NarrativeTrace.Core.Annotation` | Method or property | Preferred summary rendering for its declaring type. |
| `[Narrated]` | `NarrativeTrace.Core.Annotation` | Method | Adds human-readable narration text to a traced method. |
| `[OnError]` | `NarrativeTrace.Core.Annotation` | Method (repeatable) | Attaches contextual error text to a method. |
| `[NotTraced]` | `NarrativeTrace.Core.Annotation` | Parameter, Property, Field | Redacts a value in trace output, including members of introspected objects. |
| `[Traced]` | `NarrativeTrace.Proxy` | Method | Overrides captured parameter names positionally. |

## `[Narrated]`

Use `[Narrated]` when you want an explicit sentence in the trace instead
of relying on method name + parameters alone.

```csharp
public interface IOrderService
{
    [Narrated("Placing order of {quantity} units for customer {customerId}")]
    OrderResult PlaceOrder(string customerId, string productId, int quantity);
}
```

- Placeholders use **parameter names**: `{customerId}`.
- One level of property access is supported with **PascalCase** members:
  `{customer.Name}` reads the `Name` property of the `customer` argument.
- An unknown placeholder is left **literal** in the output (`{custmerId}`
  survives) — a built-in typo signal.

**Where it appears:** narration is rendered by `MarkdownRenderer` and (on
successful nodes) `ProseRenderer`. It is always available programmatically
as `node.Signature.Narration`.

## `[OnError]`

Attach context-specific text for exceptions. The attribute is
**repeatable**; set `ExceptionType` to scope a template to an exception
type (default `typeof(Exception)`).

```csharp
public interface IPaymentService
{
    [OnError("Payment declined for {customerId}, amount was {amount}",
             ExceptionType = typeof(PaymentDeclinedException))]
    [OnError("Temporary payment failure for {customerId}",
             ExceptionType = typeof(ExternalServiceException))]
    PaymentConfirmation Charge(
        string customerId, decimal amount, [NotTraced] string token);
}
```

How it works in .NET (matching the JVM edition):

- The template is **resolved when the exception is thrown**, using the same
  placeholder rules as `[Narrated]`, and stored as
  `node.Signature.ErrorContext`.
- Only attributes whose declared `ExceptionType` **matches the thrown
  exception** (`IsInstanceOfType`) compete; among the matches, the most
  specific declared type wins. A bare `[OnError("…")]` (implicitly
  `typeof(Exception)`) matches everything.
- If **no** declared type matches the thrown exception, no error context is
  attached. A method that returns normally never carries an error context.

**Where it appears:** on a throwing node, the resolved error context is
rendered by `MarkdownRenderer` and `ProseRenderer` (after the exception),
and is always available as `node.Signature.ErrorContext`. The structure-only
`IndentedTextRenderer` does not show it.

## `[NotTraced]`

Mark a sensitive **parameter, property, or field** so its value is redacted
everywhere the trace is rendered or exported. The name stays visible; the
value is replaced with `[REDACTED]`.

```csharp
public interface IAuthService
{
    Session Login(string username, [NotTraced] string password);
}
```

Secrets **nested inside a traced object** are covered two ways, matching the
JVM edition's redact-by-default posture:

- **Name-based deny-list** — reflective rendering redacts common sensitive
  member names (`password`, `token`, `cvv`, …) automatically via
  `RedactionPolicy` (see the
  [Configuration Guide](configuration.md#6-redaction)).
- **`[NotTraced]` on the member** — for secrets whose names match no
  pattern, annotate the property, field, or positional record parameter;
  the renderer substitutes the marker during introspection, independent of
  the deny-list (it holds even under `RedactionPolicy.Disabled`):

```csharp
public sealed record Card(string Last4, [NotTraced] string Pan);

public sealed class Payment
{
    public decimal Amount { get; init; }

    [NotTraced]
    public string ProcessorReference { get; init; } = "";
}
```

- Typical uses: passwords, tokens, secrets, card data.

**Redaction wins over a template that names it.** `[Narrated]` and
`[OnError]` resolve `{param.Property}` paths against the raw arguments, and
a path that reaches a redacted property resolves to `[REDACTED]` — never
its value, never the literal placeholder. Naming a path never weakens the
rules that apply to the value directly. If you need the value in a
narrative, remove `[NotTraced]` from the property — that removal is the
deliberate, reviewable decision, and it shows up in the diff.

## `[Traced]`

`.NET` retains parameter names in metadata by default, so — unlike the
JVM — you rarely need this. Use `[Traced]` to **override** the captured
parameter names positionally, e.g. to give a clearer domain name than the
source identifier:

```csharp
public interface IMessageBus
{
    [Traced("messageId", "payload")]
    void Publish(string id, object body);
}
```

The `id` parameter is captured as `messageId`, `body` as `payload`. Fewer
names than parameters is fine — unlisted positions keep their reflected
name.

## `[NarrativeSummary]`

Provide a short, curated summary for value rendering. Apply it to a public
**parameterless method or property**; its result (via `ToString()`) is
used whenever an instance of the type is rendered.

```csharp
public sealed record Customer(string Id, string Name, CustomerTier Tier)
{
    [NarrativeSummary]
    public string Summary => $"Customer[id={Id}, tier={Tier}]";
}
```

- `ValueRenderer` looks for a `[NarrativeSummary]` member before falling
  back to record rendering, reflective property and field introspection, or
  `ToString()`.
- The member must be public and take no parameters.
- The attribute is **inherited** — a base type's summary applies to
  derived types unless overridden.
- If invoking it throws, normal renderer fallback applies. This surfaces
  everywhere values are rendered (all renderers and exporters).

## The purity contract — side effects during tracing

NarrativeTrace may invoke a small, fixed set of code paths on your objects
while rendering a trace. Keep those members **pure** — free of side effects
such as lazy loading, access counters, cache population, or I/O — exactly as
you would for a debugger or a serializer. This matters more in .NET than on
other platforms because idiomatic C# state lives behind *properties*, and a
property getter is a method: reading it can run arbitrary code.

What is invoked during rendering:

- **Reflective introspection reads properties and fields.** A property
  getter with side effects (a counter, lazy initialization, a database
  round-trip) *will* run when an instance is rendered without a curated
  `ToString()` or `[NarrativeSummary]` member. .NET Framework Design
  Guidelines already require getters to be side-effect-free; NarrativeTrace
  relies on that convention.
- Also invoked: a custom `ToString()`, a `[NarrativeSummary]` member, and
  property paths named in `[Narrated]`/`[OnError]` templates
  (`{order.Total}`).

How the exposure is contained:

- **Bounded** — string length, collection item, and introspection depth caps
  limit how much code can run.
- **Exception-isolated** — a throwing getter or `ToString()` never fails the
  traced business call; templates fall back to the literal `{placeholder}`,
  rendering falls back to a type-name marker.
- **Eager and deterministic** — values are rendered at the call site, so any
  side effect happens once, at a predictable moment, on the calling thread.
- **Skippable** — `[NotTraced]` on a property or field means its value is
  never read at all (the `[REDACTED]` marker is emitted instead); a curated
  `ToString()`/`[NarrativeSummary]` takes precedence over introspection so
  you control exactly what is accessed; and at `TracingLevel.Off` (and for
  parameter values at `Summary`) no rendering happens whatsoever.

If a member cannot be pure, mark it `[NotTraced]` or hide it behind a
curated summary. Do not rely on tracing being disabled.

## Complete example

```csharp
public interface ITransferService
{
    [Narrated("Transferring {amount} from {fromAccountId} to {toAccountId}")]
    [OnError("Transfer rejected for source account {fromAccountId}",
             ExceptionType = typeof(InvalidOperationException))]
    TransferResult Transfer(
        string fromAccountId,
        string toAccountId,
        decimal amount,
        [NotTraced] string authToken);
}
```

One method, combining narration, targeted error context, and parameter
redaction.

## Template typos

`[Narrated]` and `[OnError]` templates use `{paramName}` and
`{paramName.Property}` placeholders. A placeholder that matches no
parameter is left literal in the resolved text (`{custmerId}` survives),
and an unknown property renders as `{propertyName}`.

**A redacted property resolves to `[REDACTED]`** — not to its value, and not
to the literal placeholder. Both halves of the redaction rule apply:
`[NotTraced]` on the property (including a positional record parameter),
and the name-based deny-list on a property no attribute covers. A
placeholder naming no parameter, or a property its owner does not declare,
is still preserved literally — that is an authoring typo, no value stands
behind it.

The xUnit fixture and NUnit base scan each captured trace after a test and
print any surviving placeholders as warnings, so a template typo surfaces
during tests:

```
WARNING: Unresolved template placeholder(s) detected:
  - OrderService.PlaceOrder: {custmerId} in narration
```

Use `TemplateWarningCollector.Collect(tree)` to run the same scan yourself.

## See also

- [Installation Guide](installation.md) — packages and integration paths
- [Configuration Guide](configuration.md) — redaction policy and levels
- [Clarity Guide](clarity.md) — scoring clean names (the first line of defense)
