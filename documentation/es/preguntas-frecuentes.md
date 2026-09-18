<!-- source: documentation/faq.md blob 7bf23139d0b6 | translated: 2026-09-18 | reviewed: - -->
# NarrativeTrace .NET — Preguntas frecuentes

[English](../faq.md) | **Español** | [Português](../pt-BR/perguntas-frequentes.md) | [简体中文](../zh-CN/常见问题.md)

## ¿Cuál gana: el nivel de tracing o el nivel de mi logger?

**Configuré el nivel de tracing en `Detail` pero no aparece nada en mis logs. O bien: configuré mi
logger en `Warning` y la traza sigue apareciendo completa en `CaptureTrace()`. ¿Qué ajuste gana?**

Los dos, porque responden preguntas distintas. NarrativeTrace tiene dos diales independientes, y un
evento capturado puede llegar a un logger por dos vías diferentes.

**El dial 1, `TracingLevel`, decide qué se captura.** `Off`, `Errors`, `Summary`, `Narrative`,
`Detail` — el ajuste propio de NarrativeTrace (`NarrativeTraceConfig.Level`), de menor a mayor. Se
sitúa al frente de la captura: una llamada que este nivel filtra nunca llega a formar parte del
árbol de trazas. No existe para `CaptureTrace()`, para ningún renderizador ni para ningún logger, y
ningún otro ajuste puede recuperarla. Este dial es también el único que cambia el coste del
tracing: `Off` se salta la captura por completo — `EnterMethod` retorna de inmediato y no se crea
ningún evento. Cualquier otro nivel intercepta cada llamada — `Errors` y `Summary` siguen
capturando un evento completo de entrada/salida, y después `TraceTreeBuilder` poda el árbol
ensamblado (un nodo con error conserva todo su subárbol; un árbol `Summary` colapsa a hojas y
marcos de error) — mientras que `Narrative` y `Detail` mantienen el árbol sin filtrar, y `Detail`
además captura los valores de los parámetros.

**El dial 2, el nivel de tu logger, decide qué se imprime.** `TraceLoggingOptions` asigna los
tipos de evento a niveles de `Microsoft.Extensions.Logging`: los eventos de entrada y de ciclo de
vida de fork/join/fire-and-forget en `Trace` (`EnterLevel`), un retorno exitoso en `Trace`
(`ReturnLevel`), una salida por excepción en `Warning` (`ExceptionLevel`) — los valores por
defecto tanto para `LoggingNarrativeContext` como para el puente de flujo de eventos
`AddNarrativeLogging()`. (Un host de ASP.NET Core además obtiene una línea de nivel `Information`
por cada traza de *petición* terminada, desde `LoggerTraceExporter`, categoría de logger
`NarrativeTrace.Export` — un resumen aparte, por petición, que no forma parte de
`TraceLoggingOptions`.) El nivel mínimo de tu propio logger hace entonces lo que siempre hace:
subirlo silencia líneas. Nunca captura más, y nunca captura menos.

**Ahora las dos vías por las que un evento capturado puede llegar a un logger — aquí es donde
viene la confusión.** `DualPathPipeline` es el nombre del tipo: un fan-out con una ranura síncrona
y una ranura con buffer, ambas opcionales.

- La **vía síncrona** corre en línea, en el hilo que hace la llamada, antes de que retorne la
  llamada trazada — o bien cableas `LoggingTraceEventListener.OnEvent` directamente en la propia
  ranura síncrona de `DualPathPipeline`, o bien usas `LoggingNarrativeContext`, un decorador de
  contexto con la misma propiedad de "antes de que la llamada retorne" que no pasa en absoluto por
  `DualPathPipeline`. De cualquier forma, la línea de log se escribe antes de que nada aguas abajo
  vea el resultado.
- La **vía con buffer** es la otra ranura de `DualPathPipeline`: un `BufferedEventConsumer` — un
  anillo acotado y sin bloqueos, drenado en un hilo en segundo plano, que descarta carga bajo
  presión para no bloquear jamás a quien llama. `services.AddNarrativeLogging()` suscribe
  automáticamente `LoggingTraceEventListener` a él en una app alojada por DI; el listener en vivo
  de OpenTelemetry (`OtelTraceEventListener`, `NarrativeTrace.Observability`) puede suscribirse
  del mismo modo.

Ambas ranuras — de hecho, todo el pipeline — son opcionales: los registros de serie
`AddNarrativeTracing`/`AddNarrativeTrace` construyen un `SyncNarrativeContext` plano sin ningún
sink, así que nada fluye en vivo hasta que un host cablea uno.

**`CaptureTrace()` no lee de ninguna de las dos vías.** El archivo de traza, el `TraceTree` que
devuelve `CaptureTrace()`, una línea base de aprobación y la exportación a OpenTelemetry de
`TraceActivityExporter` provienen todos de la propia lista de captura de un contexto — siempre
activa, síncrona, en memoria — presente se cablee o no cualquier sink del pipeline, y a la que
ningún logger consulta jamás. Así que: un logger en `Warning` y un nivel de tracing en `Detail` te
da un log silencioso y un resultado completo de `CaptureTrace()`. Un nivel de tracing en `Summary`
y un logger en `Trace` te da un log ruidoso de una traza delgada. Un nivel de tracing en `Off` no
te da nada en ningún sitio, porque no se capturó nada.

**Reglas prácticas.**

| Objetivo | Ajusta |
|---|---|
| Reducir el volumen de logs | Sube el nivel mínimo de tu logger para la categoría `NarrativeTrace` (o acota `TraceLoggingOptions`); el archivo de traza y `CaptureTrace()` quedan intactos. |
| Reducir el tamaño de la traza | Baja el `TracingLevel` (`Detail` → `Narrative` → `Summary` → `Errors`). |
| Reducir CPU/memoria | Baja el `TracingLevel` — `Off` se salta la captura por completo; el umbral del logger no cambia nada del coste de captura. |
| Mantener el tracing activo en producción pero fuera de los logs | Deja el `TracingLevel` en `Summary` o `Narrative`; o bien te saltas por completo el cableado de `AddNarrativeLogging()`/`LoggingNarrativeContext`, o subes la categoría `NarrativeTrace` por encima de `Warning` — `CaptureTrace()` y cualquier exportador siguen viendo el cuadro completo. |

Consulta la [Guía de configuración §8, "Dos diales, dos vías"](../guides/es/guia-de-configuracion.md#8-dos-diales-dos-vías)
para saber dónde vive cada dial, archivo por archivo.
