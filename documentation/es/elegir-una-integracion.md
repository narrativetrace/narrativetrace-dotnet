<!-- source: documentation/choosing-an-integration.md blob d41db990ede8 | translated: 2026-09-11 | reviewed: - -->
# Elegir una integración

[English](../choosing-an-integration.md) | **Español** | [Português](../pt-BR/escolhendo-uma-integracao.md) | [简体中文](../zh-CN/选择集成方式.md)

NarrativeTrace ofrece varias formas de conseguir que una llamada quede
trazada. Esta página es una ayuda para decidir, no un tutorial — para el
código detrás de cada vía, consulta la
[Guía de instalación](../guides/es/guia-de-instalacion.md).

## Quieres... / Empieza por...

| Quieres | Empieza por |
|---|---|
| Trazas en una prueba, con la mínima ceremonia | `NarrativeFixture` de xUnit o `NarrativeTestBase` de NUnit |
| Control explícito sobre exactamente qué se envuelve, en .NET puro | `NarrativeTraceProxy.Create<T>` (un `DispatchProxy`) |
| Todo servicio con interfaz registrado en MS.DI bajo un namespace, trazado automáticamente | `AddNarrativeTracing` (el equivalente en .NET del tracing de beans de Spring/Micronaut) |
| Ciclo de vida de peticiones HTTP en producción con ASP.NET Core | Middleware de `NarrativeTrace.AspNetCore` |
| Una puerta de calidad de nombres en CI sobre un ensamblado compilado, sin necesidad de ejecutar pruebas | `dotnet-narrativetrace clarity-scan` + `clarity-check`, o el paquete `NarrativeTrace.MSBuild` |
| Trazas encaminadas a tu pipeline de `ILogger` existente | `NarrativeTrace.Logging` (`AddNarrativeLogging()`) |
| Spans de OpenTelemetry, por lotes o en vivo | `NarrativeTrace.Observability` |
| Diagramas de secuencia (Mermaid/PlantUML) junto a una traza | `NarrativeTrace.Diagrams` |
| Cero cambios de código — una app cuyo cableado no controlas | **Aún no disponible.** Planificada (Free) — no se ha decidido un análogo en el CLR de un `-javaagent` de Java; la cuestión abierta es un spike entre generador de código fuente y IL weaving. Consulta la [Guía de funcionalidades](../feature-guide.md). |

## La decisión

Toda vía de esta implementación se conecta en un **límite de interfaz** — hoy no hay
opción de tejido de bytecode ni de instrumentación sin código (la fila de
arriba). La pregunta real es *cómo* llegas a esa llamada de interfaz:

```
¿Estás escribiendo una prueba?
  sí -> ¿xUnit?  -> NarrativeFixture
        ¿NUnit?  -> NarrativeTestBase (deriva de ella)

  no  -> ¿Construyes/posees tú mismo la instancia que se va a envolver?
           sí -> ¿Está registrada en MS.DI por interfaz?
                    sí -> AddNarrativeTracing (auto-envoltura por namespace)
                    no  -> NarrativeTraceProxy.Create<T> directamente
           no  -> ¿Es un límite de petición HTTP de ASP.NET Core?
                    sí -> Middleware de NarrativeTrace.AspNetCore
                    no  -> la instrumentación sin código está Planificada,
                           no disponible — consulta la Guía de
                           funcionalidades antes de asumir que existe
```

Por encima de esto, y con independencia de la respuesta anterior:

- ¿Quieres la traza también en tu flujo de logs existente? Añade
  `NarrativeTrace.Logging` junto a la vía de captura que hayas elegido.
- ¿Quieres spans de OpenTelemetry? Añade `NarrativeTrace.Observability` —
  exporta desde un `TraceTree` completado (por lotes) o desde un flujo de
  eventos en vivo, así que se apila sobre cualquiera de las vías anteriores
  en lugar de sustituirla.
- ¿Quieres una puerta de calidad de nombres como parte del build,
  independiente de si se ejecutan las pruebas? `NarrativeTrace.MSBuild`/la
  CLI trabajan a partir de un **ensamblado compilado** vía reflexión, no de
  trazas capturadas — una fuente de datos distinta a todo lo demás de esta
  página.

## Algo que todas las vías comparten

Envuelvas lo que envuelvas, solo las llamadas a **interfaz** son visibles —
`DispatchProxy` está limitado a interfaces por construcción, así que las
llamadas privadas e internas dentro de una implementación nunca se trazan
individualmente. Si quieres una narración más granular, separa el
comportamiento que te interesa detrás de su propia interfaz. La ocultación,
los límites y el aislamiento de excepciones son idénticos en todas las
vías, también — consulta [Privacidad y ocultación](privacidad-y-ocultacion.md);
no hay una integración "más confiable" que las relaje.

## Advertencias por vía

- **`NarrativeTraceProxy.Create<T>`** — `T` debe ser una interfaz y el
  destino debe implementarla; un desajuste aparece como la excepción de
  reflexión subyacente de .NET (`DispatchProxy.Create`/`MethodInfo.Invoke`),
  no una específica de NarrativeTrace. Consulta
  [Solución de problemas](solucion-de-problemas.md).
- **`AddNarrativeTracing`** — llámalo **el último**, después de que todo
  servicio que deba ver ya esté registrado; la envoltura solo toca los
  descriptores presentes en el momento de la llamada. La coincidencia de
  namespace es por límite de punto (`MyApp.Services` nunca coincide con
  `MyApp.ServicesExtra`) y se comprueba contra el namespace de la
  **interfaz** para los servicios registrados por fábrica, ya que el tipo
  de implementación es opaco en el momento del registro. Los servicios con
  clave y los genéricos abiertos se dejan sin envolver silenciosamente.
- **Middleware de ASP.NET Core** — coloca
  `UseMiddleware<NarrativeTraceMiddleware>()` cerca del borde exterior del
  pipeline. El código que llama a `HttpContext.GetNarrativeContext()` antes
  de que se ejecutara el middleware (o en una ruta excluida) obtiene un
  contexto silencioso sin operación, no una excepción — la petición se
  completa con normalidad, solo que sin trazar.
- **`NarrativeFixture` de xUnit** — un fixture guarda un solo contexto, así
  que atiende a una prueba a la vez; `IClassFixture<NarrativeFixture>` es
  seguro porque xUnit no paraleliza dentro de una clase, pero compartir una
  instancia entre clases que sí se ejecutan en paralelo mezcla sus spans en
  una sola traza.
- **`NarrativeTestBase` de NUnit** — deriva de ella; el cableado de
  setup/teardown es automático, y `OnTraceComplete` es tu punto de
  sobrescritura para escribir archivos o ejecutar el análisis de claridad.
- **`clarity-scan`** — necesita una ruta a un ensamblado .NET real y
  compilado; nunca ejecuta el ensamblado, solo reflexiona sobre sus
  metadatos.

## Techos por plataforma

- `net10.0` — superficie completa, incluyendo `NarrativeTrace.AspNetCore`
  (solo `net10.0` por ahora).
- `netstandard2.0` — todo excepto el middleware de ASP.NET Core.
- `net48` — vía `NarrativeTrace.Legacy`; compila en cualquier plataforma,
  pero la validación en tiempo de ejecución en Windows sigue pendiente
  (consulta la [Guía de funcionalidades](../feature-guide.md)).

## Recetas

Código completo y ejecutable para cada vía de arriba vive en la
[Guía de instalación](../guides/es/guia-de-instalacion.md#elige-una-vía-de-integración).
Para la vía de menor ceremonia de principio a fin — un servicio, una
prueba, salida real — consulta
[Ve una traza en 60 segundos](primeros-10-minutos.md).
