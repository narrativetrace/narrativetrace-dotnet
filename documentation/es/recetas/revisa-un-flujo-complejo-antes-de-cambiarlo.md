<!-- source: documentation/recipes/review-a-complex-flow-before-changing-it.md blob f318f1452900 | translated: 2026-10-11 | reviewed: - -->
# Revisa un flujo complejo antes de cambiarlo

[English](../../recipes/review-a-complex-flow-before-changing-it.md) | **Español** | [Português](../../pt-BR/receitas/revise-um-fluxo-complexo-antes-de-altera-lo.md) | [简体中文](../../zh-CN/实用方案/修改复杂流程前先审查它.md)

## El problema

> Antes de cambiar el checkout, dime qué hace realmente.

Leer el código fuente te dice lo que el código *puede* hacer. Una ejecución te
dice lo que *hace* con estas entradas: en qué orden, con qué valores y por
dónde el flujo sale del código que estás a punto de editar. Esta página
deliberadamente nunca muestra la implementación: ese es el sentido de leer una
ejecución.

## Configuración mínima

Nada más allá de la [instalación](../../guides/es/guia-de-instalacion.md) y la
integración con xUnit para los artefactos de traza. Las líneas de registro
necesitan el puente de logging, cableado como en
[Añade registro a este servicio](anade-registro-a-este-servicio.md). El nivel
de captura es una variable de entorno; `Summary` es el que esta página usa junto
al `Detail` por defecto:

```bash
NARRATIVETRACE_LEVEL=Summary   # Off | Errors | Summary | Narrative | Detail
```

Consulta la [Guía de configuración](../../guides/es/guia-de-configuracion.md)
para saber qué conserva cada nivel.

## El escenario

Una ejecución representativa del flujo: una prueba existente es la forma más
barata de conseguirla, y esta es esa prueba:

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

## La salida real

**En `Detail`** (el valor por defecto), `traces/CheckoutReviewFixture/a_customer_buys_two_keyboards.md`
dentro del directorio de salida de las pruebas:

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

**En `Summary`**, la misma ejecución de la misma prueba:

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

**El registro que escribió el mismo flujo** a través del logger del propio
proyecto, con todos sus valores:

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

**La traza estructural de esa ejecución**, `structural/CheckoutReviewFixture/a_customer_buys_two_keyboards.nt`
dentro del directorio de salida de las pruebas:

```text
scenario: A customer buys two keyboards

#1 - IOrderService.PlaceOrder(customerId, sku, quantity, cardNumber) → value
  #1.1 - IInventoryService.Reserve(sku, quantity)
  #1.2 - IPaymentService.Charge(customerId, amountCents, card) → value
  #1.3 - IOrderRepository.Save(order)
```

## Cómo leerla

**El flujo, a partir del artefacto `Detail`.** `#1` es la llamada que hizo la
prueba y la única raíz: todo lo indentado bajo ella ocurrió dentro de ella, en
el orden impreso. `#1.1` reserva dos unidades antes de cobrar nada; `#1.2` cobra
7998, dos unidades a 3999, de modo que el precio se deriva de la cantidad y no
se pasa como parámetro; `#1.3` guarda un pedido que ya lleva el id que devuelve
`#1`. Leído como una frase: este checkout primero reserva, segundo cobra,
tercero guarda, y el id del pedido se genera antes de guardar. Un cambio que
cobre antes de reservar, o que guarde antes de cobrar, es un cambio a *esto*, y
ahora lo sabes sin fiarte de un comentario.

**Para qué sirve cada nivel.** `Detail` lleva los valores de los parámetros, y
es lo que lees mientras trabajas en el flujo. En `Summary` todos los valores de
parámetro están vacíos (nunca se capturaron, que es distinto de estar ocultos al
renderizar) y solo se conservan la raíz y las llamadas hoja; este flujo tiene
dos niveles de profundidad, así que ningún marco es intermedio y aparecen las
mismas cuatro llamadas. Un flujo más profundo pierde su parte central. Los
valores de retorno sobreviven en ambos niveles: el retorno es la carga útil de
una narrativa. (`card:` está vacío y no en `[REDACTED]` aquí por la misma razón:
un valor que nunca se capturó no tiene nada que ocultar. `cardNumber` sigue
mostrando `[REDACTED]`, porque la lista de denegación por nombre se aplica en el
momento de la captura.) Usa `Summary` donde la forma y los resultados son lo que
necesitas y los valores son un coste (una ejecución de CI, una prueba de
resistencia larga), y `Off`/`Errors` en producción hasta que necesites más.

**Qué salida lee una persona y cuál un agente.** Las líneas de registro son para
la persona de guardia: se entremezclan con todo lo demás que registra el
servicio y llevan los valores. El `.nt` es para el agente y para quien revisa:
**sin valores por construcción**; compáralo con la línea de registro de arriba y
la diferencia no es que los valores se hayan sustituido, sino que nunca se
escribieron. `(customerId, sku, quantity, cardNumber)` son los *nombres* de los
parámetros; `→ value` dice que se devolvió un valor sin decir cuál. Eso es lo
que lo hace seguro de confirmar en el repositorio, de comparar en una revisión y
de entregar a un modelo: no puede filtrar un id de cliente, y no cambia cuando
cambian los datos, solo cuando cambian las *llamadas*. Fijarlo es
[Revisa una refactorización: ¿cambió el comportamiento?](revisa-una-refactorizacion-cambio-el-comportamiento.md).

## Qué no se captura

- **Las llamadas dentro de clases sin envolver.** Se envolvieron cuatro
  interfaces; una llamada entre dos clases tras ellas, o un método privado,
  nunca aparece. Un flujo que parece superficial puede ser profundo.
- **Lo que la integración elegida no intercepta.** El `DispatchProxy` solo ve
  las llamadas hechas *a través* de la referencia envuelta: una llamada interna
  que una clase se hace a sí misma (`this.Helper()`) le es invisible.
  [Elegir una integración](../elegir-una-integracion.md) es el análisis de
  compromisos por cada vía.
- **El trabajo que salió del hilo.** Una llamada entregada a otro hilo sin el
  contexto no viaja más lejos; la traza termina donde terminó el hilo.
- **Los valores que atrapó la lista de denegación o la regla de forma del
  valor**, aquí `cardNumber` y `card`, aparecen como `[REDACTED]` tanto en el
  registro como en el artefacto `Detail`, y están ausentes por construcción del
  `.nt`.
- **Una ejecución, no el contrato.** Otra entrada toma otro camino: un sku sin
  stock nunca llega a `#1.2`.

**La skill para esto:** `narrativetrace-verify`: escribe primero la intención,
ejecuta la ruta real más pequeña y lee la traza sin valores frente a esa
intención antes de dar nada por terminado.
