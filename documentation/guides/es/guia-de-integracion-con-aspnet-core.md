<!-- source: documentation/guides/aspnetcore.md blob d93be958c0a7 | translated: 2026-09-09 | reviewed: 2026-09-03 -->
# Guía de integración con ASP.NET Core

[English](../aspnetcore.md) | **Español** | [Português](../pt-BR/guia-de-integracao-com-aspnet-core.md) | [简体中文](../zh-CN/ASP.NET-Core集成指南.md)

Esta guía cubre el tracing de peticiones HTTP en una aplicación ASP.NET
Core: el ciclo de vida del middleware por petición, la exportación
enchufable, la exclusión de rutas y el contexto de petición/usuario.

## Paquete

```xml
<PackageReference Include="NarrativeTrace.AspNetCore" Version="0.1.1" />
```

## 1. Registrar y cablear el middleware

`AddNarrativeTrace` registra el middleware, las opciones, la configuración
y un exportador por defecto. `app.UseMiddleware<NarrativeTraceMiddleware>()`
lo coloca en el pipeline — pronto, para que envuelva toda la petición.

```csharp
using NarrativeTrace.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddNarrativeTrace(builder.Configuration);

var app = builder.Build();
app.UseMiddleware<NarrativeTraceMiddleware>();
```

`NarrativeTraceMiddleware` es un `IMiddleware`, así que se resuelve desde
DI (registrado como transient por `AddNarrativeTrace`). Su ciclo de vida
por petición es:

1. **Omitir** si la ruta coincide con un prefijo excluido (llama a `next` y
   retorna — no se crea contexto).
2. **Crear** un `SyncNarrativeContext` nuevo y guardarlo en
   `HttpContext.Items` (recupéralo con `HttpContext.GetNarrativeContext()`).
3. **Estampar** el nivel de petición — método HTTP, ruta, IP del cliente —
   y, si hay un `IRequestContextProvider` registrado, el nivel de usuario.
4. **Ejecutar** el resto del pipeline (`await next`).
5. **Capturar y exportar** la traza en un bloque `finally` — así una
   petición que lanza una excepción sigue produciendo una traza, con el
   código de estado de la respuesta y los milisegundos transcurridos.

## 2. Trazar tus servicios

El middleware es dueño de un contexto por petición; envuelve los servicios
que quieras trazar contra **ese** contexto para que sus llamadas aniden en
la traza de la petición. Resuélvelo con `HttpContext.GetNarrativeContext()`:

```csharp
app.MapPost("/orders", (OrderRequest request, HttpContext http) =>
{
    var context = http.GetNarrativeContext();
    var orders = NarrativeTraceProxy.Create<IOrderService>(
        new DefaultOrderService(), context);

    return orders.PlaceOrder(
        request.CustomerId, request.ProductId, request.Quantity);
});
```

`GetNarrativeContext()` devuelve `NoopContext.Instance` en una petición sin
tracing (por ejemplo, una ruta excluida), así que envolver siempre es
seguro — simplemente no registra nada cuando no hay traza activa.

> **Nota:** el contexto por petición del middleware es independiente de la
> envoltura automática de DI con `AddNarrativeTracing` (consulta la
> [Guía de configuración](guia-de-configuracion.md#3-inyección-de-dependencias)).
> La envoltura automática registra en un contexto con scope de DI que
> capturas tú; el middleware registra en `HttpContext.Items` y exporta
> automáticamente. Elige una por ruta de petición — no esperes que los
> servicios envueltos automáticamente aparezcan en la traza exportada por
> el middleware.

## 3. Exportar

Por defecto `AddNarrativeTrace` registra `LoggerTraceExporter`, que loguea
cada traza completada como salida estructurada bajo la categoría de logger
`NarrativeTrace.Export`. Proporciona tu propio `ITraceExporter` para enviar
las trazas a otro sitio:

```csharp
public sealed class OtlpTraceExporter : ITraceExporter
{
    public void Export(TraceTree tree, RequestContext request)
    {
        // request.StatusCode, request.DurationMs
        // envía `tree` a tu backend de observabilidad
    }
}

builder.Services.AddSingleton<ITraceExporter, OtlpTraceExporter>();
builder.Services.AddNarrativeTrace(builder.Configuration);
```

`AddNarrativeTrace` registra su exportador por defecto con `TryAdd`, así
que gana el registro que hagas **antes** de él. `RequestContext` lleva
`StatusCode` y `DurationMs` — los hechos del resultado que solo se conocen
cuando la petición termina.

## 4. Excluir rutas

Omite health checks, métricas y demás ruido por completo — las rutas
excluidas no crean contexto ni traza:

```csharp
builder.Services.AddNarrativeTrace(builder.Configuration, options =>
{
    options.Level = TracingLevel.Detail;
    options.ExcludedPaths.Add("/health");
    options.ExcludedPaths.Add("/metrics");
});
```

O en `appsettings.json`:

```json
{
  "NarrativeTrace": {
    "Level": "Detail",
    "ExcludedPaths": [ "/health", "/metrics" ]
  }
}
```

La coincidencia es por **segmento** de ruta y sin distinguir mayúsculas:
`/health` excluye `/health` y `/health/live`, pero no `/healthcheck`.

Usar ambas fuentes a la vez está bien definido: la configuración se enlaza
primero y el delegado de opciones se ejecuta después, así que `Level` y la
identidad del servicio toman el valor establecido **en código**.
`ExcludedPaths` es la excepción: las rutas configuradas se *añaden* a la
lista, de modo que ambas fuentes se acumulan en lugar de reemplazarse. Las
variables de entorno `NARRATIVETRACE_*`
(ver la [Guía de configuración](guia-de-configuracion.md#2-variables-de-entorno-configresolver))
son un canal opcional aparte que `AddNarrativeTrace` nunca lee.

## 5. Contexto de usuario

Para estampar la identidad del usuario en la traza (id de usuario final,
sesión, tenant), implementa `IRequestContextProvider` y regístralo. El
proveedor es una función pura — *devuelve* la identidad que ha derivado, y
el middleware decide a dónde va:

```csharp
public sealed class ClaimsRequestContextProvider : IRequestContextProvider
{
    public UserContext? ResolveUserContext(HttpContext context)
    {
        var user = context.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            return null;   // el tráfico anónimo es normal, no un error
        }

        return new UserContext(
            EnduserId: user.FindFirst("sub")?.Value,
            TenantId: user.FindFirst("tenant")?.Value);
    }
}

builder.Services.AddSingleton<
    IRequestContextProvider, ClaimsRequestContextProvider>();
```

Todos los miembros de `UserContext` son opcionales; devuelve solo lo que
sepas, y `null` cuando la petición no lleve ninguna identidad. El
middleware estampa los valores devueltos en la traza (`SetUserContext`) y
añade los campos presentes `enduserId` / `sessionId` / `tenantId` al scope
de log de la petición, junto a `traceId` / `traceName` / `httpMethod` /
`httpRoute` / `clientIp`. Si un proveedor lanza una excepción, se traga —
la observabilidad nunca hace fallar la petición.

Estos valores fluyen hacia la exportación JSON, los scopes de logging y los
spans de OpenTelemetry como los niveles de atributos `enduser.*` /
`session.*` / `tenant.*`.

## 6. Probar la integración

Puedes ejercitar el middleware directamente con un `DefaultHttpContext` —
sin necesidad de un host web:

```csharp
var middleware = new NarrativeTraceMiddleware(
    new NarrativeTraceConfig(TracingLevel.Detail));

var http = new DefaultHttpContext();
await middleware.InvokeAsync(http, ctx =>
{
    var orders = NarrativeTraceProxy.Create<IOrderService>(
        new DefaultOrderService(), ctx.GetNarrativeContext());
    orders.PlaceOrder("C-1", "SKU-1", 1);
    return Task.CompletedTask;
});
```

Para pruebas unitarias más rápidas de servicios concretos, sáltate el
middleware y usa los
[helpers de pruebas de xUnit / NUnit](guia-de-instalacion.md#opción-d--contexto-automático-en-xunit--narrativa-de-fallos)
directamente con `NarrativeTraceProxy`.

## Véase también

- [Guía de instalación](guia-de-instalacion.md) — paquetes y vías de integración
- [Guía de configuración](guia-de-configuracion.md) — niveles, rutas excluidas, envoltura automática de DI
- [Guía de atributos](guia-de-atributos.md) — narración, ocultación, contexto de error
