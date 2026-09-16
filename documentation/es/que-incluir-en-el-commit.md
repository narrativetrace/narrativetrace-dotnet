<!-- source: documentation/what-to-commit.md blob a2493fb90dbe | translated: 2026-09-13 | reviewed: - -->
# Qué incluir en el commit

[English](../what-to-commit.md) | **Español** | [Português](../pt-BR/o-que-incluir-no-commit.md) | [简体中文](../zh-CN/应提交的内容.md)

Una vez que el tracing está en marcha, tendrás archivos generados en
disco. Esta página dice cuáles son salida desechable y cuáles están
pensados para revisarse y hacer commit — verificado contra lo que los
writers de esta implementación realmente producen, no asumido.

## Los artefactos, uno por uno

| Artefacto | ¿Commit? | Por qué |
|---|---|---|
| `<output-dir>/traces/<Class>/<slug>.md` | No | Se regenera en cada ejecución; la traza legible por humanos de una prueba. |
| `<output-dir>/traces/<Class>/<slug>.json` | No | La misma traza como documento de capítulo JSON — se regenera en cada ejecución. |
| `<output-dir>/diagrams/<Class>/<slug>.mmd` | No | Diagrama de secuencia Mermaid que la acompaña — se regenera en cada ejecución. |
| `<output-dir>/manifest.json` | No | Se regenera en cada ejecución; su objeto `run` de nivel superior (`id`, `name` — la frase de tres palabras propia de la ejecución) nombra *esta ejecución*, no un escenario, así que cambia en cada ejecución aunque nada más cambie *(since 0.1.5)*. |
| `<output-dir>/structural/<Class>/<slug>.nt` | No | Traza estructural sin valores (nombres, jerarquía, tipo de resultado únicamente). El archivo en disco es la **última línea base correcta (last green)** *(since 0.1.5)*: una ejecución en verde la hace avanzar, una que no está en verde se compara contra ella (la línea "Since last green" del resumen de la suite, el delta del informe de fallo) pero nunca la sobrescribe. Sigue sin ser algo para hacer commit — consulta [Formato de traza estructural](../structural-trace-format.md) para la contraparte con commit. |
| `<approved-dir>/<Class>/<slug>.approved.nt` | **Sí**, si el [modo de aprobación](../structural-trace-format.md) está activado | *(since 0.1.5)* La traza de aprobación revisada — `NARRATIVETRACE_APPROVED_DIR` (por defecto `narratives`), actívalo con `NARRATIVETRACE_APPROVAL=true`. Este es el único archivo de esta tabla que es una decisión deliberada, no una salida. |
| `<approved-dir>/<Class>/<slug>.received.nt` | No | Se escribe cuando la aprobación no coincide, o cuando aún no existe una traza aprobada. Revísalo, ejecuta `./build.sh Approve` para promoverlo (o renómbralo a mano), y deja que la promoción lo elimine — nunca hagas commit de la traza recibida en sí. |
| `<output-dir>/traces/<Class>/<slug>.canonical.json` | No | Fixture de conformidad opcional (`NARRATIVETRACE_CANONICAL_JSON=true`), pensado para probar el propio NarrativeTrace contra el esquema canónico — no algo que un proyecto de aplicación necesite conservar. |
| `<output-dir>/traces/<Class>/<slug>.structural.json` | No | Array de entradas sin valores, opcional (`NARRATIVETRACE_STRUCTURAL_JSON=true`) — mismo razonamiento que `.canonical.json`. |
| `<output-dir>/clarity-results.json` | No | Informe de claridad a nivel de suite generado (legible por máquina). Aparece siempre que el fixture de la suite se ejecutó y acumuló al menos una entrada, independientemente de `NARRATIVETRACE_OUTPUT` — regenera, no hagas commit. |
| `<output-dir>/clarity-report.md` | No | El mismo informe, legible por humanos. |
| `clarity/clarity-scan-results.json` / `clarity-scan-report.md` (de `dotnet-narrativetrace clarity-scan`) | No | Un escaneo estático, solo por reflexión, de un ensamblado compilado — regénéralo en CI, no hagas commit. |
| `glossary.json` | **Sí**, si usas la recolección del glosario | Consulta abajo — este es el único artefacto que esta implementación trata como un archivo revisado y curado a mano. |
| `glossary.md` | **Sí**, junto a `glossary.json` | Renderizado legible por humanos del mismo archivo, reescrito solo cuando cambian los bytes del JSON (anti-churn). |
| `<output-dir>/glossary-usage.json` | No | Estadísticas de uso volátiles por ejecución — se regenera, no se cura. |
| `.claude/skills/<segmento>/SKILL.md` | **Sí** | Regenerado por `dotnet run --project src/NarrativeTrace.Cli -- skills render` a partir del catálogo tipado de skills, pero se commitea igualmente: debe publicarse exactamente en la ruta donde Claude Code lo descubre. `skills lint` falla el build si difiere de una renderización reciente. |
| La sección `<!-- narrativetrace:skills:start -->` … `<!-- narrativetrace:skills:end -->` de `AGENTS.md` | **Sí** | Mismo renderizador, insertado en el archivo en el mismo lugar — commitea el archivo completo, no solo la sección. |

La escritura de trazas está **activada por defecto** *(since 0.1.5)* (define
`NARRATIVETRACE_OUTPUT=false` para desactivarla); `<output-dir>` por
defecto es `./TestResults/narrativetrace` cuando `NARRATIVETRACE_OUTPUT_DIR`
no está definido — la convención de `.NET` que `dotnet test
--results-directory` y Visual Studio/Rider ya tratan como salida
desechable, y que el propio `.gitignore` de este repositorio ya excluye.
Mantenlo fuera del control de versiones también en tus propios proyectos, a
menos que tengas una razón concreta de CI para archivarlo como artefacto
de build (que es una decisión de retención de CI, no una de "hacer commit
al control de versiones").

## La recolección del glosario es opcional según la presencia de archivo

A diferencia de todo lo demás en esta página, la recolección del glosario
no crea nada espontáneamente: `GlossarySuiteReporter` no hace nada
silenciosamente a menos que `glossary.json` **ya exista** en la ubicación
que resuelve (una búsqueda hacia arriba en los directorios desde la
ejecución de pruebas, la variable de entorno `NARRATIVETRACE_GLOSSARY`, o
el valor literal `off` para desactivar la funcionalidad por completo). Si
quieres recolección, haz commit tú mismo de un archivo inicial:

```json
{
  "schemaVersion": 1,
  "contexts": {},
  "terms": []
}
```

A partir de entonces, cada ejecución de la suite recolecta vocabulario
nuevo de sus trazas, lo fusiona de forma aditiva en `glossary.json`, y
regenera `glossary.md` — revisa el diff como cualquier otro archivo curado
a mano. Un `glossary.json` malformado lanza una excepción en lugar de
saltarse silenciosamente, lo cual es deliberado: un typo en un archivo
revisado y con commit debería fallar ruidosamente.

## El modo de aprobación, de principio a fin

```text
la prueba pasa
   |
   v
compara la estructura actual con la traza aprobada
   |
   +-- igual      --> pasa, no se escribe nada
   +-- diferente  --> escribe .received.nt y falla
                     |
                     v
                una persona revisa el diff
                     |
                     v
                ./build.sh Approve
                     |
                     v
              .approved.nt actualizado, haz commit
```

El estado de fallo es deliberado: una prueba que pasa pero cuya *forma*
cambió — incluido un cambio que un agente de IA deslizó dentro de un
refactor por lo demás correcto — tiene que ser revisada y aprobada
explícitamente, no simplemente compilar. Consulta
[Formato de traza estructural](../structural-trace-format.md) para el
comportamiento completo del modo de aprobación, y la
[Guía de configuración](../guides/es/guia-de-configuracion.md) para
`NARRATIVETRACE_APPROVAL` / `NARRATIVETRACE_APPROVED_DIR`.

## Véase también

- [Formato de traza estructural](../structural-trace-format.md) — el
  formato `.nt`, la identidad de artefacto por invocación, la última línea
  base correcta y el modo de aprobación completo.
- [Privacidad y ocultación](privacidad-y-ocultacion.md) — qué hay dentro de
  estos archivos antes de decidir si archivarlos en algún sitio.
- [Guía de configuración](../guides/es/guia-de-configuracion.md) — las
  variables `NARRATIVETRACE_*` que controlan dónde y si se escriben estos
  archivos.
