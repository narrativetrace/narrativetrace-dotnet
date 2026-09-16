<!-- source: documentation/duplication.md blob 7ce9aac0fb0f | translated: 2026-09-13 | reviewed: - -->
# Detecção de duplicação

[English](../duplication.md) | [Español](../es/deteccion-de-duplicacion.md) | **Português** | [简体中文](../zh-CN/重复代码检测.md)

*(desde 0.1.5, não lançado)* — ferramenta de tempo de build, não um
comportamento da biblioteca em tempo de execução: nada aqui é distribuído
nos pacotes NuGet.

`./build.sh DuplicationReport` executa o [jscpd](https://github.com/kucherenko/jscpd)
(uma versão exata fixada, invocada via `npx jscpd@<versão>` — este
repositório não tem um equivalente JVM/PMD já presente no seu próprio
classpath como tem o CPD do runtime Java) sobre os fontes C# principais e
de teste, e escreve um relatório a cada commit. `./build.sh
DuplicationCheck` lê esse relatório e aplica a catraca de duplicação
descrita abaixo; faz parte de `Verify`.

## O que é medido

- **Linguagem: apenas C#, por enquanto** (o esquema comum a toda a família
  está descrito abaixo).
- **Piso de tokens: 60.** Uma correspondência abaixo de 60 tokens
  geralmente é coincidência — dois métodos não relacionados que por acaso
  compartilham uma forma curta e comum — não uma cópia estrutural que valha
  a pena tratar.
- **Identificadores e literais são ignorados.** As flags
  `--ignore-identifiers --ignore-literals` do jscpd então encontram
  duplicação *estrutural* (a mesma forma com nomes e valores diferentes),
  não apenas texto colado com os mesmos nomes — confirmado com um par de
  arquivos de teste que só difere em nomes de identificadores e valores
  literais: 0 clones sem as duas flags, 1 clone (46% das linhas do par) com
  elas, no mesmo piso de tokens. `--mode strict` é um eixo completamente
  diferente (desativa a normalização de comentários/espaços do jscpd) e
  não ignora, por si só, identificadores nem literais.
- **Os fontes principais e de teste são escaneados separadamente.** A
  árvore de testes (`tests/**` + `benchmarks/**`) é relatada — seus números
  estão em `duplication.json` e na linha de resumo — mas nunca faz
  `DuplicationCheck` falhar. O código de teste repete legitimamente
  (preparação, construtores de fixtures, blocos de asserção); um limite
  fixo ali seria ruído, não sinal.

## A catraca, não uma porcentagem fixa

Um único número de "falhar acima de N%" é o instrumento errado: o número
certo depende do piso de tokens e de quanto da árvore é naturalmente
repetitivo (tabelas de dados, código gerado), então um limite fixo acaba
sendo ou frouxo demais para disparar, ou apertado demais e bloqueia
trabalho não relacionado. Em vez disso, `DuplicationCheck` aplica uma
catraca contra uma linha de base registrada,
`config/duplication/baseline.properties`:

- **Falha quando a porcentagem da árvore principal sobe mais de 0,3 pontos
  percentuais** acima da linha de base registrada — uma pequena tolerância
  que absorve o ruído na contagem de tokens entre execuções, não
  crescimento real.
- **Falha quando um cluster não isento é maior que o maior cluster
  registrado na linha de base** — um novo bloco duplicado grande é um
  achado por si só, mesmo que a porcentagem geral permaneça estável.
- **A árvore de testes nunca faz o check falhar**, seja qual for sua
  porcentagem.

Abaixe a linha de base no mesmo commit que remove a duplicação que ela
registrou. Nunca a suba para fazer uma falha desaparecer — em vez disso,
adicione uma isenção justificada (abaixo), ou deixe o achado para uma
passada posterior.

## Isenções são dados

`config/duplication/exemptions.txt` lista duplicação deliberada: código
intencionalmente estruturado como duas cópias paralelas em vez de uma
abstração compartilhada. Cada entrada é um par `globA :: globB` (comparado
contra o caminho relativo à raiz do repositório que o jscpd relata, via
`Microsoft.Extensions.FileSystemGlobbing` — já presente no grafo desta
build através do Nuke.Common) com uma linha `# motivo` logo acima. Um
cluster só é isento quando *todas* as suas ocorrências correspondem a um
dos dois padrões do par — uma regra de negação por padrão, então um
cluster não classificado acima do piso é sempre um achado, nunca uma
aprovação silenciosa. Um par sem motivo acima, ou um par malformado, faz a
build falhar diretamente em vez de ser ignorado.

A única isenção registrada no primeiro escaneamento eram os dicionários de
listas de palavras do módulo clarity
(`src/NarrativeTrace.Clarity/*Dictionary.cs`). Uma decisão do proprietário
em 2026-09-12 (espelhando as mesmas categorias de isenção do runtime Java)
ampliou isso para três categorias justificadas, escritas como linhas
`globA :: globB` separadas em vez de um único padrão combinado (este motor
de globs não tem alternância de chaves): **tabelas de palavras
identificadas pelo arquivo** — as tabelas adjetivo/substantivo/verbo de
`TraceNamer.cs`, contra si mesmas e contra os dicionários de clarity;
**tabelas de palavras identificadas pela forma do conteúdo em vez do nome
do arquivo** — as listas de palavras `HashSet`/literal de array de
`GenericTokenDetector.cs` e `RedactionPolicy.cs`, que formam cluster entre
si e com os dicionários mesmo que nenhum dos dois arquivos corresponda ao
padrão `*Dictionary`; e **registros largos** — `CanonicalEntry.cs` e
`SpanContext.cs`, cada um um único registro posicional com um componente
anulável por campo do esquema (o equivalente deste runtime à classe
builder de um-setter-por-componente do runtime Java), isentos contra si
mesmos e entre si já que colapsar a forma de um componente por campo
mudaria a superfície pública do construtor. Cada um desses pares documenta
qualquer lacuna conhecida em que o mesmo padrão também cobre
(necessariamente) lógica real que vive ao lado dos dados que isenta, em
vez de ampliar silenciosamente o que "dados, não lógica" significa.

## Lendo o relatório

`artifacts/duplication/duplication.json` é o resultado normalizado (a
mesma forma que a ferramenta de duplicação de todo runtime NarrativeTrace
emite, para quaisquer linguagens que cubra):

```json
{"tool":"jscpd","language":"csharp","minTokens":60,
 "main":{"tokensTotal":N,"tokensDuplicated":N,"percent":x.y,
         "clusters":[{"tokens":N,"lines":N,
                       "occurrences":[{"file":"…","startLine":N,"endLine":N}]}]},
 "test":{"...":"same shape"}}
```

`tokensDuplicated` é uma **união**, não uma soma sobre clusters — contar
tokens `× ocorrências` conta duas e três vezes uma região coberta por
vários clusters (a mesma classe de bug que mediu mais de 300% de
duplicação no primeiro escaneamento do runtime Java, antes da correção por
união), então `percent` nunca pode ultrapassar 100%. O jscpd tokeniza cada
arquivo por conta própria em vez de em um único fluxo compartilhado para
todo o corpus como faz o CPD do PMD, e seu relatório JSON não carrega
nenhum índice de token por ocorrência — apenas uma linha de início/fim por
ocorrência, mais uma contagem de tokens para todo o fragmento
correspondente — então a união deste runtime opera sobre os intervalos de
linha próprios de cada arquivo em vez de um único espaço de índice de
tokens: as ocorrências são agrupadas por arquivo, intervalos que se
sobrepõem ou se tocam dentro de um mesmo arquivo são mesclados, e cada
trecho mesclado é contado uma única vez, com a *maior* contagem de tokens
entre os intervalos que o formaram, nunca a soma deles. Isso é exato
sempre que os intervalos duplicados de um arquivo forem idênticos ou não
se sobrepuserem — incluindo o caso comum de "um mesmo trecho, vários
parceiros" que uma soma ingênua calcula errado — e só conservador para uma
sobreposição dentro de um arquivo, genuinamente rara, entre dois clusters
diferentes. Veja `DuplicationReportSupport.UnionDuplicatedTokens` para o
raciocínio completo.

O log da build imprime uma linha de resumo por execução:

```
duplication: main 12.8% of tokens in 137 clusters (largest 1558 tokens
src/NarrativeTrace.Core/TraceNamer.cs:29 ↔ src/NarrativeTrace.Core/TraceNamer.cs:65)
· test 26.7% in 912 clusters (reported, not gated)
```

## Um Node/npx ausente nunca é uma aprovação silenciosa

O jscpd é um CLI Node externo, não uma biblioteca já presente no próprio
grafo desta build — então, ao contrário do PMD-CPD do runtime Java (uma
dependência de biblioteca, sempre presente), `DuplicationReport`/
`DuplicationCheck` pode encontrar um ambiente sem Node nenhum. Isso segue a
mesma convenção do `ScannerGateSupport` que `SecretsScan`/`Semgrep`/
`OsvScan` já usam: localmente, um `npx` ausente **avisa** e registra um
status `skipped` em `artifacts/duplication/scan-status/` — nunca um verde
silencioso; em CI, ou onde a ferramenta de duplicação for obrigatória, a
ausência **falha** a build. O próprio `DuplicationCheck` falha
ruidosamente (não "sem linha de base, então passa") quando
`DuplicationReport` não produziu nenhum `duplication.json` — regra 2 da
retrospectiva de release: uma ferramenta com pulo elegante precisa provar
que já rodou alguma vez.

Disponibilidade de Node por ambiente:

| Ambiente | Tem Node? |
|---|---|
| O container de desenvolvimento local | **Sim** — Node 22 (já provisionado nesse container) |
| GitHub Actions CI (`ci.yml`, `ubuntu-latest`) | **Sim** — pré-instalado na imagem do runner |
| CI privado (imagem `mcr.microsoft.com/dotnet/sdk`) | **Não** por padrão — o `before_script` do job `verify` instala o Node 22 via NodeSource, igual ao container de desenvolvimento |
| Container de verificação de publicação (o script de publicação, a mesma imagem SDK básica, sem root) | **Não** por padrão — um tarball do Node fixado é baixado no host e montado somente leitura dentro do container (não precisa de root/apt para uma execução de container sem root) |

## Adicionando uma isenção

1. Execute `./build.sh DuplicationReport` e encontre o cluster em
   `duplication.json` ou na linha de resumo.
2. Confirme que é deliberado — uma estrutura paralela genuína mantida à
   parte de propósito, não duplicação que ninguém teve tempo de remover.
3. Adicione uma linha `# motivo` e um par `globA :: globB` a
   `config/duplication/exemptions.txt`.
4. Execute `./build.sh DuplicationCheck` novamente para confirmar que
   passa.

## Abaixando a linha de base

Remova a duplicação, execute `./build.sh DuplicationReport`, e atualize
`main.percent` / `main.largestCluster` em
`config/duplication/baseline.properties` com os números recém-medidos no
mesmo commit — o mesmo idioma de "fixado no valor medido" que esta build já
usa para os pisos de cobertura e de pontuação de mutação.
