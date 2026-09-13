<!-- source: documentation/duplication.md blob 9e6fce46f9a3 | translated: 2026-09-13 | reviewed: - -->
# Detección de duplicación

[English](../duplication.md) | **Español** | [Português](../pt-BR/deteccao-de-duplicacao.md) | [简体中文](../zh-CN/重复代码检测.md)

*(desde 0.1.4, sin publicar)* — herramienta de tiempo de compilación, no un
comportamiento de la biblioteca en tiempo de ejecución: nada de esto se
distribuye en los paquetes NuGet.

`./build.sh DuplicationReport` ejecuta [jscpd](https://github.com/kucherenko/jscpd)
(una versión exacta fijada, invocada vía `npx jscpd@<versión>` — este
repositorio no tiene un equivalente JVM/PMD ya presente en su propio
classpath como sí lo tiene el CPD del runtime Java) sobre los fuentes C#
principales y de prueba, y escribe un informe en cada commit.
`./build.sh DuplicationCheck` lee ese informe y aplica el trinquete de
duplicación descrito abajo; forma parte de `Verify`.

## Qué se mide

- **Lenguaje: solo C#, por ahora** (el esquema común a toda la familia se
  describe abajo).
- **Piso de tokens: 60.** Una coincidencia por debajo de 60 tokens suele ser
  una casualidad — dos métodos no relacionados que comparten por azar una
  forma breve y común — no una copia estructural que valga la pena atender.
- **Se ignoran identificadores y literales.** Las banderas
  `--ignore-identifiers --ignore-literals` de jscpd encuentran entonces
  duplicación *estructural* (la misma forma con nombres y valores
  distintos), no simplemente texto pegado con los mismos nombres —
  confirmado con un par de archivos de prueba que solo difieren en nombres
  de identificadores y valores literales: 0 clones sin las dos banderas, 1
  clon (46% de las líneas del par) con ellas, con el mismo piso de tokens.
  `--mode strict` es un eje completamente distinto (desactiva la
  normalización de comentarios/espacios de jscpd) y no ignora por sí solo
  identificadores ni literales.
- **Los fuentes principales y de prueba se escanean por separado.** El
  árbol de pruebas (`tests/**` + `benchmarks/**`) se informa — sus números
  están en `duplication.json` y en la línea de resumen — pero nunca hace
  fallar `DuplicationCheck`. El código de pruebas repite legítimamente
  (preparación, constructores de fixtures, bloques de aserciones); un
  umbral fijo ahí sería ruido, no señal.

## El trinquete, no un porcentaje fijo

Un único número de "fallar por encima de N%" es el instrumento equivocado:
el número correcto depende del piso de tokens y de cuánto del árbol es
naturalmente repetitivo (tablas de datos, código generado), así que un
umbral fijo termina siendo o bien tan laxo que nunca se activa, o bien tan
estricto que bloquea trabajo no relacionado. En su lugar,
`DuplicationCheck` aplica un trinquete contra una línea base registrada,
`config/duplication/baseline.properties`:

- **Falla cuando el porcentaje del árbol principal sube más de 0.3 puntos
  porcentuales** por encima de la línea base registrada — una pequeña
  tolerancia que absorbe el ruido en el conteo de tokens entre ejecuciones,
  no un crecimiento real.
- **Falla cuando un clúster no exento es más grande que el clúster más
  grande registrado en la línea base** — un nuevo bloque duplicado grande
  es un hallazgo por sí solo, incluso si el porcentaje general se mantiene
  estable.
- **El árbol de pruebas nunca hace fallar el check**, cualquiera sea su
  porcentaje.

Baja la línea base con el mismo commit que elimina la duplicación que
registró. Nunca la subas para hacer desaparecer un fallo — añade en su
lugar una exención justificada (abajo), o deja el hallazgo para una pasada
posterior.

## Las exenciones son datos

`config/duplication/exemptions.txt` enumera duplicación deliberada: código
estructurado intencionalmente como dos copias paralelas en lugar de una
abstracción compartida. Cada entrada es un par `globA :: globB` (comparado
contra la ruta relativa a la raíz del repositorio que informa jscpd, vía
`Microsoft.Extensions.FileSystemGlobbing` — ya presente en el grafo de esta
compilación a través de Nuke.Common) con una línea `# motivo` justo
encima. Un clúster está exento solo cuando *todas* sus ocurrencias
coinciden con uno de los dos patrones del par — una regla de denegación
por defecto, así que un clúster sin clasificar por encima del piso siempre
es un hallazgo, nunca un pase silencioso. Un par sin motivo encima, o un
par mal formado, hace fallar la compilación directamente en lugar de ser
ignorado.

La única exención registrada del primer escaneo eran los diccionarios de
listas de palabras del módulo clarity
(`src/NarrativeTrace.Clarity/*Dictionary.cs`). Una decisión del propietario
del 2026-09-12 (que refleja las mismas categorías de exención del runtime
Java) la amplió a tres categorías justificadas, escritas como líneas
`globA :: globB` separadas en lugar de un único patrón combinado (este
motor de globs no tiene alternancia de llaves): **tablas de palabras
identificadas por archivo** — las tablas adjetivo/sustantivo/verbo de
`TraceNamer.cs`, contra sí mismas y contra los diccionarios de clarity;
**tablas de palabras identificadas por la forma de su contenido en lugar
de por el nombre del archivo** — las listas de palabras `HashSet`/literal
de arreglo de `GenericTokenDetector.cs` y `RedactionPolicy.cs`, que se
agrupan entre sí y con los diccionarios aunque ninguno de los dos archivos
coincide con el patrón `*Dictionary`; y **registros anchos** —
`CanonicalEntry.cs` y `SpanContext.cs`, cada uno un único registro
posicional con un componente anulable por campo del esquema (el
equivalente de este runtime a la clase builder de un-setter-por-componente
del runtime Java), exentos contra sí mismos y entre sí ya que colapsar la
forma de un componente por campo cambiaría la superficie pública del
constructor. Cada uno de estos pares documenta cualquier brecha conocida
donde el mismo patrón también cubre (necesariamente) lógica real que vive
junto a los datos que exime, en lugar de ampliar en silencio lo que
significa "datos, no lógica".

## Cómo leer el informe

`artifacts/duplication/duplication.json` es el resultado normalizado (la
misma forma que emite la herramienta de duplicación de todos los runtimes
de NarrativeTrace, para cualquiera de los lenguajes que cubran):

```json
{"tool":"jscpd","language":"csharp","minTokens":60,
 "main":{"tokensTotal":N,"tokensDuplicated":N,"percent":x.y,
         "clusters":[{"tokens":N,"lines":N,
                       "occurrences":[{"file":"…","startLine":N,"endLine":N}]}]},
 "test":{"...":"same shape"}}
```

`tokensDuplicated` es una **unión**, no una suma sobre clústeres — contar
tokens `× ocurrencias` cuenta dos y tres veces una región cubierta por
varios clústeres (la misma clase de error que midió más de 300% de
duplicación en el primer escaneo del runtime Java, antes de su corrección
por unión), así que `percent` nunca puede superar el 100%. jscpd tokeniza
cada archivo por su cuenta en lugar de en un único flujo compartido para
todo el corpus como hace el CPD de PMD, y su informe JSON no lleva ningún
índice de token por ocurrencia — solo una línea de inicio/fin por
ocurrencia, más un conteo de tokens para todo el fragmento coincidente —
así que la unión de este runtime opera sobre los rangos de línea propios
de cada archivo en lugar de un único espacio de índice de tokens: las
ocurrencias se agrupan por archivo, los rangos que se solapan o se tocan
dentro de un mismo archivo se fusionan, y cada tramo fusionado se cuenta
una sola vez, con el conteo de tokens *más grande* entre los rangos que lo
formaron, nunca su suma. Esto es exacto siempre que los rangos duplicados
de un archivo sean idénticos o no se solapen — incluyendo el caso común de
"un mismo tramo, varios socios" que una suma ingenua calcula mal — y solo
conservador para un solapamiento dentro de un archivo, genuinamente raro,
entre dos clústeres distintos. Ver
`DuplicationReportSupport.UnionDuplicatedTokens` para el razonamiento
completo.

El registro de la compilación imprime una línea de resumen por ejecución:

```
duplication: main 12.8% of tokens in 137 clusters (largest 1558 tokens
src/NarrativeTrace.Core/TraceNamer.cs:29 ↔ src/NarrativeTrace.Core/TraceNamer.cs:65)
· test 26.7% in 912 clusters (reported, not gated)
```

## Un Node/npx ausente nunca es un pase silencioso

jscpd es un CLI de Node externo, no una biblioteca ya presente en el grafo
propio de esta compilación — así que, a diferencia del PMD-CPD del runtime
Java (una dependencia de biblioteca, siempre presente),
`DuplicationReport`/`DuplicationCheck` puede encontrarse con un entorno sin
Node en absoluto. Eso sigue la misma convención de `ScannerGateSupport` que
ya usan `SecretsScan`/`Semgrep`/`OsvScan`: localmente, una `npx` ausente
**advierte** y registra un estado `skipped` bajo
`artifacts/duplication/scan-status/` — nunca un verde silencioso; en CI, o
donde la herramienta de duplicación sea obligatoria, su ausencia **hace
fallar** la compilación. `DuplicationCheck` mismo falla ruidosamente (no
"sin línea base, así que pasa") cuando `DuplicationReport` no produjo
ningún `duplication.json` — regla 2 de la retrospectiva de release: una
herramienta con salto elegante debe probar que alguna vez se ejecutó.

Disponibilidad de Node por entorno:

| Entorno | ¿Tiene Node? |
|---|---|
| El contenedor de desarrollo local | **Sí** — Node 22 (ya aprovisionado en ese contenedor) |
| GitHub Actions CI (`ci.yml`, `ubuntu-latest`) | **Sí** — preinstalado en la imagen del runner |
| CI privado (imagen `mcr.microsoft.com/dotnet/sdk`) | **No** por defecto — el `before_script` del job `verify` instala Node 22 vía NodeSource, igual que el contenedor de desarrollo |
| Contenedor de verificación de publicación (el script de publicación, la misma imagen SDK básica, sin root) | **No** por defecto — se descarga un tarball de Node fijado en el host y se monta de solo lectura dentro del contenedor (no hace falta root/apt para una ejecución de contenedor sin root) |

## Añadir una exención

1. Ejecuta `./build.sh DuplicationReport` y localiza el clúster en
   `duplication.json` o en la línea de resumen.
2. Confirma que es deliberado — una estructura paralela genuina mantenida
   aparte a propósito, no duplicación que nadie se ha ocupado de eliminar.
3. Añade una línea `# motivo` y un par `globA :: globB` a
   `config/duplication/exemptions.txt`.
4. Vuelve a ejecutar `./build.sh DuplicationCheck` para confirmar que pasa.

## Bajar la línea base

Elimina la duplicación, ejecuta `./build.sh DuplicationReport`, y actualiza
`main.percent` / `main.largestCluster` en
`config/duplication/baseline.properties` con los números recién medidos en
el mismo commit — el mismo modismo de "fijado al valor medido" que esta
compilación ya usa para los pisos de cobertura y de puntaje de mutación.
