<!-- source: documentation/guides/README.md blob 5814dca27052 | translated: 2026-09-03 | reviewed: 2026-09-03 -->
# NarrativeTrace .NET — Guia do usuário

[English](../README.md) | [Español](../es/guias-de-usuario.md) | **Português** | [简体中文](../zh-CN/用户指南.md)

Guias orientados a tarefas para usar o NarrativeTrace em um projeto .NET.
Para a proposta de valor e um início rápido de 60 segundos, veja o
[README do repositório](../../../LEIAME.md).

| Guia | Leia para… |
|---|---|
| [Instalação](guia-de-instalacao.md) | Adicionar os pacotes e escolher um caminho de integração (proxy, DI, ASP.NET Core, frameworks de teste, CLI, MSBuild). |
| [Configuração](guia-de-configuracao.md) | Definir níveis de tracing, variáveis de ambiente, opções de DI/ASP.NET Core, propriedades do MSBuild e ocultação. |
| [Atributos](guia-de-atributos.md) | Usar `[Narrated]`, `[OnError]`, `[NotTraced]`, `[Traced]` e `[NarrativeSummary]`. |
| [Injeção de dependências](guia-de-injecao-de-dependencias.md) | Encapsular automaticamente serviços com interface que correspondem por namespace no container do MS.DI (tracing de beans ao estilo Spring/Micronaut). |
| [Integração com ASP.NET Core](guia-de-integracao-com-aspnet-core.md) | Conectar o middleware por requisição, os exportadores, a exclusão de rotas e o contexto de usuário. |
| [Clareza](guia-de-clareza.md) | Pontuar a qualidade dos nomes e impor isso como um gate de qualidade em CI. |
| [MSBuild e CLI](guia-de-msbuild-e-cli.md) | Executar os verbos do `dotnet-narrativetrace` e conectar o gate de clareza ao `dotnet build` / `dotnet test`. |

## Para agentes de IA

- [`llms.txt`](../llms.txt) — índice conciso da biblioteca para consumidores LLM.
- [`llms-full.md`](../llms-full.md) — referência completa da API em um único arquivo.

## Filosofia

**O código é o log.** Os nomes de métodos, os nomes de parâmetros e os
valores de retorno já contam a história da execução. O NarrativeTrace
captura essa história automaticamente e a renderiza como uma narrativa
legível — assim você escreve código limpo e expressivo em vez de
instruções de log manuais, e recorre aos atributos apenas em casos
excepcionais.
