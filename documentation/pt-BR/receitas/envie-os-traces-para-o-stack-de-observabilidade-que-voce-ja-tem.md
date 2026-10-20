<!-- source: documentation/recipes/send-traces-to-the-observability-stack-you-already-run.md blob aa4796e4382b | translated: 2026-10-18 | reviewed: - -->
# Envie os traces para o stack de observabilidade que você já tem

[English](../../recipes/send-traces-to-the-observability-stack-you-already-run.md) | [Español](../../es/recetas/envia-las-trazas-al-stack-de-observabilidad-que-ya-tienes.md) | **Português** | [简体中文](../../zh-CN/实用方案/把追踪发到你已有的可观测性栈.md)

## O problema

> Já temos tracing e dashboards. Não vou adotar um segundo lugar para olhar —
> isto pode entrar no stack que a gente roda?

Pode: uma execução, dois consumidores. As linhas de narrativa continuam indo
para o seu logger, e os mesmos eventos viram spans do OpenTelemetry na saída.
Abaixo está um checkout com as duas saídas lado a lado.

## Configuração mínima

`NarrativeTrace.Observability`, que traz junto o pacote do SDK do
OpenTelemetry:

```bash
dotnet add package NarrativeTrace.Observability
```

Há duas formas de entrar, e a diferença é *quando* um span existe:

| Modo | Tipo | Use quando |
|---|---|---|
| Ao vivo, enquanto os métodos rodam | `OtelTraceEventListener` | Você quer spans no seu collector mesmo que o processo morra no meio da requisição — esta página |
| Em lote, depois do fato | `TraceActivityExporter` | Você quer os spans de uma árvore de trace já concluída, exportados uma vez, ao menor custo de execução: `TraceActivityExporter.Export(context.CaptureTrace())` |

O exportador em lote emite por um `ActivitySource` chamado `NarrativeTrace`; o
listener ao vivo, pela fonte que você der a ele. Esta página dá à sua fonte o
mesmo nome, de modo que um único `AddSource("NarrativeTrace")` e o seu
exportador no builder do OpenTelemetry cobrem os dois. Em um deploy, o listener
ao vivo é inscrito no pipeline que o seu host constrói — um
`BufferedEventConsumer.Subscribe(listener.OnEvent)` — ou colocado no slot
síncrono, como abaixo. Um exportador em memória em um teste não precisa de host
nem de SDK global, então o cenário abaixo compõe o pipeline explicitamente: esse
também é o retrato mais claro do que um host construiria.

## O cenário

O pipeline é a única ideia nova: o listener de narração que você já tem, com o
listener do OpenTelemetry composto no mesmo caminho síncrono. O checkout em si é
envolvido exatamente como em todas as outras páginas:

```csharp
using System.Diagnostics;
using System.Globalization;
using System.Text;
using Checkout;
using Microsoft.Extensions.Logging;
using NarrativeTrace.Core;
using NarrativeTrace.Logging;
using NarrativeTrace.Observability;
using NarrativeTrace.Proxy;
using NarrativeTrace.Runtime;
using OpenTelemetry;
using OpenTelemetry.Trace;
using Xunit;

namespace Checkout.Tests;

/// <summary>
/// Proves recipes/send-traces-to-the-observability-stack-you-already-run.md: ONE run of the checkout
/// writes the log lines it always wrote AND an OpenTelemetry span per traced call, with the parentage
/// and the attributes the page shows — and without the redacted values (rule 8, docs as tests).
/// The pipeline is composed explicitly — narration on the synchronous path, the OpenTelemetry listener
/// beside it — because an in-memory exporter cannot be discovered and a global SDK leaks between tests.
/// The exported spans are written out by <see cref="ExportedSpans"/>, naming and sorting everything it
/// prints, so one run's dump is byte-identical to the next: a raw span dump carries timestamps and
/// OpenTelemetry's own generated ids and could never be embedded.
/// </summary>
public sealed class ObservabilityRecipeTests
{
    private const string Slug = "send-traces-to-the-observability-stack-you-already-run";
    private const string SourceName = "NarrativeTrace";

    // A fixed W3C traceparent, so the page's embedded artifacts name the same trace every run.
    private const string DemoTraceparent = "00-c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7-c4e1d0a7c4e1d0a7-01";

    [Fact]
    public void One_run_narrates_and_exports()
    {
        var log = new StringWriter { NewLine = "\n" };
        var spans = new List<Activity>();
        var context = ExportingContext(log, spans, TracingLevel.Detail, out var cleanup);
        using var _ = cleanup;

        var orderId = CheckOut(context);

        Assert.Equal("ORD-C-1234-SKU-KB-2", orderId);
        AssertOneSpanPerTracedCall(spans);
        AssertNoRedactedValueWasExported(spans);
        CapturedOutput.Write(Slug, "checkout.log", log.ToString());
        CapturedOutput.Write(Slug, "exported-spans.txt", ExportedSpans(spans));
    }

    [Fact]
    public void At_summary_the_same_four_spans_leave_without_parameter_attributes()
    {
        var spans = new List<Activity>();
        var context = ExportingContext(new StringWriter(), spans, TracingLevel.Summary, out var cleanup);
        using var _ = cleanup;

        CheckOut(context);

        Assert.Equal(4, spans.Count);
        Assert.DoesNotContain(spans.SelectMany(span => span.TagObjects), tag => tag.Key.StartsWith("narrative.param.", StringComparison.Ordinal));
    }

    private static SyncNarrativeContext ExportingContext(
        TextWriter log, List<Activity> spans, TracingLevel level, out IDisposable cleanup)
    {
        var loggerFactory = LoggerFactory.Create(builder => builder
            .SetMinimumLevel(LogLevel.Trace)
            .AddProvider(new CorrelationLoggerProvider(log)));
        var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(SourceName)
            .AddProcessor(new SimpleActivityExportProcessor(new InMemoryExporter(spans)))
            .Build();

        // The pipeline this page is about: the narration you already have, and the OpenTelemetry
        // listener beside it on the same synchronous path. What CaptureTrace() reads is untouched.
        var narration = new LoggingTraceEventListener(loggerFactory.CreateLogger("NarrativeTrace"));
        var source = new ActivitySource(SourceName);
        var exporter = new OtelTraceEventListener(source);
        var pipeline = new DualPathPipeline(traceEvent =>
        {
            narration.OnEvent(traceEvent);
            exporter.OnEvent(traceEvent);
        });
        cleanup = new Disposables(tracerProvider, source, loggerFactory);
        return new SyncNarrativeContext(
            new NarrativeTraceConfig(level, initialTraceparent: Traceparent.Parse(DemoTraceparent)), pipeline);
    }

    private static string CheckOut(INarrativeContext context)
    {
        var inventory = NarrativeTraceProxy.Create<IInventoryService>(new InMemoryInventory("SKU-KB", 5), context);
        var payments = NarrativeTraceProxy.Create<IPaymentService>(new CardPaymentService(), context);
        var orders = NarrativeTraceProxy.Create<IOrderRepository>(new InMemoryOrderRepository(), context);
        var checkout = NarrativeTraceProxy.Create<IOrderService>(
            new DefaultOrderService(inventory, payments, orders), context);
        return checkout.PlaceOrder("C-1234", "SKU-KB", 2, "4242424242424242");
    }

    /// <summary>Four traced calls, four spans, ended innermost first — and the tree is reconstructed.</summary>
    private static void AssertOneSpanPerTracedCall(List<Activity> exported)
    {
        Assert.Equal(
            ["IInventoryService.Reserve", "IPaymentService.Charge", "IOrderRepository.Save", "IOrderService.PlaceOrder"],
            exported.Select(span => span.DisplayName));
        var root = Named(exported, "IOrderService.PlaceOrder");
        Assert.Equal(root.SpanId, Named(exported, "IPaymentService.Charge").ParentSpanId);
        Assert.DoesNotContain(exported, span => span.SpanId == root.ParentSpanId);
    }

    /// <summary>What the deny-list caught never reaches the exporter either — there is no attribute for it.</summary>
    private static void AssertNoRedactedValueWasExported(List<Activity> exported)
    {
        var tags = exported.SelectMany(span => span.TagObjects).ToList();
        Assert.DoesNotContain(tags, tag => tag.Key is "narrative.param.cardNumber" or "narrative.param.card");
        Assert.DoesNotContain(
            tags, tag => Convert.ToString(tag.Value, CultureInfo.InvariantCulture)!.Contains("4242424242424242", StringComparison.Ordinal));
    }

    private static Activity Named(List<Activity> exported, string name) =>
        exported.Single(span => span.DisplayName == name);

    /// <summary>The dump the page embeds: span name, parent, and every attribute, sorted by key.</summary>
    private static string ExportedSpans(List<Activity> exported)
    {
        var text = new StringBuilder();
        foreach (var span in exported)
        {
            var parent = exported.FirstOrDefault(other => other.SpanId == span.ParentSpanId)?.DisplayName;
            text.Append("span: ").Append(span.DisplayName).Append('\n');
            text.Append("  parent: ").Append(parent ?? "(none — this is the root)").Append('\n');
            foreach (var tag in span.TagObjects.OrderBy(tag => tag.Key, StringComparer.Ordinal))
            {
                text.Append("    ").Append(tag.Key).Append(": ")
                    .Append(Convert.ToString(tag.Value, CultureInfo.InvariantCulture)).Append('\n');
            }
        }

        return text.ToString();
    }

    private sealed class Disposables(params IDisposable[] items) : IDisposable
    {
        public void Dispose()
        {
            foreach (var item in items)
            {
                item.Dispose();
            }
        }
    }

    private sealed class InMemoryExporter(List<Activity> spans) : BaseExporter<Activity>
    {
        public override ExportResult Export(in Batch<Activity> batch)
        {
            foreach (var activity in batch)
            {
                spans.Add(activity);
            }

            return ExportResult.Success;
        }
    }
}
```

## A saída real

**As linhas de log**, inalteradas por tudo isso — a mesma narração que
[Adicione logs a este serviço](adicione-logs-a-este-servico.md) produz:

```text
TRACE [NarrativeTrace] [c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7] - → IOrderService.PlaceOrder(customerId: "C-1234", sku: "SKU-KB", quantity: 2, cardNumber: [REDACTED])
TRACE [NarrativeTrace] [c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7] - → IInventoryService.Reserve(sku: "SKU-KB", quantity: 2)
TRACE [NarrativeTrace] [c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7] - ← completed
TRACE [NarrativeTrace] [c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7] - → IPaymentService.Charge(customerId: "C-1234", amountCents: 7998, card: [REDACTED])
TRACE [NarrativeTrace] [c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7] - ← returned: "PAY-C-1234-7998"
TRACE [NarrativeTrace] [c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7] - → IOrderRepository.Save(order: Order(OrderId: "ORD-C-1234-SKU-KB-2", CustomerId: "C-1234", Sku: "SKU-KB", Quantity: 2, AmountCents: 7998))
TRACE [NarrativeTrace] [c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7] - ← completed
TRACE [NarrativeTrace] [c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7] - ← returned: "ORD-C-1234-SKU-KB-2"
```

**Os spans que saíram da mesma execução**, como o exportador em memória os
recebeu — nome, pai e todos os atributos, ordenados:

```text
span: IInventoryService.Reserve
  parent: IOrderService.PlaceOrder
    narrative.class: IInventoryService
    narrative.method: Reserve
    narrative.param.quantity: 2
    narrative.param.sku: SKU-KB
    narrative.trace_id: c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7
    narrative.trace_name: solid wand romps
    nt.entryType: entry
    nt.schemaVersion: 1.2
span: IPaymentService.Charge
  parent: IOrderService.PlaceOrder
    narrative.class: IPaymentService
    narrative.method: Charge
    narrative.outcome: "PAY-C-1234-7998"
    narrative.param.amountCents: 7998
    narrative.param.customerId: C-1234
    narrative.trace_id: c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7
    narrative.trace_name: solid wand romps
    nt.entryType: entry
    nt.schemaVersion: 1.2
span: IOrderRepository.Save
  parent: IOrderService.PlaceOrder
    narrative.class: IOrderRepository
    narrative.method: Save
    narrative.param.order: Order(OrderId: "ORD-C-1234-SKU-KB-2", CustomerId: "C-1234", Sku: "SKU-KB", Quantity: 2, AmountCents: 7998)
    narrative.trace_id: c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7
    narrative.trace_name: solid wand romps
    nt.entryType: entry
    nt.schemaVersion: 1.2
span: IOrderService.PlaceOrder
  parent: (none — this is the root)
    narrative.class: IOrderService
    narrative.method: PlaceOrder
    narrative.outcome: "ORD-C-1234-SKU-KB-2"
    narrative.param.customerId: C-1234
    narrative.param.quantity: 2
    narrative.param.sku: SKU-KB
    narrative.trace_id: c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7
    narrative.trace_name: solid wand romps
    nt.entryType: entry
    nt.schemaVersion: 1.2
```

## Como ler

- **Quatro chamadas rastreadas, quatro spans, uma árvore.** Os spans chegam na
  ordem em que *terminaram* — o mais interno primeiro, a raiz por último, que é
  como qualquer pipeline OpenTelemetry enxerga uma árvore concluída. A
  parentalidade não é inferida dessa ordem: o listener lê o id do span pai
  explícito de cada evento, e é por isso que `IInventoryService.Reserve`,
  `IPaymentService.Charge` e `IOrderRepository.Save` nomeiam todos
  `IOrderService.PlaceOrder` como pai, mesmo tendo terminado antes dele.
- **O que cada span carrega.** `narrative.class` e `narrative.method` são a
  chamada; `narrative.param.<name>` é um atributo por parâmetro, tipado
  (`narrative.param.quantity: 2` é um long, não a string `"2"`);
  `narrative.outcome` é o valor retornado, e está ausente nas duas chamadas
  `void` porque não havia o que retornar. `narrative.trace_id` e
  `narrative.trace_name` são a identidade do NarrativeTrace para o trace,
  carregada como atributos para que um span no seu backend possa ser casado com
  a linha de log e o arquivo de trace que descrevem a mesma chamada —
  [Acompanhe uma requisição entre serviços](acompanhe-uma-requisicao-entre-servicos.md)
  é o mesmo id fazendo o mesmo trabalho através de uma fronteira de processo.
  `nt.entryType` e `nt.schemaVersion` marcam o esquema que esses atributos
  seguem.
- **Um parâmetro record chega como um atributo.** `IOrderRepository.Save(order)`
  é um parâmetro, e foi exportado como um único atributo `narrative.param.order`
  com o record renderizado do mesmo jeito que a linha de log o renderiza, lendo
  seu estado e nunca chamando um `ToString()` seu.
- **A ocultação alcança este caminho também.** Não há atributo
  `narrative.param.cardNumber` nem `narrative.param.card` em nenhum span acima —
  nem um atributo contendo `[REDACTED]`, atributo nenhum. Um valor bloqueado é
  descartado na captura, antes de o evento existir, então nenhum consumidor, em
  nenhum dos dois caminhos, pode recebê-lo; o cenário afirma isso além da árvore
  de spans ([Mantenha os segredos fora dos logs](mantenha-os-segredos-fora-dos-logs.md)).
- **Qual configuração moveu qual evento para qual consumidor.** Três decisões,
  nesta ordem:
  1. `TracingLevel` decide se um evento chega a ser criado e o que ele carrega.
     O segundo teste acima roda o mesmo checkout em `Summary`: os mesmos quatro
     spans saem, sem nenhum atributo `narrative.param.*` entre eles, porque os
     valores dos parâmetros não são capturados nesse nível; em `Off` não há
     evento e, portanto, não há span
     ([Trace em produção sem o custo](trace-em-producao-sem-o-custo.md)).
  2. O formato do pipeline decide quem vê o evento. Aqui um listener síncrono —
     narração, depois exportação — roda inline, e a captura própria do
     contexto, que é o que `CaptureTrace()` e os arquivos de trace leem, fica
     intocada. Nada foi tirado do logger para alimentar o exportador.
  3. O nível do seu logger decide o que é *impresso*. Subir a categoria
     `NarrativeTrace` para `Information` esvazia o bloco de log acima e não muda
     nada nos spans.
- **Por que isto não é um segundo lugar para olhar.** Os spans são seus, no seu
  collector, sob o nome do seu serviço, ao lado dos spans que o seu framework já
  emite. O que o NarrativeTrace acrescenta é um span por *chamada de método
  rastreada*, com os parâmetros e o resultado nele — um nível abaixo do span
  HTTP que você já tem.

## O que não é capturado

- **O que o seu stack faz com os spans é do seu stack.** Amostragem, retenção,
  indexação e se um atributo é pesquisável são decisões do collector e do
  backend. Esta página mostra o que sai do processo; nenhuma afirmação é feita
  sobre a interface de qualquer fornecedor.
- **Os ids não são compartilhados.** O OpenTelemetry gera seus próprios ids de
  trace e de span para os spans que cria aqui, e a identidade do NarrativeTrace
  vai junto como os atributos `narrative.trace_id` / `narrative.trace_name`. A
  posição de um span na cascata do seu backend é do OpenTelemetry; casá-lo com
  uma linha de log é trabalho do atributo.
- **Ao vivo e em lote não são os mesmos spans.** O listener desta página cria um
  span enquanto o método roda, então um órfão (uma entrada sem saída — um
  processo morto no meio da chamada) acaba encerrado pelo limite de tempo do
  próprio listener e pelo limite de spans abertos. O exportador em lote só vê
  árvores concluídas e acrescenta `narrative.duration_ms`, que o caminho ao vivo
  não tem.
- **O custo é um listener na thread de quem chama.** Um listener no slot
  síncrono roda inline, então a criação do span fica no caminho da chamada
  rastreada. Essa é a troca por durabilidade; um `BufferedEventConsumer` tira
  isso da thread de quem chama e descarta carga sob pressão em vez de bloquear.
- **Só o que é rastreado é exportado.** Não existe span para uma chamada que
  nenhuma integração interceptou — o caminho de exportação herda exatamente a
  fronteira do caminho de captura.

**A skill para isto:** `narrativetrace-doctor` — ela confere a ligação de cada
integração e informa se algum destino realmente recebe o que é rastreado.
