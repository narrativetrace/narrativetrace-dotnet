<!-- source: examples/README.md blob ed5a75d0d1a0 | translated: 2026-09-06 | reviewed: - -->
# Ejemplos de NarrativeTrace

[English](README.md) | **Español** | [简体中文](自述文件.md)

Tutoriales ejecutables que muestran NarrativeTrace en la práctica. Estos proyectos **no se
publican**: existen para que un desarrollador (o un agente de IA) pueda ejecutar un
escenario realista, leer la traza resultante y conectarla con el código que la produjo.

Trata los proyectos como tutoriales con un orden sugerido, no como una API reutilizable.
La clase de entrada de cada ejemplo lleva en su documentación XML un orden de lectura
guiado; empieza por ahí cuando quieras profundizar en uno.

## Mapa de proyectos

| Proyecto | Lenguaje | Punto de entrada | Qué enseña |
|---|---|---|---|
| `NarrativeTrace.Examples.ECommerce` | C# | `ECommerceDemo` | El ejemplo insignia: trazar un grafo de servicios realista — cableado del contenedor con `AddNarrativeTracing`, `[Narrated]` / `[OnError]` / `[NotTraced]`, finalización asíncrona que se une a la traza, grupos fork-join y fire-and-forget (lanzar y olvidar), escenarios de éxito y de fallo. |
| `NarrativeTrace.Examples.Clarity` | C# | `ClarityExample` | Cómo el subsistema de claridad puntúa la calidad de los nombres, con un dominio de reservas de hotel con nombres deliberadamente excelentes, adecuados y pobres. |
| `NarrativeTrace.Examples.Minecraft` | C# | `MinecraftExample` | Cuánto cambia la calidad de la traza solo con los nombres: el mismo comportamiento trazado dos veces, una con nombres ricos en dominio y otra con nombres genéricos. |
| `NarrativeTrace.Examples.Library` | F# | `LibraryExample` | Usar NarrativeTrace desde F#: trazar servicios F# mediante `DispatchProxy` a partir de interfaces F# en un pequeño dominio de préstamo de libros. |
| `NarrativeTrace.Examples.Common` | C# | *(biblioteca, sin punto de entrada)* | Utilería compartida de los ejemplos: el driver `DemoRun`, la `ConsoleLoggerFactory` por la que loguean las ejecuciones, el `NarrationStreamListener` que convierte los eventos de traza en las líneas en vivo `→ ← !!`, y los marcadores de sección. |

Ningún proyecto de ejemplo tiene **dependencias NuGet**: solo referencias de proyecto a
las bibliotecas de `src/`. Los ejemplos tienen sus propios proyectos de pruebas bajo
`tests/` (`NarrativeTrace.Examples.*.Tests`), que fijan la forma de cada ejecución.

## Ejecutar los ejemplos

Cada ejemplo es un proyecto de consola corriente:

```bash
dotnet run --project examples/NarrativeTrace.Examples.ECommerce
dotnet run --project examples/NarrativeTrace.Examples.Clarity
dotnet run --project examples/NarrativeTrace.Examples.Minecraft
dotnet run --project examples/NarrativeTrace.Examples.Library

./build.sh RunExamples   # los cuatro en secuencia
```

El único modificador que acepta un ejemplo es `--classic` (después de `--`): cada línea
lleva entonces el prefijo tradicional de marca de tiempo / nivel / `[thread]` /
`[traceId]` / `[logger]` en lugar del mensaje escueto. El ritmo y el color son asunto
del lanzador, no del ejemplo.

## Lanzador de demo

La raíz del repositorio incluye `./demo.sh`, la forma más rápida de ver los ejemplos: un
solo comando, sin ruido de build, la narración en vivo coloreada e indentada según la
profundidad de llamadas, y cada renderizado anunciado como una sección propia.
`./demo.sh` abre un selector interactivo; `--example <nombre>` se ejecuta sin
interacción; `--list` enumera los ejemplos; `./build.sh Demo --example <nombre> --no-pause`
ejecuta lo mismo desde el build.

**Camina, no hace scroll.** En un terminal, la demo se detiene tras cada escenario —
`[Intro]` avanza, `q` sale — y cada escenario se abre con una nota sobre *cómo está
configurada la traza de ese escenario*: `AddNarrativeTracing` en el contenedor aquí, un
`NarrativeTraceProxy.Create` a secas allá, cuál de `[Narrated]` / `[OnError]` /
`[NotTraced]` produjo lo que estás a punto de leer. Las notas viven en
`examples/demo/wiring.awk`, indexadas por el título del escenario, y el target de build
`DemoWiringCheck` (también una prueba en `tests/BuildScript.Tests`, así que corre con la
suite) falla si un escenario pierde su nota o una nota sobrevive a su escenario. Las
ejecuciones con pausas se graban primero y se recorren después, de modo que una pausa
nunca puede inflar las duraciones que reporta el árbol de trazas; `--no-pause` reproduce
la ejecución de un tirón, en vivo, y es lo que reciben las tuberías y la CI.

**De dónde salen los renderizados** se responde una vez por ejecución, en la primera
sección de renderizado, porque es la siguiente pregunta que se hace todo espectador de la
demo. No hay renderizador por defecto ni nada que configurar: la captura produce un
`TraceTree` y tú llamas al renderizador que quieras (`IndentedTextRenderer.Render(trace)`);
los renderizadores son métodos estáticos `Render(TraceTree)`, así que el tuyo propio es
cualquier función de un árbol a una cadena. Las líneas en vivo `→ ← !!` no son un
renderizador en absoluto: son un listener sobre el `DualPathPipeline`
(`NarrationStreamListener` en `Examples.Common`, el gemelo `ILogger` del
`Slf4jTraceEventListener` de Java), la única vista que no cuesta código de renderizado. La
configuración elige un renderizador en exactamente un lugar, los archivos de traza
escritos desde las pruebas: `NARRATIVETRACE_OUTPUT=true` más
`NARRATIVETRACE_FORMAT=markdown|text|mermaid|plantuml`, donde `markdown` es el valor por
defecto y las propiedades `NarrativeTraceOutput` / `NarrativeTraceFormat` del paquete
`NarrativeTrace.MSBuild` ajustan los mismos interruptores. Cada marcador de sección nombra
el renderizador que lo produjo.

**La salida de log clásica es un modo de primera clase.** La narración es tráfico
`ILogger` corriente a través de un proveedor de logging corriente, así que se renderiza
en el formato tradicional que ingiere cualquier herramienta de logs: marcas de tiempo
completas `yyyy-MM-dd HH:mm:ss.fff`, nivel, hilo, un scope `traceId` por escenario y
nombre del logger. Tres sitios donde verlo:

- `./demo.sh --example <nombre> --classic`: la ejecución completa, tal cual.
- La demo de ecommerce muestra un escenario ("Unknown Customer") en forma clásica en
  línea, a mitad de ejecución: los mismos eventos que los escenarios estilizados, solo
  cambia la presentación.
- `dotnet run --project examples/<nombre> -- --classic`: el ejemplo solo.

**Las trazas traducidas están disponibles mediante el selector de idioma.**
`--lang es|zh-CN` re-renderiza la misma ejecución a través del glosario que cada ejemplo
tiene comprometido (`glossary.json`): los identificadores se traducen, los valores
permanecen byte-idénticos, y las frases sin traducir aparecen en un pie de "brechas de
glosario". En una terminal interactiva sin `--classic`, `demo.sh` te pide elegir un idioma
siempre que el glosario del ejemplo tenga más de uno; el selector solo lista los locales
que el glosario realmente tiene, del conjunto candidato `es` y `zh-CN`.

El lanzador es compatible con bash 3.2 (lo ejecuta el shell de un macOS de serie) y
necesita `awk`; Git Bash y WSL lo ejecutan sin cambios en Windows. `demo.ps1` es un
envoltorio fino de PowerShell — compila en silencio, ejecuta, imprime — sin el recorrido
coloreado; se escribió sin una máquina Windows y es deliberadamente mínimo.

## Los ejemplos en detalle

### ecommerce: grafo de servicios al estilo de producción

El ejemplo más completo. `ECommerceDemo` ejecuta seis escenarios:

1. **Pedido exitoso + notificación asíncrona**: el camino feliz, con una notificación
   que devuelve un `Task` y cuya finalización en un hilo del pool aterriza en la misma
   traza; renderizado como texto indentado, prosa y diagrama de secuencia Mermaid.
2. **Fallo de pago — bug de fuga de inventario**: la traza revela que se llamó a
   `IInventoryService.Reserve` pero nunca a `Release`: una demostración de trazas que
   sacan a la luz bugs reales.
3. **Servicio externo inestable**: un decorador (`FlakyNotificationService`) que envuelve
   una pasarela simulada acierta una vez y luego falla; cableado a mano con
   `NarrativeTraceProxy.Create`, sin contenedor.
4. **Cliente desconocido**: rama de fallo por validación de entrada.
5. **Sin stock**: rama de fallo por regla de negocio, renderizada además como marcado de
   diagrama de secuencia PlantUML.
6. **Captura asíncrona explícita**: el contexto `AsyncLocal` fluyendo hacia `Task.Run`,
   y después `ForkJoinGroup` y `FireAndForgetGroup` dando al trabajo concurrente
   contextos hijos aislados que se fusionan de vuelta en el padre (`ConcurrencyScenario`).

Clases de apoyo clave: `ECommerceExample` (`BuildContainer` — la `ServiceCollection` más
`AddNarrativeTracing`; `BuildTracedOrderService` — el mismo grafo cableado a mano),
`OrderService` (la orquestación que produce las trazas interesantes) y adaptadores en
memoria sencillos para catálogo, inventario, pago, cliente y notificación, para que la
traza siga siendo fácil de seguir.

### clarity: qué premia y qué penaliza el analizador de claridad

Cuatro escenarios sobre un dominio de reservas de hotel, cada uno en un nivel distinto de
calidad de nombres:

1. **Un huésped reserva una habitación**: nombres excelentes y específicos del dominio
   (`DefaultReservationService`).
2. **Reserva a través de un manager**: nombres adecuados pero menos expresivos
   (`DefaultBookingManager`).
3. **Procesamiento de datos legado**: nombres intencionadamente débiles
   (`DefaultDataProcessor`) que el analizador debería penalizar.
4. **Operaciones del repositorio de huéspedes**: un escenario centrado en la cohesión.

Tras ejecutar todos los escenarios, pasa las trazas capturadas a `ClarityAnalyzer` e
imprime la salida de `ClarityReportRenderer`, para que puedas conectar cada puntuación
con las decisiones de nombres que la causaron.

### minecraft: calidad de nombres, en contraste directo

Dos mitades realizan un trabajo comparable de "el jugador entra al mundo":

- `Refactored.cs`: nombres ricos en dominio: `IWorldGenerator`, `IPlayerInventory`,
  `ICraftingTable`, `ICreatureSpawner`, `IWorldServer`.
- `Unrefactored.cs`: la misma intención escondida tras etiquetas genéricas:
  `IDataProcessor`, `IStateManager`, `IThingFactory`, `IEntityHandler`, `IGameManager`.

`MinecraftExample` ejecuta ambas una tras otra para poder comparar las trazas lado a
lado, y cierra con la puntuación de `ClarityScanner` para cada mitad. Es una ayuda
didáctica sobre nombres y observabilidad, no una muestra de jugabilidad.

### library: consumidor F#

Un pequeño dominio de préstamo de libros (`ICatalogService`, `IMemberService`,
`ILendingService`) trazado mediante `NarrativeTraceProxy` desde F#, con un préstamo
exitoso y un escenario de fallo `BookUnavailableException`, renderizados como texto,
prosa y Mermaid. Las interfaces F# llevan los mismos atributos que usa C#
(`[<Narrated>]`, `[<OnError>]`, `[<NotTraced>]`), y los records se renderizan a través
de sus miembros `[<NarrativeSummary>]`. No hace falta ningún flag del compilador: .NET
conserva los nombres de parámetro en los metadatos, así que la traza lee los nombres F#
tal como se escribieron.

### common: utilería compartida de la demo

`DemoRun` posee el `SyncNarrativeContext` compartido (con el stream en vivo sobre su
`DualPathPipeline`), abre un scope de log `traceId` por escenario e imprime los
marcadores de sección; `ConsoleLoggerFactory` es la `ILoggerFactory` sin dependencias
por la que loguean las ejecuciones, en formato escueto o clásico; `DemoOptions` analiza
`--classic`. Es andamiaje de ejemplo, no código del producto: una aplicación real conecta
su propio proveedor de logging en la misma costura `ILogger`.

## Puertas de calidad

Los ejemplos quedan fuera del empaquetado, pero **no** están exentos de calidad de
código: los analizadores de Roslyn y Sonar, la puerta de longitud de método de 20 NCSS,
`dotnet format` y la suite de pruebas se aplican todos (`./build.sh Verify`). El proyecto
F# lo omiten `dotnet format` y los analizadores de C#, que no cubren F#.
