<!-- source: documentation/recipes/why-did-this-test-fail.md blob 5a64ca47c62a | translated: 2026-10-11 | reviewed: - -->
# Por qué falló esta prueba

[English](../../recipes/why-did-this-test-fail.md) | **Español** | [Português](../../pt-BR/receitas/por-que-este-teste-falhou.md) | [简体中文](../../zh-CN/实用方案/这个测试为什么失败了.md)

## El problema

> ¿Por qué falló esta prueba?

Una prueba del checkout está en rojo. La aserción dice qué no coincidió; no dice
qué hizo el código por el camino, que es lo que necesitas para saber si lo
equivocado es el código o la expectativa.

## Configuración mínima

Nada más allá de la [instalación](../../guides/es/guia-de-instalacion.md) y la
integración con xUnit. `NarrativeFixture` escribe una narrativa por prueba sin
configuración, también para una prueba que falla, que es el caso de esta página.
Llama a `WriteArtifacts(..., failed: true)` desde la ruta de fallo y los
archivos quedan en `TestResults/narrativetrace/traces/<TestClass>/<scenario>.md`;
`manifest.json`, a su lado, lista todos los artefactos de todos los escenarios
cuando conoces el nombre de la prueba pero no el de su archivo. Consulta
[Qué incluir en el commit](../que-incluir-en-el-commit.md) para el conjunto
completo.

## El escenario

La prueba que falla:

```csharp
using Checkout;
using NarrativeTrace.Core;
using NarrativeTrace.Proxy;
using NarrativeTrace.TestingXunit;
using Xunit;

namespace Checkout.Fixtures;

/// <summary>
/// The failing checkout test recipes/why-did-this-test-fail.md is read through. It fails on an
/// assertion, by design. Nothing here says which side is wrong — that is the reader's exercise,
/// and the narrative the run writes is what settles it.
/// </summary>
public sealed class StaleExpectationFixture : IDisposable
{
    // A fixed W3C traceparent, so the page's embedded artifact names the same trace every run.
    private const string DemoTraceparent = "00-c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7-c4e1d0a7c4e1d0a7-01";

    // One fixture per test: xUnit builds a fresh class instance for each test, so no test sees another's spans.
    private readonly NarrativeFixture _trace = new();

    public StaleExpectationFixture()
    {
        _trace.Context.AdoptTraceparent(Traceparent.Parse(DemoTraceparent));
    }

    public void Dispose() => _trace.Dispose();

    [Fact]
    public void The_customer_gets_an_order_id_for_their_keyboards()
    {
        var stock = new InMemoryInventory("SKU-KB", 5);
        var inventory = NarrativeTraceProxy.Create<IInventoryService>(stock, _trace.Context);
        var payments = NarrativeTraceProxy.Create<IPaymentService>(new CardPaymentService(), _trace.Context);
        var orders = NarrativeTraceProxy.Create<IOrderRepository>(new InMemoryOrderRepository(), _trace.Context);
        var checkout = NarrativeTraceProxy.Create<IOrderService>(
            new DefaultOrderService(inventory, payments, orders), _trace.Context);

        var failed = true;
        try
        {
            _trace.Run("The customer gets an order id for their keyboards", _ =>
            {
                var orderId = checkout.PlaceOrder("C-1234", "SKU-KB", 2, "4242424242424242");

                Assert.Equal("ORD-C-1234-SKU-KB-1", orderId);
            });
            failed = false;
        }
        finally
        {
            _trace.WriteArtifacts(nameof(StaleExpectationFixture),
                nameof(The_customer_gets_an_order_id_for_their_keyboards), failed);
        }
    }
}
```

## La salida real

Lo que reportó el ejecutor de pruebas:

```text
Assert.Equal() Failure: Strings differ
                             ↓ (pos 18)
Expected: "ORD-C-1234-SKU-KB-1"
Actual:   "ORD-C-1234-SKU-KB-2"
                             ↑ (pos 18)
```

Y lo que escribió la misma ejecución a su lado, en
`traces/StaleExpectationFixture/the_customer_gets_an_order_id_for_their_keyboards.md`:

```text
---
type: trace
scenario: The customer gets an order id for their keyboards
entry_point: IOrderService.PlaceOrder
trace_id: c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7
trace_name: solid wand romps
method_count: 4
error_count: 0
result: success
duration_ms: 10
---

## Trace: solid wand romps — IOrderService.PlaceOrder

**Scenario:** The customer gets an order id for their keyboards
**Duration:** 10ms | **Result:** FAILED

### Call Flow

- **IOrderService.PlaceOrder**(customerId: `"C-1234"`, sku: `"SKU-KB"`, quantity: `2`, cardNumber: `[REDACTED]`) → `"ORD-C-1234-SKU-KB-2"` — 10ms #1
  - **IInventoryService.Reserve**(sku: `"SKU-KB"`, quantity: `2`) — 0ms #1.1
  - **IPaymentService.Charge**(customerId: `"C-1234"`, amountCents: `7998`, card: `[REDACTED]`) → `"PAY-C-1234-7998"` — 6ms #1.2
  - **IOrderRepository.Save**(order: `Order(OrderId: "ORD-C-1234-SKU-KB-2", CustomerId: "C-1234", Sku: "SKU-KB", Quantity: 2, AmountCents: 7998)`) — 0ms #1.3
```

## Cómo leerla

Lee la narrativa de arriba abajo frente a la aserción, un span cada vez:

- `#1 IOrderService.PlaceOrder(... quantity: 2 ...) → "ORD-C-1234-SKU-KB-2"`.
  El valor que rechazó la aserción está ahí mismo, y también la entrada que lo
  produjo: la llamada pidió **dos** teclados.
- `#1.1 IInventoryService.Reserve(sku: "SKU-KB", quantity: 2)`: dos unidades
  reservadas, coherente con la llamada.
- `#1.2 IPaymentService.Charge(..., amountCents: 7998, ...)`: 7998 son dos
  unidades a 3999, así que el precio también concuerda con la cantidad.
- `#1.3 IOrderRepository.Save(order: Order(... Quantity: 2 ...))`: lo que se
  almacenó concuerda con lo que se pidió.
- `**Result:** FAILED` en la cabecera es el veredicto de la *prueba*, no del
  código: `error_count: 0` y todos los spans se completaron. Nada lanzó una
  excepción.

Todos los spans concuerdan entre sí y con la llamada que los inició. La
expectativa, `ORD-C-1234-SKU-KB-1`, es lo único en la ejecución que habla de un
teclado, así que es lo que está desactualizado; el escenario contra el que se
escribió ha cambiado desde entonces para pedir dos. La corrección está en la
prueba.

La lectura opuesta es la otra receta: cuando un span *discrepa* de la intención
(una cantidad que cambia entre dos llamadas, una llamada que debía seguir y no
lo hizo), el código está mal, y
[Ayúdame a depurar y arreglar este error](ayudame-a-depurar-y-arreglar-este-error.md)
es ese recorrido.

## Qué no se captura

- **Solo la ejecución de esa prueba.** La narrativa se escribe por método de
  prueba y por ejecución. Un fallo que ocurre una vez de cada veinte
  ejecuciones necesita que una de esas ejecuciones sea la que escribió el
  archivo; cada ejecución sobrescribe el artefacto de la anterior, y una
  ejecución fallida no hace avanzar el `.nt` de la última ejecución en verde.
- **La aserción no está en la traza.** Las dos salidas de arriba vienen de dos
  sitios (el ejecutor reportó la discrepancia, el fixture escribió el flujo) y
  nada las correlaciona automáticamente más allá del nombre del escenario.
- **Un valor que una prueba calculó pero nunca pasó a una llamada trazada es
  invisible.** La traza lleva los parámetros y los valores de retorno de las
  llamadas envueltas, no las variables locales.
- **Los tiempos en una ejecución fallida no significan nada.** La ejecución dejó
  de ser representativa en el momento en que la aserción lanzó la excepción.

**La skill para esto:** `narrativetrace-debug`: parte del síntoma, lee el primer
span donde un valor diverge y lo nombra por su id de span.
