<!-- source: documentation/recipes/send-traces-to-the-observability-stack-you-already-run.md blob aa4796e4382b | translated: 2026-10-18 | reviewed: - -->
# Envía las trazas al stack de observabilidad que ya tienes

[English](../../recipes/send-traces-to-the-observability-stack-you-already-run.md) | **Español** | [Português](../../pt-BR/receitas/envie-os-traces-para-o-stack-de-observabilidade-que-voce-ja-tem.md) | [简体中文](../../zh-CN/实用方案/把追踪发到你已有的可观测性栈.md)

## El problema

> Ya tenemos tracing y dashboards. No voy a adoptar un segundo lugar donde
> mirar: ¿puede esto ir al stack que ya usamos?

Sí: una ejecución, dos consumidores. Las líneas de narrativa siguen yendo a tu
logger, y los mismos eventos se convierten en spans de OpenTelemetry al salir.
Abajo hay un checkout con ambas salidas lado a lado.

## Configuración mínima

`NarrativeTrace.Observability`, que trae consigo el paquete del SDK de
OpenTelemetry:

```bash
dotnet add package NarrativeTrace.Observability
```

Hay dos maneras de entrar, y la diferencia es *cuándo* existe un span:

| Modo | Tipo | Úsalo cuando |
|---|---|---|
| En vivo, mientras se ejecutan los métodos | `OtelTraceEventListener` | Quieres spans en tu collector aunque el proceso muera a mitad de una petición — esta página |
| Por lotes, a posteriori | `TraceActivityExporter` | Quieres los spans de un árbol de traza terminado, exportados una sola vez, al menor coste de ejecución: `TraceActivityExporter.Export(context.CaptureTrace())` |

El exportador por lotes emite a través de un `ActivitySource` llamado
`NarrativeTrace`; el listener en vivo, a través de la fuente que le des. Esta
página nombra su fuente igual, así que un único `AddSource("NarrativeTrace")` y
tu exportador en el builder de OpenTelemetry cubren ambos. En un despliegue, el
listener en vivo se suscribe al pipeline que construye tu host —un
`BufferedEventConsumer.Subscribe(listener.OnEvent)`— o se coloca en la ranura
síncrona, como abajo. Un exportador en memoria en una prueba no necesita ni
host ni SDK global, así que el escenario de abajo compone el pipeline de forma
explícita: esa es también la imagen más clara de lo que construiría un host.

## El escenario

El pipeline es la única idea nueva: el listener de narración que ya tienes, con
el listener de OpenTelemetry compuesto sobre el mismo camino síncrono. El
checkout en sí se envuelve exactamente como en todas las demás páginas:

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

## La salida real

**Las líneas de log**, sin cambios por nada de esto: la misma narración que
produce [Añade registro a este servicio](anade-registro-a-este-servicio.md):

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

**Los spans que salieron de la misma ejecución**, tal como los recibió el
exportador en memoria: nombre, padre y cada atributo, ordenados:

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

## Cómo leerla

- **Cuatro llamadas trazadas, cuatro spans, un árbol.** Los spans llegan en el
  orden en que *terminaron* —primero el más interno, la raíz la última—, que es
  como cualquier pipeline de OpenTelemetry ve un árbol terminado. La filiación
  no se infiere de ese orden: el listener lee el id de span padre explícito de
  cada evento, y por eso `IInventoryService.Reserve`, `IPaymentService.Charge`
  e `IOrderRepository.Save` nombran todos a `IOrderService.PlaceOrder` como su
  padre aunque terminaran antes que él.
- **Qué lleva cada span.** `narrative.class` y `narrative.method` son la
  llamada; `narrative.param.<name>` es un atributo por parámetro, tipado
  (`narrative.param.quantity: 2` es un long, no la cadena `"2"`);
  `narrative.outcome` es el valor devuelto, y está ausente en las dos llamadas
  `void` porque no había nada que devolver. `narrative.trace_id` y
  `narrative.trace_name` son la identidad propia de NarrativeTrace para la
  traza, llevada como atributos para que un span en tu backend pueda
  emparejarse con la línea de log y el archivo de traza que describen la misma
  llamada —
  [Sigue una petición entre servicios](sigue-una-peticion-entre-servicios.md)
  es el mismo id haciendo el mismo trabajo a través de una frontera de
  proceso—. `nt.entryType` y `nt.schemaVersion` marcan el esquema que siguen
  estos atributos.
- **Un parámetro de tipo record llega como un solo atributo.**
  `IOrderRepository.Save(order)` es un parámetro, y se exportó como un único
  atributo `narrative.param.order` que contiene el record renderizado como lo
  renderiza la línea de log, leyendo su estado y nunca llamando a un
  `ToString()` tuyo.
- **La ocultación también llega a este camino.** No hay atributo
  `narrative.param.cardNumber` ni `narrative.param.card` en ningún span de
  arriba: ni un atributo que contenga `[REDACTED]`, ningún atributo en absoluto.
  Un valor denegado se descarta en la captura, antes de que exista el evento,
  así que ningún consumidor en ninguno de los dos caminos puede recibirlo; el
  escenario afirma eso además del árbol de spans
  ([Mantén los secretos fuera de los logs](manten-los-secretos-fuera-de-los-logs.md)).
- **Qué ajuste llevó un evento a qué consumidor.** Tres decisiones, en este
  orden:
  1. `TracingLevel` decide si se crea un evento y qué lleva. La segunda prueba
     de arriba ejecuta el mismo checkout en `Summary`: salen los mismos cuatro
     spans, sin ningún atributo `narrative.param.*` entre ellos, porque los
     valores de los parámetros no se capturan en ese nivel; en `Off` no hay
     evento y, por tanto, no hay span
     ([Traza en producción sin el coste](traza-en-produccion-sin-el-coste.md)).
  2. La forma del pipeline decide quién ve el evento. Aquí un listener síncrono
     —narración, luego exportación— se ejecuta en línea, y la captura propia
     del contexto, que es lo que leen `CaptureTrace()` y los archivos de traza,
     queda intacta. No se le quitó nada al logger para alimentar al exportador.
  3. El nivel de tu logger decide lo que se *imprime*. Subir la categoría
     `NarrativeTrace` a `Information` vacía el bloque de log de arriba y no
     cambia nada en los spans.
- **Por qué esto no es un segundo lugar donde mirar.** Los spans son tuyos, en
  tu collector, bajo el nombre de tu servicio, junto a los spans que tu
  framework ya emite. Lo que NarrativeTrace añade es un span por *llamada de
  método trazada*, con los parámetros y el resultado en él: un nivel por debajo
  del span HTTP que ya tienes.

## Qué no se captura

- **Lo que tu stack haga con los spans es cosa de tu stack.** El muestreo, la
  retención, la indexación y si un atributo se puede buscar son decisiones del
  collector y del backend. Esta página muestra lo que sale del proceso; no
  afirma nada sobre la interfaz de ningún proveedor.
- **Los ids no se comparten.** OpenTelemetry genera sus propios ids de traza y
  de span para los spans que crea aquí, y la identidad de NarrativeTrace viaja
  como atributos `narrative.trace_id` / `narrative.trace_name`. La posición de
  un span en la cascada de tu backend es de OpenTelemetry; emparejarlo con una
  línea de log es trabajo del atributo.
- **En vivo y por lotes no son los mismos spans.** El listener de esta página
  crea un span mientras el método se ejecuta, así que un huérfano (una entrada
  sin salida: un proceso terminado a mitad de una llamada) acaba cerrado por el
  límite de tiempo y el tope de spans abiertos del propio listener. El
  exportador por lotes solo ve árboles terminados y añade
  `narrative.duration_ms`, que el camino en vivo no tiene.
- **El coste es un listener en el hilo del llamador.** Un listener en la ranura
  síncrona se ejecuta en línea, así que la creación del span está en el camino
  de la llamada trazada. Ese es el precio de la durabilidad; un
  `BufferedEventConsumer` lo saca del hilo del llamador y descarta carga bajo
  presión en lugar de bloquear.
- **Solo se exporta lo que se traza.** No existe span para una llamada que
  ninguna integración interceptó: el camino de exportación hereda exactamente
  la frontera del camino de captura.

**La skill para esto:** `narrativetrace-doctor`: comprueba el cableado de cada
integración e informa de si algún destino recibe de verdad lo que se traza.
