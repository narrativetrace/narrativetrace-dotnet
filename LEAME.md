<!-- source: README.md blob 4d229847f923 | translated: 2026-09-10 | reviewed: - -->
# NarrativeTrace .NET

[English](README.md) | **Español** | [Português](LEIAME.md) | [简体中文](自述文件.md)

**Convierte lo que tu código *hizo* en una historia que puedes leer.**
NarrativeTrace registra la ejecución de métodos como una traza narrativa — un
relato anidado y legible de las llamadas, los argumentos, los resultados y los
tiempos detrás de una unidad de trabajo — y puntúa la *claridad* de tus nombres
para que el código ilegible se detecte antes de llegar a producción.

Una arquitectura pensada para la migración: la misma biblioteca funciona en
.NET moderno, `netstandard2.0` y el legacy `net48`.

## El problema

La mitad de este método es ruido de logging:

```csharp
public OrderResult PlaceOrder(string customerId, string productId, int quantity)
{
    _logger.LogInformation("Placing order for {Customer} product {Product} qty {Qty}",
        customerId, productId, quantity);

    var customer = _customers.FindCustomer(customerId);
    var unitPrice = _catalog.LookupPrice(productId);
    _logger.LogDebug("Priced {Product} at {Price}", productId, unitPrice);

    _inventory.Reserve(productId, quantity);
    var payment = _payments.Charge(customerId, unitPrice * quantity, $"tok_{customer.Id}");
    _logger.LogInformation("Payment processed: {Txn}", payment.TransactionId);
    return new OrderResult(NextOrderId(), payment.TransactionId, unitPrice * quantity, quantity);
}
```

La lógica de negocio son unas pocas líneas; el logging son otras tantas. Cada
desarrollador escribe esos logs de forma distinta — mensajes, niveles y valores
incluidos diferentes. El resultado es inconsistente, verboso y enredado con el
código que describe.

NarrativeTrace lo elimina. Escribe lógica de negocio pura:

```csharp
public OrderResult PlaceOrder(string customerId, string productId, int quantity)
{
    var customer = _customers.FindCustomer(customerId);
    var unitPrice = _catalog.LookupPrice(productId);
    _inventory.Reserve(productId, quantity);
    var payment = _payments.Charge(customerId, unitPrice * quantity, $"tok_{customer.Id}");
    return new OrderResult(NextOrderId(), payment.TransactionId, unitPrice * quantity, quantity);
}
```

La traza se genera automáticamente a partir de los nombres de métodos, los
nombres de parámetros y los valores de retorno — la información que ya estaba
ahí:

```
OrderService.PlaceOrder(customerId: "cust-1", productId: "book-123", quantity: 2)
  CustomerService.FindCustomer(customerId: "cust-1") → Customer { Id = cust-1, Name = Ada Lovelace, Tier = Premium }
  ProductCatalogService.LookupPrice(productId: "book-123") → 9.99
  InventoryService.Reserve(productId: "book-123", quantity: 2)
  PaymentService.Charge(customerId: "cust-1", amount: 19.98) → PaymentConfirmation { TransactionId = txn-00001, Amount = 19.98 }
→ OrderResult { OrderId = ORD-00001, TransactionId = txn-00001, Total = 19.98, Quantity = 2 }
```

Este flujo es el ejemplo real
[`NarrativeTrace.Examples.ECommerce`](examples/NarrativeTrace.Examples.ECommerce),
protegido contra regresiones por sus pruebas de caracterización.

## Cuando algo va mal

La traza hace visibles los errores. Cóbrale a un cliente por encima de su límite
y la historia se detiene exactamente donde se rompió:

```
OrderService.PlaceOrder(customerId: "cust-1", productId: "book-123", quantity: 2)
  CustomerService.FindCustomer(customerId: "cust-1") → Customer { Id = cust-1, … }
  ProductCatalogService.LookupPrice(productId: "book-123") → 9.99
  InventoryService.Reserve(productId: "book-123", quantity: 2)
  PaymentService.Charge(customerId: "cust-1", amount: 19.98) !! PaymentDeclinedException: Amount 19.98 exceeds approval limit 10
!! PaymentDeclinedException: Amount 19.98 exceeds approval limit 10
```

Se llamó a `Reserve` pero no aparece ningún `Release` compensatorio — la reserva
filtrada se ve en la traza, no enterrada en un archivo de log.

## La traza vale lo que valen tus nombres

El mismo flujo de "un jugador entra al mundo", trazado dos veces — una con
nombres de dominio y otra con nombres genéricos (la
[demo de nombres de Minecraft](examples/NarrativeTrace.Examples.Minecraft)):

**Refactorizado (nombres limpios):**
```
WorldServer.PlayerJoined(playerName: "Steve")
  WorldGenerator.GenerateChunk(x: 0, z: 0) → Chunk { X = 0, Z = 0, Biome = plains }
  PlayerInventory.AddItem(item: "wooden_pickaxe", count: 1) → true
  CraftingTable.Craft(recipe: "wooden_pickaxe") → "wooden_pickaxe"
  CreatureSpawner.SpawnHostile(creatureType: "zombie", x: 10, y: 64, z: 20) → "zombie"
```

**Sin refactorizar (nombres genéricos):**
```
GameManager.Handle(input: "Steve")
  DataProcessor.Process(a: 0, b: 0) → "0,0"
  StateManager.Update(key: "wooden_pickaxe", value: 1) → true
  ThingFactory.Create(spec: "wooden_pickaxe") → "wooden_pickaxe"
  EntityHandler.Execute(type: "zombie", a: 10, b: 64, c: 20) → "zombie"
```

Grafo de llamadas idéntico, valores de retorno idénticos — solo cambian los
nombres. El analizador de claridad puntúa estrictamente más alto la versión
limpia (una prueba de caracterización fija tanto la estructura idéntica como la
diferencia de puntuación). Si tu código no sabe contar su propia historia,
necesita refactorización.

## Por qué esto importa en el desarrollo asistido por IA

Cada línea `_logger.LogInformation(...)` es una línea que las herramientas de
programación con IA — Claude Code, Copilot, Cursor — tienen que parsear, en la
que gastan tokens y sobre la que tienen que razonar. En una clase de servicio
típica, el logging es un 30–50% de las líneas. Quítalo y obtienes:

- **Más lógica de negocio por ventana de contexto** — el mismo presupuesto de
  tokens cubre más de tu código real.
- **Razonamiento más limpio** — el modelo ve lo que el código *hace*, no cómo
  loguea lo que hace.
- **Diffs con solo señal** — los pull requests muestran cambios de lógica, no
  cambios mezclados de lógica y logging.

## Cómo se compara

- **El logging estructurado** (`Microsoft.Extensions.Logging`) te obliga a
  *escribir* sentencias de log. NarrativeTrace genera la narrativa a partir de
  la estructura de tu código — sin llamadas a `LogInformation`, sin plantillas
  de mensajes, sin cablear scopes. Cuando el tracing de peticiones está activo,
  además rellena un `traceId` y un nombre de traza legible y determinista.
- **El tracing distribuido** (OpenTelemetry, Jaeger) sigue el flujo de una
  petición entre servicios mediante spans. NarrativeTrace captura árboles de
  llamadas *a nivel de método* con todos los valores de parámetros y de retorno
  — detalle que los tracers basados en spans no registran. El paquete
  `NarrativeTrace.Observability` conecta ambos mundos: exporta árboles como
  spans `Activity` (por lotes) o en vivo vía `OtelTraceEventListener`.
- **El logging por interceptores/AOP** (`DispatchProxy`, Castle) loguea
  automáticamente entradas y salidas, pero produce una salida plana y mecánica.
  NarrativeTrace produce árboles de llamadas anidados y, encima, puntúa la
  calidad de tus nombres.

No sustituye a las alertas de producción ni a los mapas de topología de
servicios; te da lo que ninguno de ellos ofrece — una narrativa de ejecución
legible que además funciona como diagnóstico de calidad del código.

## No sustituye a tu framework de logging

NarrativeTrace no tiene sink, proveedor ni pipeline de envío propios. Tu
configuración de `Microsoft.Extensions.Logging` — Serilog, NLog,
Application Insights, los sinks y enrichers que ya uses — sigue
funcionando sin cambios.

Lo que sustituye son las *sentencias* de narración escritas a mano —
líneas como `_logger.LogInformation("Placing order for {Customer}...",
customerId)`. Un método trazado produce esa narrativa automáticamente,
emitida a través de la misma abstracción `ILogger` que esas líneas
habrían usado: conecta el puente de `NarrativeTrace.Logging`
(`AddNarrativeLogging()`, o el decorador `LoggingNarrativeContext`) y
cada entrada/salida se convierte en un registro de log estructurado
ordinario — la misma maquinaria `LoggerMessage`, el mismo `BeginScope`.

El logging manual y NarrativeTrace se mezclan libremente — apunta ambos
al mismo `ILogger` y comparten cada sink, filtro y enricher que tu host
ya tenga configurado. Añade un `_logger.LogWarning(...)` donde todavía
quieras uno.

## Qué obtienes

- **Narrativas de ejecución** — envuelve un servicio en un proxy de tracing y
  cada llamada se captura como un árbol de eventos `entrada → resultado` con
  parámetros, valores de retorno, excepciones y duraciones.
- **Múltiples renderizados** — la misma traza como texto indentado, Markdown,
  prosa, diagramas de secuencia Mermaid / PlantUML, JSON canónico o spans de
  OpenTelemetry.
- **Puntuación de claridad** — un analizador basado en NLP califica los nombres
  de clases/métodos/parámetros (verbos, abreviaturas, cohesión, tokens
  genéricos) y saca a la luz las incidencias.
- **Integraciones con frameworks** — middleware de peticiones de ASP.NET Core,
  envoltura automática de DI, scopes de `ILogger`, OpenTelemetry (por lotes + en
  vivo) y helpers de pruebas de xUnit / NUnit que imprimen la narrativa cuando
  una prueba falla.
- **Herramientas** — una CLI `dotnet-narrativetrace` (escanear ensamblados,
  aplicar la puerta de claridad) y un paquete `NarrativeTrace.MSBuild` que la
  cablea en tu build.

## Pruébalo localmente

Sin proyecto propio, sin cableado — ejecuta los ejemplos incluidos y observa
la narración en vivo:

```bash
./demo.sh --example ecommerce --no-pause    # el grafo de servicios insignia
./demo.sh --example ecommerce --classic     # la misma ejecución como líneas de log con marca de tiempo
./demo.sh --list                            # todos los ejemplos que incluye este repositorio
```

Consulta [Demo](#demo) más abajo para ver el selector completo, y
[First 10 Minutes](documentation/first-10-minutes.md) para un recorrido que
añade tracing a un servicio propio, paso a paso, con salida real en cada
uno.

## Añádelo a una prueba

El camino con menos ceremonia desde "biblioteca interesante" hasta "vi una
traza de mi propio código": envuelve el cuerpo de la prueba en un fixture
que imprime la historia cuando la prueba falla.

**xUnit** — envuelve el cuerpo (xUnit no entrega el resultado de la prueba a
los fixtures):

```csharp
public class OrderTests : IClassFixture<NarrativeFixture>
{
    private readonly NarrativeFixture _fixture;
    public OrderTests(NarrativeFixture fixture) => _fixture = fixture;

    [Fact]
    public void Places_an_order() => _fixture.Run(nameof(Places_an_order), ctx =>
    {
        var orders = NarrativeTraceProxy.Create<IOrderService>(new OrderService(), ctx);
        Assert.Equal("confirmed", orders.PlaceOrder("book-123", 2));
    });
}
```

**NUnit** — deriva de `NarrativeTestBase`; los fallos se detectan y narran
automáticamente en el teardown vía `TestContext`.

Define `NARRATIVETRACE_OUTPUT=true` antes de `dotnet test` y ambos escriben
archivos reales en disco (`traces/<Class>/<slug>.md`, un `.json` hermano, un
diagrama `.mmd` y un `.nt` libre de valores) —
[First 10 Minutes](documentation/first-10-minutes.md) recorre todo el
proceso, incluido renombrar un método y ver cómo cae la puntuación de
claridad.

## Elige tu integración

| Qué quieres | Empieza por |
|---|---|
| Control explícito sobre qué se envuelve, en .NET puro | `NarrativeTraceProxy.Create<T>` más abajo |
| Cada servicio con interfaz que coincida por namespace, trazado automáticamente | Inyección de dependencias, más abajo |
| El ciclo de vida de peticiones HTTP en producción con ASP.NET Core | Middleware de ASP.NET Core, más abajo |
| Trazas en pruebas, con el mínimo cableado | Añádelo a una prueba, más arriba |
| Una puerta de CI para la calidad de nombres, sin necesidad de ejecutar pruebas | [CLI](#cli-dotnet-narrativetrace) / MSBuild |

Matriz completa y un diagrama de decisión:
[Elegir una integración](documentation/choosing-an-integration.md).

**Proxy — control explícito, funciona en cualquier app .NET:**

```csharp
using NarrativeTrace.Core;
using NarrativeTrace.Proxy;
using NarrativeTrace.Runtime;

var context = new SyncNarrativeContext(new NarrativeTraceConfig(TracingLevel.Detail));
IOrderService orders = NarrativeTraceProxy.Create<IOrderService>(new OrderService(), context);
orders.PlaceOrder("book-123", quantity: 2);

Console.WriteLine(IndentedTextRenderer.Render(context.CaptureTrace()));
```

```
OrderService.PlaceOrder(sku: "book-123", quantity: 2) → "confirmed"
  PaymentService.Charge(amount: 19.98) → "approved"
  InventoryService.Reserve(sku: "book-123", quantity: 2) → true
```

**Inyección de dependencias — envuelve automáticamente cada interfaz cuya
implementación viva bajo un prefijo de namespace**, el equivalente en .NET
del tracing de beans de Spring/Micronaut (llámalo el último, después de que
todos los servicios que deba ver ya estén registrados):

```csharp
services.AddNarrativeTracing(o =>
{
    o.Level = TracingLevel.Detail;
    o.Namespaces("MyApp.Services", "MyApp.Domain");
});
```

**ASP.NET Core — traza por petición, exportada cuando la petición
termina:**

```csharp
builder.Services.AddNarrativeTrace(builder.Configuration);

var app = builder.Build();
app.UseMiddleware<NarrativeTraceMiddleware>(); // cerca del borde exterior del pipeline

app.MapGet("/orders/{id}", (string id, HttpContext http) =>
    NarrativeTraceProxy.Create<IOrderService>(new OrderService(), http.GetNarrativeContext())
        .FindOrder(id));
```

Consulta la
[Guía de integración con ASP.NET Core](documentation/guides/es/guia-de-integracion-con-aspnet-core.md)
para exportadores y contexto de usuario, y la
[Guía de instalación](documentation/guides/es/guia-de-instalacion.md) para la
CLI, MSBuild y los puentes de logging/OpenTelemetry.

**Formatos de renderizado** — elígelos con independencia de cómo capturaste
la traza:

```csharp
MarkdownRenderer.Render(trace);          // informe Markdown
IndentedTextRenderer.Render(trace);      // apto para consola / logs
ProseRenderer.Render(trace);             // prosa narrativa
MermaidSequenceRenderer.Render(trace);   // diagrama de secuencia Mermaid
PlantUmlSequenceRenderer.Render(trace);  // diagrama de secuencia PlantUML
JsonExporter.Export(trace, metadata);    // JSON canónico (validado contra el esquema)
```

## Privacidad y seguridad, en una sola pantalla

Esta biblioteca corre dentro de tu proceso y escribe archivos que tu equipo
va a compartir. Verificado contra el código, no asumido:

| Garantía | Cómo se cumple |
|---|---|
| **La ocultación es incondicional en cada integración publicada** | `[NotTraced]` y una lista de denegación de nombres siempre activa y multilingüe (más la detección de forma de valor para JWT/tarjetas de pago/`Set-Cookie`/checksums de identificación nacional, independiente del nombre del campo) se aplican por igual a la captura por proxy, el auto-wrap de DI, el middleware de ASP.NET Core, la salida de pruebas y los placeholders de plantilla. Ningún flag los desactiva. |
| **`[NotTraced]` gana siempre** | En una propiedad o campo, su getter nunca llega a invocarse; en un parámetro, el valor nunca se renderiza. Prevalece sobre un `ToString()` curado y vence a la vía de escape `RedactionPolicy.Disabled`, que existe pero que ninguna integración publicada cablea. |
| **El artefacto estructural seguro para IA no contiene ningún valor** | El archivo `.nt` (y el `.structural.json` opcional) contienen solo nombres, jerarquía y tipo de resultado — una prueba de propiedades siembra contenido hostil en cada campo de valor y falla si algo de eso sobrevive. |
| **Los fallos del tracing no pueden hacer fallar tu aplicación** | La captura y el renderizado están aislados de excepciones en cada punto de riesgo — un `ToString()` que lanza, un miembro `[NarrativeSummary]` o un getter de ruta de plantilla degradan a un placeholder sin tocar el resultado de tu llamada de negocio. |
| **El uso de recursos está acotado** | La longitud de cadenas, el tamaño de colecciones, el ancho de objetos y la profundidad de anidamiento están todos limitados, con detección de ciclos independiente de la profundidad. |

Dos límites honestos: la detección se basa en nombre/forma, no es
estadística (sin heurísticas de "esto parece aleatorio" — un secreto en un
campo con un nombre inocente no se detecta), y la resolución de
placeholders de plantilla (`[Narrated]`/`[OnError]`) siempre usa la lista de
denegación por defecto, incluso si has enhebrado un `RedactionPolicy`
personalizado en otro `ValueRenderer`.

→ [Privacidad y ocultación](documentation/privacy-and-redaction.md) para el
contrato completo, fila por fila.

## Rendimiento

El tracing hace trabajo real y cuesta algo — no hay promesa de "coste cero".
La puerta de regresión (BenchmarkDotNet) hace fallar el build ante **más de
un 15% más lento** o **cualquier** aumento de asignaciones frente a una línea
base registrada en el repositorio. Números de línea base registrados, nivel
`Detail` (el predeterminado), de
[`benchmarks/benchmark-baseline.json`](benchmarks/benchmark-baseline.json):

| Benchmark | Media | Asignado |
|---|---|---|
| `CoreBenchmarks.EnterExitCycle` | ~2.5 µs | 2368 B |
| `CoreBenchmarks.RenderObject` | ~819 ns | 1611 B |
| `RendererBenchmarks.MarkdownSmall` | ~595 ns | 2704 B |
| `ConcurrencyBenchmarks.ForkJoin_TwoTasks` | ~3.9 µs | 5602 B |

Solo en hardware nativo: un runner de CI compartido no puede sostener estos
umbrales (demostrado en la primera ejecución pública — todas las demás
puertas en verde, y luego docenas de "regresiones" espurias por vecinos
ruidosos), así que tanto GitHub Actions (`./build.sh Verify --skip
Benchmark`) como el push a la rama por defecto de GitLab lo omiten. Sigue
ejecutándose de verdad, sin supervisión, como un target NUKE normal sobre
hardware equiparable: `./build.sh Benchmark` restaura, compila, ejecuta los
16 benchmarks (el job `medium` de BenchmarkDotNet) y los evalúa frente a
[`benchmarks/benchmark-baseline.json`](benchmarks/benchmark-baseline.json),
escribiendo resultados JSON legibles por máquina en `artifacts/benchmarks/`.
Ese único comando es el punto de entrada que invoca un job nocturno/de host;
regenera la línea base tras un cambio de rendimiento intencionado con
`./build.sh BenchmarkBaseline`.

Con `TracingLevel.Off` el contexto corta por lo sano y no captura nada — una
prueba de caracterización fija que un contexto en nivel off produce una
traza vacía — pero ese camino todavía no tiene benchmark propio, así que
trata "Off es arquitectónicamente de captura cero" como algo verificado por
prueba, no como un número de ns/op medido.

## Qué es gratis y qué es Pro

**Gratis** es todo lo que hay en este repositorio — de código disponible
bajo BSL 1.1, gratis en producción: todo el pipeline de captura, cada
renderizador y formato de exportación, la puntuación de claridad, todas las
integraciones anteriores y el artefacto estructural libre de valores. Las
funcionalidades hoy reservadas a Pro (no incluidas en este repositorio,
trasladadas a `narrative-trace-dotnet-enterprise`): la agregación de flujos
de eventos entre ejecuciones (`EventAggregator`) y — planeadas, aún sin
construir — resúmenes de flujo, diffs de migración, grafos de dependencias
en tiempo de ejecución y manejadores de herramientas MCP para agentes de IA.
La [Guía de funcionalidades](documentation/feature-guide.md) es la tabla de
estado autoritativa: cada funcionalidad está etiquetada como Free, Pro, In
development o Planned, con el código detrás de cada fila ya implementada.

## Paquetes

| Paquete | Qué aporta |
|---|---|
| `NarrativeTrace.Core` | Modelo de trazas, contextos, renderizadores, tipos de claridad, configuración y los atributos de `NarrativeTrace.Core.Annotation` |
| `NarrativeTrace.Runtime` | Implementaciones de contexto, pipeline de eventos, exportadores JSON/de capítulos |
| `NarrativeTrace.Proxy` | Proxy de tracing basado en `DispatchProxy`, más `[Traced]` |
| `NarrativeTrace.DependencyInjection` | Envoltura automática por namespace con `AddNarrativeTracing` |
| `NarrativeTrace.AspNetCore` | Middleware del ciclo de vida de la petición + SPI `ITraceExporter` |
| `NarrativeTrace.Observability` | Exportación a `Activity` de OpenTelemetry — por lotes + listener en vivo |
| `NarrativeTrace.Logging` | Exportación narrativa a `ILogger` + scopes de correlación |
| `NarrativeTrace.Diagrams` | Renderizadores de secuencia Mermaid / PlantUML |
| `NarrativeTrace.Clarity` | Analizador de claridad de nombres, puntuadores, diccionarios, scanner |
| `NarrativeTrace.Testing.Xunit` / `.NUnit` | Fixtures de prueba que narran los fallos |
| `NarrativeTrace.Cli` | Herramienta global `dotnet-narrativetrace` |
| `NarrativeTrace.MSBuild` | Targets de build para el escaneo/puerta de claridad |
| `NarrativeTrace.Legacy` | Superficie de compatibilidad con `net48` |

## OpenTelemetry

```csharp
// Por lotes: exporta un árbol de trazas completado como spans Activity.
TraceActivityExporter.Export(context.CaptureTrace());

// En vivo: convierte los eventos de entrada/salida de un pipeline en spans según ocurren.
var listener = new OtelTraceEventListener(new ActivitySource("MyApp"));
bufferedConsumer.Subscribe(listener.OnEvent);
```

## Soporte de concurrencia

NarrativeTrace sigue la ejecución concurrente — paralelismo fork-join y tareas
fire-and-forget (lanzar y olvidar) — como ciudadanos de primera clase del árbol
de trazas. Como el contexto en .NET fluye por `AsyncLocal` (el análogo del
`ThreadLocal` de Java), cada rama paralela se ejecuta contra un contexto hijo
aislado cuya traza se fusiona de vuelta en el padre con metadatos de hilo.

**Fork-join** — `ForkJoinGroup` ejecuta las ramas en paralelo, las une e injerta
sus trazas bajo el padre con un `groupId` compartido:

```csharp
var fork = ForkJoinGroup.Create(context);
_ = fork.Fork(iso => NarrativeTraceProxy.Create<IProductCatalogService>(catalog, iso)
    .LookupPrice("book-123"));
_ = fork.Fork(iso => { NarrativeTraceProxy.Create<IInventoryService>(inventory, iso)
    .Reserve("book-123", 2); return true; });
await fork.JoinAsync();
```

**Fire-and-forget** — `FireAndForgetGroup` injerta un nodo lanzador en el padre y
enlaza por `groupId` la traza hija en segundo plano; el padre continúa sin
esperar:

```csharp
var group = FireAndForgetGroup.Create(context, "OrderService");
group.Launch(iso => NarrativeTraceProxy.Create<INotificationService>(notifier, iso)
    .NotifyOrderPlaced("cust-1", "ORD-00001"));
```

El Markdown renderizado incluye marcadores `⑂ fork [n tasks]` / `⑃ join`, un
lanzador `⤳ fire-and-forget`, nombres de hilos y el tiempo de reloj del join. El
escenario completo — precio/stock en paralelo y después una notificación
asíncrona — es
[`ConcurrencyScenario`](examples/NarrativeTrace.Examples.ECommerce/ConcurrencyScenario.cs),
con pruebas de caracterización que verifican el `groupId` compartido, el nodo
lanzador y los campos de concurrencia en la exportación JSON. Para la
propagación ambiental a través de un `await`, `AsyncNarrativeContext.RunAsync`
adjunta el trabajo de continuación a la misma traza.

## CLI: `dotnet-narrativetrace`

```bash
dotnet tool install --global NarrativeTrace.Cli

# Puntúa la claridad de los nombres de un ensamblado compilado (solo reflexión — nunca lo ejecuta):
dotnet-narrativetrace clarity-scan --assembly bin/MyApp.dll --output-dir clarity

# Falla CI cuando la claridad baja de un umbral:
dotnet-narrativetrace clarity-check --results clarity/clarity-scan-results.json \
    --min-score 0.7 --max-high-issues 0
```

### Integración con MSBuild

Referencia `NarrativeTrace.MSBuild` y dirige la misma puerta desde tu build:

```xml
<PropertyGroup>
  <ClarityMinScore>0.7</ClarityMinScore>
  <ClarityMaxHighIssues>0</ClarityMaxHighIssues>
  <ClarityWarnOnly>false</ClarityWarnOnly>
</PropertyGroup>
```

```bash
dotnet build /t:ClarityCheck   # escanea + aplica la puerta; incremental vía un archivo stamp
```

## Configuración

Cada ajuste se puede resolver desde variables de entorno `NARRATIVETRACE_*`
(`NARRATIVETRACE_LEVEL`, `NARRATIVETRACE_OUTPUT`, `NARRATIVETRACE_FORMAT`, …).
Niveles de captura, de menos a más detalle: `Off`, `Errors`, `Summary`,
`Narrative`, `Detail`.

## Documentación

Empieza aquí:

- [Primeros 10 minutos](documentation/es/primeros-10-minutos.md) — un servicio diminuto, una prueba, salida real en cada paso
- [Elegir una integración](documentation/es/elegir-una-integracion.md) — qué módulo necesitas, como diagrama de decisión
- [Solución de problemas](documentation/es/solucion-de-problemas.md) — síntoma → causa → arreglo para los fallos que la gente realmente encuentra
- [Qué incluir en el commit](documentation/es/que-incluir-en-el-commit.md) — qué archivos generados son desechables y cuáles se revisan
- [Privacidad y ocultación](documentation/es/privacidad-y-ocultacion.md) — el contrato de ocultación fila por fila, verificado contra el código

Las guías orientadas a tareas viven en
[`documentation/guides/es/`](documentation/guides/es/):

- [Instalación](documentation/guides/es/guia-de-instalacion.md) — paquetes y vías de integración
- [Configuración](documentation/guides/es/guia-de-configuracion.md) — niveles, variables de entorno, DI, MSBuild, ocultación
- [Atributos](documentation/guides/es/guia-de-atributos.md) — `[Narrated]`, `[OnError]`, `[NotTraced]`, `[Traced]`, `[NarrativeSummary]`
- [Inyección de dependencias](documentation/guides/es/guia-de-inyeccion-de-dependencias.md) — envoltura automática por namespace en el contenedor de MS.DI
- [Integración con ASP.NET Core](documentation/guides/es/guia-de-integracion-con-aspnet-core.md) — middleware de peticiones, exportadores, contexto de usuario
- [Claridad](documentation/guides/es/guia-de-claridad.md) — modelo de puntuación y puerta de CI
- [MSBuild y CLI](documentation/guides/es/guia-de-msbuild-y-cli.md) — los verbos de `dotnet-narrativetrace` y la puerta de claridad en tiempo de build

Para profundizar:

- [Guía de funcionalidades](documentation/feature-guide.md) — tabla de estado canónica (Free/Pro/In development/Planned) para cada funcionalidad, con el código detrás de cada fila ya implementada
- [Herramientas de seguridad](documentation/security-tooling.md) — el catálogo de escáneres y qué condiciona el build frente a lo que corre en un horario
- [Pruebas de seguridad](documentation/security-testing.md) — la suite de fuzzing/propiedades reflejada en todas las implementaciones de NarrativeTrace

Para consumidores de IA (solo en inglés):
[`llms.txt`](documentation/guides/llms.txt) y
[`llms-full.md`](documentation/guides/llms-full.md).

## Frameworks de destino

- Runtime moderno: `net10.0`
- Compatibilidad: `netstandard2.0`
- Legacy: `net48` (en `NarrativeTrace.Legacy`)

## Demo

La forma más rápida de ver los ejemplos en marcha: un solo comando, la
narración en vivo `→ ← !!` coloreada e indentada según la profundidad de
llamadas, y una pausa tras cada escenario con una nota sobre cómo está cableada
la traza de ese escenario:

```bash
./demo.sh                                 # selector interactivo
./demo.sh --example ecommerce             # no interactivo; --list enumera los ejemplos
./demo.sh --example ecommerce --classic   # la misma ejecución como logs tradicionales con marca de tiempo
./demo.sh --example ecommerce --no-pause  # de un tirón, sin pausas (lo que reciben las tuberías y la CI)
```

`./build.sh Demo --example <nombre> --no-pause` y `./build.sh RunExamples`
ejecutan lo mismo desde el build; `demo.ps1` es el envoltorio fino de
PowerShell. Se incluyen cuatro ejemplos — `ecommerce`, `clarity`, `minecraft` y
`library` (F#) — y cada uno también se ejecuta por su cuenta con
`dotnet run --project examples/<proyecto>`. Consulta
[`examples/LEAME.md`](examples/LEAME.md) para el mapa de proyectos y lo que
enseña cada uno.

## Build y pruebas

```bash
dotnet build NarrativeTrace.sln
dotnet test NarrativeTrace.sln
```

O vía [NUKE](https://nuke.build) (`./build.sh`, `build.ps1`, `build.cmd`):

```bash
./build.sh Test        # ejecuta la suite completa
./build.sh Verify      # clean + format + analyze + test + coverage + metrics + benchmark
./build.sh VerifyAll   # todas las verificaciones de este repo, gate y pesadas por igual — ver más abajo
./build.sh Coverage    # informes cobertura de Coverlet
./build.sh Mutation    # pruebas de mutación con Stryker.NET
./build.sh Pack        # produce los paquetes NuGet
```

### Puertas de calidad (impuestas)

El formateo (`dotnet format`), el análisis estático (Roslyn + SonarAnalyzer,
incluido un límite de 20 líneas por método vía S138, y toda la categoría de
reglas de seguridad CA5xxx), el escaneo de secretos (gitleaks, hook
pre-commit sobre el diff en stage más un barrido completo del historial),
las pruebas (xUnit / NUnit), la cobertura (Coverlet) y la regresión de
benchmarks (BenchmarkDotNet) condicionan el build. Las pruebas de mutación
(Stryker.NET, `./build.sh Mutation`) están definidas y son ejecutables —
incluso en el espejo público vía
[`.github/workflows/mutation.yml`](.github/workflows/mutation.yml) — pero
**no** son una dependencia de `Verify`: igual que el conjunto de reglas OSS
de C# de Semgrep y el escaneo de vulnerabilidades de dependencias
(OSV-Scanner, `dotnet list package --vulnerable`), se ejecutan en un nivel
programado/manual, nunca por commit — ver
[`documentation/security-tooling.md`](documentation/security-tooling.md).
`tests/BuildScript.Tests` valida el comportamiento del propio build. CI ejecuta
`./build.sh Verify` en GitHub Actions (`.github/workflows/ci.yml`).

`./build.sh VerifyAll` es el único comando que ejecuta todas las
verificaciones que tiene este repositorio, tanto las de gate como las
pesadas — pruebas unitarias, cobertura, mutación, pruebas de propiedades,
ambos niveles de fuzzing, arquitectura, conformidad, benchmarks/asignación,
estrés de concurrencia, y las comprobaciones de secretos/SAST/SCA/lint/
formato/complejidad/traducción — en una sola pasada, recogiendo el resultado
de cada categoría en vez de detenerse en el primer fallo. Escribe
`reports/verification/<date>.json` y su gemelo `.md` renderizado, con la
forma multi-runtime que este repositorio Java de la familia define
(`reports/verification/SCHEMA.md`): los mismos nombres de campo, los mismos
cuatro estados (`passed`/`failed`/`skipped`/`not-implemented`), los mismos 21
ids de categoría en todos los runtimes de NarrativeTrace. Es lento por
diseño — la mutación en todos los módulos de Stryker y el barrido de estrés
con iteraciones elevadas son, históricamente, cuestión de minutos, no de
segundos — así que es una ejecución programada/manual, nunca por commit.

## Preguntas frecuentes

**¿Cuánto overhead añade esto, y qué pasa con alta concurrencia?** No vamos a
afirmar "overhead cero" — consulta [Rendimiento](#rendimiento) más arriba para
los números fechados que resume esta respuesta (BenchmarkDotNet,
`TracingLevel.Detail`, el nivel por defecto, de
[`benchmarks/benchmark-baseline.json`](benchmarks/benchmark-baseline.json),
actualizados el 2026-08-12): un ciclo completo de entrada/salida cuesta ~2,5 µs
y asigna 2368 B; renderizar un objeto cuesta ~819 ns/1611 B; un render Markdown
pequeño cuesta ~595 ns/2704 B. Todavía no hay un benchmark separado para
`TracingLevel.Off` — mejor decirlo con claridad que insinuar un número que no
existe: un contexto en nivel off está verificado por test de caracterización
para producir una traza vacía, arquitectónicamente sin captura, pero ese camino
aún no es una cifra medida en ns/op.

Lo que NarrativeTrace añade por sí mismo es esa captura — interceptar la
llamada, leer los argumentos, construir el árbol de traza. Todo lo que viene
después de la captura (la escritura de `ILogger`, el collector, el disco o la
red) es el mismo coste que tu stack de logging ya paga; NarrativeTrace no añade
un segundo destino. Para un equipo que reemplaza sentencias de log escritas a
mano, el lado del destino queda casi en tablas: N llamadas de log por método se
convierten en una escritura de traza, y esas sentencias dejan de escribirse,
revisarse y mantenerse sincronizadas con el código.

Bajo concurrencia, los dos caminos del pipeline tienen garantías distintas. Un
listener síncrono — la exportación a `ILogger` de `NarrativeTrace.Logging`, si
la conectas — corre en línea: la escritura se completa antes de que el método
retorne, así que es exactamente tan duradero — y cuesta exactamente lo mismo —
que una llamada de log ya cuesta. El camino con buffer, de mejor esfuerzo, es
un anillo acotado que descarta por encima del 70% de ocupación en lugar de
bloquear a quien llama, y cada evento descartado se **cuenta** —
sobrescrituras del anillo y descartes del drenaje adaptativo por igual, vía
`BufferedEventConsumer.DroppedCount` — y se muestra en el propio pie de página
`TraceLoss` de la traza ("N events dropped (buffer full)"), nunca en silencio.

**El límite honesto:** hoy no existe muestreo (sampling) en esta
implementación, ni en ninguna implementación de NarrativeTrace — toda llamada
trazada se captura por completo en su `TracingLevel` configurado. Un
muestreador por porcentaje o por tasa está en la hoja de ruta, no distribuido.
Si necesitas acotar el volumen de captura ahora, usa `TracingLevel.Off` o
acota el scope trazado al límite que importa.

**¿Cómo sé que un parámetro con PII o credenciales no se filtrará en una
traza?** Cuatro capas independientes, no una sola promesa general — el
contrato fila por fila es [Privacidad y
ocultación](documentation/es/privacidad-y-ocultacion.md):

1. `[NotTraced]` en un parámetro, propiedad o campo — ocultación explícita que
   tú controlas. En una propiedad o campo el getter ni siquiera se invoca;
   prevalece sobre un `ToString()` cuidadosamente escrito, y es incondicional
   — ningún flag lo desactiva.
2. Una lista de denegación por nombre, siempre activa y multilingüe — compara
   nombres de campos y parámetros con patrones en inglés, español, portugués,
   francés, alemán y chino para contraseñas, tokens, identificaciones
   nacionales y similares, ampliable (nunca sustituible) vía
   `NARRATIVETRACE_REDACTION_ADDITIONALPATTERNS`. Activa por defecto, no
   opcional.
3. Coincidencia por la forma del valor, independiente del nombre del campo —
   un string con forma de JWT, un número de tarjeta válido por Luhn, un valor
   con forma de `Set-Cookie`, o un checksum de identificación nacional o una
   regla estructural (RUT chileno, CPF/CNPJ brasileño, DNI/NIE español, NIR
   francés, cédula de identidad china, o un número de Seguro Social
   estadounidense escrito con guiones) se oculta aunque llegue bajo un
   nombre inocuo como `data` o `value`.
4. El artefacto estructural `.nt` sin valores (más el `.structural.json`
   opcional) — la garantía categórica. Solo nombres, jerarquía y tipo de
   resultado, cero valores en tiempo de ejecución; un test de propiedades
   siembra contenido hostil en cada campo de valor y falla si alguno
   sobrevive.

Dos límites honestos, ya nombrados en la [tabla de privacidad y
seguridad](#privacidad-y-seguridad-en-una-sola-pantalla) de más arriba: la
detección es por nombre/forma, no estadística — un secreto en un campo con
nombre inocuo, con una forma que ninguna de las comprobaciones reconoce, no se
captura — y la resolución de placeholders de plantilla
(`[Narrated]`/`[OnError]`) siempre usa la lista de denegación por defecto,
aunque hayas conectado un `RedactionPolicy` personalizado en `ValueRenderer`
en otro lugar. Las capas 1–3 son heurísticas y extensibles; la capa 4, el
artefacto estructural, es la única *categórica* — recurre a ella si tu modelo
de amenaza exige que ningún valor pueda salir jamás del proceso.

**¿Pueden los IDs de traza correlacionarse con un ID de correlación estándar
entre servicios, o el tracing es solo local?** Solo local, hoy — y preferimos
decirlo con claridad antes que dejar que el paquete `NarrativeTrace.Observability`
dé a entender lo contrario. Esta implementación no analiza una cabecera W3C
`traceparent` entrante, ni adjunta una en las llamadas HTTP salientes; es una
decisión explícita y probada de no hacerlo por ahora, no un descuido — un test
de propiedades stub en la suite de seguridad afirma que todavía no existe
ningún tipo `Traceparent` en `NarrativeTrace.Core`, precisamente para que un
futuro parser llegue con sus casos de fuzzing ya preparados. `TraceId` se
genera localmente y tiene forma W3C (32 caracteres hexadecimales en
minúscula, comparable byte a byte con los IDs que produciría el runtime de
Java o una cabecera `traceparent` real) — pero nunca se *deriva* de una
cabecera entrante, así que un ID de traza de NarrativeTrace no será igual al
de una traza distribuida previa.

Lo que sí existe: `TraceActivityExporter` (por lotes) y
`OtelTraceEventListener` (en vivo) de `NarrativeTrace.Observability`
convierten un árbol de NarrativeTrace completado o en curso en spans de
`System.Diagnostics.Activity` bajo un `ActivitySource("NarrativeTrace")` —
pero esto es una repetición, no instrumentación en vivo: los spans llevan la
jerarquía propia de la traza y no son hijos de lo que sea que
`Activity.Current` valga en el momento de la exportación. Si la correlación
entre servicios es un requisito duro hoy, propaga tu propio ID de correlación
a través de los scopes de `ILogger` de `NarrativeTrace.Logging` junto con la
salida de NarrativeTrace; tratar la propagación de contexto W3C como algo ya
distribuido aquí sería inexacto — es un hueco en la hoja de ruta, no una
funcionalidad publicada.

**¿Los valores de parámetros y de retorno se serializan de forma eager?** Sí —
los valores se renderizan a cadenas en el momento de la captura, según el nivel
configurado. Es deliberado: la traza registra lo que el valor *era* en el
momento de la llamada, no una vista posterior y posiblemente mutada. Consulta
[Privacidad y seguridad](#privacidad-y-seguridad-en-una-sola-pantalla) más
arriba y la página
[Privacidad y ocultación](documentation/privacy-and-redaction.md) para el
contrato de ocultación completo y verificado.

**¿Puede el tracing provocar efectos secundarios en mi código?** El renderizado
puede invocar un conjunto pequeño y documentado de miembros: getters de
propiedades durante la introspección reflexiva, un `ToString()` propio, un
miembro `[NarrativeSummary]` y las rutas de propiedades nombradas en las
plantillas de `[Narrated]`/`[OnError]`. Mantenlos puros (que los getters no
tengan efectos secundarios ya es una .NET Framework Design Guideline) — o marca
el miembro con `[NotTraced]`, en cuyo caso su valor no se lee en absoluto. Cada
invocación está acotada (límites de longitud/profundidad/elementos), aislada de
excepciones (un getter que lanza nunca puede hacer fallar tu llamada de negocio)
y ocurre de forma eager en el sitio de la llamada; con `TracingLevel.Off` no se
renderiza nada en absoluto. Consulta el contrato de pureza en la
[guía de atributos](documentation/guides/es/guia-de-atributos.md).

**¿Puede trazar métodos privados o que no sean de interfaz?** El proxy
`DispatchProxy` traza llamadas *de interfaz*, así que el tracing ocurre en las
fronteras de interfaz — las llamadas a métodos privados e internos dentro de una
implementación no se trazan individualmente. Esto mantiene la narrativa al nivel
de la colaboración (servicio a servicio), que suele ser el nivel que quieres
leer. Para más granularidad, separa el comportamiento tras una interfaz.

**¿Cómo interactúa NarrativeTrace con otras bibliotecas que envuelven
métodos (AOP, proxies, bibliotecas de contratos)?** Narra los cruces de
frontera de negocio, no la maquinaria de implementación — un decorador
generado, un método puente o el stub de otro proxy nunca se convierte en
un frame de traza por derecho propio. Cuando una biblioteca de contratos
o validación rechaza una llamada en una interfaz que `NarrativeTraceProxy`
también envuelve, el rechazo siempre llega a la traza como una excepción,
sin importar cuál de los dos wrappers se registre primero — solo *a qué*
frame se adjunta (interior o exterior) depende de ese orden. Una vía de
ingesta dedicada para eventos de violación que no lanzan excepción está
planeada pero aún no construida. Las opciones de exclusión de hoy son
limitadas: `ExcludeNamespaces` de `AddNarrativeTracing` excluye espacios
de nombres de interfaz del auto-wrap de DI con coincidencia de límite de
punto, y el proxy crudo solo envuelve lo que le pasas explícitamente a
`NarrativeTraceProxy.Create`. Todavía no existe una lista de exclusión por
defecto para formas de maquinaria conocidas (decoradores generados, tipos
generados por el compilador), y el diseño de `DispatchProxy` de una
interfaz por proxy significa que una instancia envuelta no expone
ninguna *otra* interfaz marcador que implemente — ambos siguen siendo
elementos abiertos en la hoja de ruta.

## Estado

La versión 0.1.0 está publicada — los paquetes están en NuGet. El pipeline de
captura, el esquema JSON canónico, el modelo de atributos de dos niveles, el
ciclo de vida de ASP.NET Core, el cableado de DI, OpenTelemetry (por lotes + en
vivo), las herramientas de claridad + CLI y las integraciones con frameworks de
pruebas y los ejemplos ejecutables con su lanzador de demo ya están en su sitio.
Trabajo pendiente: validación de `net48` en tiempo de ejecución.

## Licencia

La API y el formato de salida de NarrativeTrace son estándares abiertos
(Apache 2.0). Su runtime es gratuito y de código disponible (BSL 1.1, que se
convierte en Apache 2.0 cuatro años después de cada versión publicada). Pro
es comercial.

Todo lo que hay en este repositorio es el runtime, bajo la
[Business Source License 1.1](LICENSE). Puedes usarlo en producción para
cualquier fin, tanto de forma interna como en los productos y servicios que
ofreces a tus propios clientes — la única exclusión es ofrecer NarrativeTrace
en sí, o un producto o servicio cuyo valor derive sustancialmente de él, a
terceros como un producto o servicio de registro, trazado o narrativa de
código. Cuatro años después de publicarse, cada versión pasa a Apache 2.0 —
la concesión es automática y por versión, así que lo que adoptas hoy tiene un
plazo que puedes consultar.

La prosa de la documentación es [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/); los archivos de esquema
JSON son Apache 2.0.

<!-- legal:trademark:begin -->
NarrativeTrace es una marca comercial de Empower Agile. La licencia no
concede ningún derecho de marca.
<!-- legal:trademark:end -->

### La licencia, en palabras sencillas

Todo lo que hay en este repositorio es Business Source License 1.1 hoy; la
capa de contrato abierta llega con la separación de la api.

<!-- legal:plain-words:begin -->
**Gratis para ejecutar.** El runtime es de código disponible bajo la Business
Source License 1.1: puedes leerlo, auditarlo, modificarlo y usarlo en producción
sin coste — incluso dentro de los productos y servicios que vendes a tus propios
clientes.

**Una sola exclusión.** No puedes ofrecer NarrativeTrace en sí —o un producto o
servicio cuyo valor derive sustancialmente de él— a terceros como producto o
servicio de logging, tracing o narrativa de código.

**Se abre en una fecha.** Cada versión se convierte a Apache 2.0 cuatro años
después de publicarse; la fecha exacta se imprime en el LICENSE de esa versión.

*Este resumen es una cortesía, no una licencia. El archivo LICENSE es el único
texto vinculante; donde ambos difieran, prevalece el LICENSE.*
<!-- legal:plain-words:end -->
