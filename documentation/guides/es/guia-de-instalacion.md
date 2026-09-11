<!-- source: documentation/guides/installation.md blob 75b1451e0562 | translated: 2026-09-09 | reviewed: 2026-09-03 -->
# NarrativeTrace .NET — Guía de instalación

[English](../installation.md) | **Español** | [Português](../pt-BR/guia-de-instalacao.md) | [简体中文](../zh-CN/安装指南.md)

Esta guía cubre la instalación y el cableado de NarrativeTrace en un
proyecto .NET.

## Requisitos previos

- SDK de .NET 10 para compilar; las bibliotecas son multi-target `net10.0`
  y `netstandard2.0`, así que se ejecutan en cualquier runtime de .NET
  compatible con netstandard2.0 (.NET Core 2.0+, .NET 5+ y — vía el
  paquete `Legacy` — .NET Framework 4.8).
- **No hace falta ningún flag del compilador.** A diferencia de la JVM
  (que necesita `-parameters`), .NET conserva los nombres de los
  parámetros en los metadatos por defecto, así que las trazas muestran los
  nombres reales desde el primer momento. Usa el atributo
  [`[Traced]`](guia-de-atributos.md#traced) solo cuando quieras
  sobrescribir un nombre.

## Paquetes

Cada proyecto bajo `src/` se publica como un paquete NuGet con el mismo
id. Empieza por el mínimo y añade solo lo que necesites.

```xml
<!-- Mínimo: captura + renderizado -->
<PackageReference Include="NarrativeTrace.Core" Version="0.1.3" />
<PackageReference Include="NarrativeTrace.Runtime" Version="0.1.3" />
<PackageReference Include="NarrativeTrace.Proxy" Version="0.1.3" />
```

| Paquete | Cuándo añadirlo |
|---|---|
| `NarrativeTrace.Core` | Siempre — modelo de trazas, `INarrativeContext`, configuración, ocultación, renderizadores Markdown/Prose/texto y los atributos `[Narrated]`/`[OnError]`/`[NotTraced]`/`[NarrativeSummary]` (namespace `NarrativeTrace.Core.Annotation`). |
| `NarrativeTrace.Runtime` | Siempre — el motor de captura (`SyncNarrativeContext`, `AsyncNarrativeContext`, exportadores JSON/de capítulos). |
| `NarrativeTrace.Proxy` | Tracing de interfaces vía `DispatchProxy`, más la sobrescritura de nombres de parámetros `[Traced]`, específica del proxy. |
| `NarrativeTrace.DependencyInjection` | `AddNarrativeTracing` — envoltura automática de los servicios con interfaz que coinciden por namespace en el contenedor de MS.DI. |
| `NarrativeTrace.AspNetCore` | Middleware con ciclo de vida de trazas por petición para ASP.NET Core. |
| `NarrativeTrace.Testing.Xunit` | `NarrativeFixture` — contexto por prueba e impresión de la narrativa en los fallos (namespace `NarrativeTrace.TestingXunit`). |
| `NarrativeTrace.Testing.NUnit` | `NarrativeTestBase` — lo mismo para NUnit (namespace `NarrativeTrace.TestingNUnit`). |
| `NarrativeTrace.Diagrams` | Renderizadores de diagramas de secuencia Mermaid / PlantUML. |
| `NarrativeTrace.Clarity` | Análisis e informes de claridad de los nombres (`ClarityScanner`, `ClarityAnalyzer`). |
| `NarrativeTrace.Observability` | Puente con OpenTelemetry — exportación por lotes y creación de spans en vivo vía `System.Diagnostics.ActivitySource`. |
| `NarrativeTrace.Logging` | Puente con `Microsoft.Extensions.Logging` (`LoggingNarrativeContext`, `TraceLogExporter`, `AddNarrativeLogging()`). |
| `NarrativeTrace.Cli` | Herramienta global `dotnet-narrativetrace` — análisis de claridad solo por reflexión + puerta de calidad en CI. |
| `NarrativeTrace.MSBuild` | Paquete solo de build que cablea la CLI en `dotnet build` / `dotnet test`. |

> Las versiones son pre-1.0 (`0.1.3`). Usa la versión que realmente
> instalaste; mantén todos los paquetes `NarrativeTrace.*` en la misma
> versión.

## Elige una vía de integración

### Opción A — DispatchProxy (funciona en cualquier app .NET)

Envuelve un servicio tipado por interfaz; cada llamada a través del proxy
se registra en el contexto compartido.

```csharp
using NarrativeTrace.Core;
using NarrativeTrace.Proxy;
using NarrativeTrace.Runtime;

var context = new SyncNarrativeContext(
    new NarrativeTraceConfig(TracingLevel.Detail));

var orders = NarrativeTraceProxy.Create<IOrderService>(
    new DefaultOrderService(), context);

orders.PlaceOrder("C-1234", "SKU-KB", 2);

Console.WriteLine(IndentedTextRenderer.Render(context.CaptureTrace()));
context.Reset();
```

Usa esta opción cuando tus servicios estén basados en interfaces.
Comparte **un solo** contexto entre los servicios que colaboran para que
sus llamadas aniden en un único árbol.

### Opción B — Envoltura automática por inyección de dependencias

El equivalente en `.NET` de la envoltura automática de beans de
Spring/Micronaut. Registra tus servicios como siempre y luego envuelve
aquellos cuyo namespace de implementación coincida con un prefijo:

```csharp
using NarrativeTrace.DependencyInjection;

services.AddScoped<IOrderService, DefaultOrderService>();

services.AddNarrativeTracing(options => options
    .Namespaces("MyApp.Services", "MyApp.Payments"));
```

Solo se envuelven los servicios registrados por **interfaz** cuyo
namespace de implementación coincide con un prefijo configurado. Se
registra automáticamente un `INarrativeContext` scoped. Consulta la
[Guía de configuración](guia-de-configuracion.md#3-inyección-de-dependencias)
para ver las opciones.

### Opción C — Tracing de peticiones en ASP.NET Core

Añade los servicios y coloca el middleware al principio del pipeline. Cada
petición recibe una traza nueva (en `HttpContext`), que el middleware
captura y exporta cuando la petición termina. Traza tus servicios
resolviendo ese contexto por petición:

```csharp
using NarrativeTrace.AspNetCore;
using NarrativeTrace.Proxy;

builder.Services.AddNarrativeTrace(builder.Configuration);

var app = builder.Build();
app.UseMiddleware<NarrativeTraceMiddleware>();

app.MapGet("/orders/{id}", (string id, HttpContext http) =>
{
    var orders = NarrativeTraceProxy.Create<IOrderService>(
        new DefaultOrderService(), http.GetNarrativeContext());
    return orders.FindOrder(id);
});
```

Consulta la
[Guía de integración con ASP.NET Core](guia-de-integracion-con-aspnet-core.md)
para exportadores, rutas excluidas y contexto de petición/usuario.

### Opción D — Contexto automático en xUnit + narrativa de fallos

```csharp
using NarrativeTrace.Proxy;
using NarrativeTrace.TestingXunit;   // ojo: sin punto antes de Xunit
using Xunit;

public sealed class OrderServiceTests
{
    [Fact]
    public void Customer_places_order()
    {
        using var narrative = new NarrativeFixture();

        narrative.Run("Customer places order", context =>
        {
            var orders = NarrativeTraceProxy.Create<IOrderService>(
                new DefaultOrderService(), context);
            orders.PlaceOrder("C-1234", "SKU-KB", 2);
        });
    }
}
```

Crea un `NarrativeFixture` por prueba con `using var` para que cada una
tenga un contexto limpio (el dispose lo reinicia). `NarrativeFixture.Run`
imprime la narrativa capturada en la consola cuando el cuerpo lanza una
excepción y luego la relanza — así una prueba que falla se explica sola.

### Opción E — Contexto automático en NUnit + narrativa de fallos

```csharp
using NarrativeTrace.Proxy;
using NarrativeTrace.TestingNUnit;   // ojo: sin punto antes de NUnit
using NUnit.Framework;

public sealed class OrderServiceTests : NarrativeTestBase
{
    [Test]
    public void Customer_places_order()
    {
        var orders = NarrativeTraceProxy.Create<IOrderService>(
            new DefaultOrderService(), Context);
        orders.PlaceOrder("C-1234", "SKU-KB", 2);
    }
}
```

`NarrativeTestBase` abre un contexto nuevo por prueba (`[SetUp]`) e
imprime la narrativa cuando falla (`[TearDown]`). Sobrescribe
`OnTraceComplete` para escribir archivos o ejecutar el análisis de
claridad.

### Opción F — CLI `dotnet-narrativetrace`

Instala la herramienta global y analiza la claridad de los nombres a
partir de un ensamblado compilado — sin necesidad de ejecutar pruebas:

```bash
dotnet tool install --global NarrativeTrace.Cli

dotnet-narrativetrace clarity-scan --assembly bin/Release/net10.0/MyApp.dll
dotnet-narrativetrace clarity-check --results clarity-results.json --min-score 0.80 --max-high-issues 0
```

Consulta la [Guía de claridad](guia-de-claridad.md) para el modelo de
puntuación y la puerta de calidad en CI.

### Opción G — Integración con MSBuild

Añade el paquete solo de build para ejecutar la puerta de claridad como
parte de tu build:

```xml
<PackageReference Include="NarrativeTrace.MSBuild" Version="0.1.3"
                  PrivateAssets="all" />
```

Esto registra los targets `ClarityScan` y `ClarityCheck` y reenvía los
ajustes `NARRATIVETRACE_*` al host de pruebas. Configura los umbrales con
propiedades de MSBuild (consulta la
[Guía de configuración](guia-de-configuracion.md#5-msbuild)).

## Configurar la salida de trazas

La biblioteca lee cuatro variables de entorno `NARRATIVETRACE_*` a través
de `ConfigResolver` — el canal de sobrescritura nativo de `.NET`:

| Variable | Valores | Por defecto |
|---|---|---|
| `NARRATIVETRACE_LEVEL` | `Off`, `Errors`, `Summary`, `Narrative`, `Detail` | `Detail` |
| `NARRATIVETRACE_OUTPUT` | `true` / `false` (también `1`) | `false` |
| `NARRATIVETRACE_OUTPUT_DIR` | cualquier ruta con permisos de escritura | (ninguno) |
| `NARRATIVETRACE_FORMAT` | `Markdown`, `Text`, `Prose`, `Json` | `Markdown` |

Los valores inválidos degradan al valor por defecto en lugar de lanzar
una excepción, así que una configuración incorrecta nunca rompe la
captura. El parseo del nivel es tolerante a mayúsculas y puntuación
(`detail`, `DETAIL`, `Detail` resuelven todos).

## Validar la instalación

Renderiza una traza en la consola:

```csharp
var context = new SyncNarrativeContext(
    new NarrativeTraceConfig(TracingLevel.Detail));
var svc = NarrativeTraceProxy.Create<IGreeter>(new Greeter(), context);
svc.Greet("world");
Console.WriteLine(IndentedTextRenderer.Render(context.CaptureTrace()));
```

Deberías ver una narrativa anidada con el nombre del método, los valores
de los parámetros y el valor de retorno.

## Véase también

- [Guía de configuración](guia-de-configuracion.md) — niveles de tracing, variables de entorno, DI, ASP.NET Core, MSBuild
- [Guía de atributos](guia-de-atributos.md) — `[Narrated]`, `[OnError]`, `[NotTraced]`, `[Traced]`, `[NarrativeSummary]`
- [Guía de integración con ASP.NET Core](guia-de-integracion-con-aspnet-core.md) — middleware, exportadores, contexto de petición/usuario
- [Guía de claridad](guia-de-claridad.md) — modelo de puntuación, scanner, puerta de calidad en CI
