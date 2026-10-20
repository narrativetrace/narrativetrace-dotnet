<!-- source: documentation/recipes/trace-in-production-without-the-cost.md blob 1000c15fdfdb | translated: 2026-10-18 | reviewed: - -->
# Traza en producción sin el coste

[English](../../recipes/trace-in-production-without-the-cost.md) | **Español** | [Português](../../pt-BR/receitas/trace-em-producao-sem-o-custo.md) | [简体中文](../../zh-CN/实用方案/在生产环境追踪而不付出代价.md)

## El problema

> Quiero esto en producción para las peticiones que salen mal. No voy a pagar
> por una traza completa de cada petición que sale bien.

Eso es un solo ajuste —`TracingLevel`— y esta página es la misma ejecución vista
a través de él dos veces: el checkout con pago rechazado en `Detail`, donde se
captura cada valor, y en `Errors`, donde solo sobreviven las ejecuciones que
fallaron.

## Configuración mínima

Un nivel, establecido allí donde se configure tu despliegue. Estas tres formas
dicen lo mismo:

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

Consulta la [Guía de configuración](../../guides/es/guia-de-configuracion.md)
para ver qué conserva cada nivel y qué fuente gana. El nivel es el dial propio
de NarrativeTrace y está al frente: una llamada que filtra nunca pasa a formar
parte de la traza, para ningún consumidor. El nivel de tu logger es un segundo
dial, independiente, que decide lo que se *imprime*
([Guía de configuración §8, "Dos diales, dos vías"](../../guides/es/guia-de-configuracion.md#8-dos-diales-dos-vías)
es la versión larga), y la distinción importa aquí porque subir el nivel del
logger te ahorra volumen de log sin cambiar nada de la captura.

## El escenario

La ejecución es un checkout con pago rechazado —`Reserve`, luego `Charge`
lanzando una excepción, luego `Release`, el mismo que reproduce
[Ayúdame a depurar y arreglar este error](ayudame-a-depurar-y-arreglar-este-error.md)—
reutilizado, porque el tema de esta página es el dial y no otro flujo. El
fixture de abajo lo ejecuta, y ejecuta también un checkout *exitoso*, bajo el
nivel con el que se inicie; el build lo inicia una vez en `Detail` y otra en
`Errors`:

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

## La salida real

**En `Detail`**, la ejecución rechazada, con cada valor capturado:

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

**En `Errors`**, la misma ejecución del mismo código:

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

**Y en `Errors`, un checkout que tuvo éxito**: ningún artefacto en absoluto. No
hay nada que incrustar, que es justo el punto: el fixture afirma las dos
mitades de esa afirmación, la ausencia del archivo en `Errors` y su presencia
en `Detail`, de modo que el vacío es obra del nivel y no de un camino que
nunca estuvo bien.

## Cómo leerla

- **Qué conserva `Errors`.** La ejecución fallida, entera. `#1` lanzó una
  excepción, y el subárbol de un nodo con error se conserva intacto: así que
  `Reserve` (`#1.1`), `Charge` (`#1.2`) y `Release` (`#1.3`) siguen todos ahí,
  incluidas las dos llamadas que retornaron con normalidad. Es deliberado: las
  llamadas alrededor de un fallo son lo que necesitas para diagnosticarlo, y
  descartar al hermano exitoso te dejaría con una excepción y sin historia. Una
  llamada que retornó *y* no conduce a ningún fallo por debajo se descarta, y
  una raíz sin ningún fallo en todo su subárbol se descarta por completo, razón
  por la cual el checkout exitoso no escribió nada.
- **Qué descarta `Errors`.** Cada valor de parámetro: `customerId` y `quantity`
  se renderizan como un par vacío de acentos graves. El vacío es supresión en la
  *captura*: el valor nunca se renderizó, así que no se puede recuperar a partir
  de estos eventos después, en ningún nivel posterior. Lo que sobrevive es la
  forma, los resultados y la excepción, y `cardNumber: [REDACTED]` sigue
  oculto porque la ocultación es una decisión distinta de la supresión.
- **La poda ocurre después de la captura, no en lugar de ella.** Cada llamada
  se interceptó y cada evento se publicó; el constructor del árbol construyó
  entonces el árbol y quitó lo que el nivel no conserva. Esa es la forma
  honesta del coste: `Errors` es más barato que `Detail` porque los valores no
  se renderizan —la parte cara—, no porque haya llamadas sin interceptar.
- **Solo `Off` se salta la intercepción.** En `Off`, la compuerta `IsActive` del
  proxy retorna antes de que exista cualquier array de argumentos o evento, y
  nada aguas abajo puede traer la llamada de vuelta. `Errors`, `Summary` y
  `Narrative` siguen interceptando todas las llamadas. Si la pregunta es "cuánto
  cuesta el tracing en este bucle caliente", la respuesta es otro nivel para ese
  ámbito, o `Off`.
- **Los números.** Las cifras medidas —el coste de un ciclo de entrada/salida,
  la asignación de memoria por llamada, la compuerta de regresión que las
  sostiene— están en la
  [sección Performance del README](../../../README.md#performance), junto a la
  línea base confirmada de la que proceden. No se repiten aquí, porque una
  copia que se mueve con las mediciones vale más que dos que discrepan.

## Qué no se captura

- **Las cifras publicadas son mediciones, no una promesa.** Se tomaron con
  BenchmarkDotNet en el host que nombra el README, con una operación de
  benchmark por llamada. Tus números dependen de tu runtime, tu hardware, la
  forma de tus parámetros (un grafo de objetos profundo se renderiza durante más
  tiempo que un `int`) y de cuánto de tu código se traza. Mide el nivel que
  pretendes ejecutar.
- **Un nivel no es un control de privacidad.** `Errors` escribe menos valores
  porque los suprime, pero supresión y ocultación son mecanismos distintos con
  garantías distintas:
  [Mantén los secretos fuera de los logs](manten-los-secretos-fuera-de-los-logs.md)
  es el que se sostiene en todos los niveles.
- **Nada de aquí cambia cuánto escribe tu logger.** Cada evento capturado se
  sigue ofreciendo al puente de logging, así que el nivel propio de la
  categoría `NarrativeTrace` es el que decide el volumen de log. Dos diales.
- **Un nivel no se puede subir con efecto retroactivo.** Los valores no
  capturados se han perdido; reproducir la ejecución en `Detail` es la única
  vuelta atrás, que es lo que hace la receta de depuración.
- **Aquí no se muestran niveles por ámbito.** Esta página establece un nivel
  para toda una ejecución. Acotar en cambio el ámbito trazado —qué clases se
  envuelven en absoluto— es la otra mitad de la cuestión del coste, y
  pertenece a [Elegir una integración](../elegir-una-integracion.md).

**La skill para esto:** `narrativetrace-doctor`: informa del nivel con el que
un proyecto se ejecuta realmente, que es lo primero que hay que comprobar
cuando una traza es más escasa de lo esperado.
