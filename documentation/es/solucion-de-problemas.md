<!-- source: documentation/troubleshooting.md blob ba545b8d5198 | translated: 2026-09-03 | reviewed: 2026-09-03 -->
# Solución de problemas

[English](../troubleshooting.md) | **Español** | [Português](../pt-BR/solucao-de-problemas.md) | [简体中文](../zh-CN/故障排查.md)

Síntoma → causa → arreglo, recogidos del propio código y las propias
pruebas de este port. Cuando algo es una aspereza conocida y sin cubrir en
lugar de una garantía demostrada, se señala como tal — esta página dice lo
que es realmente cierto hoy, no lo que sería bonito prometer.

## Un parámetro se renderiza como `arg0`, `arg1` en lugar de su nombre real

**Causa:** el código de captura lee `ParameterInfo.Name`, que es `null`
solo cuando los metadatos compilados nunca llevaron nombres de
parámetros — delegados construidos dinámicamente
(`System.Reflection.Emit`) o algunas interfaces generadas por interop.
**El código C#/F# ordinario compilado por `dotnet build` siempre conserva
los nombres de los parámetros**, incluso en builds Release y bajo
trimming, así que esto no debería pasar en uso normal.

**Arreglo:** si te ocurre, comprueba si la interfaz vino de un generador
de código o de una vía de ensamblado dinámico que descartó los metadatos
de parámetros. Esta es la única diferencia con la JVM: .NET no necesita
ningún flag `-parameters` del compilador para código ordinario.

## `NarrativeTraceProxy.Create<T>` lanza una excepción al arrancar

**Causa:** `T` no es una interfaz, o el destino no la implementa. Este
port no añade ninguna cláusula de guarda propia aquí — la excepción que
ves es la excepción de reflexión subyacente de .NET (`DispatchProxy.Create`),
no una específica de NarrativeTrace.

**Arreglo:** confirma que `T` es un tipo interfaz y que la instancia que
estás envolviendo realmente la implementa.

## Un servicio registrado en DI no se está trazando

**Causa**, cualquiera de estas:

- No está registrado por **interfaz** — `AddNarrativeTracing` solo envuelve
  registros tipados por interfaz.
- El namespace de su implementación no coincide con un prefijo
  configurado. La coincidencia es por límite de punto: `MyApp.Services`
  nunca coincide con `MyApp.ServicesExtra`.
- Es un **servicio con clave** o un **genérico abierto** — ambos se dejan
  sin envolver silenciosamente.
- `AddNarrativeTracing` se llamó **antes** de que el servicio se
  registrara — la envoltura solo toca los descriptores presentes en el
  momento de la llamada.
- Está registrado por fábrica, y la coincidencia recurrió al namespace de
  la **interfaz** (el tipo de implementación es opaco en el momento del
  registro) — que puede diferir del namespace de la implementación real.

**Arreglo:** llama a `AddNarrativeTracing` el último, revisa dos veces el
prefijo de namespace, y evita los registros con clave/genéricos abiertos
para los servicios que necesites trazar.

## Las peticiones de ASP.NET Core no muestran traza, y ningún error

**Causa:** `HttpContext.GetNarrativeContext()` devuelve un contexto
silencioso sin operación siempre que el middleware no guardó nada — una
ruta excluida, o código que se ejecuta antes de
`UseMiddleware<NarrativeTraceMiddleware>()` en el pipeline. La petición se
completa con normalidad; simplemente no queda trazada.

**Arreglo:** coloca el middleware cerca del borde exterior del pipeline, y
revisa `ExcludedPaths` si una ruta concreta es la que queda en silencio.

## El trabajo de un `Task.Run` en segundo plano falta, o aparece como una raíz separada

**Causa:** el contexto `AsyncLocal` fluye correctamente para el trabajo
iniciado *y* observado dentro de un scope `RunAsync`. Un `Task.Run` a
secas que sigue en marcha cuando su scope se cierra se conecta en el
**momento de la ejecución, no en el del envío** — una divergencia
documentada, no un bug — así que puede aparecer como una segunda raíz en
lugar de anidarse bajo la llamada que lo lanzó.

**Arreglo:** usa `AsyncNarrativeContext.RunAsync`, `ForkJoinGroup` o
`FireAndForgetGroup` para la garantía de linaje que realmente necesitas,
en lugar de un `Task.Run` a secas.

## `CaptureTrace()` devuelve una traza vacía cuando esperabas contenido

**Causa:** leer la captura desde fuera de cualquier scope de
`AsyncNarrativeContext` — o después de que dos scopes se ejecutaran de
forma concurrente — devuelve deliberadamente un árbol **vacío** en lugar
de uno ajeno. Una narrativa ausente es honesta; la narrativa de la
petición de otro no lo sería.

**Arreglo:** captura desde dentro del scope que produjo el trabajo, o
asegúrate de haber esperado (`await`) el trabajo antes de capturar.

## Un fallo de fire-and-forget nunca aparece en ningún sitio

**Causa:** `FireAndForgetGroup` traga una excepción en el trabajo lanzado
por diseño — eso es lo que hace seguro lanzar y olvidar — y excluye la
rama fallida de `ChildRoots`. Es un aislamiento intencional, no una
funcionalidad que falte.

**Arreglo:** si necesitas observar el fallo, añade tu propio manejo dentro
del trabajo lanzado; no dependas de la traza para revelarlo.

## Las ramas de un `ForkJoinGroup` se ejecutaron pero faltan sus spans

**Causa:** saltarte `await fork.JoinAsync()` significa que los spans de
las ramas nunca llegan a la traza padre — el trabajo se sigue ejecutando,
pero la narrativa lo pierde.

**Arreglo:** haz siempre `await` del join.

## Las trazas de dos pruebas de xUnit aparecen mezcladas

**Causa:** un `NarrativeFixture` guarda un solo contexto, así que atiende
a una prueba a la vez. `IClassFixture<NarrativeFixture>` es seguro porque
xUnit nunca paraleliza dentro de una clase — pero compartir una instancia
de fixture entre clases (o vía una colección) que sí se ejecutan en
paralelo mezcla sus spans en una sola traza.

**Arreglo:** deja que cada clase de pruebas tenga su propia instancia de
fixture; no compartas una entre colecciones de pruebas paralelas.

## `clarity-scan --assembly` falla en lugar de imprimir un error limpio

**Causa:** un archivo ausente se maneja limpiamente (`error: assembly not
found`, código de salida `1`). Un archivo **existente pero inválido** — no
un ensamblado .NET real — no: se carga vía `MetadataLoadContext` sin
ninguna guarda, así que un archivo malformado aparece como una excepción
.NET sin manejar en bruto en lugar de uno de los códigos de salida
documentados. Es una aspereza conocida y sin cubrir, no un comportamiento
documentado.

**Arreglo:** comprueba dos veces que la ruta realmente apunta a un
ensamblado .NET compilado. Si te encuentras con el fallo, merece la pena
reportarlo en lugar de trabajar alrededor de él.

## Un typo en `NARRATIVETRACE_*` no hace nada, silenciosamente

**Causa:** por diseño, toda variable de entorno `NARRATIVETRACE_*`
degrada a su valor por defecto en lugar de lanzar una excepción ante un
valor no reconocido — "una configuración incorrecta nunca rompe la
captura". `NARRATIVETRACE_LEVEL=Detial` (typo) resuelve tranquilamente a
`Detail`, no a un error.

**Arreglo:** no confíes en que un typo se detecte — revisa la ortografía
dos veces, o registra la configuración resuelta al arrancar si necesitas
estar seguro.

## `NARRATIVETRACE_OUTPUT=true` pero no aparece ningún archivo

**Causa**, cualquiera de estas:

- `NARRATIVETRACE_LEVEL` es `Off` — nunca se capturó nada.
- La traza realmente está vacía. **Una traza vacía no escribe nada en
  absoluto, por diseño** — un artefacto ausente significa "no se capturó
  nada", no "el escrito falló". Esto normalmente significa que la prueba
  llamó al servicio crudo sin envolver en lugar del envuelto por el proxy.

**Arreglo:** confirma que el nivel no es `Off`, y confirma que estás
llamando a través de `NarrativeTraceProxy.Create<T>` (o un servicio de DI
auto-envuelto), no a la implementación desnuda.

## `clarity-report.md` no coincide con lo que espero de `NARRATIVETRACE_OUTPUT`

**Causa:** el `clarity-results.json`/`clarity-report.md` a nivel de suite
se escriben siempre que el fixture de la suite se ejecuta y acumula al
menos una entrada — **independientemente de `NARRATIVETRACE_OUTPUT`**.
Solo los archivos `.md`/`.json`/`.mmd`/`.nt` por prueba necesitan ese flag.

**Arreglo:** no trates "sin archivos de traza por prueba" como "sin
informe de claridad" — están condicionados por dos condiciones distintas.

## La recolección del glosario nunca escribe nada

**Causa:** la recolección es opcional según la **presencia de archivo** —
`GlossarySuiteReporter` no hace nada silenciosamente a menos que
`glossary.json` ya exista en la ubicación resuelta (una búsqueda hacia
arriba en los directorios, `NARRATIVETRACE_GLOSSARY`, o el valor literal
`off`).

**Arreglo:** haz commit de un `glossary.json` inicial si quieres que la
recolección se ejecute — consulta
[Qué incluir en el commit](que-incluir-en-el-commit.md#la-recolección-del-glosario-es-opcional-según-la-presencia-de-archivo).

## Referenciar `NarrativeTrace.MSBuild` lo mete en las dependencias de mi paquete

**Causa:** está pensado para ser una dependencia solo en tiempo de build.

**Arreglo:** referéncialo con `PrivateAssets="all"` para que no fluya de
forma transitiva a los consumidores de tu propio paquete NuGet.
