<!-- source: documentation/recipes/review-a-complex-flow-before-changing-it.md blob f318f1452900 | translated: 2026-10-11 | reviewed: - -->
# Revise um fluxo complexo antes de alterá-lo

[English](../../recipes/review-a-complex-flow-before-changing-it.md) | [Español](../../es/recetas/revisa-un-flujo-complejo-antes-de-cambiarlo.md) | **Português** | [简体中文](../../zh-CN/实用方案/修改复杂流程前先审查它.md)

## O problema

> Antes de alterar o checkout, diga-me o que ele realmente faz.

Ler o código-fonte diz o que o código *pode* fazer. Uma execução diz o que ele
*faz* com estas entradas — em que ordem, com quais valores, e onde o fluxo sai
do código que você está prestes a editar. Esta página deliberadamente nunca
mostra a implementação: esse é o ponto de ler uma execução.

## Configuração mínima

Nada além da [instalação](../../guides/pt-BR/guia-de-instalacao.md) e da
integração com xUnit para os artefatos de trace. As linhas de log precisam da
ponte de logging, ligada como em
[Adicione logs a este serviço](adicione-logs-a-este-servico.md). O nível de
captura é uma única variável de ambiente; `Summary` é a que esta página usa ao
lado do `Detail` padrão:

```bash
NARRATIVETRACE_LEVEL=Summary   # Off | Errors | Summary | Narrative | Detail
```

Veja o [Guia de Configuração](../../guides/pt-BR/guia-de-configuracao.md) para
saber o que cada nível mantém.

## O cenário

Uma execução representativa do fluxo — um teste existente é a opção mais
barata de emprestar, e este é esse teste:

```csharp
using Checkout;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using NarrativeTrace.Core;
using NarrativeTrace.Logging;
using NarrativeTrace.Proxy;
using NarrativeTrace.TestingXunit;
using Xunit;

namespace Checkout.Fixtures;

/// <summary>
/// One successful checkout, the flow recipes/review-a-complex-flow-before-changing-it.md is read
/// before changing anything. A fixture because the recipe runs it twice under different tracing
/// levels and keeps the log lines the run wrote beside the trace artifacts. It passes either way —
/// nothing here fails by design.
/// </summary>
public sealed class CheckoutReviewFixture : IDisposable
{
    // A fixed W3C traceparent, so the page's embedded artifacts name the same trace every run.
    private const string DemoTraceparent = "00-c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7-c4e1d0a7c4e1d0a7-01";

    // One fixture per test: xUnit builds a fresh class instance for each test, so no test sees another's spans.
    private readonly NarrativeFixture _trace = new();

    public CheckoutReviewFixture()
    {
        _trace.Context.AdoptTraceparent(Traceparent.Parse(DemoTraceparent));
    }

    public void Dispose() => _trace.Dispose();

    [Fact]
    public void A_customer_buys_two_keyboards()
    {
        var log = new StringWriter { NewLine = "\n" };
        var original = Console.Out;
        Console.SetOut(log);
        try
        {
            RunCheckoutThroughALogger();
        }
        finally
        {
            Console.SetOut(original);
        }

        var outputDirectory = Environment.GetEnvironmentVariable("NARRATIVETRACE_OUTPUT_DIR");
        Assert.False(string.IsNullOrEmpty(outputDirectory), "the recipe run names its output directory");
        File.WriteAllText(Path.Combine(outputDirectory, "checkout.log"), log.ToString());
        _trace.WriteArtifacts(nameof(CheckoutReviewFixture), nameof(A_customer_buys_two_keyboards), failed: false);
    }

    private void RunCheckoutThroughALogger()
    {
        using var loggerFactory = LoggerFactory.Create(builder =>
            builder.SetMinimumLevel(LogLevel.Trace).AddSimpleConsole(
                options => options.ColorBehavior = LoggerColorBehavior.Disabled));
        var context = new LoggingNarrativeContext(_trace.Context, loggerFactory.CreateLogger("NarrativeTrace"));
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

## A saída real

**Em `Detail`** (o padrão), `traces/CheckoutReviewFixture/a_customer_buys_two_keyboards.md`
no diretório de saída dos testes:

```text
---
type: trace
scenario: A customer buys two keyboards
entry_point: IOrderService.PlaceOrder
trace_id: c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7
trace_name: solid wand romps
method_count: 4
error_count: 0
result: success
duration_ms: 21
---

## Trace: solid wand romps — IOrderService.PlaceOrder

**Scenario:** A customer buys two keyboards
**Duration:** 21ms | **Result:** PASSED

### Call Flow

- **IOrderService.PlaceOrder**(customerId: `"C-1234"`, sku: `"SKU-KB"`, quantity: `2`, cardNumber: `[REDACTED]`) → `"ORD-C-1234-SKU-KB-2"` — 21ms #1
  - **IInventoryService.Reserve**(sku: `"SKU-KB"`, quantity: `2`) — 0ms #1.1
  - **IPaymentService.Charge**(customerId: `"C-1234"`, amountCents: `7998`, card: `[REDACTED]`) → `"PAY-C-1234-7998"` — 6ms #1.2
  - **IOrderRepository.Save**(order: `Order(OrderId: "ORD-C-1234-SKU-KB-2", CustomerId: "C-1234", Sku: "SKU-KB", Quantity: 2, AmountCents: 7998)`) — 0ms #1.3
```

**Em `Summary`**, a mesma execução do mesmo teste:

```text
---
type: trace
scenario: A customer buys two keyboards
entry_point: IOrderService.PlaceOrder
trace_id: c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7
trace_name: solid wand romps
method_count: 4
error_count: 0
result: success
duration_ms: 24
---

## Trace: solid wand romps — IOrderService.PlaceOrder

**Scenario:** A customer buys two keyboards
**Duration:** 24ms | **Result:** PASSED

### Call Flow

- **IOrderService.PlaceOrder**(customerId: ``, sku: ``, quantity: ``, cardNumber: `[REDACTED]`) → `"ORD-C-1234-SKU-KB-2"` — 24ms #1
  - **IInventoryService.Reserve**(sku: ``, quantity: ``) — 0ms #1.1
  - **IPaymentService.Charge**(customerId: ``, amountCents: ``, card: ``) → `"PAY-C-1234-7998"` — 10ms #1.2
  - **IOrderRepository.Save**(order: ``) — 0ms #1.3
```

**O log que o mesmo fluxo gravou** através do logger do próprio projeto, com
todos os valores:

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

**O trace estrutural dessa execução**, `structural/CheckoutReviewFixture/a_customer_buys_two_keyboards.nt`
no diretório de saída dos testes:

```text
scenario: A customer buys two keyboards

#1 - IOrderService.PlaceOrder(customerId, sku, quantity, cardNumber) → value
  #1.1 - IInventoryService.Reserve(sku, quantity)
  #1.2 - IPaymentService.Charge(customerId, amountCents, card) → value
  #1.3 - IOrderRepository.Save(order)
```

## Como ler

**O fluxo, a partir do artefato `Detail`.** `#1` é a chamada que o teste fez
e a única raiz: tudo o que está indentado sob ela aconteceu dentro dela, na
ordem impressa. `#1.1` reserva duas unidades antes de qualquer cobrança;
`#1.2` cobra 7998 — duas unidades a 3999, então o preço é derivado da
quantidade e não passado como argumento; `#1.3` salva um pedido que já traz o
id que `#1` retorna. Lido como uma frase: este checkout reserva primeiro,
cobra em segundo, salva em terceiro, e o id do pedido é gerado antes do save.
Uma mudança que cobre antes de reservar, ou salve antes de cobrar, é uma
mudança *nisto*, e agora você sabe disso sem confiar em um comentário.

**Para que serve cada nível.** `Detail` traz os valores dos parâmetros, e é o
que você lê enquanto trabalha no fluxo. Em `Summary`, todo valor de parâmetro
está vazio — nunca foi capturado, o que é diferente de ocultado na
renderização — e apenas a raiz e as chamadas folha são mantidas; este fluxo
tem dois níveis de profundidade, então nenhum quadro é intermediário e as
mesmas quatro chamadas aparecem. Um fluxo mais profundo perde o meio. Os
valores de retorno sobrevivem nos dois níveis: o retorno é a carga útil de uma
narrativa. (`card:` fica vazio em vez de `[REDACTED]` aqui pelo mesmo motivo:
um valor que nunca foi capturado não tem o que ocultar. `cardNumber` continua
mostrando `[REDACTED]`, porque a lista de bloqueio por nome se aplica no
momento da captura.) Use `Summary` onde a forma e os resultados são o que
você precisa e os valores são custo — uma execução de CI, um teste de longa
duração — e `Off`/`Errors` em produção até precisar de mais.

**Qual saída um humano lê e qual um agente lê.** As linhas de log são para o
humano de plantão: elas se intercalam com tudo o mais que o serviço registra e
trazem os valores. O `.nt` é para o agente e para o revisor: **sem valores por
construção** — compare-o com a linha de log acima e a diferença não é que os
valores foram substituídos, mas que nunca foram escritos.
`(customerId, sku, quantity, cardNumber)` são os *nomes* dos parâmetros;
`→ value` diz que um valor foi retornado sem dizer qual. É isso que o torna
seguro para commitar, para comparar em uma revisão e para entregar a um
modelo: ele não pode vazar um id de cliente e não muda quando os dados mudam —
apenas quando as *chamadas* mudam. Fixá-lo é
[Revise uma refatoração: o comportamento mudou](revise-uma-refatoracao-o-comportamento-mudou.md).

## O que não é capturado

- **Chamadas dentro de classes não envolvidas.** Quatro interfaces foram
  envolvidas; uma chamada entre duas classes por trás delas, ou um método
  privado, nunca aparece. Um fluxo que parece raso pode ser profundo.
- **O que a integração escolhida não intercepta.** O `DispatchProxy` vê
  apenas as chamadas feitas *através* da referência envolvida: uma chamada
  interna que uma classe faz a si mesma (`this.Helper()`) é invisível para
  ele. [Escolhendo uma Integração](../escolhendo-uma-integracao.md) traz o
  trade-off de cada caminho.
- **Trabalho que saiu da thread.** Uma chamada entregue a outra thread sem o
  contexto não vai além; o trace termina onde a thread terminou.
- **Valores que a lista de bloqueio ou a regra de formato de valor pegaram**,
  aqui `cardNumber` e `card`, aparecem como `[REDACTED]` tanto no log quanto
  no artefato `Detail`, e estão ausentes por construção do `.nt`.
- **Uma execução, não o contrato.** Outra entrada toma outro caminho — um sku
  sem estoque nunca chega a `#1.2`.

**A skill para isto:** `narrativetrace-verify` — ela escreve a intenção
primeiro, executa o menor caminho real e lê o trace sem valores contra essa
intenção antes de qualquer coisa ser dada como concluída.
