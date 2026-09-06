<!-- source: documentation/guides/clarity.md blob 6910fbf8f013 | translated: 2026-08-30 | reviewed: 2026-09-03 -->
# NarrativeTrace .NET — Guía de claridad

[English](../clarity.md) | **Español** | [Português](../pt-BR/guia-de-clareza.md) | [简体中文](../zh-CN/清晰度指南.md)

Si la traza *es* el código, entonces la calidad de la traza es la calidad
del código. El paquete `NarrativeTrace.Clarity` analiza los nombres de tus
métodos, clases y parámetros, y puntúa lo bien que comunican la intención.

## Dos formas de puntuar

### A partir de una traza capturada (`ClarityAnalyzer`)

Analiza los nombres que realmente aparecieron en una ejecución:

```csharp
using NarrativeTrace.Clarity;

var result = ClarityAnalyzer.Analyze(context.CaptureTrace());
Console.WriteLine($"Overall clarity: {result.Overall:F2}");
```

### A partir de un ensamblado, sin ejecutar nada (`ClarityScanner`)

Solo por reflexión — inspecciona los métodos públicos de los tipos
indicados sin ejecutarlos (seguro para CI):

```csharp
IReadOnlyDictionary<string, ClarityResult> results =
    ClarityScanner.Scan([typeof(OrderService), typeof(PaymentService)]);

foreach (var (typeName, result) in results)
{
    Console.WriteLine($"{typeName}: {result.Overall:F2}");
}
```

Los resultados se indexan por `Type.Name`. Es también lo que usa la
[CLI `dotnet-narrativetrace`](#imposición-en-el-build).

## Qué se puntúa

`ClarityResult` lleva una puntuación global más cinco componentes
ponderados (todos entre `0.0` y `1.0`):

| Componente | Peso | Qué mide |
|---|---|---|
| Método (`Method`) | 30% | Calidad del verbo y especificidad de los tokens en los nombres de métodos. |
| Parámetro (`Parameter`) | 25% | Especificidad del dominio frente a tokens de parámetro genéricos o sin significado. |
| Clase (`Class`) | 20% | Calidad del sufijo de rol y especificidad del prefijo en los nombres de clases. |
| Estructural (`Structural`) | 15% | Penalizaciones por número de parámetros y profundidad de llamadas. |
| Cohesión (`Cohesion`) | 10% | Si los métodos encajan con el sufijo de rol de la clase. |

```csharp
public sealed record ClarityResult(
    double Overall, double Method, double Class,
    double Parameter, double Structural, double Cohesion,
    IReadOnlyList<ClarityIssue> Issues);
```

### La intuición de la puntuación

- **Nombres de métodos** — el primer token se trata como un verbo. Los
  verbos de *dominio* (`calculate`, `validate`, `reserve`) puntúan más
  alto; los prefijos *booleanos* (`is`, `has`, `can`) puntúan alto; los
  verbos *estándar* (`create`, `find`) quedan en medio; los verbos
  *genéricos* (`get`, `process`, `handle`, `execute`) puntúan más bajo.
  Los tokens adicionales añaden especificidad (`reserveInventory` gana a
  `reserve`).
- **Nombres de clases** — un prefijo de dominio más un sufijo funcional
  puntúa bien (`OrderService`); un sufijo a secas (`Service`) o un
  `Manager` genérico puntúa mal.
- **Nombres de parámetros** — específicos del dominio (`customerId`,
  `checkInDate`) gana a genéricos por tipo (`id`, `count`), que gana a
  vagos (`data`, `info`), que gana a los que no significan nada (`x`,
  `tmp`).
- **Estructural** — se penalizan los métodos con muchos parámetros o
  cadenas de llamadas profundas.
- **Cohesión** — los métodos se contrastan con los verbos esperados para
  el sufijo de rol de la clase (de un `Repository` se espera
  `find`/`save`/`delete`).

## Incidencias

`ClarityResult.Issues` lista problemas concretos de nombres como
`ClarityIssue(Severity, Description, Suggestion)`. Hoy el analizador marca
los **nombres de método con verbo genérico** como incidencias de severidad
`high` — por ejemplo, un método cuyo primer token es `get`/`process`/`handle`:

```
severity:    high
description: Generic verb 'process' in DataProcessor.process
suggestion:  Use a domain-specific verb
```

La severidad es un token de texto en minúsculas (`high`).
`ClarityReportRenderer` mapea las severidades a iconos en la salida
Markdown.

## Tu propio vocabulario, a partir del glosario que ya tienes

Los diccionarios integrados conocen el inglés general del software. No saben que
`Fold` es un verbo de tu dominio, que `Tranche` es un sustantivo preciso, o que
`Fx` es la abreviatura aceptada de tu equipo para *foreign exchange* — y un
nombre que no conocen se puntúa como desconocido, no como específico del
dominio.

Se los enseñas con el fichero de vocabulario que tu repositorio ya lleva: el
`glossary.json` commiteado (ADR-012). No hay un segundo fichero de diccionario
que mantener sincronizado.

| Entrada del glosario | Tipo | Qué aprende la claridad |
|---|---|---|
| `settle trade` | `verb-phrase` | `settle` es un verbo del dominio; `trade` es un sustantivo del dominio |
| `credit tranche` | `noun-phrase` | `credit` y `tranche` son sustantivos del dominio |
| `fx` | `word` | `fx` es un sustantivo del dominio |

Los términos de varias palabras enseñan token a token, porque los
identificadores se puntúan token a token. Todos los contextos delimitados
contribuyen: un identificador no lleva namespace, así que el alcance por
contexto no puede aplicarse al puntuar.

### Las abreviaturas aceptadas se declaran, no se infieren

La abreviatura que tu equipo acepta vive en su propia sección de nivel raíz, lo
que eleva el fichero a `schemaVersion: 2`:

```json
{
  "schemaVersion": 2,
  "contexts": { "trading": { "packages": ["Acme.Trading"] } },
  "abbreviations": { "fx": "foreign exchange", "calc": "calculate" },
  "terms": []
}
```

Nunca se vuelve a pedir que se desarrolle un token de esa lista, y la expansión
es *con lo que* las notas didácticas lo desarrollan: `noun 'fx' (foreign
exchange)`.

Solo esa sección acepta abreviaturas. Un token que simplemente aparece dentro de
un término commiteado (`calc` en `calc total`) se enseña como sustantivo del
dominio y sigue siendo una abreviatura, porque nadie decidió que fuera
abreviatura aceptada. Aceptar una es una decisión que alguien toma y revisa, no
un efecto secundario de una cosecha.

De ahí se derivan dos reglas del formato de fichero:

- La sección es **propiedad humana** — una cosecha nunca la escribe, y un merge
  la arrastra intacta, igual que `definition` y `translations`.
- El sello `2` aparece **solo cuando la sección tiene entradas**, así que un
  repositorio que nunca usa la función sigue escribiendo el mismo fichero
  schema 1, byte a byte, que escribía antes. Los lectores aceptan la sección en
  cualquier versión a partir de 1.

### Lo que el glosario no puede hacer

Los diccionarios integrados conservan su autoridad. Un proyecto puede enseñar a
los puntuadores una palabra que no conocen; no puede anular una que sí conocen.

- **Los verbos genéricos siguen siendo genéricos.** Commitear `process` o
  `handle` no los promociona, y lo mismo vale para los prefijos booleanos
  (`is`, `has`).
- **Los marcadores sin significado siguen sin significado.** `temp`, `foo` y
  compañía no se rescatan por estar escritos.
- **Los sinónimos obsoletos nunca son vocabulario.** Un alias existe para ser
  señalado; promocionarlo silenciaría la incidencia `non-canonical-term` para la
  que está declarado.
- **Los términos `stale` no son vocabulario.** Marcar un término como stale dice
  que la palabra salió del dominio.

Solo cuenta el fichero *commiteado*. Nada de lo que una ejecución recolecte
realimenta las puntuaciones de esa misma ejecución — un vocabulario que se
expande solo haría las puntuaciones no deterministas y autocertificadas. El
commit es la aprobación humana.

### Dónde se aplica

El fixture de xUnit, el informe de suite de NUnit y
`dotnet-narrativetrace clarity-scan` encuentran el glosario con la misma
búsqueda ascendente que usa la recolección (`GlossarySettings.ResolveFile`):
`NARRATIVETRACE_GLOSSARY` nombra una ruta explícita, `off` desactiva la
funcionalidad por completo y, si no, gana el `glossary.json` más cercano por
encima del directorio de trabajo.

La lectura es incondicional allí donde exista un glosario — no cambia nada en
disco. Un repositorio sin `glossary.json` se puntúa exactamente como antes de
que existiera esta funcionalidad, y un glosario que no se puede leer degrada a
los diccionarios integrados con un aviso por consola, en lugar de hacer fallar
la suite.

```csharp
var vocabulary = GlossaryVocabulary.FromFile(
    GlossarySettings.ResolveFile(
        Environment.GetEnvironmentVariable, Directory.GetCurrentDirectory()));

var result = ClarityAnalyzer.Analyze(tree, propertyNames, vocabulary);
```

## Salida del informe

`ClarityReportRenderer.Render` produce una tabla Markdown de puntuaciones
por escenario:

```csharp
var report = ClarityReportRenderer.Render(
[
    new ScenarioClarity("Order placement", orderResult),
    new ScenarioClarity("Legacy processing", legacyResult),
]);
```

## Imposición en el build

La claridad pasa a ser una *puerta*, no una sugerencia, mediante la CLI
`dotnet-narrativetrace` (o el paquete `NarrativeTrace.MSBuild` que la
envuelve).

```bash
# 1. Escanea un ensamblado compilado hacia clarity-results.json:
dotnet-narrativetrace clarity-scan --assembly bin/Release/net10.0/MyApp.dll

# 2. Falla el build por debajo de un umbral o por encima de un presupuesto de incidencias HIGH:
dotnet-narrativetrace clarity-check --results clarity-results.json \
    --min-score 0.80 --max-high-issues 0
```

Códigos de salida de `clarity-check`: `0` correcto (o `--warn-only`), `1`
fallo de la puerta, `2` error de uso / resultados mal formados. Si falta
el archivo de resultados se **omite** (salida 0), no falla — así un
proyecto que aún no ha escaneado no rompe CI.

Para el cableado con MSBuild (targets `ClarityScan` / `ClarityCheck` y las
propiedades `ClarityMinScore` / `ClarityMaxHighIssues` / `ClarityWarnOnly`),
consulta la [Guía de configuración](guia-de-configuracion.md#5-msbuild).

### Contrato JSON

`clarity-results.json` es el contrato entre el escaneo y la puerta — un
array de objetos de escenario:

```json
[
  {
    "scenario": "OrderService",
    "overall": 0.85,
    "method": 0.90,
    "class": 0.95,
    "parameter": 0.80,
    "structural": 1.00,
    "cohesion": 0.70,
    "issues": [
      {
        "severity": "high",
        "description": "Generic verb 'process' in DataProcessor.process",
        "suggestion": "Use a domain-specific verb"
      }
    ]
  }
]
```

La puerta cuenta las incidencias de severidad HIGH sin distinguir
mayúsculas, así que se admiten tanto `high` (lo que emite el scanner) como
`HIGH`.

## Componentes de NLP

El módulo de claridad usa NLP hecho a mano, sin dependencias externas:

| Componente | Propósito |
|---|---|
| `IdentifierTokenizer` | Divide `camelCase` / `snake_case` en tokens. |
| `VerbDictionary` | Categoriza verbos (`Domain`, `Standard`, `Boolean`, `Generic`, `Unknown`). |
| `RoleSuffixDictionary` | Clasifica los sufijos de clase (patrón de diseño, funcional, genérico). |
| `GenericTokenDetector` | Ordena la especificidad de los tokens (sin significado → específico del dominio). |
| `AbbreviationDictionary` | Puntúa abreviaturas por nivel (universal, bien conocida, ambigua). |
| `MorphologyAnalyzer` | Detecta la categoría gramatical por sufijos (`-tion`, `-ize`, `-able`). |
| `CollocationDictionary` | Reconoce frases de dominio comunes de varios tokens. |
| `CohesionScorer` | Comprueba la alineación del verbo del método con el rol de la clase. |
| `MethodNameScorer` / `ClassNameScorer` / `ParameterNameScorer` / `StructuralScorer` | Los cinco puntuadores de componentes. |
| `DomainVocabulary` | Las palabras propias del proyecto, leídas del glosario commiteado; extiende todos los diccionarios anteriores sin anularlos. |

## Véase también

- [Guía de instalación](guia-de-instalacion.md) — la herramienta `dotnet-narrativetrace`
- [Guía de configuración](guia-de-configuracion.md) — la puerta de claridad en MSBuild
- [Guía de atributos](guia-de-atributos.md) — primero nombres limpios, después atributos
