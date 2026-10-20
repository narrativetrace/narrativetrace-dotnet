<!-- source: documentation/recipes/trace-in-production-without-the-cost.md blob 1000c15fdfdb | translated: 2026-10-18 | reviewed: - -->
# Trace em produção sem o custo

[English](../../recipes/trace-in-production-without-the-cost.md) | [Español](../../es/recetas/traza-en-produccion-sin-el-coste.md) | **Português** | [简体中文](../../zh-CN/实用方案/在生产环境追踪而不付出代价.md)

## O problema

> Quero isto em produção para as requisições que dão errado. Não vou pagar por
> um trace completo de toda requisição que dá certo.

Isso é uma única configuração — `TracingLevel` — e esta página é a mesma
execução vista através dela duas vezes: o checkout com pagamento recusado em
`Detail`, onde todo valor é capturado, e em `Errors`, onde só sobrevivem as
execuções que falharam.

## Configuração mínima

Um nível, definido onde quer que o seu deploy seja configurado. Os três abaixo
dizem a mesma coisa:

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

Veja o [Guia de Configuração](../../guides/pt-BR/guia-de-configuracao.md) para o
que cada nível mantém e qual fonte vence. O nível é o seletor próprio do
NarrativeTrace e fica na frente: uma chamada que ele filtra nunca passa a fazer
parte do trace, para nenhum consumidor. O nível do seu logger é um segundo
seletor, independente, que decide o que é *impresso* —
[Guia de Configuração §8, "Dois seletores, dois caminhos"](../../guides/pt-BR/guia-de-configuracao.md#8-dois-seletores-dois-caminhos)
é a versão longa, e a distinção importa aqui porque subir o nível do logger
economiza volume de log sem mudar nada na captura.

## O cenário

A execução é um checkout com pagamento recusado — `Reserve`, depois `Charge`
lançando exceção, depois `Release`, o mesmo que
[Ajude-me a depurar e corrigir este bug](ajude-me-a-depurar-e-corrigir-este-bug.md)
reproduz — reaproveitado, porque o assunto desta página é o seletor e não outro
fluxo. O fixture abaixo o executa, e executa também um checkout *bem-sucedido*,
sob o nível com que for iniciado; o build o inicia uma vez em `Detail` e outra
em `Errors`:

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

## A saída real

**Em `Detail`**, a execução recusada, todo valor capturado:

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

**Em `Errors`**, a mesma execução do mesmo código:

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

**E em `Errors`, um checkout que deu certo**: nenhum artefato. Não há nada para
embutir, e esse é o ponto — o fixture afirma as duas metades dessa alegação, a
ausência do arquivo em `Errors` e a presença dele em `Detail`, de modo que o
vazio é obra do nível e não de um caminho que nunca esteve certo.

## Como ler

- **O que o `Errors` mantém.** A execução que falhou, inteira. O `#1` lançou
  exceção, e a subárvore de um nó com erro é mantida intacta — então `Reserve`
  (`#1.1`), `Charge` (`#1.2`) e `Release` (`#1.3`) continuam todos ali,
  inclusive as duas chamadas que retornaram normalmente. Isso é deliberado: as
  chamadas em volta de uma falha são o que você precisa para diagnosticá-la, e
  descartar o irmão bem-sucedido deixaria você com uma exceção e sem história.
  Uma chamada que retornou *e* não leva a falha alguma abaixo dela é descartada,
  e uma raiz sem nenhuma falha em toda a sua subárvore é descartada por
  completo — e é por isso que o checkout bem-sucedido não escreveu nada.
- **O que o `Errors` descarta.** Todo valor de parâmetro: `customerId` e
  `quantity` são renderizados como um par vazio de crases. O vazio é supressão
  na *captura* — o valor nunca foi renderizado, então não pode ser recuperado
  depois a partir desses eventos, em nenhum nível posterior. O que sobrevive é a
  forma, os resultados e a exceção, e `cardNumber: [REDACTED]` continua oculto
  porque a ocultação é uma decisão diferente da supressão.
- **A poda acontece depois da captura, não no lugar dela.** Toda chamada foi
  interceptada e todo evento foi publicado; o construtor da árvore então montou
  a árvore e removeu o que o nível não mantém. Essa é a forma honesta do custo:
  o `Errors` é mais barato que o `Detail` porque os valores não são renderizados
  — a parte cara — e não porque chamadas deixem de ser interceptadas.
- **Só o `Off` pula a interceptação.** Em `Off`, o portão `IsActive` do proxy
  retorna antes de existir qualquer array de argumentos ou evento, e nada adiante
  pode trazer a chamada de volta. `Errors`, `Summary` e `Narrative` ainda
  interceptam toda chamada. Se a pergunta é "quanto o tracing custa neste laço
  quente", a resposta é um nível diferente para esse escopo, ou `Off`.
- **Os números.** As cifras medidas — o custo de um ciclo de entrada/saída, a
  alocação por chamada, o portão de regressão que as sustenta — estão na
  [seção Performance do README](../../../README.md#performance), ao lado da
  baseline commitada de onde vêm. Elas não são repetidas aqui, porque uma cópia
  que se move com as medições vale mais do que duas que discordam.

## O que não é capturado

- **As cifras publicadas são medições, não uma promessa.** Foram tomadas com
  BenchmarkDotNet no host que o README nomeia, uma operação de benchmark por
  chamada. Os seus números dependem do seu runtime, do seu hardware, do formato
  dos seus parâmetros (um grafo de objetos profundo leva mais tempo para
  renderizar que um `int`) e de quanto do seu código é rastreado. Meça o nível
  que você pretende usar.
- **Um nível não é um controle de privacidade.** O `Errors` escreve menos
  valores porque os suprime, mas supressão e ocultação são mecanismos diferentes
  com garantias diferentes:
  [Mantenha os segredos fora dos logs](mantenha-os-segredos-fora-dos-logs.md) é
  a que vale em todos os níveis.
- **Nada aqui muda quanto o seu logger escreve.** Cada evento capturado ainda é
  oferecido à ponte de logging, então o nível da própria categoria
  `NarrativeTrace` é o que decide o volume de log. Dois seletores.
- **Um nível não pode ser elevado retroativamente.** Valores não capturados
  estão perdidos; reproduzir a execução em `Detail` é o único caminho de volta,
  e é o que a receita de depuração faz.
- **Níveis por escopo não são mostrados aqui.** Esta página define um nível para
  a execução inteira. Restringir o escopo rastreado — quais classes são
  envolvidas, afinal — é a outra metade da questão do custo, e pertence a
  [Escolhendo uma Integração](../escolhendo-uma-integracao.md).

**A skill para isto:** `narrativetrace-doctor` — ela informa o nível em que um
projeto está de fato rodando, que é a primeira coisa a conferir quando um trace
vem mais magro do que o esperado.
