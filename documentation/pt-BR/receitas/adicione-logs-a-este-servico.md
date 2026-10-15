<!-- source: documentation/recipes/add-logging-to-this-service.md blob 3e78a8d6744b | translated: 2026-10-11 | reviewed: - -->
# Adicione logs a este serviço

[English](../../recipes/add-logging-to-this-service.md) | [Español](../../es/recetas/anade-registro-a-este-servicio.md) | **Português** | [简体中文](../../zh-CN/实用方案/给这个服务加日志.md)

## O problema

> Adicione logs a este serviço.

O serviço é um checkout. Ele reserva estoque, cobra um cartão e salva um
pedido, e não contém nenhuma instrução de log:

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

O que você realmente quer é aquilo para o qual o log é um meio: o que esta
chamada fez, com os valores com que fez, no log que sua equipe já lê. Por isso
a resposta abaixo também não escreve nenhuma instrução de log.

## Configuração mínima

Além da [instalação](../../guides/pt-BR/guia-de-instalacao.md) — o
`NarrativeTrace.Proxy` traz `Runtime` e `Core` junto —, esta página precisa de
mais dois pacotes, um para a ponte e outro para o provedor de console do
logger:

```bash
dotnet add package NarrativeTrace.Logging
dotnet add package Microsoft.Extensions.Logging.Console
```

O `LoggingNarrativeContext` envolve o contexto em que os proxies gravam e
espelha cada chamada para o `ILogger` que receber. Nada no checkout o
referencia. Em um app que já tem injeção de dependência,
`AddNarrativeLogging()` anexa a mesma ponte a partir do contêiner; veja o
[Guia de Instalação](../../guides/pt-BR/guia-de-instalacao.md).

## O cenário

Cada colaborador é envolvido uma única vez, onde é construído — o próprio
checkout por último, para que as chamadas feitas através dele sejam capturadas
antes de chegarem à implementação real. Nada mais muda:

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

O teste é um teste xUnit porque é ali que um fluxo como este já é exercitado;
a mesma ligação funciona em um `Program.cs`, em um registro de DI ou em um
pipeline de requisição. O `traceparent` fixo está ali apenas para que a saída
desta página sempre nomeie o mesmo trace — seu próprio código o deixa sem
definir.

## A saída real

O que o logger escreveu, através do provedor do próprio projeto:

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

## Como ler

- `→` abre uma chamada, `←` fecha a mais recentemente aberta, então a
  indentação é desnecessária: o primeiro `→ IOrderService.PlaceOrder` e o
  último `← returned: "ORD-C-1234-SKU-KB-2"` são a mesma chamada, e tudo o que
  está entre eles aconteceu dentro dela. Nos artefatos de trace, as mesmas
  quatro chamadas levam os ids de span `#1`, `#1.1`, `#1.2` e `#1.3` —
  [Revise um fluxo complexo antes de alterá-lo](revise-um-fluxo-complexo-antes-de-altera-lo.md)
  as lê dessa forma.
- Cada linha traz os *nomes* dos parâmetros que o método declara, e é isso que
  a torna uma frase em vez de uma tupla: `Reserve(sku: "SKU-KB", quantity: 2)`
  dispensa legenda.
- `← completed` é um método `void` retornando; `← returned: …` traz o valor.
  O `"PAY-C-1234-7998"` que `Charge` retorna é o id do próprio gateway, e
  `amountCents: 7998` é de onde vem o preço de dois teclados. O número entre
  colchetes depois da categoria é o id de evento do registro: `[1]` uma
  entrada, `[2]` um retorno com valor, `[9]` uma conclusão de método `void`.
- Dois valores sumiram. `cardNumber: [REDACTED]` foi ocultado pelo seu
  *nome* — a lista de bloqueio sempre ativa, sem atributo, sem configuração.
  `card: [REDACTED]`, uma linha abaixo, foi ocultado pelo seu *valor*: o
  parâmetro se chama `card`, que a lista de bloqueio não conhece, e o número
  que ele continha é válido pelo algoritmo de Luhn, então a regra de formato
  de valor o pegou de qualquer forma. Veja
  [Privacidade e Ocultação](../privacidade-e-ocultacao.md).
- O texto do registro é tudo o que o provedor de console imprime por padrão.
  Os ids de span e de trace viajam no escopo do logger, de modo que um provedor
  configurado para incluir escopos pode agrupar todas as linhas de um mesmo
  checkout.

A conclusão que isso sustenta: a narrativa veio dos nomes dos métodos, dos
nomes dos parâmetros e dos valores, e nenhuma instrução de log foi escrita
para obtê-la.

## O que não é capturado

- **Apenas o código envolvido é registrado.** Quatro interfaces foram
  envolvidas acima, então aparecem quatro chamadas. Uma chamada entre duas
  classes não envolvidas — ou um método privado dentro de
  `DefaultOrderService` — fica invisível aqui. Se envolver cada colaborador à
  mão não é o que você quer, `AddNarrativeTracing` envolve os serviços de
  interface que seu contêiner registra sob um namespace, sem tocar no código
  de construção: [Escolhendo uma Integração](../escolhendo-uma-integracao.md).
- **Um valor que corresponde às regras de ocultação é substituído antes de
  ser escrito,** inclusive nesta linha de log. Isso não é configurável para
  baixo, e significa que um parâmetro ocultado não pode ser recuperado a
  partir do log.
- **O nível do próprio logger continua valendo.** Estes registros são
  `Trace`; com o nível mínimo da categoria em `Information`, a saída fica
  vazia e nada avisa você.
- **Os tempos não estão nas linhas de log.** A duração de cada chamada está
  nos artefatos de trace, não aqui.

**A skill para isto:** `add-narrative-tracing` — ela instala os pacotes,
envolve a primeira classe e confere a ligação com `narrativetrace-doctor`.
