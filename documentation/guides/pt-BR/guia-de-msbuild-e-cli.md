<!-- source: documentation/guides/msbuild-cli.md blob 228d982ae965 | translated: 2026-09-26 | reviewed: 2026-09-03 -->
# NarrativeTrace .NET — Guia de MSBuild e CLI

[English](../msbuild-cli.md) | [Español](../es/guia-de-msbuild-y-cli.md) | **Português** | [简体中文](../zh-CN/MSBuild与CLI指南.md)

O NarrativeTrace traz duas superfícies de integração com o build: a
ferramenta de linha de comando `dotnet-narrativetrace` e o pacote
`NarrativeTrace.MSBuild` que a envolve. Juntas, elas transformam a clareza
dos nomes em um gate do build e encaminham a configuração de trace para o
host de testes.

A divisão de trabalho é deliberada: **toda decisão vive na CLI**; o pacote
do MSBuild é uma camada fina que apenas declara valores padrão e invoca a
ferramenta. Aprenda a CLI primeiro; a integração com o MSBuild decorre
naturalmente dela.

## Instalar a ferramenta

```bash
# Ferramenta global:
dotnet tool install --global NarrativeTrace.Cli

# …ou adicione-a a um manifesto local (recomendado para reprodutibilidade em CI):
dotnet new tool-manifest
dotnet tool install NarrativeTrace.Cli
```

Os targets do MSBuild invocam a ferramenta pelo nome
(`dotnet-narrativetrace`), então ela precisa estar no `PATH` (instalação
global) ou restaurada a partir de um manifesto local antes de você
executar `ClarityScan` / `ClarityCheck`.

## Verbos da CLI

`dotnet-narrativetrace <verbo> [opções]`. Sem verbo — ou com um
desconhecido — a ferramenta imprime o uso e sai com `2`. As opções são
interpretadas como pares `--nome valor` (flags como `--warn-only` não
recebem valor).

### `init` e `uninstall`

Copia as duas habilidades de agente para `.agents/skills/` (e também para `.claude/skills/`,
assim que detecta `.claude/` ou `CLAUDE.md`) e escreve uma seção marcada em `AGENTS.md`;
`uninstall` remove exatamente o que `init` escreveu, e nada mais. Zero rede — o portador viaja
dentro deste pacote.

```bash
dotnet tool run dotnet-narrativetrace init --dry-run
dotnet tool run dotnet-narrativetrace init
dotnet tool run dotnet-narrativetrace uninstall
```

| Opção | Significado |
|---|---|
| `--dry-run` | Mostra o plano e o diff unificado. Não grava nada, sempre sai com `0`. |
| `--write-existing` | Permissão para tocar em um `AGENTS.md` ou `CLAUDE.md` que já exista. |
| `--force` | Permissão para sobrescrever um diretório de habilidade que pertence a outra pessoa. |
| `--only <half>` | `skills` \| `agents-md`. As duas metades por padrão. |
| `--vendor <vendor>` | `claude` \| `none`. Detectado a partir do projeto por padrão. |
| `--from <carrier>` | Um diretório, um `.nupkg`, ou uma coordenada `Id/Version` já presente no cache local do NuGet. O portador embutido nesta CLI por padrão. |
| `--dir <path>` | O diretório do projeto. O diretório atual por padrão. |
| `--json` | Saída legível por máquina em vez de texto para humanos. |

Códigos de saída:

| Código | Quando |
|---|---|
| `0` | Aplicado, ou um dry run — um dry run sempre sai com `0`. |
| `1` | Pelo menos uma ação foi recusada (um `AGENTS.md`/`CLAUDE.md` já existente que `init` não toca sem `--write-existing`, ou um diretório de habilidade que pertence a outra pessoa sem `--force`), ou o portador ou o diretório do projeto não puderam ser lidos. |
| `2` | A linha de comando não pôde ser interpretada. |

Veja as [habilidades de agente](../../agent-skills.md) para saber o que é escrito e por quê.

### `clarity-scan`

Escaneamento somente por reflection de um assembly compilado, gerando um
relatório de clareza. Carrega o assembly em um contexto somente de
metadados e nunca o executa, então é seguro em CI.

```bash
dotnet-narrativetrace clarity-scan \
    --assembly bin/Release/net10.0/MyApp.dll \
    --output-dir narrativetrace \
    --format both
```

| Opção | Obrigatória | Padrão | Significado |
|---|---|---|---|
| `--assembly <caminho>` | sim | — | Assembly a ser escaneado. |
| `--output-dir <dir>` | não | `.` | Diretório onde os relatórios serão gravados (criado se não existir). |
| `--format <both\|md\|json>` | não | `both` | `json` grava `clarity-results.json`; `md` grava `clarity-report.md`; `both` grava ambos. |

Códigos de saída:

| Código | Quando |
|---|---|
| `0` | O escaneamento foi concluído e o(s) relatório(s) foi(ram) gravado(s). |
| `1` | Arquivo do assembly não encontrado. |
| `2` | Faltando `--assembly`, ou valor de `--format` desconhecido. |

### `clarity-aggregate`

Combina os artefatos `*.clarity.json` por teste (gravados em runtime pelas
integrações com frameworks de teste) encontrados em um diretório em um
único envelope `clarity-results.json`. Use isso quando você quiser que o
gate pontue **traces realmente capturados** — profundidade de chamadas e
aninhamento reais — em vez do escaneamento estático por reflection de
profundidade 1.

```bash
dotnet-narrativetrace clarity-aggregate \
    --input-dir narrativetrace \
    --output-dir narrativetrace
```

| Opção | Obrigatória | Padrão | Significado |
|---|---|---|---|
| `--input-dir <dir>` | sim | — | Diretório onde são procurados arquivos `*.clarity.json`. |
| `--output-dir <dir>` | não | valor de `--input-dir` | Onde `clarity-results.json` é gravado. |

Códigos de saída:

| Código | Quando |
|---|---|
| `0` | A agregação foi concluída (mesmo que nenhum arquivo tenha correspondido). |
| `1` | Diretório de entrada não encontrado. |
| `2` | Faltando `--input-dir`. |

### `clarity-check`

O gate. Interpreta um envelope `clarity-results.json` e falha quando algum
cenário pontua abaixo de `--min-score` ou tem mais problemas de severidade
HIGH do que `--max-high-issues` (contados sem diferenciar maiúsculas de
minúsculas).

```bash
dotnet-narrativetrace clarity-check \
    --results narrativetrace/clarity-results.json \
    --min-score 0.80 \
    --max-high-issues 0
```

| Opção | Obrigatória | Padrão | Significado |
|---|---|---|---|
| `--results <caminho>` | sim | — | Caminho para o envelope `clarity-results.json`. |
| `--min-score <x>` | não | `0.0` | Falha qualquer cenário que pontue abaixo deste valor geral. |
| `--max-high-issues <n>` | não | `2147483647` (`int.MaxValue`) | Falha qualquer cenário com mais problemas HIGH do que este número. |
| `--warn-only` | não | desativado | Rebaixa uma falha do gate para um aviso (saída `0`). |

Códigos de saída:

| Código | Quando |
|---|---|
| `0` | O gate passou, **ou** `--warn-only` foi definido, **ou** o arquivo de resultados estava ausente (um arquivo ausente é ignorado, não falha — um projeto que ainda não tem escaneamento não quebra o CI). |
| `1` | O gate falhou (um cenário está abaixo de `--min-score` ou acima de `--max-high-issues`). |
| `2` | Faltando `--results`, ou o arquivo de resultados é um JSON malformado. |

`--min-score` e `--max-high-issues` são interpretados com valores
numéricos de cultura invariável; um valor que não pode ser interpretado
volta para o seu padrão em vez de gerar erro.

## Integração com o MSBuild

Adicione o pacote somente de build. `PrivateAssets="all"` o mantém fora
das dependências transitivas do seu pacote:

```xml
<PackageReference Include="NarrativeTrace.MSBuild" Version="0.3.0"
                  PrivateAssets="all" />
```

### Propriedades

Sobrescreva qualquer uma delas no projeto consumidor ou na linha de
comando (`/p:Nome=Valor`). O pacote apenas declara valores padrão — toda a
lógica de limiares vive na CLI.

| Propriedade | Padrão | Propósito |
|---|---|---|
| `NarrativeTraceOutput` | `false` | Quando `true`, encaminha `NARRATIVETRACE_*` para o host de testes durante o `VSTest`. |
| `NarrativeTraceOutputDir` | `$(MSBuildProjectDirectory)/narrativetrace` | Onde os resultados de escaneamento/agregação e os traces são gravados. |
| `NarrativeTraceFormat` | `markdown` | Formato de saída do trace. Válidos: `markdown`, `text`, `mermaid`, `plantuml`. |
| `NarrativeTraceLevel` | `DETAIL` | Nível de captura do host de testes. Válidos: `OFF`, `ERRORS`, `SUMMARY`, `NARRATIVE`, `DETAIL`. |
| `NarrativeTraceClaritySource` | `scan` | Qual produtor o gate pontua: `scan` (escaneamento estático por reflection) ou `runtime` (agregação de traces capturados por teste). |
| `ClarityMinScore` | `0.0` | Encaminhado para `clarity-check --min-score`. |
| `ClarityMaxHighIssues` | `2147483647` | Encaminhado para `clarity-check --max-high-issues`. |
| `ClarityWarnOnly` | `false` | Quando `true`, encaminha `--warn-only` (falhas do gate se tornam avisos). |
| `NarrativeTraceRefreshSkills` | `true` | Quando `false`, ignora por completo a atualização automática de habilidades descrita abaixo. |

Valores inválidos de `NarrativeTraceLevel`, `NarrativeTraceFormat` ou
`NarrativeTraceClaritySource` fazem o build falhar **cedo** (via o target
`_NarrativeTraceValidateConfig`, antes de `Build` / `VSTest` /
`ClarityScan`) com uma mensagem clara, em vez de encaminhar
silenciosamente um erro de digitação para o host de testes.

> A lista de valores permitidos de `NarrativeTraceFormat` aqui
> (`markdown`/`text`/`mermaid`/`plantuml`) é o formato de renderização de
> trace do lado do build, que difere dos valores da variável de ambiente
> `NARRATIVETRACE_FORMAT` em runtime (`Markdown`/`Text`/`Prose`/`Json`)
> documentados no [Guia de configuração](guia-de-configuracao.md).

### Targets

| Target | Depende de | O que executa |
|---|---|---|
| `ClarityScan` | `Build` | `clarity-scan --assembly $(TargetPath) --output-dir $(NarrativeTraceOutputDir)` |
| `ClarityAggregate` | — | `clarity-aggregate --input-dir $(NarrativeTraceOutputDir) --output-dir $(NarrativeTraceOutputDir)` |
| `ClarityCheck` | `ClarityScan` ou `ClarityAggregate` (conforme `NarrativeTraceClaritySource`) | `clarity-check --results … --min-score … --max-high-issues … [--warn-only]` |
| `_NarrativeTraceRefreshSkills` | `Build` (antes, somente no build externo) | Atualiza as páginas de habilidades de agente já instaladas e a seção do `AGENTS.md` — nunca as cria |

Execute o gate como parte de um build:

```bash
# Produtor de escaneamento estático (padrão):
dotnet build /t:ClarityCheck /p:ClarityMinScore=0.80 /p:ClarityMaxHighIssues=0

# Pontuar traces realmente capturados em vez do escaneamento estático:
dotnet test   /p:NarrativeTraceOutput=true
dotnet build  /t:ClarityCheck /p:NarrativeTraceClaritySource=runtime /p:ClarityMinScore=0.80
```

Quando `NarrativeTraceClaritySource=runtime`, `ClarityCheck` depende de
`ClarityAggregate` (que combina os arquivos `*.clarity.json` por teste) em
vez de `ClarityScan`. Produza esses arquivos por teste executando a suíte
de testes primeiro com `NarrativeTraceOutput=true`, para que a agregação
tenha algo para combinar.

### Verificação incremental (arquivo stamp)

`ClarityCheck` é incremental. Ele declara:

- **Entradas:** `$(NarrativeTraceOutputDir)/clarity-results.json`
- **Saídas:** `$(NarrativeTraceOutputDir)/clarity-check.stamp`

Após uma verificação bem-sucedida, ele toca o arquivo `.stamp`. No próximo
build, o MSBuild compara os timestamps e **pula completamente a nova
verificação quando `clarity-results.json` não mudou** — assim um projeto
sem alterações não paga o gate duas vezes. Exclua o stamp (ou o diretório
de saída) para forçar uma nova verificação.

### Atualizando as habilidades de agente instaladas no build

Depois que um projeto executa `init` (acima) pelo menos uma vez, o `NarrativeTrace.MSBuild`
mantém as páginas de habilidades de agente instaladas e a seção do `AGENTS.md` atualizadas em
todo build externo — nunca as cria, então um projeto que nunca executou `init` nunca invoca a
ferramenta. O `_NarrativeTraceRefreshSkills` roda com `BeforeTargets="Build"`, condicionado de
três formas:

- **A página sentinela.** `.agents/skills/narrativetrace-doctor/SKILL.md` precisa ser encontrada
  subindo a partir do diretório do projeto (`GetDirectoryNameOfFileAbove`) — o único sinal que o
  target procura antes de invocar qualquer coisa.
- **Somente no build externo.** Os builds internos por TFM de um projeto multi-target são
  ignorados; a atualização roda uma única vez.
- **Como desativar.** Defina `NarrativeTraceRefreshSkills=false` para desativá-la por completo.

Quando roda, ela substitui apenas os blocos e as páginas que o próprio `init` desta implementação
escreveu — o equivalente à forma que aplica mudanças de `init --dry-run`, restrita ao conteúdo já
instalado — e imprime uma linha dizendo o que reescreveu. Um `dotnet-narrativetrace` ausente no
`PATH` avisa uma vez e continua; nunca falha o build.

### Saída de trace para o host de testes

Quando `NarrativeTraceOutput=true`, o target `_NarrativeTraceExportEnv`
(executado antes do `VSTest`) anexa a configuração de runtime a
`VSTestEnvironmentVariables`, de modo que o host de testes vê:

```
NARRATIVETRACE_OUTPUT=true
NARRATIVETRACE_OUTPUT_DIR=$(NarrativeTraceOutputDir)
NARRATIVETRACE_FORMAT=$(NarrativeTraceFormat)
NARRATIVETRACE_LEVEL=$(NarrativeTraceLevel)
```

```bash
dotnet test /p:NarrativeTraceOutput=true /p:NarrativeTraceLevel=NARRATIVE
```

## Receitas de CI

### Gate de clareza estático (sem necessidade de executar testes)

Escaneie o assembly compilado e falhe abaixo de um limiar — o gate mais
enxuto.

```bash
dotnet build -c Release
dotnet-narrativetrace clarity-scan \
    --assembly bin/Release/net10.0/MyApp.dll --output-dir narrativetrace
dotnet-narrativetrace clarity-check \
    --results narrativetrace/clarity-results.json \
    --min-score 0.80 --max-high-issues 0
```

Ou, deixando o MSBuild conduzir toda a cadeia em um único comando:

```bash
dotnet build /t:ClarityCheck /p:ClarityMinScore=0.80 /p:ClarityMaxHighIssues=0
```

### Gate de clareza em runtime (pontuar traces reais)

Execute os testes para emitir capturas por teste e, em seguida, aplique o
gate sobre a agregação:

```bash
dotnet test /p:NarrativeTraceOutput=true
dotnet build /t:ClarityCheck \
    /p:NarrativeTraceClaritySource=runtime \
    /p:ClarityMinScore=0.80 /p:ClarityMaxHighIssues=0
```

### Suba a régua sem quebrar o build

Reporte a clareza como um aviso enquanto você aumenta a pontuação, depois
desative `ClarityWarnOnly` quando você superar a régua:

```bash
dotnet build /t:ClarityCheck \
    /p:ClarityMinScore=0.85 /p:ClarityWarnOnly=true
```

## Veja também

- [Guia de instalação](guia-de-instalacao.md) — pacotes e caminhos de integração (Opções F e G)
- [Guia de configuração](guia-de-configuracao.md) — níveis de tracing, variáveis de ambiente e a referência de propriedades do MSBuild
- [Guia de clareza](guia-de-clareza.md) — o modelo de pontuação, o contrato `clarity-results.json` e a semântica do gate
</content>
