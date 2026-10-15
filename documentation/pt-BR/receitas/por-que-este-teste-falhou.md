<!-- source: documentation/recipes/why-did-this-test-fail.md blob 5a64ca47c62a | translated: 2026-10-11 | reviewed: - -->
# Por que este teste falhou

[English](../../recipes/why-did-this-test-fail.md) | [Español](../../es/recetas/por-que-fallo-esta-prueba.md) | **Português** | [简体中文](../../zh-CN/实用方案/这个测试为什么失败了.md)

## O problema

> Por que este teste falhou?

Um teste do checkout está vermelho. A asserção diz o que não bateu; ela não
diz o que o código fez no caminho até ali, que é a parte de que você precisa
para saber se o errado é o código ou a expectativa.

## Configuração mínima

Nada além da [instalação](../../guides/pt-BR/guia-de-instalacao.md) e da
integração com xUnit. O `NarrativeFixture` grava uma narrativa por teste sem
nenhuma configuração — inclusive para um teste que falha, que é o caso desta
página. Chame `WriteArtifacts(..., failed: true)` a partir do caminho de falha
e os arquivos são gravados em
`TestResults/narrativetrace/traces/<TestClass>/<scenario>.md`; o
`manifest.json` ao lado lista todos os artefatos de todos os cenários quando
você sabe o nome do teste mas não o do arquivo dele. Veja
[O que incluir no commit](../o-que-incluir-no-commit.md) para o conjunto
completo.

## O cenário

O teste que falha:

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

## A saída real

O que o runner informou:

```text
Assert.Equal() Failure: Strings differ
                             ↓ (pos 18)
Expected: "ORD-C-1234-SKU-KB-1"
Actual:   "ORD-C-1234-SKU-KB-2"
                             ↑ (pos 18)
```

E o que a mesma execução gravou ao lado, em
`traces/StaleExpectationFixture/the_customer_gets_an_order_id_for_their_keyboards.md`:

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

## Como ler

Leia a narrativa de cima para baixo contra a asserção, um span por vez:

- `#1 IOrderService.PlaceOrder(... quantity: 2 ...) → "ORD-C-1234-SKU-KB-2"`.
  O valor que a asserção rejeitou está bem ali, e também a entrada que o
  produziu: a chamada pediu **dois** teclados.
- `#1.1 IInventoryService.Reserve(sku: "SKU-KB", quantity: 2)` — duas
  unidades reservadas, de acordo com a chamada.
- `#1.2 IPaymentService.Charge(..., amountCents: 7998, ...)` — 7998 são duas
  unidades a 3999, então o preço também concorda com a quantidade.
- `#1.3 IOrderRepository.Save(order: Order(... Quantity: 2 ...))` — o que foi
  armazenado concorda com o que foi pedido.
- `**Result:** FAILED` no cabeçalho é o veredito do *teste*, não do código:
  `error_count: 0` e todos os spans foram concluídos. Nada lançou exceção.

Todo span concorda com todos os outros e com a chamada que os iniciou. A
expectativa — `ORD-C-1234-SKU-KB-1` — é a única coisa na execução que diz um
teclado, então é ela que está desatualizada; o cenário contra o qual foi
escrita mudou desde então para pedir dois. A correção está no teste.

A leitura oposta é a outra receita: quando um span *discorda* da intenção —
uma quantidade que muda entre duas chamadas, uma chamada que deveria ter
vindo em seguida e não veio —, o código é que está errado, e
[Ajude-me a depurar e corrigir este bug](ajude-me-a-depurar-e-corrigir-este-bug.md)
é esse passo a passo.

## O que não é capturado

- **Apenas a execução daquele teste.** A narrativa é gravada por método de
  teste, por execução. Uma falha que acontece uma vez em vinte execuções
  precisa que uma dessas execuções seja a que gravou o arquivo; cada execução
  sobrescreve o artefato da anterior, e uma execução com falha não avança o
  último `.nt` verde.
- **A asserção não está no trace.** As duas saídas acima vêm de dois lugares
  — o runner informou a divergência, o fixture gravou o fluxo — e nada as
  correlaciona automaticamente além do nome do cenário.
- **Um valor que um teste calculou mas nunca passou a uma chamada rastreada é
  invisível.** O trace traz parâmetros e valores de retorno das chamadas
  envolvidas, não variáveis locais.
- **Os tempos de uma execução com falha não significam nada.** A execução
  deixou de ser representativa no momento em que a asserção lançou a exceção.

**A skill para isto:** `narrativetrace-debug` — ela parte do sintoma, lê o
primeiro span em que um valor diverge e o nomeia pelo id do span.
