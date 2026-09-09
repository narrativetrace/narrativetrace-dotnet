<!-- source: documentation/guides/msbuild-cli.md blob 212975dfb472 | translated: 2026-09-09 | reviewed: 2026-09-03 -->
# NarrativeTrace .NET — Guía de MSBuild y CLI

[English](../msbuild-cli.md) | **Español** | [Português](../pt-BR/guia-de-msbuild-e-cli.md) | [简体中文](../zh-CN/MSBuild与CLI指南.md)

NarrativeTrace incluye dos superficies de integración con el build: la
herramienta de línea de comandos `dotnet-narrativetrace` y el paquete
`NarrativeTrace.MSBuild` que la envuelve. Juntas convierten la claridad de
los nombres en una puerta del build y reenvían la configuración de tracing
al host de pruebas — el análogo en `.NET` del plugin de Gradle en el lado
JVM.

La división del trabajo es deliberada: **todas las decisiones viven en la
CLI**; el paquete de MSBuild es una capa fina que solo declara valores por
defecto e invoca la herramienta. Aprende primero la CLI y el cableado de
MSBuild se deducirá solo.

## Instalar la herramienta

```bash
# Herramienta global:
dotnet tool install --global NarrativeTrace.Cli

# …o añádela a un manifiesto local (recomendado para reproducibilidad en CI):
dotnet new tool-manifest
dotnet tool install NarrativeTrace.Cli
```

Los targets de MSBuild invocan la herramienta por nombre
(`dotnet-narrativetrace`), así que debe estar en el `PATH` (global) o
restaurada desde un manifiesto local antes de ejecutar `ClarityScan` /
`ClarityCheck`.

## Verbos de la CLI

`dotnet-narrativetrace <verbo> [opciones]`. Sin verbo — o con uno
desconocido — la herramienta imprime el uso y sale con `2`. Las opciones se
parsean como pares `--nombre valor` (los flags como `--warn-only` no llevan
valor).

### `clarity-scan`

Escaneo solo por reflexión de un ensamblado compilado hacia un informe de
claridad. Carga el ensamblado en un contexto de solo metadatos y nunca lo
ejecuta, así que es seguro en CI.

```bash
dotnet-narrativetrace clarity-scan \
    --assembly bin/Release/net10.0/MyApp.dll \
    --output-dir narrativetrace \
    --format both
```

| Opción | Obligatoria | Por defecto | Significado |
|---|---|---|---|
| `--assembly <ruta>` | sí | — | Ensamblado a escanear. |
| `--output-dir <dir>` | no | `.` | Directorio donde escribir los informes (se crea si no existe). |
| `--format <both\|md\|json>` | no | `both` | `json` escribe `clarity-results.json`; `md` escribe `clarity-report.md`; `both` escribe ambos. |

Códigos de salida:

| Código | Cuándo |
|---|---|
| `0` | El escaneo terminó y se escribieron los informes. |
| `1` | No se encontró el archivo del ensamblado. |
| `2` | Falta `--assembly`, o el valor de `--format` es desconocido. |

### `clarity-aggregate`

Fusiona en un único envoltorio `clarity-results.json` los artefactos
`*.clarity.json` por prueba (escritos en runtime por las integraciones con
los frameworks de pruebas) que haya bajo un directorio. Úsalo cuando
quieras que la puerta puntúe **trazas realmente capturadas** — profundidad
de llamadas y anidamiento reales — en lugar del escaneo estático por
reflexión de profundidad 1.

```bash
dotnet-narrativetrace clarity-aggregate \
    --input-dir narrativetrace \
    --output-dir narrativetrace
```

| Opción | Obligatoria | Por defecto | Significado |
|---|---|---|---|
| `--input-dir <dir>` | sí | — | Directorio donde se buscan archivos `*.clarity.json`. |
| `--output-dir <dir>` | no | el valor de `--input-dir` | Dónde se escribe `clarity-results.json`. |

Códigos de salida:

| Código | Cuándo |
|---|---|
| `0` | La agregación terminó (aunque no coincidiera ningún archivo). |
| `1` | No se encontró el directorio de entrada. |
| `2` | Falta `--input-dir`. |

### `clarity-check`

La puerta. Parsea un envoltorio `clarity-results.json` y falla cuando algún
escenario puntúa por debajo de `--min-score` o tiene más de
`--max-high-issues` incidencias de severidad HIGH (contadas sin distinguir
mayúsculas).

```bash
dotnet-narrativetrace clarity-check \
    --results narrativetrace/clarity-results.json \
    --min-score 0.80 \
    --max-high-issues 0
```

| Opción | Obligatoria | Por defecto | Significado |
|---|---|---|---|
| `--results <ruta>` | sí | — | Ruta al envoltorio `clarity-results.json`. |
| `--min-score <x>` | no | `0.0` | Falla cualquier escenario que puntúe por debajo de este valor global. |
| `--max-high-issues <n>` | no | `2147483647` (`int.MaxValue`) | Falla cualquier escenario con más incidencias HIGH que este número. |
| `--warn-only` | no | desactivado | Degrada el fallo de la puerta a una advertencia (salida `0`). |

Códigos de salida:

| Código | Cuándo |
|---|---|
| `0` | La puerta pasó, **o** se indicó `--warn-only`, **o** faltaba el archivo de resultados (un archivo ausente se omite, no falla — un proyecto que aún no ha escaneado no rompe CI). |
| `1` | La puerta falló (un escenario está por debajo de `--min-score` o por encima de `--max-high-issues`). |
| `2` | Falta `--results`, o el archivo de resultados es JSON mal formado. |

`--min-score` y `--max-high-issues` se parsean con numéricos de cultura
invariante; un valor no parseable recurre a su valor por defecto en lugar
de dar error.

## Integración con MSBuild

Añade el paquete solo de build. `PrivateAssets="all"` lo mantiene fuera de
las dependencias transitivas de tu paquete:

```xml
<PackageReference Include="NarrativeTrace.MSBuild" Version="0.1.1"
                  PrivateAssets="all" />
```

### Propiedades

Sobrescribe cualquiera de estas en el proyecto consumidor o en la línea de
comandos (`/p:Nombre=Valor`). El paquete solo declara valores por defecto —
toda la lógica de umbrales vive en la CLI.

| Propiedad | Por defecto | Propósito |
|---|---|---|
| `NarrativeTraceOutput` | `false` | Con `true`, reenvía `NARRATIVETRACE_*` al host de pruebas durante `VSTest`. |
| `NarrativeTraceOutputDir` | `$(MSBuildProjectDirectory)/narrativetrace` | Dónde se escriben los resultados de escaneo/agregación y las trazas. |
| `NarrativeTraceFormat` | `markdown` | Formato de salida de trazas. Válidos: `markdown`, `text`, `mermaid`, `plantuml`. |
| `NarrativeTraceLevel` | `DETAIL` | Nivel de captura del host de pruebas. Válidos: `OFF`, `ERRORS`, `SUMMARY`, `NARRATIVE`, `DETAIL`. |
| `NarrativeTraceClaritySource` | `scan` | Qué productor puntúa la puerta: `scan` (escaneo estático por reflexión) o `runtime` (agregación de las trazas capturadas por prueba). |
| `ClarityMinScore` | `0.0` | Se reenvía a `clarity-check --min-score`. |
| `ClarityMaxHighIssues` | `2147483647` | Se reenvía a `clarity-check --max-high-issues`. |
| `ClarityWarnOnly` | `false` | Con `true`, reenvía `--warn-only` (los fallos de la puerta pasan a ser advertencias). |

Los valores inválidos de `NarrativeTraceLevel`, `NarrativeTraceFormat` o
`NarrativeTraceClaritySource` fallan el build **pronto** (vía el target
`_NarrativeTraceValidateConfig`, antes de `Build` / `VSTest` /
`ClarityScan`) con un mensaje claro, en lugar de reenviar una errata en
silencio al host de pruebas.

> La lista de valores permitidos de `NarrativeTraceFormat` aquí
> (`markdown`/`text`/`mermaid`/`plantuml`) es el formato de renderizado de
> trazas del lado del build, y difiere de los valores de la variable de
> entorno `NARRATIVETRACE_FORMAT` en runtime
> (`Markdown`/`Text`/`Prose`/`Json`) documentados en la
> [Guía de configuración](guia-de-configuracion.md).

### Targets

| Target | Depende de | Qué ejecuta |
|---|---|---|
| `ClarityScan` | `Build` | `clarity-scan --assembly $(TargetPath) --output-dir $(NarrativeTraceOutputDir)` |
| `ClarityAggregate` | — | `clarity-aggregate --input-dir $(NarrativeTraceOutputDir) --output-dir $(NarrativeTraceOutputDir)` |
| `ClarityCheck` | `ClarityScan` o `ClarityAggregate` (según `NarrativeTraceClaritySource`) | `clarity-check --results … --min-score … --max-high-issues … [--warn-only]` |

Ejecuta la puerta como parte de un build:

```bash
# Productor de escaneo estático (por defecto):
dotnet build /t:ClarityCheck /p:ClarityMinScore=0.80 /p:ClarityMaxHighIssues=0

# Puntuar trazas realmente capturadas en lugar del escaneo estático:
dotnet test   /p:NarrativeTraceOutput=true
dotnet build  /t:ClarityCheck /p:NarrativeTraceClaritySource=runtime /p:ClarityMinScore=0.80
```

Con `NarrativeTraceClaritySource=runtime`, `ClarityCheck` depende de
`ClarityAggregate` (que fusiona los archivos `*.clarity.json` por prueba)
en lugar de `ClarityScan`. Produce esos archivos por prueba ejecutando
antes la suite con `NarrativeTraceOutput=true`, para que la agregación
tenga algo que fusionar.

### Comprobación incremental (archivo stamp)

`ClarityCheck` es incremental. Declara:

- **Entradas:** `$(NarrativeTraceOutputDir)/clarity-results.json`
- **Salidas:** `$(NarrativeTraceOutputDir)/clarity-check.stamp`

Tras una comprobación correcta toca el archivo `.stamp`. En el siguiente
build, MSBuild compara marcas de tiempo y **omite por completo la nueva
comprobación cuando `clarity-results.json` no ha cambiado** — así un
proyecto sin cambios no paga la puerta dos veces. Borra el stamp (o el
directorio de salida) para forzar una nueva comprobación.

### Salida de trazas hacia el host de pruebas

Con `NarrativeTraceOutput=true`, el target `_NarrativeTraceExportEnv`
(ejecutado antes de `VSTest`) añade la configuración de runtime a
`VSTestEnvironmentVariables`, de modo que el host de pruebas ve:

```
NARRATIVETRACE_OUTPUT=true
NARRATIVETRACE_OUTPUT_DIR=$(NarrativeTraceOutputDir)
NARRATIVETRACE_FORMAT=$(NarrativeTraceFormat)
NARRATIVETRACE_LEVEL=$(NarrativeTraceLevel)
```

```bash
dotnet test /p:NarrativeTraceOutput=true /p:NarrativeTraceLevel=NARRATIVE
```

## Recetas para CI

### Puerta de claridad estática (sin necesidad de ejecutar pruebas)

Escanea el ensamblado compilado y falla por debajo de un umbral — la
puerta más ligera.

```bash
dotnet build -c Release
dotnet-narrativetrace clarity-scan \
    --assembly bin/Release/net10.0/MyApp.dll --output-dir narrativetrace
dotnet-narrativetrace clarity-check \
    --results narrativetrace/clarity-results.json \
    --min-score 0.80 --max-high-issues 0
```

O dejando que MSBuild dirija toda la cadena en un solo comando:

```bash
dotnet build /t:ClarityCheck /p:ClarityMinScore=0.80 /p:ClarityMaxHighIssues=0
```

### Puerta de claridad en runtime (puntuar trazas reales)

Ejecuta las pruebas para emitir capturas por prueba y luego aplica la
puerta sobre la agregación:

```bash
dotnet test /p:NarrativeTraceOutput=true
dotnet build /t:ClarityCheck \
    /p:NarrativeTraceClaritySource=runtime \
    /p:ClarityMinScore=0.80 /p:ClarityMaxHighIssues=0
```

### Subir el listón sin romper el build

Reporta la claridad como advertencia mientras subes la puntuación, y luego
desactiva `ClarityWarnOnly` cuando superes el listón:

```bash
dotnet build /t:ClarityCheck \
    /p:ClarityMinScore=0.85 /p:ClarityWarnOnly=true
```

## Véase también

- [Guía de instalación](guia-de-instalacion.md) — paquetes y vías de integración (opciones F y G)
- [Guía de configuración](guia-de-configuracion.md) — niveles de tracing, variables de entorno y la referencia de propiedades de MSBuild
- [Guía de claridad](guia-de-claridad.md) — el modelo de puntuación, el contrato `clarity-results.json` y la semántica de la puerta
