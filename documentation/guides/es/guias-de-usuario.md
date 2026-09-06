<!-- source: documentation/guides/README.md blob 5814dca27052 | translated: 2026-09-03 | reviewed: 2026-09-03 -->
# NarrativeTrace .NET — Guías de usuario

[English](../README.md) | **Español** | [Português](../pt-BR/guia-de-usuario.md) | [简体中文](../zh-CN/用户指南.md)

Guías orientadas a tareas para usar NarrativeTrace en un proyecto .NET.
Para la propuesta de valor y un inicio rápido de 60 segundos, consulta el
[README del repositorio](../../../LEAME.md).

| Guía | Léela para… |
|---|---|
| [Instalación](guia-de-instalacion.md) | Añadir los paquetes y elegir una vía de integración (proxy, DI, ASP.NET Core, frameworks de pruebas, CLI, MSBuild). |
| [Configuración](guia-de-configuracion.md) | Ajustar niveles de tracing, variables de entorno, opciones de DI/ASP.NET Core, propiedades de MSBuild y ocultación. |
| [Atributos](guia-de-atributos.md) | Usar `[Narrated]`, `[OnError]`, `[NotTraced]`, `[Traced]` y `[NarrativeSummary]`. |
| [Inyección de dependencias](guia-de-inyeccion-de-dependencias.md) | Envolver automáticamente los servicios con interfaz que coincidan por namespace en el contenedor de MS.DI (tracing de beans al estilo Spring/Micronaut). |
| [Integración con ASP.NET Core](guia-de-integracion-con-aspnet-core.md) | Cablear el middleware por petición, los exportadores, la exclusión de rutas y el contexto de usuario. |
| [Claridad](guia-de-claridad.md) | Puntuar la calidad de los nombres e imponerla como puerta de calidad en CI. |
| [MSBuild y CLI](guia-de-msbuild-y-cli.md) | Ejecutar los verbos de `dotnet-narrativetrace` y cablear la puerta de claridad en `dotnet build` / `dotnet test`. |

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
