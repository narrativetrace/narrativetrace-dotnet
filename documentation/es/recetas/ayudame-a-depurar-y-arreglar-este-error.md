<!-- source: documentation/recipes/help-me-debug-and-fix-this-bug.md blob 245347510dd7 | translated: 2026-10-11 | reviewed: - -->
# Ayúdame a depurar y arreglar este error

[English](../../recipes/help-me-debug-and-fix-this-bug.md) | **Español** | [Português](../../pt-BR/receitas/ajude-me-a-depurar-e-corrigir-este-bug.md) | [简体中文](../../zh-CN/实用方案/帮我调试并修复这个缺陷.md)

## El problema

> Ayúdame a depurar y arreglar este error: a un cliente le rechazaron la
> tarjeta y el teclado nunca volvió al stock.

El error que tiene el equipo es `PaymentDeclinedException: card declined by the
issuer`, y ese no es el error. El error es lo que hizo el checkout *después*.

## Configuración mínima

Nada más allá de la [instalación](../../guides/es/guia-de-instalacion.md) y la
integración con xUnit: `NarrativeFixture` escribe una traza por prueba sin
configuración. El último paso de esta página, fijar el flujo ya corregido,
activa el modo de aprobación, que es una variable de entorno, establecida para
toda la ejecución de pruebas desde un archivo `.runsettings`:

```xml
<RunSettings>
  <RunConfiguration>
    <EnvironmentVariables>
      <NARRATIVETRACE_APPROVAL>true</NARRATIVETRACE_APPROVAL>
    </EnvironmentVariables>
  </RunConfiguration>
</RunSettings>
```

Consulta [Qué incluir en el commit](../que-incluir-en-el-commit.md) y la
[Guía de configuración](../../guides/es/guia-de-configuracion.md).

## El escenario

Reprodúcelo en la prueba más pequeña que pueda fallar por este motivo: una
tarjeta rechazada debe dejar el stock como lo encontró. La segunda prueba es la
misma reproducción contra el checkout tal como estaba antes de la corrección, y
falla por diseño: es la ejecución de la que sale la primera traza de abajo.

```csharp
using Checkout;
using NarrativeTrace.Core;
using NarrativeTrace.Proxy;
using NarrativeTrace.TestingXunit;
using Xunit;

namespace Checkout.Fixtures;

/// <summary>
/// The reproduction recipes/help-me-debug-and-fix-this-bug.md is written around: a declined card
/// must leave the stock as it found it. The second test is the same reproduction against the
/// checkout as it stood before the fix, so the page's "before" trace comes from a real failing run
/// rather than from prose; it fails by design.
/// </summary>
public sealed class DeclinedPaymentFixture : IDisposable
{
    // A fixed W3C traceparent, so the page's embedded artifacts name the same trace every run.
    private const string DemoTraceparent = "00-c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7-c4e1d0a7c4e1d0a7-01";

    // One fixture per test: xUnit builds a fresh class instance for each test, so no test sees another's spans.
    private readonly NarrativeFixture _trace = new();

    public DeclinedPaymentFixture()
    {
        _trace.Context.AdoptTraceparent(Traceparent.Parse(DemoTraceparent));
    }

    public void Dispose() => _trace.Dispose();

    [Fact]
    public void The_stock_is_released_when_the_card_is_declined()
    {
        var stock = new InMemoryInventory("SKU-KB", 5);
        var repository = new InMemoryOrderRepository();
        var inventory = NarrativeTraceProxy.Create<IInventoryService>(stock, _trace.Context);
        var payments = NarrativeTraceProxy.Create<IPaymentService>(new CardPaymentService(), _trace.Context);
        var orders = NarrativeTraceProxy.Create<IOrderRepository>(repository, _trace.Context);
        var checkout = NarrativeTraceProxy.Create<IOrderService>(
            new DefaultOrderService(inventory, payments, orders), _trace.Context);

        _trace.Run("The stock is released when the card is declined", _ =>
        {
            Assert.Throws<PaymentDeclinedException>(
                () => checkout.PlaceOrder("C-1234", "SKU-KB", 2, CardPaymentService.DecliningCard));

            Assert.Empty(repository.Saved);
            Assert.Equal(5, stock.AvailableUnits("SKU-KB"));
        });
        _trace.WriteArtifacts(nameof(DeclinedPaymentFixture),
            nameof(The_stock_is_released_when_the_card_is_declined), failed: false);
    }

    [Fact]
    public void The_stock_is_released_when_the_card_is_declined_before_the_fix()
    {
        var stock = new InMemoryInventory("SKU-KB", 5);
        var repository = new InMemoryOrderRepository();
        var inventory = NarrativeTraceProxy.Create<IInventoryService>(stock, _trace.Context);
        var payments = NarrativeTraceProxy.Create<IPaymentService>(new CardPaymentService(), _trace.Context);
        var orders = NarrativeTraceProxy.Create<IOrderRepository>(repository, _trace.Context);
        var checkout = NarrativeTraceProxy.Create<IOrderService>(
            new OrderServiceWithoutRelease(inventory, payments, orders), _trace.Context);

        try
        {
            _trace.Run("The stock is released when the card is declined, before the fix", _ =>
            {
                Assert.Throws<PaymentDeclinedException>(
                    () => checkout.PlaceOrder("C-1234", "SKU-KB", 2, CardPaymentService.DecliningCard));

                Assert.Empty(repository.Saved);
                Assert.Equal(5, stock.AvailableUnits("SKU-KB"));
            });
        }
        catch
        {
            _trace.WriteArtifacts(nameof(DeclinedPaymentFixture),
                nameof(The_stock_is_released_when_the_card_is_declined_before_the_fix), failed: true);
            throw;
        }
    }
}
```

El checkout que cablea la segunda prueba, antes de la corrección:

```csharp
namespace Checkout;

/// <summary>
/// The same checkout with no release path: a declined charge leaves the reservation behind.
/// Two recipe pages need this code to run, not to be described — the bug before its fix and the
/// refactoring that drops the release while every test stays green. Never the implementation to
/// copy: <see cref="DefaultOrderService"/> is.
/// </summary>
public sealed class OrderServiceWithoutRelease(
    IInventoryService inventory, IPaymentService payments, IOrderRepository orders) : IOrderService
{
    private const int UnitPriceCents = 3999;

    public string PlaceOrder(string customerId, string sku, int quantity, string cardNumber)
    {
        inventory.Reserve(sku, quantity);
        var amountCents = UnitPriceCents * quantity;
        payments.Charge(customerId, amountCents, cardNumber);
        var orderId = $"ORD-{customerId}-{sku}-{quantity}";
        orders.Save(new Order(orderId, customerId, sku, quantity, amountCents));
        return orderId;
    }
}
```

## La salida real

Lo que dijo el ejecutor de pruebas sobre la ejecución fallida:

```text
Assert.Equal() Failure: Values differ
Expected: 5
Actual:   3
```

Ese es el síntoma (dos unidades siguen retenidas) y no dice por qué. La propia
narrativa de la ejecución fallida sí lo dice, escrita a su lado en
`traces/DeclinedPaymentFixture/the_stock_is_released_when_the_card_is_declined_before_the_fix.md`
dentro del directorio de salida de las pruebas:

```text
---
type: trace
scenario: The stock is released when the card is declined before the fix
entry_point: IOrderService.PlaceOrder
trace_id: c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7
trace_name: solid wand romps
method_count: 3
error_count: 2
result: error
duration_ms: 5
---

## Trace: solid wand romps — IOrderService.PlaceOrder

**Scenario:** The stock is released when the card is declined before the fix
**Duration:** 5ms | **Result:** FAILED

### Call Flow

- **IOrderService.PlaceOrder**(customerId: `"C-1234"`, sku: `"SKU-KB"`, quantity: `2`, cardNumber: `[REDACTED]`) — 5ms #1
  - **IInventoryService.Reserve**(sku: `"SKU-KB"`, quantity: `2`) — 1ms #1.1
  - **IPaymentService.Charge**(customerId: `"C-1234"`, amountCents: `7998`, card: `[REDACTED]`) ❌ PaymentDeclinedException: card declined by the issuer — 1ms #1.2
  - ❌ PaymentDeclinedException: card declined by the issuer
```

La corrección es un solo `catch`: liberar lo que se reservó y luego dejar que el
rechazo se propague como antes.

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

La misma reproducción, ahora pasando, contra el checkout corregido:

```text
---
type: trace
scenario: The stock is released when the card is declined
entry_point: IOrderService.PlaceOrder
trace_id: c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7
trace_name: solid wand romps
method_count: 4
error_count: 2
result: error
duration_ms: 1
---

## Trace: solid wand romps — IOrderService.PlaceOrder

**Scenario:** The stock is released when the card is declined
**Duration:** 1ms | **Result:** PASSED

### Call Flow

- **IOrderService.PlaceOrder**(customerId: `"C-1234"`, sku: `"SKU-KB"`, quantity: `2`, cardNumber: `[REDACTED]`) — 1ms #1
  - **IInventoryService.Reserve**(sku: `"SKU-KB"`, quantity: `2`) — 0ms #1.1
  - **IPaymentService.Charge**(customerId: `"C-1234"`, amountCents: `7998`, card: `[REDACTED]`) ❌ PaymentDeclinedException: card declined by the issuer — 0ms #1.2
  - **IInventoryService.Release**(sku: `"SKU-KB"`, quantity: `2`) — 0ms #1.3
  - ❌ PaymentDeclinedException: card declined by the issuer
```

Con el modo de aprobación activado, la primera ejecución de la prueba corregida
escribe su estructura sin valores junto a la prueba como un `.received.nt` y
falla; `dotnet tool run dotnet-narrativetrace approve` la promueve a la línea
base confirmada en el repositorio, que pasa a ser la forma de este flujo:

```text
scenario: The stock is released when the card is declined

#1 - IOrderService.PlaceOrder(customerId, sku, quantity, cardNumber) !! PaymentDeclinedException
  #1.1 - IInventoryService.Reserve(sku, quantity)
  #1.2 - IPaymentService.Charge(customerId, amountCents, card) !! PaymentDeclinedException
  #1.3 - IInventoryService.Release(sku, quantity)
```

## Cómo leerla

En la traza fallida:

- `#1 IOrderService.PlaceOrder` termina con `❌ PaymentDeclinedException` y sin
  valor de retorno: la llamada que hizo el cliente no se completó.
- `#1.1 IInventoryService.Reserve(sku: "SKU-KB", quantity: 2)` no tiene `❌` ni
  valor: un método `void` que se completó. Ahora hay dos unidades retenidas.
- `#1.2 IPaymentService.Charge` lanzó una excepción. Sus parámetros dicen qué se
  intentó: `amountCents: 7998` por las dos unidades, `card: [REDACTED]`.
- **Nada sigue a `#1.2`.** `#1` pasa directamente del cobro a su propio fallo,
  así que las dos unidades que reservó `#1.1` siguen retenidas. Esa ausencia es
  el error, y el mensaje de la excepción nunca la menciona: la traza muestra el
  flujo completo y no solo la línea que lanzó la excepción, y eso es lo que hace
  visible una omisión.

En la traza que pasa: `#1.3 IInventoryService.Release(sku: "SKU-KB", quantity: 2)`
está ahora entre el cobro y el fallo. `#1` sigue terminando en el rechazo, y eso
es correcto: la tarjeta del cliente de verdad fue rechazada, y el stock ha
vuelto.

En la línea base: el `.nt` aprobado lleva las mismas cuatro llamadas con los
*nombres* de sus parámetros y ningún valor, y por eso se puede confirmar en el
repositorio y leer en una revisión. `#1.3` forma ahora parte de la forma fijada:
un cambio posterior que elimine la liberación hace que una prueba que pasa falle
con un diff que nombra a `IInventoryService.Release`, de modo que este error no
puede volver en silencio. Eso es
[Revisa una refactorización: ¿cambió el comportamiento?](revisa-una-refactorizacion-cambio-el-comportamiento.md).

## Qué no se captura

- **La traza muestra las llamadas que ocurrieron, no las que debieron
  ocurrir.** El `Release` que falta es una ausencia que el lector infiere del
  flujo; nada en el artefacto dice "falta una llamada". Lo que añade la línea
  base es la comparación que convierte una ausencia en un fallo de compilación,
  una vez.
- **Una excepción tragada dentro de código sin envolver es invisible.** Solo
  aparecen las interfaces envueltas. Un `try`/`catch` dentro de
  `DefaultOrderService` que se comiera el rechazo y devolviera con normalidad se
  vería como una llamada completada.
- **El número de tarjeta no se puede recuperar** de la traza ni del registro. Se
  oculta en la captura, aquí por su valor: el parámetro se llama `card`, así que
  la lista de denegación por nombre nunca lo vio, y el número es válido según
  Luhn.
- **Los tiempos no son evidencia de un problema de rendimiento** en una prueba
  tan pequeña: la primera llamada a través de un proxy en un proceso lleva su
  configuración inicial única.

**La skill para esto:** `narrativetrace-debug`: reproduce el síntoma con la
entrada más pequeña, nombra el span donde el valor o la llamada diverge, lo
corrige ahí y fija el flujo como prueba de regresión con tu aprobación.
