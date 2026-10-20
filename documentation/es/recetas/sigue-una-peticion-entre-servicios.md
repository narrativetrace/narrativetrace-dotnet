<!-- source: documentation/recipes/follow-one-request-across-services.md blob 622d7d0321ce | translated: 2026-10-18 | reviewed: - -->
# Sigue una petición entre servicios (trazado distribuido)

[English](../../recipes/follow-one-request-across-services.md) | **Español** | [Português](../../pt-BR/receitas/acompanhe-uma-requisicao-entre-servicos.md) | [简体中文](../../zh-CN/实用方案/跨服务追踪一次请求.md)

## El problema

> El checkout llama al servicio de pago por HTTP. Cuando un pedido sale mal,
> quiero ver una sola petición a través de ambos, no dos registros que tenga
> que alinear a mano.

Dos servicios, dos archivos de registro, dos juegos de ids. Lo que los convierte
en una sola historia es un único valor que viaja con la petición: la cabecera
W3C [`traceparent`](https://www.w3.org/TR/trace-context/#traceparent-header).
El llamador envía la traza en la que está y el span desde el que llama; el
llamado adopta ambos en lugar de iniciar una traza propia. Abajo hay una
ejecución de exactamente eso, con las líneas de ambos servicios en un solo
archivo.

## Configuración mínima

Además de la [instalación](../../guides/es/guia-de-instalacion.md), el llamador
necesita un handler en el `HttpClient` que ya usa: `TraceparentPropagationHandler`,
de `NarrativeTrace.Runtime`, adjunta la cabecera a cada petición saliente. Con
`IHttpClientFactory` en un llamador ASP.NET Core,
`AddNarrativeTraceTraceparentPropagation()` sobre el builder del cliente hace lo
mismo. El llamado necesita la mitad entrante, y qué código es esa mitad depende
de cómo esté construido:

| El llamado es | La mitad entrante es |
|---|---|
| Una aplicación ASP.NET Core | El middleware de `NarrativeTrace.AspNetCore` (`UseNarrativeTrace()`), que adopta la cabecera por ti — [Guía de integración con ASP.NET Core](../../guides/es/guia-de-integracion-con-aspnet-core.md) |
| Cualquier otra cosa — un `HttpListener` simple, un consumidor de colas, otro framework | Tres líneas en lo primero que se ejecute por petición: un contexto nuevo, `AdoptTraceparent(Traceparent.Parse(header))`, atender |

El servicio de pago de esta página es la primera fila, y la prueba lo aloja: un
servidor Kestrel de loopback con el middleware, iniciado por la prueba en un
puerto que elige el sistema operativo. La petición cruza realmente un socket y
la cabecera llega realmente como cabecera.

Las líneas de registro vienen del puente de logging, como en
[Añade registro a este servicio](anade-registro-a-este-servicio.md). Esta
página usa un logger que imprime el id de traza sin procesar entre corchetes
tras la categoría, porque el id de traza es lo que se sigue. Lo lee del campo
`nt.traceId` que NarrativeTrace pone en cada ámbito (scope) de registro, y
escribe solo las categorías `NarrativeTrace`, de modo que el registro de
peticiones del propio host queda fuera de la página:

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

## El escenario

**El lado del llamador: un cliente corriente.** El `IPaymentService` del
checkout es ahora un cliente HTTP. No sabe nada de tracing: el `HttpClient` que
recibe lleva la cabecera:

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

**El lado del llamado: su propio contexto.** El servicio de pago es un segundo
servicio, lo que significa un segundo contexto de narrativa —uno con ámbito de
petición que registra `AddNarrativeTrace`— y su propia categoría de logger, de
modo que un único archivo de registro puede distinguir a los dos
(`recipes/checkout/Checkout.Tests/PaymentServiceHost.cs`):

```csharp
var builder = WebApplication.CreateSlimBuilder();
builder.WebHost.UseUrls("http://127.0.0.1:0");
builder.Logging.ClearProviders().SetMinimumLevel(LogLevel.Trace).AddProvider(new CorrelationLoggerProvider(log));
builder.Services.AddSingleton<ITraceExporter>(host); // the seam that hands over this service's own trace
builder.Services.AddNarrativeTrace(); // a request-scoped context, the middleware, the exporter
```

**El lado del llamado: adoptar, atender, exportar.** Esta es toda la mitad
entrante, en el mismo archivo. El middleware adopta el `traceparent` de la
petición, deja que el endpoint se ejecute y entrega la traza propia de la
petición al exportador registrado arriba:

```csharp
host._app.UseNarrativeTrace(); // adopts the inbound traceparent and exports the request's trace
host._app.MapPost("/charge", (Delegate)host.Charge);
```

**La ejecución.** El checkout, envuelto como siempre, con el cliente HTTP donde
antes estaba el servicio de pago local:

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

## La salida real

**Las líneas de ambos servicios, en un solo archivo**: las del llamador bajo
`NarrativeTrace`, las del servicio de pago bajo `NarrativeTrace.Payments`:

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

**La traza propia del llamador,** el artefacto estructural sin valores de esa
ejecución; los ids de span de abajo son los que usa esta página para leer:

```text
scenario: A customer checks out against the payment service

#1 - IOrderService.PlaceOrder(customerId, sku, quantity, cardNumber) → value
  #1.1 - IInventoryService.Reserve(sku, quantity)
  #1.2 - IPaymentService.Charge(customerId, amountCents, card) → value
  #1.3 - IOrderRepository.Save(order)
```

**La narrativa propia del servicio de pago,** capturada por su propio contexto
al final de la petición y renderizada junto a la del llamador:

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

## Cómo leerla

- **Un solo id de traza, en las líneas de ambos servicios.**
  `c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7` es el segundo corchete de cada línea de
  arriba, tanto las del llamador como las del llamado. El del llamador es la
  traza en la que está esta ejecución; el del llamado es ese mismo id porque lo
  *adoptó*. Nada los correlaciona a posteriori: nunca estuvieron separados.
- **Los dos servicios se distinguen por la categoría del logger, no por el
  id.** `[NarrativeTrace]` es el checkout, `[NarrativeTrace.Payments]` es el
  servicio de pago, porque su contexto se envolvió con un logger de ese nombre.
- **El `#1.2` del llamador y el `#1` del llamado son el mismo trabajo, visto
  desde ambos extremos.** En la traza del llamador, `#1.2 IPaymentService.Charge`
  es el span *cliente*: empieza cuando el checkout llama al cliente y termina
  cuando vuelve la respuesta, así que incluye todo el viaje de ida y vuelta. En
  la narrativa del servicio de pago, `#1 IPaymentService.Charge` es el span
  *servidor*: el trabajo que realmente hizo. Los ids de span son por árbol de
  traza y cada servicio numera los suyos desde `#1`; lo que los une es el id de
  traza, no el número.
- **El id de span padre en la raíz del llamado es el span del llamador.** La
  cabecera que llevaba la petición era
  `00-c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7-<el id del span #1.2 del llamador>-01`,
  y el span raíz del llamado registra ese id como su padre. Eso no se ve en una
  línea de registro —un id de span se genera por span, así que difiere en cada
  ejecución y aquí no podría imprimirse nada estable byte a byte—, por lo que
  se afirma en su lugar, en `AssertTheTraceCrossedTheWire` más arriba: la raíz
  del llamado lleva el id de traza enviado *y* el id de span padre enviado, y
  el id de span padre enviado es el id de span del `Charge` del llamador. La
  filiación a través de la red es la afirmación de esta página, y esa aserción
  es su prueba.
- **Cómo unirlos en una búsqueda de registros.** El id de traza está en el
  ámbito del registro bajo `nt.traceId` en cada línea que NarrativeTrace
  escribe, en cualquiera de los dos servicios. Un proveedor que renderiza
  ámbitos (como el de arriba hace con este campo) o un formateador JSON lo pone
  en la línea, y una petición a través de ambos servicios es
  `grep c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7` o, en el agregador que uses,
  `nt.traceId:"c4e1…"`. El *nombre* de traza por el que
  [Añade registro a este servicio](anade-registro-a-este-servicio.md) hace
  grep se deriva del mismo id, así que también sirve como clave de búsqueda, y
  se lee mejor para una persona.
- **Las dos narrativas siguen siendo dos.** Los spans del servicio de pago
  están en la traza del servicio de pago, no en la del llamador:
  `CaptureTrace()` en cualquiera de los dos lados devuelve el trabajo de ese
  lado. Un id de traza, dos árboles: exactamente lo que tienes en producción,
  donde el otro servicio es un proceso distinto en otro host.
- **Por qué el artefacto del llamador es el `.nt`.** El renderizado Markdown
  del llamador marca como lenta cualquier llamada de más de 200 ms, y una
  llamada que hace un viaje HTTP real puede cruzar esa línea en un arranque en
  frío, lo que cambiaría la salida de una ejecución a otra. La traza
  estructural no lleva tiempos y es estable byte a byte.

## Qué no se captura

- **Un salto por un servicio que no reenvía la cabecera rompe la cadena.** La
  correlación es cooperativa: la cadena aguanta mientras cada servicio de ella
  *pase el `traceparent`*. Un proxy, un consumidor de colas o un servicio de
  terceros que descarta la cabecera deja al siguiente servicio con
  NarrativeTrace sin nada que adoptar, y este inicia una traza nueva —con
  razón, porque no tiene un padre honesto al que nombrar—. Un servicio sin
  NarrativeTrace alguno está bien *si* reenvía la cabecera que recibió.
- **No se envía cabecera cuando no hay nada que nombrar.** Sin ningún span
  abierto y sin nada adoptado, no hay traza que propagar y el handler no envía
  nada, en lugar de un `traceparent` que apunte a un span que nunca existió.
  Una llamada sin traza saliente del checkout, por tanto, no se puede unir, y
  es por diseño. Un `traceparent` que el llamador ya haya establecido se deja
  intacto.
- **Solo `traceparent`.** El cliente de arriba no envía `tracestate` ni
  baggage: nada en este camino lee ni escribe estado de proveedor, y ningún
  valor de la aplicación cruza la red a causa del tracing.
- **Nada vuelve.** La respuesta no lleva cabecera de traza, así que la
  narrativa del llamador dice qué envió y qué recibió, nunca qué hizo el
  llamado por dentro. La narrativa propia del llamado es el registro de eso, y
  el id de traza es lo que la encuentra.
- **Dos servicios, un proceso, un socket.** Lo que esta ejecución no ejercita
  es un segundo proceso y una red que puede fallar. El mecanismo de la
  cabecera no cambia por ninguna de las dos cosas; los modos de fallo de la
  propia llamada son los de tu cliente HTTP.

**La skill para esto:** `narrativetrace-verify`: un cambio que cruza servicios
es exactamente el caso para el que existe: escribe la intención, ejecuta el
camino real más pequeño y lee la traza contra ella antes de darlo por
terminado.
