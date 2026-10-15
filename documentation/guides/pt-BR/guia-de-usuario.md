<!-- source: documentation/guides/README.md blob be6db3ed5a3f | translated: 2026-10-11 | reviewed: 2026-09-03 -->
# NarrativeTrace .NET — Guia do usuário

[English](../README.md) | [Español](../es/guias-de-usuario.md) | **Português** | [简体中文](../zh-CN/用户指南.md)

Guias orientados a tarefas para usar o NarrativeTrace em um projeto .NET.
Para a proposta de valor e um início rápido de 60 segundos, veja o
[README do repositório](../../../LEIAME.md).

| Guia | Leia para… |
|---|---|
| [Instalação](guia-de-instalacao.md) | Adicionar os pacotes e escolher um caminho de integração (proxy, DI, ASP.NET Core, frameworks de teste, CLI, MSBuild). |
| [Configuração](guia-de-configuracao.md) | Definir níveis de tracing, variáveis de ambiente, opções de DI/ASP.NET Core, propriedades do MSBuild e ocultação. |
| [Atributos](guia-de-atributos.md) | Usar `[Narrated]`, `[OnError]`, `[NotTraced]`, `[Traced]`, `[NarrativeSummary]` e `[NarrativeElements]`. |
| [Injeção de dependências](guia-de-injecao-de-dependencias.md) | Encapsular automaticamente serviços com interface que correspondem por namespace no container do MS.DI (tracing de beans). |
| [Integração com ASP.NET Core](guia-de-integracao-com-aspnet-core.md) | Conectar o middleware por requisição, os exportadores, a exclusão de rotas e o contexto de usuário. |
| [Clareza](guia-de-clareza.md) | Pontuar a qualidade dos nomes e impor isso como um gate de qualidade em CI. |
| [MSBuild e CLI](guia-de-msbuild-e-cli.md) | Executar os verbos do `dotnet-narrativetrace` e conectar o gate de clareza ao `dotnet build` / `dotnet test`. |

## Receitas

Uma página por problema, nas palavras que um usuário digitaria, cada uma com o código e a saída reais de uma execução.

- [Adicione logs a este serviço](../../pt-BR/receitas/adicione-logs-a-este-servico.md) — Levar cada chamada, com nomes de parâmetros e resultados, ao `ILogger` do projeto sem escrever uma instrução de log.
- [Ajude-me a depurar e corrigir este bug](../../pt-BR/receitas/ajude-me-a-depurar-e-corrigir-este-bug.md) — Encontrar a chamada que falta a um fluxo, corrigi-la e fixar o fluxo corrigido como linha de base aprovada.
- [Por que este teste falhou](../../pt-BR/receitas/por-que-este-teste-falhou.md) — Ler a narrativa de um teste vermelho diante da sua asserção e distinguir um bug de código de uma expectativa desatualizada.
- [Revise um fluxo complexo antes de alterá-lo](../../pt-BR/receitas/revise-um-fluxo-complexo-antes-de-altera-lo.md) — Ver o que um fluxo faz em `Detail` e em `Summary`, e o `.nt` sem valores que um agente lê.
- [Revise uma refatoração: o comportamento mudou?](../../pt-BR/receitas/revise-uma-refatoracao-o-comportamento-mudou.md) — Pegar uma chamada removida que os testes verdes não notaram, com o diff de aprovação.

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
