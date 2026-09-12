<!-- source: documentation/what-to-commit.md blob 4477b106b539 | translated: 2026-09-12 | reviewed: - -->
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
| `<output-dir>/structural/<Class>/<slug>.nt` | No, por ahora | Traza estructural sin valores (nombres, jerarquía, tipo de resultado únicamente). Determinista y diffable por construcción, pero **nada en esta implementación la vuelve a leer todavía** — no hay modo de aprobación ni bucle de comparación por delta contra una ejecución anterior, así que no hay línea base con commit contra la que compararla. Se regenera en cada ejecución como el resto. |
| `<output-dir>/traces/<Class>/<slug>.canonical.json` | No | Fixture de conformidad opcional (`NARRATIVETRACE_CANONICAL_JSON=true`), pensado para probar el propio NarrativeTrace contra el esquema canónico — no algo que un proyecto de aplicación necesite conservar. |
| `<output-dir>/traces/<Class>/<slug>.structural.json` | No | Array de entradas sin valores, opcional (`NARRATIVETRACE_STRUCTURAL_JSON=true`) — mismo razonamiento que `.canonical.json`. |
| `<output-dir>/clarity-results.json` | No | Informe de claridad a nivel de suite generado (legible por máquina). Aparece siempre que el fixture de la suite se ejecutó y acumuló al menos una entrada, independientemente de `NARRATIVETRACE_OUTPUT` — regenera, no hagas commit. |
| `<output-dir>/clarity-report.md` | No | El mismo informe, legible por humanos. |
| `clarity/clarity-scan-results.json` / `clarity-scan-report.md` (de `dotnet-narrativetrace clarity-scan`) | No | Un escaneo estático, solo por reflexión, de un ensamblado compilado — regénéralo en CI, no hagas commit. |
| `glossary.json` | **Sí**, si usas la recolección del glosario | Consulta abajo — este es el único artefacto que esta implementación trata como un archivo revisado y curado a mano. |
| `glossary.md` | **Sí**, junto a `glossary.json` | Renderizado legible por humanos del mismo archivo, reescrito solo cuando cambian los bytes del JSON (anti-churn). |
| `<output-dir>/glossary-usage.json` | No | Estadísticas de uso volátiles por ejecución — se regenera, no se cura. |

La escritura de trazas está **activada por defecto** *(since 0.1.4, unreleased)* (define
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

## Lo que esta implementación todavía no tiene

El modo de aprobación aún no está aquí: esta implementación no tiene archivos
`.approved.nt` / `.received.nt`, ni verbo `approve`, ni nada que compare la
traza estructural de una ejecución contra una anterior. El artefacto
estructural `.nt` existe y es determinista, pero todo artefacto de esta
página es solo salida de regeneración hoy — todavía no hay un flujo de
trabajo de "línea base con commit que falla un build ante un cambio de
comportamiento sin revisar" al que apuntarse.

## Véase también

- [Privacidad y ocultación](privacidad-y-ocultacion.md) — qué hay dentro de
  estos archivos antes de decidir si archivarlos en algún sitio.
- [Guía de configuración](../guides/es/guia-de-configuracion.md) — las
  variables `NARRATIVETRACE_*` que controlan dónde y si se escriben estos
  archivos.
