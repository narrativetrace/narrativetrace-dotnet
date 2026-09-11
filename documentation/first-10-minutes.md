# First 10 Minutes

**English** | [Español](es/primeros-10-minutos.md) | [Português](pt-BR/primeiros-10-minutos.md) | [简体中文](zh-CN/前10分钟.md)

One tiny service, one xUnit test, real output at every step. Everything
below was run for real against this repository's own packages — no
imagined output.

## 1. Add the packages

```xml
<PackageReference Include="NarrativeTrace.Core" Version="0.1.3" />
<PackageReference Include="NarrativeTrace.Runtime" Version="0.1.3" />
<PackageReference Include="NarrativeTrace.Proxy" Version="0.1.3" />
<PackageReference Include="NarrativeTrace.Testing.Xunit" Version="0.1.3" />
```

## 2. Add one service interface and implementation

```csharp
using NarrativeTrace.Core.Annotation;

public interface IOrderService
{
    string PlaceOrder(
        string customerId, string productId, int quantity,
        [NotTraced] string internalNote);
}

public sealed class OrderService : IOrderService
{
    public string PlaceOrder(
        string customerId, string productId, int quantity, string internalNote)
        => $"confirmed:{customerId}:{productId}:{quantity}";
}
```

`[NotTraced]` goes on the **interface** parameter — the proxy dispatches
against the interface's method metadata, so that's where attributes are read
from.

## 3. Add one xUnit test

```csharp
using NarrativeTrace.Proxy;
using NarrativeTrace.TestingXunit;
using Xunit;

public sealed class OrderServiceTests : IClassFixture<NarrativeFixture>
{
    private readonly NarrativeFixture _fixture;
    public OrderServiceTests(NarrativeFixture fixture) => _fixture = fixture;

    [Fact]
    public void Places_an_order()
    {
        _fixture.Run(nameof(Places_an_order), ctx =>
        {
            var orders = NarrativeTraceProxy.Create<IOrderService>(new OrderService(), ctx);
            orders.PlaceOrder("cust-1", "book-123", 2, "gift wrap");
        });

        _fixture.WriteArtifacts(nameof(OrderServiceTests), nameof(Places_an_order), failed: false);
    }
}
```

`WriteArtifacts` is the explicit step that writes files — `NarrativeFixture`
is otherwise inert so it costs nothing in a normal test run.

## 4. Run the suite

```bash
export NARRATIVETRACE_OUTPUT=true
dotnet test
```

## 5. Open the narrative

```text
narrativetrace-output/traces/OrderServiceTests/places_an_order.md
```

You'll see the call rendered with every argument except `internalNote`
(covered in step 7), the return value, and timing — generated entirely from
the method and parameter names above, with no log statement written by hand.

## 6. Rename `PlaceOrder` to `Process` and watch clarity drop

Rename the method (interface and implementation) to `Process`, keep
everything else the same, and score the same captured trace inline — no
CLI, no compiled-assembly step, just the analyzer over the trace you already
have:

```csharp
using NarrativeTrace.Clarity;

var before = ClarityAnalyzer.Analyze(_fixture.CaptureTrace());
Console.WriteLine(before.Overall); // PlaceOrder: a domain-specific verb
```

Rerun after the rename and print `Overall` again — it drops, because
`Process` is exactly the kind of generic, content-free verb the analyzer is
built to penalize. Nothing else about the call changed; only the name did.

## 7. Add `[NotTraced]` and see redaction

Reopen `places_an_order.md` from step 5: `internalNote` never appears as its
real value. To see the *marker* explicitly, render the trace to text instead
of reading the Markdown file:

```csharp
using NarrativeTrace.Core;

Console.WriteLine(IndentedTextRenderer.Render(_fixture.CaptureTrace()));
```

The `internalNote` argument renders as `[REDACTED]` — substituted before the
value was ever read, not merely hidden after the fact. Remove `[NotTraced]`
and rerun to see the real value appear in its place, so the difference is
unambiguous.

## 8. Look at the AI-safe structural artifact

Alongside the Markdown file, the same run wrote a second, value-free file:

```text
narrativetrace-output/structural/OrderServiceTests/places_an_order.nt
```

Open it: names, call hierarchy, and outcome kind only — no
`internalNote`, no `customerId`, no return value. This is the file safe to
hand to an AI tool or paste into a ticket without a redaction review, because
there is nothing in it to redact in the first place. See
[Privacy and Redaction](privacy-and-redaction.md#guarantees) for what's
verified about it, and [What to Commit](what-to-commit.md) for whether to
keep it around.

## Where to go next

- [Choosing an Integration](choosing-an-integration.md) — proxy, DI,
  ASP.NET Core, or a test framework: which one for your app.
- [Installation Guide](guides/installation.md) — every package and
  integration path in full.
- [Configuration Guide](guides/configuration.md) — tracing levels, output
  format, and every `NARRATIVETRACE_*` variable used above.
- [Troubleshooting](troubleshooting.md) — symptom → cause → fix for the
  failure modes people actually hit.
