<!-- source: documentation/first-10-minutes.md blob 85c4e7df07b9 | translated: 2026-09-12 | reviewed: - -->
# Ve una traza en 60 segundos

[English](../first-10-minutes.md) | **Español** | [Português](../pt-BR/primeiros-10-minutos.md) | [简体中文](../zh-CN/前10分钟.md)

Sin sentencias de log, sin framework de pruebas, sin archivos que abrir: una
app de consola, un `dotnet run`, y una traza en tu terminal. Todo lo que
sigue se ejecutó de verdad contra los paquetes publicados — la salida está
pegada, no imaginada.

## 1. App de consola nueva, añade los paquetes

```bash
dotnet new console -n Hello && cd Hello
dotnet add package NarrativeTrace.Core
dotnet add package NarrativeTrace.Runtime
dotnet add package NarrativeTrace.Proxy
```

Tres paquetes, resueltos desde nuget.org — todavía no hay un único
metapaquete que los traiga todos de una vez.

## 2. Reemplaza Program.cs

```csharp
using NarrativeTrace.Core;
using NarrativeTrace.Proxy;
using NarrativeTrace.Runtime;

var context = new SyncNarrativeContext(new NarrativeTraceConfig());
var orders = NarrativeTraceProxy.Create<IOrderService>(new OrderService(), context);

orders.PlaceOrder("cust-1", "book-123", 2);

Console.WriteLine(IndentedTextRenderer.Render(context.CaptureTrace()));

public interface IOrderService
{
    string PlaceOrder(string customerId, string productId, int quantity);
}

public sealed class OrderService : IOrderService
{
    public string PlaceOrder(string customerId, string productId, int quantity)
        => $"confirmed:{customerId}:{productId}:{quantity}";
}
```

## 3. Ejecútalo

```bash
dotnet run
```

```text
└── IOrderService.PlaceOrder(customerId: "cust-1", productId: "book-123", quantity: 2) → "confirmed:cust-1:book-123:2" — 9ms
```

(El tiempo variará de una ejecución a otra — todo lo demás es estable.)

No escribiste ni una sola sentencia de log. Esa narrativa salió por completo
del nombre de tu método, los nombres de tus parámetros y el valor que
devolviste.

## Lo que acaba de pasar

- `NarrativeTraceProxy.Create<T>` envuelve `OrderService` detrás de su
  interfaz `IOrderService` y registra cada llamada hecha a través del
  wrapper.
- `SyncNarrativeContext` guarda la grabación en memoria;
  `NarrativeTraceConfig()` usa por defecto `TracingLevel.Detail` — captura
  completa de parámetros y valores de retorno, sin ningún flag que activar.
- `CaptureTrace()` devuelve el árbol grabado; `IndentedTextRenderer` lo
  imprimió arriba. `MarkdownRenderer` y `ProseRenderer` renderizan el mismo
  árbol en otros formatos. Envuelve un servicio que llama a otros servicios
  trazados y el árbol se anida — una llamada, una historia.

## Envíalo a tu logger

Dos paquetes más, el mismo árbol capturado, sin lógica de captura nueva —
`TraceLogExporter` lo reproduce sobre el `ILogger` que tu app ya tenga
cableado.

```bash
dotnet add package NarrativeTrace.Logging
dotnet add package Microsoft.Extensions.Logging.Console
```

```diff
+using Microsoft.Extensions.Logging;
 using NarrativeTrace.Core;
+using NarrativeTrace.Logging;
 using NarrativeTrace.Proxy;
 using NarrativeTrace.Runtime;

 var context = new SyncNarrativeContext(new NarrativeTraceConfig());
 var orders = NarrativeTraceProxy.Create<IOrderService>(new OrderService(), context);

 orders.PlaceOrder("cust-1", "book-123", 2);

-Console.WriteLine(IndentedTextRenderer.Render(context.CaptureTrace()));
+var tree = context.CaptureTrace();
+Console.WriteLine(IndentedTextRenderer.Render(tree));
+
+using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
+TraceLogExporter.ExportToLogger(tree, loggerFactory.CreateLogger("NarrativeTrace"));
```

```bash
dotnet run
```

```text
└── IOrderService.PlaceOrder(customerId: "cust-1", productId: "book-123", quantity: 2) → "confirmed:cust-1:book-123:2" — 0ms

info: NarrativeTrace[1]
      IOrderService.PlaceOrder(customerId: "cust-1", productId: "book-123", quantity: 2) -> "confirmed:cust-1:book-123:2"
```

(El tiempo variará de una ejecución a otra — todo lo demás es estable.)

La misma traza, dos destinos: el renderizador de consola queda exactamente
igual, y el registro de `ILogger` de abajo demuestra que el árbol llega al
sumidero que ya tienes — cambia `AddConsole()` por tu proveedor real y nada
más cambia. Consulta la [Guía de instalación](../guides/es/guia-de-instalacion.md)
para los demás puntos de entrada de `NarrativeTrace.Logging`
(`LoggingNarrativeContext` para logging en vivo por llamada,
`AddNarrativeLogging()` para DI).

## Siguiente paso

- **Úsalo en tus pruebas, o cablealo en DI / ASP.NET Core** —
  [Elegir una integración](elegir-una-integracion.md) es el diagrama de
  decisión; la [Guía de instalación](../guides/es/guia-de-instalacion.md)
  tiene la referencia completa de paquetes y cableado.
- **Mantén un valor fuera de la traza** — `[NotTraced]`, la lista de
  bloqueo de nombres siempre activa, y el artefacto estructural `.nt` libre
  de valores son tres capas independientes — consulta
  [Privacidad y ocultación](privacidad-y-ocultacion.md).
- **Puntúa tu nomenclatura** — renombra `PlaceOrder` a `Process`, deja todo
  lo demás igual, y la puntuación de claridad cae — consulta la
  [Guía de claridad](../guides/es/guia-de-claridad.md).
- **Cada opción** — [Guía de configuración](../guides/es/guia-de-configuracion.md):
  niveles de tracing, formato de salida, cada variable `NARRATIVETRACE_*`.
- **¿Algo no funciona?** — [Solución de problemas](solucion-de-problemas.md):
  síntoma → causa → arreglo para los fallos que la gente realmente encuentra.
