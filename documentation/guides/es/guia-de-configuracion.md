<!-- source: documentation/guides/configuration.md blob 559edb2058e0 | translated: 2026-09-13 | reviewed: - -->
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

### Siembra de traceparent

Siembra un [`traceparent`](https://www.w3.org/TR/trace-context/#traceparent-header)
W3C inicial *(since 0.1.4)* para que cada contexto construido a
partir de una configuración continúe la traza de quien llama en lugar de
iniciar la suya propia — el equivalente sin cabecera HTTP de lo que
`NarrativeTraceMiddleware` adopta de una petición entrante (§4 más abajo):

```csharp
var config = new NarrativeTraceConfig(
    initialTraceparent: Traceparent.Parse("00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01"));
var context = new SyncNarrativeContext(config);
```

`Traceparent.Parse` nunca lanza excepción — un valor malformado o ausente
devuelve `null`, que `NarrativeTraceConfig` trata como "sin semilla", así
que un contexto construido a partir de él recae en una traza fresca,
generada aleatoriamente. Fijo en el momento de construcción como
`ServiceIdentity`: no hay setter, y volver a sembrar un contexto en
ejecución necesita `context.AdoptTraceparent(...)` directamente en su
lugar.

Déjalo sin establecer para tráfico de producción — cada contexto
construido a partir de una `NarrativeTraceConfig` compartida adopta el
mismo valor fijo, lo cual es correcto para un único contexto de nivel
superior (una demo, un script de un solo uso) pero anula todo el propósito
de `AsyncNarrativeContext` de dar a cada scope su propia traza distinta.
No combines ambos.

## 2. Variables de entorno (`ConfigResolver`)

`ConfigResolver` lee seis variables — el canal de sobrescritura nativo de
`.NET` que mantiene `Core` libre de dependencias. Los valores inválidos
**degradan al valor por defecto en lugar de lanzar una excepción**, así que
una configuración incorrecta nunca rompe la captura.

| Variable | Valores | Por defecto |
|---|---|---|
| `NARRATIVETRACE_LEVEL` | `Off`, `Errors`, `Summary`, `Narrative`, `Detail` | `Detail` |
| `NARRATIVETRACE_OUTPUT` | `true` / `false` (o `1` / `0`) | `true` |
| `NARRATIVETRACE_OUTPUT_DIR` | cualquier ruta con permisos de escritura | `TestResults/narrativetrace` |
| `NARRATIVETRACE_FORMAT` | `Markdown`, `Text`, `Prose`, `Json` | `Markdown` |
| `NARRATIVETRACE_CANONICAL_JSON` | `true` / `false` (o `1`) | `false` |
| `NARRATIVETRACE_STRUCTURAL_JSON` | `true` / `false` (o `1`) | `false` |
| `NARRATIVETRACE_APPROVAL` | `true` / `false` (o `1`) | `false` |
| `NARRATIVETRACE_APPROVED_DIR` | cualquier ruta con permiso de escritura | `narratives` |

El parseo del nivel y del formato es tolerante (insensible a mayúsculas y
puntuación: `detail`, `DETAIL` y `Detail` resuelven todos).

`NARRATIVETRACE_OUTPUT` está **activado por defecto** (decisión del
responsable, 2026-09-11) *(since 0.1.4)*: los artefactos por prueba que escriben el fixture
de xUnit y la base de NUnit son la recompensa de adoptar esta biblioteca,
así que la escritura ocurre sin ninguna opción. Solo un
`NARRATIVETRACE_OUTPUT=false` explícito (o `0`) lo desactiva; `true`/`1` se
aceptan como no-op para los scripts que aún lo definen explícitamente. Sin
una anulación de `NARRATIVETRACE_OUTPUT_DIR`, los archivos caen bajo
`TestResults/narrativetrace/` — la convención de `.NET` que `dotnet test
--results-directory` y Visual Studio/Rider ya tratan como desechable, y que
el propio `.gitignore` de este repositorio ya excluye.

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

`NARRATIVETRACE_APPROVAL` activa el modo de aprobación *(since 0.1.4)*: después de una prueba que **pasa**, la estructura sin
valores del escenario (el mismo render que el artefacto `.nt`) se verifica
contra la traza aprobada con commit
`<approvedDir>/<TestClassSimpleName>/<artifact_name>.approved.nt` — la
misma identidad de artefacto que cualquier otro archivo por prueba, así
que un método que se ejecuta más de una vez tiene una traza aprobada por
invocación. Una traza aprobada ausente o una diferencia estructural hace
fallar la prueba con un diff legible y escribe la estructura actual junto
a ella como `*.received.nt`; revísala y acéptala mediante el build target
`Approve` (`./build.sh Approve`) o renómbrala a mano. La estructura de una
prueba que falla nunca se verifica — está a mitad de camino y no debe
agitar las trazas recibidas. `NARRATIVETRACE_APPROVED_DIR` nombra el
directorio donde viven esas líneas base (por defecto `narratives`).
Consulta [Formato de traza estructural](../../structural-trace-format.md)
para el comportamiento completo, y [Qué incluir en el commit](../../es/que-incluir-en-el-commit.md)
para saber cuáles de estos archivos hacer commit.

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

`NarrativeTraceMiddleware` adopta automáticamente una cabecera de petición
`traceparent` entrante *(since 0.1.4)* — sin opción para
desactivarlo; una cabecera ausente, malformada o con versión prohibida se
ignora y la petición obtiene una traza recién generada, igual que la vía
sembrada por configuración de arriba pero impulsada por la cabecera de
quien llama en lugar de un valor fijo.

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

**Conectar una política personalizada a la vía del proxy.** El
`RenderOptions` de arriba es lo que toma `ValueRenderer.Render` cuando lo
llamas tú mismo; llegar a la vía de captura *distribuida* de
`NarrativeTraceProxy` es un paso aparte, mediante `ProxyOptions.Redaction`
*(since 0.1.4)*:

```csharp
var proxy = NarrativeTraceProxy.Create<IOrderService>(
    new OrderService(), context,
    new ProxyOptions(Redaction: RedactionPolicy.OfPatterns(["ssn", "holderName"])));
```

Dada explícitamente, la política *reemplaza* la decisión por defecto
basada en nombre — tanto para el nombre propio de un parámetro del proxy
como para los nombres de propiedad reflejados de un objeto anidado — en
lugar de ampliarla, así que `RedactionPolicy.Disabled` aquí realmente
desactiva la ocultación basada en nombre de principio a fin (`[NotTraced]`
sigue ocultando de todos modos). Deja `Redaction` sin definir (el valor
por defecto) y un proxy se comporta exactamente como antes.

La auto-envoltura de DI y la integración de ASP.NET Core exponen el mismo
gancho *(since 0.1.4)*: `NarrativeTracingDiOptions.Redaction`
en [`AddNarrativeTracing`](guia-de-inyeccion-de-dependencias.md) alcanza
cada servicio que esa llamada envuelve, y `NarrativeTraceOptions.Redaction`
en [`AddNarrativeTrace`](guia-de-integracion-con-aspnet-core.md) también
alcanza los proxies auto-envueltos cuando ambos están registrados en la
misma aplicación — una política configurada en cualquiera de los dos lados
es visible para el otro, ya que los dos paquetes comparten una única
colección de servicios. El propio `Redaction` de `AddNarrativeTracing`,
cuando está definido, tiene prioridad para los servicios que envuelve.
Consulta [Privacidad y ocultación](../../es/privacidad-y-ocultacion.md)
para el cuadro completo, superficie por superficie.

**Ampliar todas las superficies a la vez, sin tocar el código.**
`NARRATIVETRACE_REDACTION_ADDITIONALPATTERNS` es una lista de patrones de
nombre de campo separados por comas
que se une a `RedactionPolicy.Default` en sí:

```bash
NARRATIVETRACE_REDACTION_ADDITIONALPATTERNS=holderName,betalingskort
```

A diferencia de `ProxyOptions.Redaction`, esto solo puede añadir patrones,
nunca quitar los integrados, y alcanza toda superficie de la tabla de
arriba — incluidas la auto-envoltura de DI y el middleware de ASP.NET
Core, que no tienen ningún gancho por llamada. Se lee una sola vez, en un
campo `static readonly`, así que debe fijarse antes de que algo en el
proceso toque `RedactionPolicy` por primera vez (el propio código de
arranque de una app, o una prueba que lo fija en tiempo de ejecución,
llegan demasiado tarde).

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

**Una nota sobre el orden con el logger de consola de serie.** Si el
`logger` de arriba está respaldado por `Microsoft.Extensions.Logging.Console`,
sus líneas se escriben a través de una cola en segundo plano por defecto,
así que pueden aparecer después, o entremezcladas de forma extraña con, una
salida que tu proceso escribe de forma síncrona (`Console.Write*`, otro
proveedor, un test runner capturando stdout) — un comportamiento de la
plataforma propio del proveedor de consola, no un defecto de NarrativeTrace.
El arreglo que realmente funciona: libera (`Dispose`) el `ILoggerFactory` (o
el proveedor de consola) — lo más simple con `using var loggerFactory =
LoggerFactory.Create(...)` — antes de que algo dependa del orden;
`Dispose()` bloquea hasta que el hilo escritor en segundo plano del
proveedor haya vaciado todo lo que tenía en cola. Consulta el paso
["Envíalo a tu logger" del tutorial de sesenta segundos](../../es/sesenta-segundos.md#envíalo-a-tu-logger)
para un ejemplo verificado y trabajado.

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

**Una ejecución de la suite de pruebas también tiene nombre**
*(desde 0.1.4, sin publicar)*: mientras `NarrativeTrace.Testing.Xunit` o
`NarrativeTrace.Testing.NUnit` tenga una ejecución de suite activa, ambos
puentes anteriores añaden además `nt.runName` al scope — la frase de tres
palabras propia de la ejecución, junto a `nt.traceId`/`nt.traceName`, de
modo que un solo grep encuentra las líneas de log de una ejecución. Está
ausente por completo fuera de una ejecución rastreada. La misma identidad
también nombra el pie de página de consola de la suite (`run: bold elk
soars`) y el objeto `run` de nivel superior de `manifest.json`, y precede
al frontmatter del documento Markdown de la traza (`run:`) y a la línea de
apertura de los renderizadores de texto/prosa — nunca al artefacto
estructural `.nt`, sin valores (ver
[Formato de Traza Estructural](../../structural-trace-format.md)).

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
| Trabajo local en una funcionalidad | `Detail` | activada por defecto, `FORMAT=Markdown` |
| Ejecuciones de pruebas en CI | `Narrative` o `Summary` | activada por defecto, `FORMAT=Markdown` |
| Producción sensible al rendimiento | `Errors` (u `Off`) | ningún fixture de prueba se ejecuta aquí — sin salida a archivo |

## 10. Ejemplos guiados de variables de entorno

Todas las variables `NARRATIVETRACE_*` que lee el código, en un solo
lugar — qué configura cada una, su valor por defecto y una demostración
ejecutable del efecto. Las variables de `ConfigResolver` se presentan en
el §2 anterior; el resto se presenta donde vive (la ocultación en el §6,
el puente de logging en el §7); esta sección es el acompañante con
ejemplos guiados de todas ellas.

| Variable | Configura | Por defecto |
|---|---|---|
| `NARRATIVETRACE_LEVEL` | El `TracingLevel` con el que captura un contexto. | `Detail` |
| `NARRATIVETRACE_OUTPUT` | Si se escriben en disco los artefactos de traza por prueba. | `true` |
| `NARRATIVETRACE_OUTPUT_DIR` | El directorio bajo el que se escriben los artefactos de traza. | `TestResults/narrativetrace` |
| `NARRATIVETRACE_FORMAT` | El formato principal del artefacto (`Markdown`/`Text`/`Prose`/`Json`). | `Markdown` |
| `NARRATIVETRACE_CANONICAL_JSON` | Si además se escribe el array de entradas canónico por prueba. | `false` |
| `NARRATIVETRACE_STRUCTURAL_JSON` | Si además se escribe el array de entradas sin valores por prueba. | `false` |
| `NARRATIVETRACE_APPROVAL` | Si la estructura de una prueba que pasa se compara con su línea base `*.approved.nt` comprometida. | `false` |
| `NARRATIVETRACE_APPROVED_DIR` | El directorio donde viven las líneas base de aprobación. | `narratives` |
| `NARRATIVETRACE_REDACTION_ADDITIONALPATTERNS` | Patrones de nombre de campo, separados por comas, unidos a `RedactionPolicy.Default`. | *(ninguno)* |
| `NARRATIVETRACE_NARRATION` | Veta el puente de logging cuando vale `off`; cualquier otro valor lo deja activo. | *(sin definir — narrando)* |
| `NARRATIVETRACE_GLOSSARY` | Una ruta de archivo de glosario explícita para la recolección de la suite, o `off` para deshabilitarla del todo. | Búsqueda ascendente desde la ejecución de pruebas de `glossary.json` |
| `NARRATIVETRACE_GLOSSARY_PATH` | Un archivo de glosario a cargar para la traducción en vivo, que sobreescribe el archivo junto a la app desplegada. | El `glossary.json` junto a la app, si existe |

Cada ejemplo guiado de abajo se ejecuta a través de la misma sobrecarga de
lector inyectado que `ConfigResolver`/`GlossarySettings`/`GlossaryLoader`/
`AddNarrativeLogging` ya exponen para las pruebas (`Resolve(Func<string,
string?> read, …)` y afines) — el mecanismo que permite que la
demostración fije exactamente una variable sin tocar el entorno real del
proceso. En tu propia shell, fija la variable de verdad; el efecto
observable es idéntico en ambos casos.

### `NARRATIVETRACE_LEVEL`

```bash
export NARRATIVETRACE_LEVEL=Narrative
```

```csharp
var resolved = ConfigResolver.Resolve(key => key == "NARRATIVETRACE_LEVEL" ? "Narrative" : null);
Console.WriteLine($"resolved.Level == TracingLevel.{resolved.Level}");
```

Efecto observable — el nivel resuelto refleja la variable, interpretada de forma tolerante:

```text
resolved.Level == TracingLevel.Narrative
```

### `NARRATIVETRACE_OUTPUT`

```bash
export NARRATIVETRACE_OUTPUT=false
```

```csharp
var resolved = ConfigResolver.Resolve(key => key == "NARRATIVETRACE_OUTPUT" ? "false" : null);
Console.WriteLine($"resolved.Output == {resolved.Output}");
```

Efecto observable — solo un `false`/`0` explícito desactiva la escritura (el §2 anterior detalla toda la tolerancia):

```text
resolved.Output == False
```

### `NARRATIVETRACE_OUTPUT_DIR`

```bash
export NARRATIVETRACE_OUTPUT_DIR=artifacts/traces
```

```csharp
var resolved = ConfigResolver.Resolve(key => key == "NARRATIVETRACE_OUTPUT_DIR" ? "artifacts/traces" : null);
Console.WriteLine($"resolved.OutputDir == \"{resolved.OutputDir}\"");
```

Efecto observable — la ruta configurada pasa tal cual (recortada; en blanco se trata como no definida):

```text
resolved.OutputDir == "artifacts/traces"
```

### `NARRATIVETRACE_FORMAT`

```bash
export NARRATIVETRACE_FORMAT=Json
```

```csharp
var resolved = ConfigResolver.Resolve(key => key == "NARRATIVETRACE_FORMAT" ? "Json" : null);
Console.WriteLine($"resolved.Format == OutputFormat.{resolved.Format}");
```

Efecto observable — el formato principal del artefacto cambia respecto al valor por defecto `Markdown`:

```text
resolved.Format == OutputFormat.Json
```

### `NARRATIVETRACE_CANONICAL_JSON`

```bash
export NARRATIVETRACE_CANONICAL_JSON=true
```

```csharp
var resolved = ConfigResolver.Resolve(key => key == "NARRATIVETRACE_CANONICAL_JSON" ? "true" : null);
Console.WriteLine($"resolved.CanonicalJson == {resolved.CanonicalJson}");
```

Efecto observable — el artefacto máquina por prueba `<test>.canonical.json` empieza a escribirse junto al formato principal:

```text
resolved.CanonicalJson == True
```

### `NARRATIVETRACE_STRUCTURAL_JSON`

```bash
export NARRATIVETRACE_STRUCTURAL_JSON=true
```

```csharp
var resolved = ConfigResolver.Resolve(key => key == "NARRATIVETRACE_STRUCTURAL_JSON" ? "true" : null);
Console.WriteLine($"resolved.StructuralJson == {resolved.StructuralJson}");
```

Efecto observable — el array sin valores `<test>.structural.json` por prueba empieza a escribirse:

```text
resolved.StructuralJson == True
```

### `NARRATIVETRACE_APPROVAL`

```bash
export NARRATIVETRACE_APPROVAL=true
```

```csharp
var resolved = ConfigResolver.Resolve(key => key == "NARRATIVETRACE_APPROVAL" ? "true" : null);
Console.WriteLine($"resolved.Approval == {resolved.Approval}");
```

Efecto observable — la estructura de una prueba que pasa ahora se compara con su línea base comprometida (véase [Formato de Traza Estructural](../../structural-trace-format.md)):

```text
resolved.Approval == True
```

### `NARRATIVETRACE_APPROVED_DIR`

```bash
export NARRATIVETRACE_APPROVED_DIR=baselines
```

```csharp
var resolved = ConfigResolver.Resolve(key => key == "NARRATIVETRACE_APPROVED_DIR" ? "baselines" : null);
Console.WriteLine($"resolved.ApprovedDir == \"{resolved.ApprovedDir}\"");
```

Efecto observable — las líneas base de aprobación ahora se leen y escriben en `baselines/` en lugar del valor por defecto `narratives`:

```text
resolved.ApprovedDir == "baselines"
```

### `NARRATIVETRACE_REDACTION_ADDITIONALPATTERNS`

A diferencia de las variables anteriores, aquí no hay código que cambiar
en el punto de llamada — `RedactionPolicy.Default` lee esta variable ella
misma, una sola vez, en un campo `static readonly` (§6 anterior). Eso
significa que la demostración tiene que ejecutarse en su **propio
proceso**, arrancado con la variable ya fijada, en lugar de a través del
mecanismo de lector inyectado que usan los demás ejemplos — así que este
se verifica a mano en lugar de comprobarse contra un archivo fuente
comprometido, según la misma regla 8 que respalda cada otro ejemplo de
esta página (una ejecución real y compilada, no una suposición escrita a
mano):

```bash
export NARRATIVETRACE_REDACTION_ADDITIONALPATTERNS=holderName,betalingskort
```

```csharp
using NarrativeTrace.Core;

Console.WriteLine(RedactionPolicy.Default.ShouldRedact("holderName"));
Console.WriteLine(RedactionPolicy.Default.ShouldRedact("password"));
```

Efecto observable — ejecuta una vez con la variable sin definir y otra
con ella fijada (`dotnet run` dos veces, en dos invocaciones de proceso
separadas):

```text
# sin definir
False
True

# NARRATIVETRACE_REDACTION_ADDITIONALPATTERNS=holderName,betalingskort
True
True
```

`password` se oculta en ambos casos (ya está en la lista de bloqueo
integrada); `holderName` solo se oculta una vez que la variable la
amplía. Fijar la variable *después* de que algo en el proceso ya haya
tocado `RedactionPolicy` no tiene efecto — véase el §6 anterior.

### `NARRATIVETRACE_NARRATION`

```bash
export NARRATIVETRACE_NARRATION=off
```

```csharp
var services = new ServiceCollection();
services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);

services.AddNarrativeLogging(null, key => key == "NARRATIVETRACE_NARRATION" ? "off" : null);

var listener = services.BuildServiceProvider().GetService<LoggingTraceEventListener>();
Console.WriteLine($"listener is null == {listener is null}");
```

Efecto observable — `off` (sin distinguir mayúsculas) veta el registro
por completo, incluso con un `ILoggerFactory` presente; cualquier otro
valor, incluida una errata, deja la narración activa:

```text
listener is null == True
```

### `NARRATIVETRACE_GLOSSARY`

```bash
export NARRATIVETRACE_GLOSSARY=off
```

```csharp
var resolved = GlossarySettings.ResolveFile(
    key => key == "NARRATIVETRACE_GLOSSARY" ? "off" : null, root);
Console.WriteLine($"resolved == {(resolved is null ? "null (harvesting disabled)" : resolved)}");
```

(`root` arriba es el directorio desde el que empezaría la búsqueda
ascendente — en la práctica, el directorio de trabajo de una ejecución de
pruebas.) Efecto observable — el valor literal `off` deshabilita la
recolección por completo, anulando la búsqueda ascendente de
`glossary.json` incluso cuando de otro modo se encontraría un archivo:

```text
resolved == null (harvesting disabled)
```

### `NARRATIVETRACE_GLOSSARY_PATH`

```bash
export NARRATIVETRACE_GLOSSARY_PATH=/srv/app/committed-glossary.json
```

```csharp
var loaded = GlossaryLoader.Load(
    key => key == "NARRATIVETRACE_GLOSSARY_PATH" ? overridePath : null, root);
Console.WriteLine($"loaded from override == {loaded is not null}");
```

(`overridePath` arriba es el archivo que nombra la variable; `root` es el
directorio base de la aplicación desplegada.) Efecto observable — la
sobreescritura gana sobre cualquier `glossary.json` situado junto a la
aplicación desplegada:

```text
loaded from override == True
```

## Véase también

- [Guía de instalación](guia-de-instalacion.md) — paquetes y vías de integración
- [Guía de atributos](guia-de-atributos.md) — atributos de ocultación y narración
- [Guía de integración con ASP.NET Core](guia-de-integracion-con-aspnet-core.md) — middleware y exportadores
- [Guía de claridad](guia-de-claridad.md) — la puerta `Clarity*` de MSBuild/CLI
