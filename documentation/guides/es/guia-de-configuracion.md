<!-- source: documentation/guides/configuration.md blob 39d1b98903b3 | translated: 2026-09-07 | reviewed: - -->
# NarrativeTrace .NET — Guía de configuración

[English](../configuration.md) | **Español** | [Português](../pt-BR/guia-de-configuracao.md) | [简体中文](../zh-CN/配置指南.md)

Esta guía documenta la configuración de runtime y de build de
NarrativeTrace.

## Superficie de configuración

| Vía | Mecanismo | Ideal para |
|---|---|---|
| Programática | Constructor de `NarrativeTraceConfig` | Cualquier app — control directo |
| Entorno | Variables `NARRATIVETRACE_*` (`ConfigResolver`) | CI, contenedores, hosts de pruebas |
| Inyección de dependencias | `AddNarrativeTracing(options => …)` | Apps con MS.DI |
| ASP.NET Core | `AddNarrativeTrace(configuration)` + sección `"NarrativeTrace"` | Apps web |
| MSBuild | Propiedades de MSBuild `NarrativeTrace*` / `Clarity*` | Puerta de claridad en tiempo de build |

## 1. Niveles de tracing (`NarrativeTraceConfig`)

Un contexto se crea a partir de un `NarrativeTraceConfig`, que por defecto
es `Detail`.

```csharp
using NarrativeTrace.Core;
using NarrativeTrace.Runtime;

var config = new NarrativeTraceConfig(TracingLevel.Narrative);
var context = new SyncNarrativeContext(config);
```

Niveles disponibles (enum `TracingLevel`, de menor a mayor):

| Nivel | Comportamiento |
|---|---|
| `Off` | No se captura tracing. |
| `Errors` | Solo se capturan los caminos con excepción. |
| `Summary` | Entrada raíz, hoja más profunda y cadenas de excepción completas. |
| `Narrative` | Flujo de llamadas completo, con los valores de parámetros suprimidos. |
| `Detail` | Flujo de llamadas completo con valores de parámetros y de retorno. |

El nivel es mutable en runtime (`config.Level = TracingLevel.Errors;`).
`IsActive()` es `true` en `Errors` y por encima (solo `Off` desactiva la
captura); `CapturesParameterValues` es `true` únicamente en `Detail`.

### Identidad del servicio

Estampa una identidad de proceso estable en cada span para correlacionar
entre servicios:

```csharp
var identity = new ServiceIdentity(
    ServiceName: "order-service",
    ServiceVersion: "2.0.0",
    Environment: "production");

var config = new NarrativeTraceConfig(TracingLevel.Detail, identity);
```

Aparecen como `service.name` / `service.version` / `service.environment` en
la exportación JSON, los scopes de logging y los spans de OpenTelemetry.

## 2. Variables de entorno (`ConfigResolver`)

`ConfigResolver` lee seis variables — el canal de sobrescritura nativo de
`.NET` que mantiene `Core` libre de dependencias. Los valores inválidos
**degradan al valor por defecto en lugar de lanzar una excepción**, así que
una configuración incorrecta nunca rompe la captura.

| Variable | Valores | Por defecto |
|---|---|---|
| `NARRATIVETRACE_LEVEL` | `Off`, `Errors`, `Summary`, `Narrative`, `Detail` | `Detail` |
| `NARRATIVETRACE_OUTPUT` | `true` / `false` (o `1`) | `false` |
| `NARRATIVETRACE_OUTPUT_DIR` | cualquier ruta con permisos de escritura | (ninguno) |
| `NARRATIVETRACE_FORMAT` | `Markdown`, `Text`, `Prose`, `Json` | `Markdown` |
| `NARRATIVETRACE_CANONICAL_JSON` | `true` / `false` (o `1`) | `false` |
| `NARRATIVETRACE_STRUCTURAL_JSON` | `true` / `false` (o `1`) | `false` |

El parseo del nivel y del formato es tolerante (insensible a mayúsculas y
puntuación: `detail`, `DETAIL` y `Detail` resuelven todos).

```csharp
var resolved = ConfigResolver.Resolve();          // lee el entorno del proceso
var config = new NarrativeTraceConfig(resolved.Level);
```

`Resolve()` devuelve un
`ResolvedConfig(Level, Output, OutputDir, Format, CanonicalJson, StructuralJson)`.

Las dos últimas activan los artefactos legibles por máquina que se escriben
junto al fichero de traza, sea cual sea el formato principal:

- `<test>.canonical.json` — la traza aplanada en entradas del esquema canónico
  (un `method_enter` y un `method_exit` por llamada), para consumidores del
  esquema y para las fixtures de conformidad entre plataformas.
- `<test>.structural.json` — el mismo array con todos los valores de ejecución
  elididos (ADR-002 nivel 1), pensado para entregárselo a una IA: los nombres
  de los parámetros sobreviven; sus valores, los valores de retorno y los
  mensajes de excepción no.

Ambos están desactivados por defecto: son artefactos de máquina, no algo que se
lea junto a la traza.

## 3. Inyección de dependencias

`AddNarrativeTracing` envuelve los servicios registrados por interfaz cuyo
namespace de implementación coincide con un prefijo configurado (el
equivalente en `.NET` de la envoltura automática de beans de
Spring/Micronaut).

```csharp
using NarrativeTrace.DependencyInjection;

services.AddNarrativeTracing(options =>
{
    options.Level = TracingLevel.Detail;           // por defecto: Detail
    options.Namespaces("MyApp.Orders", "MyApp.Payments");
});
```

Comportamiento:

- Solo se consideran los tipos de servicio que son **interfaz**; los
  genéricos abiertos se omiten (una limitación de `DispatchProxy`).
- La coincidencia de namespaces usa semántica de **frontera por punto**:
  `MyApp.Orders` coincide con `MyApp.Orders` y `MyApp.Orders.Sub`, pero no
  con `MyApp.OrdersLegacy`.
- Se **preserva el tiempo de vida** del registro original (un singleton
  sigue siendo singleton, etc.).
- Se registra automáticamente un `INarrativeContext` **scoped**; todos los
  servicios envueltos en el mismo scope lo comparten, así que sus llamadas
  anidan en un único árbol.
- Para un servicio registrado por factoría o por instancia se usa el
  namespace de la implementación concreta; si no está disponible, se usa
  como respaldo el namespace del tipo de servicio (la interfaz).

## 4. ASP.NET Core

`AddNarrativeTrace` enlaza una sección opcional de `IConfiguration` llamada
`NarrativeTrace` y luego deja que un delegado la sobrescriba.

```csharp
builder.Services.AddNarrativeTrace(builder.Configuration, options =>
{
    options.Level = TracingLevel.Detail;
    options.ExcludedPaths.Add("/health");
    options.ExcludedPaths.Add("/metrics");
});
```

`appsettings.json`:

```json
{
  "NarrativeTrace": {
    "Level": "Detail",
    "ExcludedPaths": [ "/health", "/metrics" ]
  }
}
```

| Clave | Tipo | Propósito |
|---|---|---|
| `Level` | `TracingLevel` | Nivel de captura del contexto de la petición. |
| `ExcludedPaths` | `string[]` | Prefijos de ruta omitidos por completo (coincidencia por segmento). |

Consulta la
[Guía de integración con ASP.NET Core](guia-de-integracion-con-aspnet-core.md)
para el cableado del middleware y del exportador.

## 5. MSBuild

El paquete `NarrativeTrace.MSBuild` es una capa fina sobre la CLI
`dotnet-narrativetrace` — toda la lógica de umbrales vive en la CLI.
Declara estas propiedades sobrescribibles:

| Propiedad | Por defecto | Propósito |
|---|---|---|
| `NarrativeTraceOutput` | `false` | Emitir `NARRATIVETRACE_OUTPUT=true` al host de pruebas. |
| `NarrativeTraceOutputDir` | `$(MSBuildProjectDirectory)/narrativetrace` | Dónde se escriben resultados y trazas. |
| `NarrativeTraceFormat` | `markdown` | Formato de salida de las trazas. |
| `NarrativeTraceLevel` | `DETAIL` | Nivel de captura del host de pruebas. |
| `ClarityMinScore` | `0.0` | Falla el build por debajo de esta puntuación global. |
| `ClarityMaxHighIssues` | `2147483647` | Falla el build por encima de este número de incidencias HIGH. |
| `ClarityWarnOnly` | `false` | Degrada un fallo de la puerta a una advertencia. |

También registra dos targets:

- **`ClarityScan`** (depende de `Build`) — escaneo solo por reflexión del
  ensamblado compilado hacia `clarity-results.json`.
- **`ClarityCheck`** (depende de `ClarityScan`) — ejecuta la puerta; es
  incremental gracias a un archivo `.stamp`, así que se omite una nueva
  comprobación si `clarity-results.json` no ha cambiado.

```bash
# Imponer un umbral como parte del build:
dotnet build /t:ClarityCheck /p:ClarityMinScore=0.80 /p:ClarityMaxHighIssues=0
```

La herramienta debe estar instalada
(`dotnet tool install --global NarrativeTrace.Cli`, o mediante un
manifiesto de herramientas local).

## 6. Ocultación

El renderizado reflexivo de valores **oculta por defecto, no filtra por
defecto**. Cuando un objeto trazado no tiene un resumen cuidado,
`ValueRenderer` refleja sobre sus propiedades — pero una lista de
denegación por nombre (`RedactionPolicy.Default`) sustituye por
`[REDACTED]`, antes de renderizar, los valores cuyo nombre de campo
coincide con un patrón sensible (`password`, `secret`, `token`, `apikey`,
`cvv`, `ssn`, `authorization`, `credential`, `privatekey`, `cardNumber`,
`jwt`, `cookie`, `setCookie`, `sessionId`, `accountNumber`,
`routingNumber`, `pan`, `iban`, …).

```csharp
var options = new RenderOptions(
    MaxStringLength: 200,
    MaxArrayItems: 5,
    MaxObjectKeys: 5,
    MaxDepth: 4,
    Redaction: RedactionPolicy.OfPatterns(["ssn", "pan"]));  // lista de denegación propia
```

- `RedactionPolicy.Default` — la lista de denegación integrada.
- `RedactionPolicy.OfPatterns(...)` — tus propios patrones de subcadena,
  insensibles a mayúsculas.
- `RedactionPolicy.Disabled` — desactivarla (los valores se renderizan tal
  cual, y también desactiva el enmascarado por forma de valor descrito
  más abajo).

La coincidencia es por subcadena para todos los patrones **excepto `pan`
e `iban`**, que se comparan por límites de token del identificador — como
subcadena, `pan` también ocultaría `companyName`, `planId` y `spanCount`,
e `iban` capturaría campos de negocio comunes de la misma forma.
`cardPan` e `ibanNumber` siguen coincidiendo (el patrón es un token
completo dentro del nombre); `companyName` no.

**El enmascarado por forma de valor es un segundo eje, independiente del
nombre.** Sin importar cómo se llame un campo (o si tiene nombre — un
valor de retorno directo, un elemento de una lista), una cadena se oculta
cuando su estructura corresponde a un JWT (tres segmentos base64url, el
primero comenzando con `eyJ`), un número de tarjeta de pago válido según
Luhn (13–19 dígitos, se toleran separadores `[ -]`), o un valor de
cabecera HTTP `Set-Cookie` (un par `nombre=valor` seguido de un atributo
con nombre como `Path=` o `Secure`). Es deliberadamente estricto — sin
heurísticas de entropía — así que un identificador numérico ordinario que
no pasa la verificación de Luhn permanece visible. `RedactionPolicy.OfPatterns(...)`
mantiene activo el enmascarado por forma de valor (una lista de nombres
personalizada no es una opinión sobre si estas formas de bytes son una
credencial); solo `RedactionPolicy.Disabled` lo desactiva.

Para **parámetros** sensibles, prefiere el atributo
[`[NotTraced]`](guia-de-atributos.md#nottraced) — oculta por posición, sin
depender del nombre.

## 7. Puente de logging (`Microsoft.Extensions.Logging`)

Encamina los eventos de traza a través de tu logging existente con el
paquete `NarrativeTrace.Logging`. `LoggingNarrativeContext` decora
cualquier contexto y loguea cada entrada/retorno/excepción vía un
`ILogger`:

```csharp
using NarrativeTrace.Logging;

var inner = new SyncNarrativeContext(new NarrativeTraceConfig());
INarrativeContext context = new LoggingNarrativeContext(inner, logger);
```

Para exportar un árbol completado una sola vez, usa `TraceLogExporter`:

```csharp
TraceLogExporter.ExportToLogger(context.CaptureTrace(), logger);
```

En una aplicación con host, cablea el puente sobre el **flujo de eventos**
mediante DI en lugar de construirlo a mano:

```csharp
services.AddNarrativeLogging();   // llámalo al final, igual que AddNarrativeTracing
```

Registra `LoggingTraceEventListener` como singleton construido a partir del
`ILoggerFactory` registrado, y lo engancha a cualquier flujo de eventos
`IEventSubscribable` registrado, de modo que el flujo llega ya narrando. Dos
condiciones lo silencian, ambas deliberadamente sin lanzar excepción: que no
haya ningún `ILoggerFactory` registrado (el puente no tendría dónde escribir),
o que `NARRATIVETRACE_NARRATION=off` vete la narración — la forma .NET del
`narrativetrace.narration=off` de Java. `off` es el único valor que veta, así
que una errata te deja narrando en lugar de silenciado sin avisar.

El código bien estructurado — métodos pequeños con nombres claros, valores
calculados que se devuelven en lugar de loguearse — apenas necesita
llamadas manuales de log; NarrativeTrace captura la historia a partir de
las firmas y los valores de retorno. Mezcla llamadas a `ILogger` solo para
decisiones que no afloran en las fronteras de los métodos, y elimínalas a
medida que refactorizas.

## 8. TracingLevel frente al nivel de logging

Son dos filtros independientes. **TracingLevel** controla qué se *registra*
en el árbol de trazas (y por tanto el coste de la captura). El **nivel de
logging** controla qué *emite* un puente de logging. Una llamada filtrada
por TracingLevel nunca llega al árbol, ni a los renderizadores, ni a ningún
logger.

| Objetivo | Ajusta |
|---|---|
| Reducir el ruido de logs | Sube el nivel del logger (el árbol se sigue capturando para archivos/renderizadores). |
| Reducir el tamaño de la traza | Baja el `TracingLevel` (`Narrative` → `Summary`). |
| Reducir CPU/memoria | Baja el `TracingLevel` — el nivel de logging no afecta al coste de captura. |

## 9. Valores por defecto recomendados según el entorno

| Entorno | Nivel | Salida |
|---|---|---|
| Trabajo local en una funcionalidad | `Detail` | `NARRATIVETRACE_OUTPUT=true`, `FORMAT=Markdown` |
| Ejecuciones de pruebas en CI | `Narrative` o `Summary` | `OUTPUT=true`, `FORMAT=Markdown` |
| Producción sensible al rendimiento | `Errors` (u `Off`) | sin salida a archivo |

## Véase también

- [Guía de instalación](guia-de-instalacion.md) — paquetes y vías de integración
- [Guía de atributos](guia-de-atributos.md) — atributos de ocultación y narración
- [Guía de integración con ASP.NET Core](guia-de-integracion-con-aspnet-core.md) — middleware y exportadores
- [Guía de claridad](guia-de-claridad.md) — la puerta `Clarity*` de MSBuild/CLI
