<!-- source: documentation/guides/dependency-injection.md blob bd0449f214ec | translated: 2026-09-09 | reviewed: 2026-09-03 -->
# Guía de envoltura automática con inyección de dependencias

[English](../dependency-injection.md) | **Español** | [Português](../pt-BR/guia-de-injecao-de-dependencias.md) | [简体中文](../zh-CN/依赖注入指南.md)

Esta guía cubre `AddNarrativeTracing` — la integración de NarrativeTrace
con `Microsoft.Extensions.DependencyInjection`. Envuelve automáticamente
los servicios registrados por interfaz cuyo namespace de implementación
coincide con un prefijo configurado, de modo que sus llamadas se trazan sin
tocar los sitios de llamada. Es el equivalente en `.NET` del tracing de
beans de Spring/Micronaut: le señalas los namespaces de tus servicios y
decora in situ los beans que coinciden.

## Paquete

```xml
<PackageReference Include="NarrativeTrace.DependencyInjection" Version="0.1.1" />
```

## 1. Registrar y envolver automáticamente

Registra tus servicios como siempre y luego llama a `AddNarrativeTracing`
**después** de ellos — reescribe descriptores que ya están en la colección,
así que no ve los registros añadidos más tarde.

```csharp
using NarrativeTrace.Core;
using NarrativeTrace.DependencyInjection;

services.AddScoped<IOrderService, DefaultOrderService>();
services.AddScoped<IPaymentGateway, StripePaymentGateway>();

services.AddNarrativeTracing(options => options
    .Namespaces("MyApp.Orders", "MyApp.Payments"));
```

Cada descriptor de interfaz que coincide se sustituye por otro que resuelve
la implementación original y devuelve un `NarrativeTraceProxy` sobre ella.
El proxy registra cada llamada en un `INarrativeContext` compartido con
scope de DI.

`AddNarrativeTracing` también registra, con `TryAdd` (así gana un registro
previo tuyo):

- un `NarrativeTraceConfig` singleton construido a partir de
  `options.Level`;
- un `INarrativeContext` **scoped** (un `SyncNarrativeContext` sobre esa
  configuración).

Captura la traza tú mismo desde el scope en el que ejecutaste el trabajo:

```csharp
using var scope = provider.CreateScope();
scope.ServiceProvider.GetRequiredService<IOrderService>()
    .PlaceOrder("C-1234", "SKU-KB", 2);

var trace = scope.ServiceProvider
    .GetRequiredService<INarrativeContext>()
    .CaptureTrace();
```

## 2. Opciones

`AddNarrativeTracing(Action<NarrativeTracingDiOptions>)` requiere un
delegado de configuración. `NarrativeTracingDiOptions` expone:

| Miembro | Tipo | Por defecto | Propósito |
|---|---|---|---|
| `Level` | `TracingLevel` | `Detail` | Nivel de captura del contexto scoped compartido. |
| `Namespaces(params string[])` | fluida | (vacío) | Namespaces base a envolver automáticamente. |
| `ExcludeNamespaces(params string[])` | fluida | (vacío) | Namespaces de interfaz a excluir, incluso cuando su namespace de implementación esté incluido. |

`Namespaces` y `ExcludeNamespaces` son aditivos y encadenables, y cada uno
devuelve la instancia de opciones:

```csharp
services.AddNarrativeTracing(options =>
{
    options.Level = TracingLevel.Narrative;
    options
        .Namespaces("MyApp.Orders", "MyApp.Payments")
        .ExcludeNamespaces("MyApp.Payments.Spi");
});
```

Si no configuras ningún namespace base, no coincide nada — la envoltura
automática es estrictamente opt-in por namespace.

## 3. Semántica de coincidencia de namespaces

Intervienen dos namespaces distintos, y se contrastan con reglas
diferentes:

- La **inclusión** (`Namespaces`) se comprueba contra el namespace de la
  **implementación** — el del tipo concreto (consulta la
  [sección 5](#5-cómo-se-resuelve-el-namespace-de-implementación)).
- La **exclusión** (`ExcludeNamespaces`) se comprueba contra el namespace
  de la **interfaz** (el tipo de servicio). Esto te permite incluir un
  namespace de implementación de forma amplia y luego excluir las
  interfaces de framework/SPI que viven en otro namespace — el análogo de
  la exclusión de SPI/configuración en Spring.

Ambas usan la misma regla de **frontera por punto**: un candidato coincide
con una base cuando es exactamente igual a ella, o cuando empieza por la
base **seguida de un punto**. Un hermano que solo comparte un prefijo de
caracteres no coincide.

Dada la base `MyApp`:

| Namespace candidato | ¿Coincide? | Por qué |
|---|---|---|
| `MyApp` | sí | exacto |
| `MyApp.Orders` | sí | sub-namespace |
| `MyApp.Orders.Internal` | sí | sub-namespace profundo |
| `MyApp2` | no | hermano, sin frontera de punto |
| `MyAppImpl` | no | prefijo compartido, sin frontera de punto |
| `My` | no | más corto que la base |
| `Other` | no | sin relación |
| `null` / `""` | no | un namespace desconocido nunca coincide |

La coincidencia es **ordinal / sensible a mayúsculas** y basta con que
coincida una sola de las bases configuradas.

## 4. Qué se envuelve (y qué no)

Un descriptor se envuelve solo cuando se cumple **todo** lo siguiente:

- el **tipo de servicio es una interfaz** (los registros de clases
  concretas se dejan intactos);
- **no es un genérico abierto** (por ejemplo `IRepository<>`) —
  `DispatchProxy` no puede envolver genéricos abiertos;
- **no es un registro keyed** — los servicios keyed se toleran y se dejan
  sin envolver (sin excepción); aún no están soportados;
- su **namespace de interfaz no está excluido** por `ExcludeNamespaces`;
- su **namespace de implementación está incluido** por `Namespaces`.

Todo lo que falle una comprobación se deja exactamente como estaba
registrado, así que resolverlo devuelve la implementación cruda.

> **Divergencia con múltiples interfaces.** `DispatchProxy` soporta una
> interfaz por proxy. Una clase registrada bajo dos interfaces (por
> ejemplo `AddSingleton<IAlpha>(impl)` y `AddSingleton<IBeta>(impl)`) se
> envuelve **dos veces** — dos objetos proxy distintos sobre el mismo
> destino compartido. Esto difiere de Spring/Micronaut, que construyen un
> único proxy que implementa todas las interfaces del bean. La divergencia
> es intencionada; igualarla en .NET requeriría Castle.DynamicProxy.

## 5. Cómo se resuelve el namespace de implementación

La comprobación de inclusión necesita un namespace concreto. Se toma, por
orden, de:

1. el `ImplementationType` del descriptor (servicios registrados por tipo);
2. si no, el tipo en runtime de su `ImplementationInstance` (servicios
   registrados por instancia);
3. si no, un respaldo al **namespace del tipo de servicio (la interfaz)** —
   usado para los servicios registrados por **factoría**
   (`AddScoped<IFoo>(sp => …)`), cuyo tipo de implementación es opaco en el
   momento del registro.

Como las interfaces y sus implementaciones casi siempre están juntas, el
respaldo de la factoría envuelve correctamente el caso habitual; si tu
factoría devuelve un tipo de un namespace distinto al de la interfaz,
entonces coincidirá por el namespace de la interfaz.

## 6. Comportamiento de tiempos de vida y scopes

- Se **preserva el tiempo de vida** del registro original. Un singleton
  sigue siendo singleton (cada resolución devuelve la misma instancia de
  proxy), un servicio scoped sigue siendo scoped, y así con el resto.
- El `INarrativeContext` compartido es **scoped**. Todos los servicios
  envueltos que se resuelvan dentro del mismo scope de DI registran en el
  **mismo** contexto, así que sus llamadas anidan en un único árbol de
  trazas. Crea un scope por unidad de trabajo (por petición, por job) y
  captura la traza desde ese scope.
- Como el contexto es scoped pero un **singleton** envuelto sobrevive a
  cualquier scope, un proxy singleton resuelve en cada llamada el contexto
  del scope actual a través del service provider — no se queda con un
  contexto obsoleto.

## 7. Interacción con el middleware de ASP.NET Core

El middleware de `NarrativeTrace.AspNetCore` y la envoltura automática de
`AddNarrativeTracing` son **dos vías de tracing independientes** — no las
combines en la misma ruta de petición:

| | Middleware (`NarrativeTraceMiddleware`) | Envoltura automática de DI (`AddNarrativeTracing`) |
|---|---|---|
| El contexto vive en | `HttpContext.Items` (por petición) | un `INarrativeContext` con scope de DI |
| Trazas mediante | envolver contra `HttpContext.GetNarrativeContext()` | resolver desde el scope los servicios envueltos automáticamente |
| Captura y exportación | automáticas, en el `finally` del middleware | llamas tú mismo a `CaptureTrace()` |

**Elige una por ruta de petición.** Los servicios envueltos
automáticamente registran en el contexto con scope de DI, no en
`HttpContext.Items`, así que **no** aparecerán en la traza exportada por el
middleware. Si quieres la captura y exportación automáticas por petición
del middleware, envuelve tus servicios contra
`HttpContext.GetNarrativeContext()` (consulta la
[Guía de integración con ASP.NET Core](guia-de-integracion-con-aspnet-core.md#2-trazar-tus-servicios)).
Recurre a la envoltura automática de DI en hosts que no son web (workers,
apps de consola, consumidores de mensajes) donde tú eres dueño del scope y
capturas la traza directamente.

## Véase también

- [Guía de instalación](guia-de-instalacion.md#opción-b--envoltura-automática-por-inyección-de-dependencias) — las vías de integración de un vistazo
- [Guía de configuración](guia-de-configuracion.md#3-inyección-de-dependencias) — resumen de opciones junto a las demás superficies de configuración
- [Guía de integración con ASP.NET Core](guia-de-integracion-con-aspnet-core.md) — la alternativa del middleware por petición
- [Guía de atributos](guia-de-atributos.md) — `[Narrated]`, `[NotTraced]`, ocultación en las llamadas envueltas
