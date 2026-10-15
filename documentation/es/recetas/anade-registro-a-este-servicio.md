<!-- source: documentation/recipes/add-logging-to-this-service.md blob 3e78a8d6744b | translated: 2026-10-11 | reviewed: - -->
# Añade registro a este servicio

[English](../../recipes/add-logging-to-this-service.md) | **Español** | [Português](../../pt-BR/receitas/adicione-logs-a-este-servico.md) | [简体中文](../../zh-CN/实用方案/给这个服务加日志.md)

## El problema

> Añade registro (logging) a este servicio.

El servicio es un checkout. Reserva stock, cobra una tarjeta y guarda un
pedido, y no contiene ninguna instrucción de registro:

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

Lo que realmente quieres es aquello para lo que el registro es un medio: qué
hizo esta llamada, con qué valores lo hizo, en el registro que tu equipo ya
lee. Por eso la respuesta de abajo tampoco escribe ninguna instrucción de
registro.

## Configuración mínima

Además de la [instalación](../../guides/es/guia-de-instalacion.md),
`NarrativeTrace.Proxy` trae consigo `Runtime` y `Core`, esta página necesita
dos paquetes más: uno para el puente y otro para el proveedor de consola del
logger:

```bash
dotnet add package NarrativeTrace.Logging
dotnet add package Microsoft.Extensions.Logging.Console
```

`LoggingNarrativeContext` envuelve el contexto en el que graban los proxies y
refleja cada llamada en el `ILogger` que se le entregue. Nada del checkout lo
referencia. En una aplicación que ya tiene inyección de dependencias,
`AddNarrativeLogging()` conecta el mismo puente desde el contenedor; consulta
la [Guía de instalación](../../guides/es/guia-de-instalacion.md).

## El escenario

Cada colaborador se envuelve una sola vez, donde se construye; el checkout, el
último, de modo que las llamadas a través de él se capturan antes de llegar a
la implementación real. Nada más cambia:

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

La prueba es una prueba xUnit porque ahí es donde un flujo como este ya se
ejercita; el mismo cableado funciona en un `Program.cs`, en un registro de DI o
en un pipeline de peticiones. El `traceparent` fijo está ahí solo para que la
salida de esta página nombre siempre la misma traza; tu propio código lo deja
sin establecer.

## La salida real

Lo que escribió el logger, a través del proveedor del propio proyecto:

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

## Cómo leerla

- `→` abre una llamada, `←` cierra la abierta más recientemente, de modo que la
  indentación es innecesaria: el primer `→ IOrderService.PlaceOrder` y el
  último `← returned: "ORD-C-1234-SKU-KB-2"` son la misma llamada, y todo lo
  que hay entre ambos ocurrió dentro de ella. En los artefactos de traza esas
  mismas cuatro llamadas llevan los ids de span `#1`, `#1.1`, `#1.2` y `#1.3`;
  [Revisa un flujo complejo antes de cambiarlo](revisa-un-flujo-complejo-antes-de-cambiarlo.md)
  las lee de esa manera.
- Cada línea lleva los *nombres* de parámetro que declara el método, y eso es
  lo que la convierte en una frase y no en una tupla:
  `Reserve(sku: "SKU-KB", quantity: 2)` no necesita leyenda.
- `← completed` es el retorno de un método `void`; `← returned: …` lleva el
  valor. Que `Charge` devuelva `"PAY-C-1234-7998"` es el id propio de la
  pasarela, y `amountCents: 7998` es de donde sale el precio de dos teclados. El
  número entre corchetes tras la categoría es el id de evento del registro:
  `[1]` una entrada, `[2]` un retorno con valor, `[9]` la finalización de un
  método `void`.
- Faltan dos valores. `cardNumber: [REDACTED]` se ocultó por su *nombre*: la
  lista de denegación siempre activa, sin atributo ni configuración.
  `card: [REDACTED]`, una línea más abajo, se ocultó por su *valor*: el
  parámetro se llama `card`, que la lista de denegación no conoce, y el número
  que contenía es válido según Luhn, así que la regla de forma del valor lo
  detectó de todos modos. Consulta
  [Privacidad y ocultación](../privacidad-y-ocultacion.md).
- El texto del registro es todo lo que el proveedor de consola imprime por
  defecto. Los ids de span y de traza viajan en el ámbito (scope) del logger,
  así que un proveedor configurado para incluir ámbitos puede agrupar todas las
  líneas de un mismo checkout.

La conclusión que esto respalda: la narrativa salió de los nombres de método,
los nombres de parámetro y los valores, y no se escribió ni una sola
instrucción de registro para obtenerla.

## Qué no se captura

- **Solo se registra el código envuelto.** Arriba se envolvieron cuatro
  interfaces, así que aparecen cuatro llamadas. Una llamada entre dos clases
  sin envolver, o un método privado dentro de `DefaultOrderService`, es
  invisible aquí. Si envolver cada colaborador a mano no es lo que quieres,
  `AddNarrativeTracing` envuelve los servicios de interfaz que tu contenedor
  registra bajo un espacio de nombres, sin tocar el código de construcción:
  [Elegir una integración](../elegir-una-integracion.md).
- **Un valor que coincide con las reglas de ocultación se reemplaza antes de
  escribirse,** también en esta línea de registro. Eso no se puede relajar, y
  significa que un parámetro oculto no se puede recuperar a partir del
  registro.
- **El nivel propio del logger sigue aplicándose.** Estos registros son de nivel
  `Trace`; con el nivel mínimo de la categoría en `Information` la salida está
  vacía y nada te avisa.
- **Los tiempos no están en las líneas del registro.** La duración de cada
  llamada está en los artefactos de traza, no aquí.

**La skill para esto:** `add-narrative-tracing`: instala los paquetes, envuelve
la primera clase y comprueba el cableado con `narrativetrace-doctor`.
