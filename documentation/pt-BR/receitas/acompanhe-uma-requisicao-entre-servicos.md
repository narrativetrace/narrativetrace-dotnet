<!-- source: documentation/recipes/follow-one-request-across-services.md blob 622d7d0321ce | translated: 2026-10-18 | reviewed: - -->
# Acompanhe uma requisição entre serviços (tracing distribuído)

[English](../../recipes/follow-one-request-across-services.md) | [Español](../../es/recetas/sigue-una-peticion-entre-servicios.md) | **Português** | [简体中文](../../zh-CN/实用方案/跨服务追踪一次请求.md)

## O problema

> O checkout chama o serviço de pagamento por HTTP. Quando um pedido dá errado,
> quero ver uma única requisição atravessando os dois — não dois logs que eu
> tenha de alinhar à mão.

Dois serviços, dois arquivos de log, dois conjuntos de ids. O que os transforma
em uma única história é um valor que viaja com a requisição: o cabeçalho
[`traceparent`](https://www.w3.org/TR/trace-context/#traceparent-header) do W3C.
Quem chama envia o trace em que está e o span a partir do qual chama; quem é
chamado adota os dois, em vez de iniciar um trace próprio. Abaixo está uma
execução exatamente disso, com as linhas dos dois serviços em um só arquivo.

## Configuração mínima

Além da [instalação](../../guides/pt-BR/guia-de-instalacao.md), quem chama
precisa de um handler no `HttpClient` que já usa: o
`TraceparentPropagationHandler`, de `NarrativeTrace.Runtime`, anexa o cabeçalho
a toda requisição de saída. Com `IHttpClientFactory` em um chamador ASP.NET
Core, `AddNarrativeTraceTraceparentPropagation()` no builder do cliente faz o
mesmo. Quem é chamado precisa da metade de entrada — e qual código é esse
depende de como ele é construído:

| O serviço chamado é | A metade de entrada é |
|---|---|
| Uma aplicação ASP.NET Core | O middleware do `NarrativeTrace.AspNetCore` (`UseNarrativeTrace()`), que adota o cabeçalho por você — [Guia de Integração com ASP.NET Core](../../guides/pt-BR/guia-de-integracao-com-aspnet-core.md) |
| Qualquer outra coisa — um `HttpListener` simples, um consumidor de fila, outro framework | Três linhas no que rodar primeiro a cada requisição: um contexto novo, `AdoptTraceparent(Traceparent.Parse(header))`, atender |

O serviço de pagamento desta página é a primeira linha, e o teste o hospeda: um
servidor Kestrel em loopback com o middleware, iniciado pelo teste em uma porta
escolhida pelo sistema operacional. A requisição realmente atravessa um socket e
o cabeçalho realmente chega como cabeçalho.

As linhas de log vêm da ponte de logging, como em
[Adicione logs a este serviço](adicione-logs-a-este-servico.md). Esta página usa
um logger que imprime o id bruto do trace entre colchetes depois da categoria,
porque o id do trace é justamente o que está sendo acompanhado. Ele lê o id do
campo `nt.traceId` que o NarrativeTrace coloca em cada escopo de log, e escreve
apenas as categorias `NarrativeTrace`, para que o log de requisições do próprio
host fique fora da página:

```csharp
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Checkout.Tests;

/// <summary>
/// The logger configuration the correlation recipes run under: one line per message —
/// <c>LEVEL [category] [trace id] - message</c> — with the trace id read from the <c>nt.traceId</c>
/// field NarrativeTrace puts in each log scope. The trace id is the thing those pages follow; the
/// built-in console formatter would print it only as part of a scope dump. It writes the
/// <c>NarrativeTrace</c> categories only, so a hosted service's request logging stays out of the page. Deliberately no
/// timestamp: the embedded block is a real run's bytes, and a clock differs on every run.
/// </summary>
internal sealed class CorrelationLoggerProvider(TextWriter output) : ILoggerProvider, ISupportExternalScope
{
    private const string OwnCategories = "NarrativeTrace";

    private readonly object _writing = new();
    private IExternalScopeProvider _scopes = new LoggerExternalScopeProvider();

    public void SetScopeProvider(IExternalScopeProvider scopeProvider) => _scopes = scopeProvider;

    // Only the narrative's own categories: the framework's request logging is not what these pages follow.
    public ILogger CreateLogger(string categoryName) =>
        categoryName.StartsWith(OwnCategories, StringComparison.Ordinal)
            ? new CorrelationLogger(categoryName, output, _writing, () => _scopes)
            : NullLogger.Instance;

    public void Dispose()
    {
    }

    private sealed class CorrelationLogger(string category, TextWriter output, object writing, Func<IExternalScopeProvider> scopes) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => scopes().Push(state);

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var line = $"{logLevel.ToString().ToUpperInvariant(),-5} [{category}] [{TraceIdOfScope()}] - {formatter(state, exception)}";
            lock (writing)
            {
                output.WriteLine(line);
            }
        }

        private string TraceIdOfScope()
        {
            var traceId = "";
            scopes().ForEachScope(
                (scope, _) =>
                {
                    if (scope is IEnumerable<KeyValuePair<string, object>> fields)
                    {
                        foreach (var (key, value) in fields)
                        {
                            traceId = key == "nt.traceId" ? value.ToString() ?? "" : traceId;
                        }
                    }
                },
                0);
            return traceId;
        }
    }
}
```

## O cenário

**O lado de quem chama: um cliente comum.** O `IPaymentService` do checkout
agora é um cliente HTTP. Ele não sabe nada sobre tracing — o `HttpClient` que
recebe é que carrega o cabeçalho:

```csharp
namespace Checkout;

/// <summary>
/// The checkout's payment collaborator when the payment service is a different process: an ordinary
/// HTTP client call. It knows nothing about tracing — the client it is given carries the
/// <c>traceparent</c> header through a delegating handler, so this class stays the code a project
/// already has.
/// </summary>
public sealed class HttpPaymentService(HttpClient client, Uri endpoint) : IPaymentService
{
    private const int DeclinedStatus = 402;

    public string Charge(string customerId, int amountCents, string card)
    {
        using var body = new StringContent($"{customerId},{amountCents},{card}");
        // SendAsync, not the synchronous Send: a handler's SendAsync override only runs on this path.
        using var response = client.PostAsync(endpoint, body).GetAwaiter().GetResult();
        var text = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        return (int)response.StatusCode == DeclinedStatus ? throw new PaymentDeclinedException(text) : text;
    }
}
```

**O lado de quem é chamado: o contexto próprio.** O serviço de pagamento é um
segundo serviço, o que significa um segundo contexto de narrativa — um contexto
com escopo de requisição que o `AddNarrativeTrace` registra — e uma categoria de
logger própria, para que um único arquivo de log consiga distinguir os dois
(`recipes/checkout/Checkout.Tests/PaymentServiceHost.cs`):

```csharp
var builder = WebApplication.CreateSlimBuilder();
builder.WebHost.UseUrls("http://127.0.0.1:0");
builder.Logging.ClearProviders().SetMinimumLevel(LogLevel.Trace).AddProvider(new CorrelationLoggerProvider(log));
builder.Services.AddSingleton<ITraceExporter>(host); // the seam that hands over this service's own trace
builder.Services.AddNarrativeTrace(); // a request-scoped context, the middleware, the exporter
```

**O lado de quem é chamado: adotar, atender, exportar.** Esta é a metade de
entrada inteira, no mesmo arquivo. O middleware adota o `traceparent` da
requisição, deixa o endpoint rodar e entrega o trace da própria requisição ao
exportador registrado acima:

```csharp
host._app.UseNarrativeTrace(); // adopts the inbound traceparent and exports the request's trace
host._app.MapPost("/charge", (Delegate)host.Charge);
```

**A execução.** O checkout, envolvido como sempre, com o cliente HTTP no lugar
onde ficava o serviço de pagamento local:

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
/// Proves recipes/follow-one-request-across-services.md: the checkout charges a card through a
/// second service over HTTP, the W3C <c>traceparent</c> goes with the request, and the callee's own
/// trace carries the caller's trace id and names the caller's span as its parent (rule 8, docs as tests).
/// The callee's narrative is captured here, not by the caller's context: the second service has a
/// context — and a narrative — of its own, which is the point of the page.
/// </summary>
public sealed class AcrossServicesRecipeTests
{
    private const string Slug = "follow-one-request-across-services";

    // A fixed W3C traceparent, so the page's embedded artifacts name the same trace every run.
    private const string DemoTraceparent = "00-c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7-c4e1d0a7c4e1d0a7-01";

    [Fact]
    public async Task A_customer_checks_out_against_the_payment_service()
    {
        var log = new StringWriter { NewLine = "\n" };
        await using var paymentService = await PaymentServiceHost.StartAsync(log);
        var stock = new InMemoryInventory("SKU-KB", 5);
        var repository = new InMemoryOrderRepository();
        var context = CallerContext(log);
        using var client = new HttpClient(new TraceparentPropagationHandler(context) { InnerHandler = new HttpClientHandler() });

        var orderId = CheckoutCalling(client, paymentService, context, stock, repository)
            .PlaceOrder("C-1234", "SKU-KB", 2, "4242424242424242");

        Assert.Equal("ORD-C-1234-SKU-KB-2", orderId);
        Assert.Single(repository.Saved);
        AssertTheTraceCrossedTheWire(paymentService, context.CaptureTrace());
        CapturedOutput.Write(Slug, "checkout.log", log.ToString());
        CapturedOutput.Write(Slug, "caller.nt", StructuralTraceRenderer.RenderDocument(context.CaptureTrace(), "A customer checks out against the payment service"));
        CapturedOutput.Write(Slug, "payment-service.md", MarkdownRenderer.Render(paymentService.LastTrace));
    }

    private static LoggingNarrativeContext CallerContext(TextWriter log)
    {
        var loggerFactory = LoggerFactory.Create(builder => builder
            .SetMinimumLevel(LogLevel.Trace)
            .AddProvider(new CorrelationLoggerProvider(log)));
        var traceparent = Traceparent.Parse(DemoTraceparent);
        return new LoggingNarrativeContext(
            new SyncNarrativeContext(new NarrativeTraceConfig(initialTraceparent: traceparent)),
            loggerFactory.CreateLogger("NarrativeTrace"));
    }

    /// <summary>The checkout, with the HTTP payment client where the local one used to be.</summary>
    private static IOrderService CheckoutCalling(
        HttpClient client, PaymentServiceHost paymentService, INarrativeContext context,
        InMemoryInventory stock, InMemoryOrderRepository repository)
    {
        var inventory = NarrativeTraceProxy.Create<IInventoryService>(stock, context);
        var payments = NarrativeTraceProxy.Create<IPaymentService>(
            new HttpPaymentService(client, paymentService.Endpoint), context);
        var orders = NarrativeTraceProxy.Create<IOrderRepository>(repository, context);
        return NarrativeTraceProxy.Create<IOrderService>(
            new DefaultOrderService(inventory, payments, orders), context);
    }

    /// <summary>
    /// One trace, two services: the header named the caller's own span, and the callee's root adopted
    /// it as its parent instead of starting a trace of its own.
    /// </summary>
    private static void AssertTheTraceCrossedTheWire(PaymentServiceHost paymentService, TraceTree callerTrace)
    {
        var sent = Traceparent.Parse(paymentService.InboundTraceparent);
        Assert.NotNull(sent);
        Assert.Equal(Traceparent.Parse(DemoTraceparent)!.TraceId, sent.TraceId);

        var calleeRoot = Assert.Single(paymentService.LastTrace.Roots);
        Assert.Equal("Charge", calleeRoot.Signature.MethodName);
        Assert.Equal(sent.TraceId, calleeRoot.SpanContext!.TraceId);
        Assert.Equal(sent.ParentSpanId, calleeRoot.SpanContext.ParentSpanId);
        var exportedEnter = System.Text.Json.JsonDocument.Parse(
                JsonExporter.Export(paymentService.LastTrace, new TraceMetadata("callee", ScenarioResult.Success)))
            .RootElement.GetProperty("events")[0];
        Assert.Equal(sent.ParentSpanId.Value, exportedEnter.GetProperty("parentSpanId").GetString());
        var callersCharge = callerTrace.Roots[0].Children.Single(n => n.Signature.MethodName == "Charge");
        Assert.Equal(callersCharge.SpanContext!.SpanId, sent.ParentSpanId);
    }
}
```

## A saída real

**As linhas dos dois serviços, em um só arquivo** — as de quem chama sob
`NarrativeTrace`, as do serviço de pagamento sob `NarrativeTrace.Payments`:

```text
TRACE [NarrativeTrace] [c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7] - → IOrderService.PlaceOrder(customerId: "C-1234", sku: "SKU-KB", quantity: 2, cardNumber: [REDACTED])
TRACE [NarrativeTrace] [c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7] - → IInventoryService.Reserve(sku: "SKU-KB", quantity: 2)
TRACE [NarrativeTrace] [c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7] - ← completed
TRACE [NarrativeTrace] [c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7] - → IPaymentService.Charge(customerId: "C-1234", amountCents: 7998, card: [REDACTED])
TRACE [NarrativeTrace.Payments] [c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7] - → IPaymentService.Charge(customerId: "C-1234", amountCents: 7998, card: [REDACTED])
TRACE [NarrativeTrace.Payments] [c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7] - ← returned: "PAY-C-1234-7998"
TRACE [NarrativeTrace] [c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7] - ← returned: "PAY-C-1234-7998"
TRACE [NarrativeTrace] [c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7] - → IOrderRepository.Save(order: Order(OrderId: "ORD-C-1234-SKU-KB-2", CustomerId: "C-1234", Sku: "SKU-KB", Quantity: 2, AmountCents: 7998))
TRACE [NarrativeTrace] [c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7] - ← completed
TRACE [NarrativeTrace] [c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7] - ← returned: "ORD-C-1234-SKU-KB-2"
```

**O trace próprio de quem chama,** o artefato estrutural, sem valores, dessa
execução — os ids de span abaixo são os que esta página usa para ler:

```text
scenario: A customer checks out against the payment service

#1 - IOrderService.PlaceOrder(customerId, sku, quantity, cardNumber) → value
  #1.1 - IInventoryService.Reserve(sku, quantity)
  #1.2 - IPaymentService.Charge(customerId, amountCents, card) → value
  #1.3 - IOrderRepository.Save(order)
```

**A narrativa própria do serviço de pagamento,** capturada pelo contexto dele ao
fim da requisição e renderizada ao lado da de quem chama:

```text
---
type: trace
entry_point: IPaymentService.Charge
trace_id: c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7
trace_name: solid wand romps
method_count: 1
error_count: 0
result: success
duration_ms: 0
---

- **IPaymentService.Charge**(customerId: `"C-1234"`, amountCents: `7998`, card: `[REDACTED]`) → `"PAY-C-1234-7998"` — 0ms #1
```

## Como ler

- **Um id de trace, nas linhas dos dois serviços.**
  `c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7` é o segundo colchete de cada linha acima,
  tanto nas de quem chama quanto nas de quem é chamado. O de quem chama é o
  trace em que esta execução está; o de quem é chamado é esse mesmo id porque
  ele o *adotou*. Nada os correlaciona depois do fato — eles nunca estiveram
  separados.
- **Os dois serviços se distinguem pela categoria do logger, não pelo id.**
  `[NarrativeTrace]` é o checkout, `[NarrativeTrace.Payments]` é o serviço de
  pagamento, porque o contexto dele foi envolvido com um logger com esse nome.
- **O `#1.2` de quem chama e o `#1` de quem é chamado são o mesmo trabalho,
  visto das duas pontas.** No trace de quem chama, `#1.2 IPaymentService.Charge`
  é o span *cliente*: começa quando o checkout chama o cliente e termina quando
  a resposta volta, então inclui a viagem de ida e volta inteira. Na narrativa
  do serviço de pagamento, `#1 IPaymentService.Charge` é o span *servidor*: o
  trabalho que ele de fato fez. Os ids de span são por árvore de trace e cada
  serviço numera os seus a partir de `#1` — o que une os dois é o id do trace,
  não o número.
- **O id do span pai na raiz de quem é chamado é o span de quem chama.** O
  cabeçalho que a requisição carregava era
  `00-c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7-<o id do span #1.2 de quem chama>-01`, e
  o span raiz de quem é chamado registra esse id como seu pai. Isso não é
  visível em uma linha de log — o id de span é gerado por span, então muda a
  cada execução e nada estável byte a byte poderia ser impresso aqui —, por
  isso é afirmado em vez disso, em `AssertTheTraceCrossedTheWire` acima: a raiz
  de quem é chamado carrega o id de trace enviado *e* o id do span pai enviado,
  e o id do span pai enviado é o id do span do `Charge` de quem chama. A
  parentalidade através da rede é a afirmação desta página, e essa asserção é a
  prova dela.
- **Como juntá-los em uma busca de logs.** O id do trace está no escopo do log
  sob `nt.traceId` em toda linha que o NarrativeTrace escreve, em qualquer um
  dos serviços. Um provedor que renderiza escopos (como o de cima faz para este
  campo) ou um formatador JSON o coloca na linha, e uma requisição através dos
  dois serviços é `grep c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7` — ou, no agregador que
  você usar, `nt.traceId:"c4e1…"`. O *nome* do trace pelo qual
  [Adicione logs a este serviço](adicione-logs-a-este-servico.md) faz o grep
  deriva do mesmo id, então também funciona como chave de busca, e é mais
  legível para uma pessoa.
- **As duas narrativas continuam duas.** Os spans do serviço de pagamento estão
  no trace do serviço de pagamento, não no de quem chama: `CaptureTrace()` de
  qualquer um dos lados devolve o trabalho daquele lado. Um id de trace, duas
  árvores — exatamente o que você tem em produção, onde o outro serviço é um
  processo diferente em outro host.
- **Por que o artefato de quem chama é o `.nt`.** A renderização em Markdown de
  quem chama marca como lenta qualquer chamada acima de 200 ms, e uma chamada
  que faz uma viagem HTTP real pode cruzar esse limite em uma partida a frio, o
  que mudaria a saída de uma execução para outra. O trace estrutural não traz
  tempos e é estável byte a byte.

## O que não é capturado

- **Um salto por um serviço que não repassa o cabeçalho quebra a cadeia.** A
  correlação é cooperativa: a cadeia se sustenta enquanto todo serviço nela
  *repassar o `traceparent`*. Um proxy, um consumidor de fila ou um serviço de
  terceiros que descarta o cabeçalho deixa o próximo serviço NarrativeTrace sem
  nada para adotar, e ele inicia um trace novo — corretamente, já que não tem um
  pai honesto para nomear. Um serviço sem NarrativeTrace nenhum não é problema
  *se* repassar o cabeçalho que recebeu.
- **Nenhum cabeçalho é enviado quando não há o que nomear.** Sem span aberto e
  sem nada adotado, não há trace para propagar, e o handler não envia nada em
  vez de um `traceparent` apontando para um span que nunca existiu. Uma chamada
  sem trace saindo do checkout, portanto, não pode ser unida, e isso é
  intencional. Um `traceparent` que quem chama já definiu fica intocado.
- **Somente `traceparent`.** O cliente acima não envia `tracestate` nem
  baggage: nada neste caminho lê ou escreve estado de fornecedor, e nenhum valor
  da aplicação cruza a rede por causa do tracing.
- **Nada volta.** A resposta não traz cabeçalho de trace, então a narrativa de
  quem chama diz o que enviou e o que recebeu, nunca o que quem é chamado fez
  por dentro. A narrativa própria de quem é chamado é o registro disso, e o id
  do trace é o que a encontra.
- **Dois serviços, um processo, um socket.** O que esta execução não exercita é
  um segundo processo e uma rede que pode falhar. O mecanismo do cabeçalho não
  muda com nenhum dos dois; os modos de falha da chamada em si são os do seu
  cliente HTTP.

**A skill para isto:** `narrativetrace-verify` — uma mudança que atravessa
serviços é exatamente o caso para o qual ela existe: escrever a intenção, rodar
o menor caminho real e ler o trace contra ela antes de dar a tarefa por
concluída.
