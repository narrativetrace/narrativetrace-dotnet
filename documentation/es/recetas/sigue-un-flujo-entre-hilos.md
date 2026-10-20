<!-- source: documentation/recipes/follow-a-flow-across-threads.md blob f1abd00eb8da | translated: 2026-10-18 | reviewed: - -->
# Sigue un flujo entre hilos

[English](../../recipes/follow-a-flow-across-threads.md) | **Español** | [Português](../../pt-BR/receitas/acompanhe-um-fluxo-entre-threads.md) | [简体中文](../../zh-CN/实用方案/跨线程追踪一个流程.md)

## El problema

> El checkout pasa las partes lentas a otros hilos. La traza se detiene en el
> envío y no puedo ver qué hizo el trabajo en segundo plano.

Un hilo no sabe nada del span en el que está otro hilo, así que entregar
trabajo a un worker entrega el trabajo y nada más. La traza también hay que
entregarla, de forma explícita, en el momento del envío. Abajo hay una
ejecución que lo hace y otra que no.

## Configuración mínima

Nada más allá de la [instalación](../../guides/es/guia-de-instalacion.md):
`ForkJoinGroup` y `FireAndForgetGroup` están en `NarrativeTrace.Runtime`, junto
a los contextos. Cuál usar depende de lo que haga el llamador a continuación:

| La llamada que lanza | El helper | El paso final |
|---|---|---|
| Espera el trabajo en segundo plano (esta página) | `ForkJoinGroup` | `await group.JoinAsync()` — injerta lo que registraron las ramas bajo el span que las lanzó |
| Retorna sin esperar | `FireAndForgetGroup` | Ninguno. Los hijos son un registro aparte, que se lee mediante `ChildRoots()` |

A una rama se le entrega un **contexto hijo aislado** y debe trazar a través de
ese argumento: un colaborador envuelto con `NarrativeTraceProxy.Create(service, branch)`
dentro de la rama es lo que registra el trabajo. Si tu código permanece en un
mismo flujo lógico a través de `await`, `AsyncNarrativeContext.RunAsync` lleva
la traza ambiente por ti y no escribes ninguna de las dos cosas anteriores: la
[Guía de funcionalidades](../../feature-guide.md) es la versión fila por fila.

Las líneas de registro usan el mismo logger que
[Sigue una petición entre servicios](sigue-una-peticion-entre-servicios.md),
con el id de traza sin procesar entre corchetes tras la categoría; en esta
página el corchete vacío es tan revelador como los llenos.

## El escenario

**El checkout, con los dos pasos lentos fuera de su propio hilo.** La entrega
es `group.Fork(branch => …)` alrededor de cada tarea y `await group.JoinAsync()`
después de iniciar ambas. El inventario y la comprobación antifraude se pasan
sin envolver, de modo que la clase decide por qué contexto traza cada rama:

```csharp
using NarrativeTrace.Core;
using NarrativeTrace.Proxy;
using NarrativeTrace.Runtime;

namespace Checkout;

/// <summary>
/// The same checkout with the two slow steps off the caller's thread: reserve the stock and run the
/// fraud check as two branches of a <see cref="ForkJoinGroup"/>, wait for both, then charge and save.
/// A branch runs on a pool thread that knows nothing about the caller's trace, so each branch is
/// handed an isolated child context of the group and traces through it; <c>JoinAsync</c> grafts what
/// the branches recorded back under the span that launched them. Skip the join and the branches stay
/// out of the caller's trace. The inventory and the fraud check are passed unwrapped for that reason:
/// this class decides which context each call is traced through.
/// </summary>
public sealed class ParallelCheckout(
    INarrativeContext context,
    IInventoryService inventory,
    IFraudCheckService fraud,
    IPaymentService payments,
    IOrderRepository orders) : IOrderService
{
    private const int UnitPriceCents = 3999;

    public string PlaceOrder(string customerId, string sku, int quantity, string cardNumber)
    {
        var amountCents = UnitPriceCents * quantity;
        var group = ForkJoinGroup.Create(context);
        var reserving = group.Fork(branch =>
        {
            NarrativeTraceProxy.Create(inventory, branch).Reserve(sku, quantity);
            return true;
        });
        var scoring = group.Fork(branch => NarrativeTraceProxy.Create(fraud, branch).Approve(customerId, amountCents));
        group.JoinAsync().GetAwaiter().GetResult();

        if (!scoring.Result)
        {
            NarrativeTraceProxy.Create(inventory, context).Release(sku, quantity);
            throw new PaymentDeclinedException("the fraud check refused the order");
        }

        _ = reserving.Result;
        payments.Charge(customerId, amountCents, cardNumber);
        var orderId = $"ORD-{customerId}-{sku}-{quantity}";
        orders.Save(new Order(orderId, customerId, sku, quantity, amountCents));
        return orderId;
    }
}
```

**La ejecución, y el mismo trabajo sin la entrega.** La primera prueba ejecuta
ese checkout. La segunda entrega una llamada trazada a un hilo worker sin
ningún grupo, que es el aspecto que tenía el código antes de añadir la
entrega: el worker es un hilo que ya estaba en marcha, y se mantiene retenido
hasta que la llamada que lanza ha retornado, así que el orden de los eventos es
el mismo en cada ejecución:

```csharp
using System.Collections.Concurrent;
using Checkout;
using Microsoft.Extensions.Logging;
using NarrativeTrace.Core;
using NarrativeTrace.Logging;
using NarrativeTrace.Proxy;
using NarrativeTrace.Runtime;
using Xunit;

namespace Checkout.Tests;

/// <summary>
/// Proves recipes/follow-a-flow-across-threads.md: the two branches the checkout hands to a
/// <see cref="ForkJoinGroup"/> come home inside the caller's trace — same trace id, grafted under the
/// span that launched them, recorded on other threads — and a task handed to a worker without the
/// group is not under that span at all (rule 8, docs as tests).
/// </summary>
public sealed class AcrossThreadsRecipeTests
{
    private const string Slug = "follow-a-flow-across-threads";

    // A fixed W3C traceparent, so the page's embedded artifacts name the same trace every run.
    private const string DemoTraceparent = "00-c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7-c4e1d0a7c4e1d0a7-01";

    [Fact]
    public void The_background_tasks_stay_in_the_customers_trace()
    {
        var log = new StringWriter { NewLine = "\n" };
        var context = CallerContext(log);
        var stock = new InMemoryInventory("SKU-KB", 5);
        var repository = new InMemoryOrderRepository();
        var checkout = ParallelCheckoutOver(context, stock, repository);

        var orderId = checkout.PlaceOrder("C-1234", "SKU-KB", 2, "4242424242424242");

        Assert.Equal("ORD-C-1234-SKU-KB-2", orderId);
        Assert.Equal(3, stock.AvailableUnits("SKU-KB"));
        Assert.Single(repository.Saved);
        var trace = context.CaptureTrace();
        AssertTheChildrenCameHome(trace);
        CapturedOutput.Write(Slug, "checkout.log", log.ToString());
        CapturedOutput.Write(Slug, "with-handover.nt", StructuralTraceRenderer.RenderDocument(trace, "The background tasks stay in the customers trace"));
    }

    [Fact]
    public async Task A_task_handed_to_a_worker_without_the_handover_is_not_under_the_checkout()
    {
        var context = new SyncNarrativeContext(new NarrativeTraceConfig(initialTraceparent: Traceparent.Parse(DemoTraceparent)));
        var stock = new InMemoryInventory("SKU-KB", 5);
        var jobs = new BlockingCollection<Action>();
        var launched = new ManualResetEventSlim();
        var worker = StartWorker(jobs, launched);
        var checkout = NarrativeTraceProxy.Create<IOrderService>(
            new HandsOverWithoutTheGroup(NarrativeTraceProxy.Create<IInventoryService>(stock, context), jobs), context);

        checkout.PlaceOrder("C-1234", "SKU-KB", 2, "4242424242424242");
        launched.Set();
        await worker;

        Assert.Equal(3, stock.AvailableUnits("SKU-KB"));
        var trace = context.CaptureTrace();
        AssertTheWorkIsADetachedRoot(trace);
        CapturedOutput.Write(Slug, "without-handover.nt", StructuralTraceRenderer.RenderDocument(trace, "A task handed to a worker without the handover"));
    }

    private static LoggingNarrativeContext CallerContext(TextWriter log)
    {
        var loggerFactory = LoggerFactory.Create(builder => builder
            .SetMinimumLevel(LogLevel.Trace)
            .AddProvider(new CorrelationLoggerProvider(log)));
        return new LoggingNarrativeContext(
            new SyncNarrativeContext(new NarrativeTraceConfig(initialTraceparent: Traceparent.Parse(DemoTraceparent))),
            loggerFactory.CreateLogger("NarrativeTrace"));
    }

    /// <summary>The checkout with the stock and the fraud check unwrapped: the group decides what each branch traces through.</summary>
    private static IOrderService ParallelCheckoutOver(
        INarrativeContext context, InMemoryInventory stock, InMemoryOrderRepository repository)
    {
        var payments = NarrativeTraceProxy.Create<IPaymentService>(new CardPaymentService(), context);
        var orders = NarrativeTraceProxy.Create<IOrderRepository>(repository, context);
        return NarrativeTraceProxy.Create<IOrderService>(
            new ParallelCheckout(context, stock, new RuleBasedFraudCheck(), payments, orders), context);
    }

    /// <summary>
    /// The branches are in the caller's tree, in the customer's trace, name the launching span as their
    /// parent, and ran on other threads.
    /// </summary>
    private static void AssertTheChildrenCameHome(TraceTree trace)
    {
        var root = Assert.Single(trace.Roots);
        Assert.Equal(
            ["IInventoryService.Reserve", "IFraudCheckService.Approve", "IPaymentService.Charge", "IOrderRepository.Save"],
            root.Children.Select(n => $"{n.Signature.ClassName}.{n.Signature.MethodName}"));
        var branches = root.Children.Where(n => n.Concurrency is not null).ToList();
        Assert.Equal(2, branches.Count);
        Assert.All(branches, branch => Assert.Equal(root.SpanContext!.TraceId, branch.SpanContext!.TraceId));
        Assert.All(branches, branch => Assert.Equal(root.SpanContext!.SpanId, branch.SpanContext!.ParentSpanId));
        Assert.All(branches, branch => Assert.NotEqual(Environment.CurrentManagedThreadId, branch.Concurrency!.ThreadId));
        Assert.Single(branches.Select(branch => branch.Concurrency!.GroupId).Distinct());
    }

    /// <summary>The work happened, but as a second root: nothing joined it to the call that launched it.</summary>
    private static void AssertTheWorkIsADetachedRoot(TraceTree trace)
    {
        Assert.Equal(
            ["IOrderService.PlaceOrder", "IInventoryService.Reserve"],
            trace.Roots.Select(n => $"{n.Signature.ClassName}.{n.Signature.MethodName}"));
        Assert.Empty(trace.Roots[0].Children);
        Assert.Null(trace.Roots[1].Concurrency);
    }

    /// <summary>
    /// A long-lived worker thread, named so a failure names it. It takes one job, then waits for the
    /// launching call to have returned before running it: the order is the point of the page, and a
    /// race would make the output differ from run to run.
    /// </summary>
    private static Task StartWorker(BlockingCollection<Action> jobs, ManualResetEventSlim launched)
    {
        var finished = new TaskCompletionSource();
        var worker = new Thread(() =>
        {
            var job = jobs.Take();
            launched.Wait();
            job();
            finished.SetResult();
        })
        { IsBackground = true, Name = "checkout-worker-1" };
        worker.Start();
        return finished.Task;
    }

    /// <summary>The checkout as it looked before the handover was added: the reservation goes to a worker, the trace does not.</summary>
    private sealed class HandsOverWithoutTheGroup(IInventoryService inventory, BlockingCollection<Action> jobs) : IOrderService
    {
        public string PlaceOrder(string customerId, string sku, int quantity, string cardNumber)
        {
            jobs.Add(() => inventory.Reserve(sku, quantity));
            return $"ORD-{customerId}-{sku}-{quantity}";
        }
    }
}
```

## La salida real

**El log de la ejecución que conserva la traza:**

```text
TRACE [NarrativeTrace] [c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7] - → IOrderService.PlaceOrder(customerId: "C-1234", sku: "SKU-KB", quantity: 2, cardNumber: [REDACTED])
TRACE [NarrativeTrace] [] - ⑂ fork group created [groupId: fork-1]
TRACE [NarrativeTrace] [] - Graft IInventoryService.Reserve
TRACE [NarrativeTrace] [] - Graft IFraudCheckService.Approve
TRACE [NarrativeTrace] [] - ⑃ fork joined [groupId: fork-1, members: 2]
TRACE [NarrativeTrace] [c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7] - → IPaymentService.Charge(customerId: "C-1234", amountCents: 7998, card: [REDACTED])
TRACE [NarrativeTrace] [c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7] - ← returned: "PAY-C-1234-7998"
TRACE [NarrativeTrace] [c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7] - → IOrderRepository.Save(order: Order(OrderId: "ORD-C-1234-SKU-KB-2", CustomerId: "C-1234", Sku: "SKU-KB", Quantity: 2, AmountCents: 7998))
TRACE [NarrativeTrace] [c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7] - ← completed
TRACE [NarrativeTrace] [c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7] - ← returned: "ORD-C-1234-SKU-KB-2"
```

**La traza estructural de esa ejecución:**

```text
scenario: The background tasks stay in the customers trace

#1 - IOrderService.PlaceOrder(customerId, sku, quantity, cardNumber) → value
  ~ fork [2]
    #1.1 - IFraudCheckService.Approve(customerId, amountCents) → value
    #1.2 - IInventoryService.Reserve(sku, quantity)
  #1.3 - IPaymentService.Charge(customerId, amountCents, card) → value
  #1.4 - IOrderRepository.Save(order)
```

**Y la traza estructural de la ejecución sin la entrega:**

```text
scenario: A task handed to a worker without the handover

#1 - IOrderService.PlaceOrder(customerId, sku, quantity, cardNumber) → value
#2 - IInventoryService.Reserve(sku, quantity)
```

El artefacto Markdown de la primera ejecución lleva tres cosas más —el hilo en
el que se ejecutó cada rama, el tiempo de reloj del join y una pista de "async
secuencial" donde las ramas no se solaparon— y no se incrusta aquí porque los
ids de hilo y los tiempos difieren en cada ejecución.

## Cómo leerla

- **La entrega es visible, dos veces.**
  `⑂ fork group created [groupId: fork-1]` es la creación del grupo, antes de
  que empiece ninguna rama; `⑃ fork joined [groupId: fork-1, members: 2]` es
  `JoinAsync` informando de lo que volvió. Entre ambas hay trabajo en segundo
  plano que pertenece a este checkout. Esas líneas llevan un id de traza vacío
  —el último corchete es `[]`— porque no son eventos de span: son el ciclo de
  vida propio del grupo, publicado por el contexto y no por una llamada
  trazada.
- **Las dos líneas `Graft` son las ramas volviendo a casa.** Una rama traza a
  través de su contexto hijo aislado, así que no escribe ninguna línea de log
  propia mientras se ejecuta; en el join, la llamada registrada de cada rama se
  injerta bajo el span que la lanzó y el puente registra eso
  —`Graft IInventoryService.Reserve`, `Graft IFraudCheckService.Approve`— antes
  de que empiece `Charge`. Lee el log como el orden en que las cosas
  *ocurrieron*: el grupo, los injertos, el join y luego las llamadas
  secuenciales que dependen de ellos.
- **Los hijos están en la traza del cliente, en el árbol, una sola vez.** La
  traza estructural contiene las dos ramas dentro de `~ fork [2]`, `#1.1` y
  `#1.2`, bajo `#1`, y luego `Charge` y `Save` como `#1.3` y `#1.4`: el fork
  está donde se ejecutó el join, antes de las llamadas que dependen de él, así
  que el árbol se lee en el orden en que ocurrieron las cosas. Los miembros de
  un grupo se listan en un orden fijo, no en el orden en que terminaron los
  hilos, que es lo que mantiene el archivo estable byte a byte.
- **Lo que afirma la prueba y un log no puede mostrar.** Un id de span se
  genera por span y difiere en cada ejecución, y lo mismo un id de hilo, así
  que `AssertTheChildrenCameHome` afirma en su lugar lo que habrían mostrado:
  cada rama lleva el id de traza de la raíz y nombra a `#1` como su span padre,
  el hilo registrado de cada rama no es el del llamador, y las dos comparten un
  id de grupo. Esa aserción es la prueba de esta página. El padre se fija al
  crear el contexto de la rama, así que un consumidor que sigue los ids de span
  padre —el listener de OpenTelemetry en vivo, por ejemplo— ve cada rama bajo
  `#1` desde su primer evento, no como una raíz.
- **Cómo difiere la segunda ejecución.** La misma llamada trazada, entregada a
  un worker sin grupo, sí ocurrió: el stock se reserva. No está bajo el
  checkout. La traza estructural tiene dos raíces —`#1 PlaceOrder` sin hijos, y
  `#2 Reserve` a su lado— porque el worker ejecutó la llamada después de que el
  span que la lanzó se hubiera cerrado, y nada la unió de vuelta. La segunda
  prueba afirma exactamente eso: `PlaceOrder` no tiene hijos y `Reserve` es una
  raíz sin grupo de concurrencia. El id de traza es el mismo, porque este
  escenario comparte un único contexto; lo que falta es el *lugar* en la
  historia.
- **Por qué se retiene al worker.** Si sueltas al worker mientras `PlaceOrder`
  sigue abierto, la llamada cae bajo él por accidente de temporización, y un
  segundo worker que haga lo mismo en el mismo contexto compartido entrelaza
  sus spans con los del primero. El grupo existe para que ninguno quede a
  merced de la temporización: cada rama recibe su propio contexto, y el join es
  el único lugar por el que vuelven.
- **Por qué un solo worker.** El log y la traza estructural son bytes de
  ejecuciones reales, y dos hilos se entrelazan de forma distinta cada vez. El
  código de entrega es el mismo para cualquier número de ramas; lo que añaden
  más hilos es salida no determinista, no un segundo mecanismo.

## Qué no se captura

- **El trabajo iniciado sin la entrega no está en el span que lo lanzó.** Esa
  es la segunda mitad de esta página: sin grupo, sin join, sin lugar bajo `#1`.
  Nada te avisa, porque desde el punto de vista de la biblioteca una llamada
  trazada en otro hilo es una llamada corriente.
- **Una rama que no traza a través de su propio contexto no está aislada.**
  Capturar a través del contexto exterior desde una rama anula el aislamiento y
  entrelaza spans; el argumento que se entrega a la rama es el que hay que usar.
- **Un grupo del que nunca se hace join no recoge nada en la traza del
  llamador.** Las ramas se ejecutaron, y después nada las injertó: el stock se
  reserva y el árbol del llamador no lo dice.
  [Solución de problemas](../solucion-de-problemas.md) tiene la entrada.
- **Una rama que lanza una excepción no vuelve a casa.** El join hace aflorar el
  fallo al llamador y los spans de la rama fallida no se injertan; observarlo
  es el `try`/`catch` del llamador.
- **El trabajo que sobrevive a la llamada es otro helper.** `FireAndForgetGroup`
  no tiene join por diseño —el método que lanza ya retornó, así que no queda
  nadie con quien fusionarse— y se traga el fallo de una tarea lanzada.
- **Entrelazado.** Con un pool real las ramas terminan en el orden en que los
  hilos se planifiquen, que es la razón de que un log se lea por id de traza y
  no por posición. La estructura injertada no se ve afectada: sigue al grupo,
  no al reloj.

**La skill para esto:** `narrativetrace-verify`: "se ejecuta en async" y "cruza
hilos" son dos de los casos para los que existe, y lee la traza estructural
contra la intención antes de dar nada por terminado.
