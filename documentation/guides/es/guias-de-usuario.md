<!-- source: documentation/guides/README.md blob bb49bcf3b400 | translated: 2026-10-18 | reviewed: 2026-09-03 -->
# NarrativeTrace .NET — Guías de usuario

[English](../README.md) | **Español** | [Português](../pt-BR/guia-de-usuario.md) | [简体中文](../zh-CN/用户指南.md)

Guías orientadas a tareas para usar NarrativeTrace en un proyecto .NET.
Para la propuesta de valor y un inicio rápido de 60 segundos, consulta el
[README del repositorio](../../../LEAME.md).

| Guía | Léela para… |
|---|---|
| [Instalación](guia-de-instalacion.md) | Añadir los paquetes y elegir una vía de integración (proxy, DI, ASP.NET Core, frameworks de pruebas, CLI, MSBuild). |
| [Configuración](guia-de-configuracion.md) | Ajustar niveles de tracing, variables de entorno, opciones de DI/ASP.NET Core, propiedades de MSBuild y ocultación. |
| [Atributos](guia-de-atributos.md) | Usar `[Narrated]`, `[OnError]`, `[NotTraced]`, `[Traced]`, `[NarrativeSummary]` y `[NarrativeElements]`. |
| [Inyección de dependencias](guia-de-inyeccion-de-dependencias.md) | Envolver automáticamente los servicios con interfaz que coincidan por namespace en el contenedor de MS.DI (tracing de beans). |
| [Integración con ASP.NET Core](guia-de-integracion-con-aspnet-core.md) | Cablear el middleware por petición, los exportadores, la exclusión de rutas y el contexto de usuario. |
| [Claridad](guia-de-claridad.md) | Puntuar la calidad de los nombres e imponerla como puerta de calidad en CI. |
| [MSBuild y CLI](guia-de-msbuild-y-cli.md) | Ejecutar los verbos de `dotnet-narrativetrace` y cablear la puerta de claridad en `dotnet build` / `dotnet test`. |

## Recetas

Una página por problema, en las palabras que un usuario escribiría, cada una con el código y la salida reales de una ejecución.

- [Añade registro a este servicio](../../es/recetas/anade-registro-a-este-servicio.md) — Llevar cada llamada, con nombres de parámetros y resultados, al `ILogger` del proyecto sin escribir una sentencia de log.
- [Ayúdame a depurar y arreglar este error](../../es/recetas/ayudame-a-depurar-y-arreglar-este-error.md) — Encontrar la llamada que le falta a un flujo, arreglarla y fijar el flujo corregido como línea base aprobada.
- [Por qué falló esta prueba](../../es/recetas/por-que-fallo-esta-prueba.md) — Leer la narrativa de una prueba en rojo frente a su aserción y distinguir un error de código de una expectativa obsoleta.
- [Revisa un flujo complejo antes de cambiarlo](../../es/recetas/revisa-un-flujo-complejo-antes-de-cambiarlo.md) — Ver qué hace un flujo en `Detail` y en `Summary`, y el `.nt` sin valores que lee un agente.
- [Revisa una refactorización: ¿cambió el comportamiento?](../../es/recetas/revisa-una-refactorizacion-cambio-el-comportamiento.md) — Atrapar una llamada eliminada que las pruebas en verde no vieron, con el diff de aprobación.
- [Sigue una petición entre servicios (trazado distribuido)](../../es/recetas/sigue-una-peticion-entre-servicios.md) — Llevar un mismo id de traza y el span del llamador a través de un salto HTTP, para que las líneas de dos servicios se lean como una sola petición.
- [Mantén los secretos fuera de los logs](../../es/recetas/manten-los-secretos-fuera-de-los-logs.md) — Ver una contraseña y un número de tarjeta ocultados por nombre y por valor, y demostrarlo con una prueba que vuelve a leer el log.
- [Sigue un flujo entre hilos](../../es/recetas/sigue-un-flujo-entre-hilos.md) — Mantener el trabajo en segundo plano dentro de la traza con un grupo fork-join, y ver cómo se ve sin él.
- [Envía las trazas al stack de observabilidad que ya tienes](../../es/recetas/envia-las-trazas-al-stack-de-observabilidad-que-ya-tienes.md) — Alimentar la misma ejecución a tu logger y a spans de OpenTelemetry, lado a lado.
- [Traza en producción sin el coste](../../es/recetas/traza-en-produccion-sin-el-coste.md) — Conservar solo las ejecuciones que fallaron con el nivel `Errors`, y saber qué descarta.

## Para agentes de IA

- [`llms.txt`](../llms.txt) — índice conciso de la biblioteca para consumidores LLM.
- [`llms-full.md`](../llms-full.md) — referencia completa de la API en un solo archivo.

## Filosofía

**El código es el log.** Los nombres de métodos, los nombres de parámetros
y los valores de retorno ya cuentan la historia de la ejecución.
NarrativeTrace captura esa historia automáticamente y la renderiza como una
narrativa legible — así escribes código limpio y expresivo en lugar de
sentencias de log manuales, y recurres a los atributos solo en casos
excepcionales.
