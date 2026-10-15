<!-- source: documentation/recipes/review-a-refactoring-did-behaviour-change.md blob 8810b3b8224d | translated: 2026-10-11 | reviewed: - -->
# Revisa una refactorización: ¿cambió el comportamiento?

[English](../../recipes/review-a-refactoring-did-behaviour-change.md) | **Español** | [Português](../../pt-BR/receitas/revise-uma-refatoracao-o-comportamento-mudou.md) | [简体中文](../../zh-CN/实用方案/审查重构行为变了吗.md)

## El problema

> Refactoricé el checkout. Las pruebas están en verde. ¿Cambió el comportamiento?

Verde significa que ninguna aserción lo notó. Una refactorización que elimina
una llamada sobre la que nadie hace aserciones está en verde y equivocada, y esta
es la forma habitual: la llamada compensatoria en una ruta de error, a la que no
llega ninguna prueba del camino feliz.

## Configuración mínima

Además de la [instalación](../../guides/es/guia-de-instalacion.md), el modo de
aprobación: una variable de entorno y un directorio de líneas base confirmadas
en el repositorio:

```bash
NARRATIVETRACE_APPROVAL=true      # compare, don't just write
NARRATIVETRACE_APPROVED_DIR=narratives   # the default, beside the test project
```

La primera ejecución de cada prueba escribe
`narratives/<TestClass>/<scenario>.received.nt` y falla; la lees, ejecutas
`dotnet tool run dotnet-narrativetrace approve` para promover todos los
archivos revisados a `.approved.nt`, y los confirmas en el repositorio. Consulta
[Qué incluir en el commit](../que-incluir-en-el-commit.md) y la
[Guía de configuración](../../guides/es/guia-de-configuracion.md).

## El escenario

La prueba fija la ruta de error que una refactorización tiene más probabilidades
de romper (una tarjeta rechazada no debe guardar ningún pedido) y pasa contra
ambas versiones del checkout, que es todo el problema:

```csharp
using Checkout;
using NarrativeTrace.Core;
using NarrativeTrace.Proxy;
using NarrativeTrace.TestingXunit;
using Xunit;

namespace Checkout.Fixtures;

/// <summary>
/// The test recipes/review-a-refactoring-did-behaviour-change.md pins: a declined card must save
/// no order. It passes before and after the refactoring — which is the whole point of the page —
/// so only the approved structure can object.
/// </summary>
public sealed class RefactoredCheckoutFixture : IDisposable
{
    // A fixed W3C traceparent, so the page's embedded artifacts name the same trace every run.
    private const string DemoTraceparent = "00-c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7-c4e1d0a7c4e1d0a7-01";

    // One fixture per test: xUnit builds a fresh class instance for each test, so no test sees another's spans.
    private readonly NarrativeFixture _trace = new();

    public RefactoredCheckoutFixture()
    {
        _trace.Context.AdoptTraceparent(Traceparent.Parse(DemoTraceparent));
    }

    /// <summary>
    /// Which version of the checkout this run wires: the one that releases the reservation when the
    /// charge is declined, or the refactoring that dropped that call. An environment switch,
    /// because one build has to run both — in your project the refactoring is an edit to one file
    /// and this switch does not exist.
    /// </summary>
    private static bool RefactoringDroppedTheRelease =>
        Environment.GetEnvironmentVariable("CHECKOUT_RECIPE_REFACTORING") == "dropped-release";

    public void Dispose() => _trace.Dispose();

    [Fact]
    public void A_declined_card_saves_no_order()
    {
        var repository = new InMemoryOrderRepository();
        var inventory = NarrativeTraceProxy.Create<IInventoryService>(new InMemoryInventory("SKU-KB", 5), _trace.Context);
        var payments = NarrativeTraceProxy.Create<IPaymentService>(new CardPaymentService(), _trace.Context);
        var orders = NarrativeTraceProxy.Create<IOrderRepository>(repository, _trace.Context);
        IOrderService service = RefactoringDroppedTheRelease
            ? new OrderServiceWithoutRelease(inventory, payments, orders)
            : new DefaultOrderService(inventory, payments, orders);
        var checkout = NarrativeTraceProxy.Create<IOrderService>(service, _trace.Context);

        _trace.Run("A declined card saves no order", _ =>
        {
            Assert.Throws<PaymentDeclinedException>(
                () => checkout.PlaceOrder("C-1234", "SKU-KB", 2, CardPaymentService.DecliningCard));

            Assert.Empty(repository.Saved);
        });
        _trace.WriteArtifacts(nameof(RefactoredCheckoutFixture), nameof(A_declined_card_saves_no_order), failed: false);
    }
}
```

## La salida real

La línea base confirmada en el repositorio, en
`narratives/RefactoredCheckoutFixture/a_declined_card_saves_no_order.approved.nt`:

```text
scenario: A declined card saves no order

#1 - IOrderService.PlaceOrder(customerId, sku, quantity, cardNumber) !! PaymentDeclinedException
  #1.1 - IInventoryService.Reserve(sku, quantity)
  #1.2 - IPaymentService.Charge(customerId, amountCents, card) !! PaymentDeclinedException
  #1.3 - IInventoryService.Release(sku, quantity)
```

El veredicto de la ejecución refactorizada: las aserciones pasaron, la
estructura no:

```text
NarrativeTrace.Core.NarrativeApprovalException : Trace changed against the approved trace (-1 call IInventoryService.Release):
 scenario: A declined card saves no order
 
 #1 - IOrderService.PlaceOrder(customerId, sku, quantity, cardNumber) !! PaymentDeclinedException
   #1.1 - IInventoryService.Reserve(sku, quantity)
   #1.2 - IPaymentService.Charge(customerId, amountCents, card) !! PaymentDeclinedException
-  #1.3 - IInventoryService.Release(sku, quantity)
Received: artifacts/recipes/review-a-refactoring-did-behaviour-change/approved-after-the-refactoring/approved/RefactoredCheckoutFixture/a_declined_card_saves_no_order.received.nt
If this change is intended, promote it with: dotnet tool run dotnet-narrativetrace approve
```

Y el archivo que ofrece para su revisión, que `approve` promovería:

```text
scenario: A declined card saves no order

#1 - IOrderService.PlaceOrder(customerId, sku, quantity, cardNumber) !! PaymentDeclinedException
  #1.1 - IInventoryService.Reserve(sku, quantity)
  #1.2 - IPaymentService.Charge(customerId, amountCents, card) !! PaymentDeclinedException
```

(La ruta de `Received:` es la de este mismo repositorio: la ejecución que
produce esta página apunta `NARRATIVETRACE_APPROVED_DIR` bajo `artifacts/`, sobre
una copia de la línea base, porque la compilación que escribe una página no debe
escribir en el árbol de fuentes. En tu proyecto es `narratives/`, junto a la
línea base.)

## Cómo leerla

- **La primera línea nombra el cambio, no solo el hecho de que lo hay:**
  `-1 call IInventoryService.Release`. Una llamada menos, y cuál.
- **El cuerpo del diff es la forma fijada con la línea eliminada marcada.** `#1`,
  `#1.1` y `#1.2` no cambian y no están marcados;
  `-  #1.3 - IInventoryService.Release(sku, quantity)` es el comportamiento que
  se fue. Quien revisa y nunca ha visto este código puede saber qué hizo la
  refactorización a partir de cuatro líneas.
- **Las aserciones se mantuvieron.** El fallo es el de la aprobación, y ese es
  el sentido: la prueba afirma que no se guarda ningún pedido, y la
  refactorización no rompió eso. Nada en la prueba miró jamás la liberación, y
  no hacía falta.
- **Ahora decide.** Si eliminar la liberación es intencionado (por ejemplo,
  porque el llamador se hizo cargo de ella), ejecuta
  `dotnet tool run dotnet-narrativetrace approve`, revisa el diff en el archivo
  promovido y confírmalo en el repositorio: la nueva forma pasa a ser la fijada.
  Si no es intencionado, acabas de atrapar el error que
  [Ayúdame a depurar y arreglar este error](ayudame-a-depurar-y-arreglar-este-error.md)
  corrigió, cuando volvía a colarse. En cualquier caso, la decisión queda
  registrada en el repositorio, por una persona, en un archivo que quien revisa
  puede leer.
- **La línea base no contiene valores**, así que aprobarla no filtra nada y un
  cambio en los datos de prueba nunca la hace fallar:
  `(customerId, sku, quantity, cardNumber)` son nombres, y
  `!! PaymentDeclinedException` es un resultado.

## Qué no se captura

- **Esto es una comparación estructural: las mismas llamadas, en el mismo orden,
  con los mismos resultados.** No es una prueba de que todos los valores sean
  iguales. Una refactorización que cobra 7998 donde antes cobraba 3999, con
  todas las llamadas en su sitio, pasa esta puerta: para eso están tus
  aserciones, y el artefacto `Detail` es donde lo verías.
- **Solo las llamadas que la integración intercepta.** Una llamada privada
  eliminada, o una entre dos clases sin envolver, es invisible para la línea
  base exactamente igual que lo es para la traza.
- **Una prueba que no se ejecuta no se compara.** La aprobación protege los
  escenarios que fijaste; un flujo sin prueba no tiene línea base, y borrar una
  prueba borra su puerta (su `.approved.nt` se queda, una línea base que ya nada
  compara). Un `.received.nt` que nadie promovió es lo que
  `narrativetrace-doctor` reporta, como `trap.approval-traces`.
- **Los tiempos nunca forman parte de ella.** La línea base no lleva duraciones,
  así que no puede atrapar un cambio que solo hizo el flujo más lento.

**La skill para esto:** `narrativetrace-verify`: lee la traza estructural frente
a la intención del cambio y fija el flujo como línea base con tu aprobación.
