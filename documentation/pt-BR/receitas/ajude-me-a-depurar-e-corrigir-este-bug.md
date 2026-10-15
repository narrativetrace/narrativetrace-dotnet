<!-- source: documentation/recipes/help-me-debug-and-fix-this-bug.md blob 245347510dd7 | translated: 2026-10-11 | reviewed: - -->
# Ajude-me a depurar e corrigir este bug

[English](../../recipes/help-me-debug-and-fix-this-bug.md) | [Español](../../es/recetas/ayudame-a-depurar-y-arreglar-este-error.md) | **Português** | [简体中文](../../zh-CN/实用方案/帮我调试并修复这个缺陷.md)

## O problema

> Ajude-me a depurar e corrigir este bug: o cartão de um cliente foi recusado
> e o teclado nunca voltou ao estoque.

O erro que a equipe tem é `PaymentDeclinedException: card declined by the
issuer`, e esse não é o bug. O bug é o que o checkout fez *depois* dele.

## Configuração mínima

Nada além da [instalação](../../guides/pt-BR/guia-de-instalacao.md) e da
integração com xUnit: o `NarrativeFixture` grava um trace por teste sem
nenhuma configuração. O último passo desta página — fixar o fluxo corrigido —
ativa o modo de aprovação, que é uma única variável de ambiente, definida para
toda a execução dos testes a partir de um arquivo `.runsettings`:

```xml
<RunSettings>
  <RunConfiguration>
    <EnvironmentVariables>
      <NARRATIVETRACE_APPROVAL>true</NARRATIVETRACE_APPROVAL>
    </EnvironmentVariables>
  </RunConfiguration>
</RunSettings>
```

Veja [O que incluir no commit](../o-que-incluir-no-commit.md) e o
[Guia de Configuração](../../guides/pt-BR/guia-de-configuracao.md).

## O cenário

Reproduza-o no menor teste que pode falhar por este motivo — um cartão
recusado deve deixar o estoque como o encontrou. O segundo teste é a mesma
reprodução contra o checkout como ele estava antes da correção, e falha de
propósito: é a execução da qual vem o primeiro trace abaixo.

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

O checkout que o segundo teste monta, antes da correção:

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

## A saída real

O que o test runner disse sobre a execução com falha:

```text
Assert.Equal() Failure: Values differ
Expected: 5
Actual:   3
```

Esse é o sintoma — duas unidades ainda retidas — e ele não diz por quê. A
narrativa da própria execução com falha diz, gravada ao lado em
`traces/DeclinedPaymentFixture/the_stock_is_released_when_the_card_is_declined_before_the_fix.md`
no diretório de saída dos testes:

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

A correção é um único `catch`: liberar o que foi reservado e deixar a recusa
se propagar como antes.

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

A mesma reprodução, agora passando, contra o checkout corrigido:

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

Com o modo de aprovação ativado, a primeira execução do teste corrigido grava
sua estrutura sem valores ao lado do teste como um `.received.nt` e falha;
`dotnet tool run dotnet-narrativetrace approve` o promove à baseline
commitada, que passa a ser a forma deste fluxo:

```text
scenario: The stock is released when the card is declined

#1 - IOrderService.PlaceOrder(customerId, sku, quantity, cardNumber) !! PaymentDeclinedException
  #1.1 - IInventoryService.Reserve(sku, quantity)
  #1.2 - IPaymentService.Charge(customerId, amountCents, card) !! PaymentDeclinedException
  #1.3 - IInventoryService.Release(sku, quantity)
```

## Como ler

No trace com falha:

- `#1 IOrderService.PlaceOrder` termina com `❌ PaymentDeclinedException` e
  sem valor de retorno: a chamada que o cliente fez não foi concluída.
- `#1.1 IInventoryService.Reserve(sku: "SKU-KB", quantity: 2)` não tem `❌`
  nem valor — um método `void` que foi concluído. Duas unidades agora estão
  retidas.
- `#1.2 IPaymentService.Charge` lançou exceção. Seus parâmetros dizem o que
  foi tentado: `amountCents: 7998` para as duas unidades, `card: [REDACTED]`.
- **Nada vem depois de `#1.2`.** O `#1` vai direto da cobrança para a própria
  falha, então as duas unidades que o `#1.1` reservou continuam retidas. Essa
  ausência é o bug, e a mensagem da exceção nunca a menciona — o trace mostra
  o fluxo inteiro e não apenas a linha que lançou a exceção, e é isso que
  torna uma omissão visível.

No trace que passa: `#1.3 IInventoryService.Release(sku: "SKU-KB", quantity: 2)`
agora está entre a cobrança e a falha. O `#1` ainda termina na recusa — isso
está correto, o cartão do cliente realmente foi recusado — e o estoque voltou.

Na baseline: o `.nt` aprovado traz as mesmas quatro chamadas com os *nomes*
dos parâmetros e nenhum valor, e é por isso que pode ser commitado e lido em
uma revisão. O `#1.3` agora faz parte da forma fixada: uma mudança posterior
que remova a liberação faz um teste que passa falhar, com um diff que nomeia
`IInventoryService.Release`, de modo que este bug não pode voltar em silêncio.
Isso é
[Revise uma refatoração: o comportamento mudou](revise-uma-refatoracao-o-comportamento-mudou.md).

## O que não é capturado

- **O trace mostra as chamadas que aconteceram, não as que deveriam ter
  acontecido.** O `Release` ausente é uma ausência que o leitor infere do
  fluxo; nada no artefato diz "falta uma chamada". O que a baseline acrescenta
  é a comparação que transforma uma ausência em uma falha de build — uma vez.
- **Uma exceção engolida dentro de código não envolvido é invisível.** Apenas
  as interfaces envolvidas aparecem. Um `try`/`catch` dentro de
  `DefaultOrderService` que engolisse a recusa e retornasse normalmente
  apareceria como uma chamada concluída.
- **O número do cartão não pode ser recuperado** a partir do trace nem do log.
  Ele é ocultado na captura, aqui pelo valor — o parâmetro se chama `card`,
  então a lista de bloqueio por nome nunca o viu, e o número é válido pelo
  algoritmo de Luhn.
- **Os tempos não são evidência de um problema de desempenho** em um teste tão
  pequeno: a primeira chamada através de um proxy em um processo carrega sua
  configuração única.

**A skill para isto:** `narrativetrace-debug` — ela reproduz o sintoma com a
menor entrada, nomeia o span em que o valor ou a chamada diverge, corrige ali
e fixa o fluxo como um teste de regressão com a sua aprovação.
