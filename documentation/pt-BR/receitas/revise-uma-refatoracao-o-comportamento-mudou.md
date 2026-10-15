<!-- source: documentation/recipes/review-a-refactoring-did-behaviour-change.md blob 8810b3b8224d | translated: 2026-10-11 | reviewed: - -->
# Revise uma refatoração: o comportamento mudou

[English](../../recipes/review-a-refactoring-did-behaviour-change.md) | [Español](../../es/recetas/revisa-una-refactorizacion-cambio-el-comportamiento.md) | **Português** | [简体中文](../../zh-CN/实用方案/审查重构行为变了吗.md)

## O problema

> Refatorei o checkout. Os testes estão verdes. O comportamento mudou?

Verde significa que nenhuma asserção percebeu. Uma refatoração que remove uma
chamada sobre a qual ninguém faz asserção fica verde e errada, e essa é a
forma comum disso: a chamada compensatória em um caminho de erro, que nenhum
teste do caminho feliz alcança.

## Configuração mínima

Além da [instalação](../../guides/pt-BR/guia-de-instalacao.md), o modo de
aprovação — uma variável de ambiente e um diretório de baselines commitadas:

```bash
NARRATIVETRACE_APPROVAL=true      # compare, don't just write
NARRATIVETRACE_APPROVED_DIR=narratives   # the default, beside the test project
```

A primeira execução de cada teste grava
`narratives/<TestClass>/<scenario>.received.nt` e falha; você o lê, executa
`dotnet tool run dotnet-narrativetrace approve` para promover todos os
arquivos revisados a `.approved.nt` e os commita. Veja
[O que incluir no commit](../o-que-incluir-no-commit.md) e o
[Guia de Configuração](../../guides/pt-BR/guia-de-configuracao.md).

## O cenário

O teste fixa o caminho de erro que uma refatoração tem mais chance de quebrar
— um cartão recusado não deve salvar nenhum pedido — e ele passa contra as
duas versões do checkout, que é todo o problema:

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

## A saída real

A baseline commitada, em
`narratives/RefactoredCheckoutFixture/a_declined_card_saves_no_order.approved.nt`:

```text
scenario: A declined card saves no order

#1 - IOrderService.PlaceOrder(customerId, sku, quantity, cardNumber) !! PaymentDeclinedException
  #1.1 - IInventoryService.Reserve(sku, quantity)
  #1.2 - IPaymentService.Charge(customerId, amountCents, card) !! PaymentDeclinedException
  #1.3 - IInventoryService.Release(sku, quantity)
```

O veredito da execução refatorada — as asserções passaram, a estrutura não:

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

E o arquivo que ele oferece para revisão, que o `approve` promoveria:

```text
scenario: A declined card saves no order

#1 - IOrderService.PlaceOrder(customerId, sku, quantity, cardNumber) !! PaymentDeclinedException
  #1.1 - IInventoryService.Reserve(sku, quantity)
  #1.2 - IPaymentService.Charge(customerId, amountCents, card) !! PaymentDeclinedException
```

(O caminho em `Received:` é o do próprio repositório: a execução que produz
esta página aponta `NARRATIVETRACE_APPROVED_DIR` para dentro de `artifacts/`,
sobre uma cópia da baseline, porque o build que escreve uma página não deve
escrever na árvore de código-fonte. No seu projeto, é `narratives/`, ao lado
da baseline.)

## Como ler

- **A primeira linha nomeia a mudança, não apenas o fato dela:**
  `-1 call IInventoryService.Release`. Uma chamada a menos, e qual.
- **O corpo do diff é a forma fixada com a linha removida marcada.** `#1`,
  `#1.1` e `#1.2` não mudaram e não estão marcados;
  `-  #1.3 - IInventoryService.Release(sku, quantity)` é o comportamento que
  saiu. Um revisor que nunca viu este código consegue dizer o que a
  refatoração fez a partir de quatro linhas.
- **As asserções se mantiveram.** A falha é da aprovação, e esse é o ponto: o
  teste afirma que nenhum pedido é salvo, e a refatoração não quebrou isso.
  Nada no teste jamais olhou para a liberação, e nada precisava.
- **Agora decida.** Se remover a liberação é intencional — o chamador assumiu
  essa tarefa, por exemplo —, execute
  `dotnet tool run dotnet-narrativetrace approve`, revise o diff no arquivo
  promovido e commite-o: a nova forma passa a ser a fixada. Se não é
  intencional, você acabou de pegar o bug que
  [Ajude-me a depurar e corrigir este bug](ajude-me-a-depurar-e-corrigir-este-bug.md)
  corrigiu, no caminho de volta. De qualquer forma, a decisão fica registrada
  no repositório, por um humano, em um arquivo que um revisor pode ler.
- **A baseline não tem valores**, então aprová-la não vaza nada e uma mudança
  nos dados de teste nunca a faz falhar: `(customerId, sku, quantity, cardNumber)`
  são nomes, `!! PaymentDeclinedException` é um resultado.

## O que não é capturado

- **Esta é uma comparação estrutural: as mesmas chamadas, na mesma ordem, com
  os mesmos resultados.** Não é uma prova de que todo valor é igual. Uma
  refatoração que cobra 7998 onde cobrava 3999, com todas as chamadas no
  lugar, passa por este portão — para isso servem as suas asserções, e o
  artefato `Detail` é onde você veria.
- **Apenas as chamadas que a integração intercepta.** Uma chamada privada
  removida, ou uma entre duas classes não envolvidas, é invisível para a
  baseline exatamente como é para o trace.
- **Um teste que não é executado não é comparado.** A aprovação protege os
  cenários que você fixou; um fluxo sem teste não tem baseline, e apagar um
  teste apaga o seu portão (o `.approved.nt` dele permanece, uma baseline que
  nada mais compara). Um `.received.nt` que ninguém promoveu é o que o
  `narrativetrace-doctor` relata, como `trap.approval-traces`.
- **Os tempos nunca fazem parte disso.** A baseline não traz durações, então
  não consegue pegar uma mudança que apenas tornou o fluxo mais lento.

**A skill para isto:** `narrativetrace-verify` — ela lê o trace estrutural
contra a intenção da mudança e fixa o fluxo como baseline com a sua
aprovação.
