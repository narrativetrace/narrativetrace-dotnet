<!-- source: documentation/guides/clarity.md blob 6910fbf8f013 | translated: 2026-09-03 | reviewed: 2026-09-03 -->
# NarrativeTrace .NET — Guia de clareza

[English](../clarity.md) | [Español](../es/guia-de-claridad.md) | **Português** | [简体中文](../zh-CN/清晰度指南.md)

Se o trace *é* o código, então a qualidade do trace é a qualidade do
código. O pacote `NarrativeTrace.Clarity` analisa os nomes dos seus
métodos, classes e parâmetros, e pontua o quão bem eles comunicam a
intenção.

## Duas formas de pontuar

### A partir de um trace capturado (`ClarityAnalyzer`)

Analisa os nomes que realmente apareceram em uma execução:

```csharp
using NarrativeTrace.Clarity;

var result = ClarityAnalyzer.Analyze(context.CaptureTrace());
Console.WriteLine($"Overall clarity: {result.Overall:F2}");
```

### A partir de um assembly, sem executar nada (`ClarityScanner`)

Somente reflection — inspeciona os métodos públicos dos tipos informados
sem executá-los (seguro para CI):

```csharp
IReadOnlyDictionary<string, ClarityResult> results =
    ClarityScanner.Scan([typeof(OrderService), typeof(PaymentService)]);

foreach (var (typeName, result) in results)
{
    Console.WriteLine($"{typeName}: {result.Overall:F2}");
}
```

Os resultados são indexados por `Type.Name`. É também isso que a
[CLI `dotnet-narrativetrace`](#aplicação-no-build) usa.

## O que é pontuado

`ClarityResult` carrega uma pontuação geral mais cinco componentes
ponderados (todos entre `0.0` e `1.0`):

| Componente | Peso | O que mede |
|---|---|---|
| Método (`Method`) | 30% | Qualidade do verbo e especificidade dos tokens nos nomes de métodos. |
| Parâmetro (`Parameter`) | 25% | Especificidade de domínio em comparação com tokens de parâmetro genéricos ou sem sentido. |
| Classe (`Class`) | 20% | Qualidade do sufixo de papel e especificidade do prefixo nos nomes de classes. |
| Estrutural (`Structural`) | 15% | Penalidades por número de parâmetros e profundidade de chamadas. |
| Coesão (`Cohesion`) | 10% | Se os métodos se alinham com o sufixo de papel da classe. |

```csharp
public sealed record ClarityResult(
    double Overall, double Method, double Class,
    double Parameter, double Structural, double Cohesion,
    IReadOnlyList<ClarityIssue> Issues);
```

### A intuição da pontuação

- **Nomes de métodos** — o primeiro token é tratado como um verbo. Verbos
  de *domínio* (`calculate`, `validate`, `reserve`) pontuam mais alto;
  prefixos *booleanos* (`is`, `has`, `can`) pontuam alto; verbos *padrão*
  (`create`, `find`) ficam no meio; verbos *genéricos* (`get`, `process`,
  `handle`, `execute`) pontuam mais baixo. Tokens extras aumentam a
  especificidade (`reserveInventory` supera `reserve`).
- **Nomes de classes** — um prefixo de domínio mais um sufixo funcional
  pontua bem (`OrderService`); um sufixo isolado (`Service`) ou um
  `Manager` genérico pontua mal.
- **Nomes de parâmetros** — específico de domínio (`customerId`,
  `checkInDate`) supera genérico-tipado (`id`, `count`), que supera vago
  (`data`, `info`), que supera sem sentido (`x`, `tmp`).
- **Estrutural** — métodos com muitos parâmetros ou cadeias de chamadas
  profundas são penalizados.
- **Coesão** — os métodos são verificados em relação aos verbos esperados
  para o sufixo de papel da classe (espera-se que um `Repository` faça
  `find`/`save`/`delete`).

## Problemas

`ClarityResult.Issues` lista problemas específicos de nomenclatura como
`ClarityIssue(Severity, Description, Suggestion)`. Hoje o analisador
sinaliza **nomes de método com verbo genérico** como problemas de
severidade `high` — por exemplo, um método cujo primeiro token é
`get`/`process`/`handle`:

```
severity:    high
description: Generic verb 'process' in DataProcessor.process
suggestion:  Use a domain-specific verb
```

A severidade é um token de texto em minúsculas (`high`).
`ClarityReportRenderer` mapeia as severidades para ícones na saída em
Markdown.

## Seu próprio vocabulário, a partir do glossário que você já tem

Os dicionários integrados conhecem o inglês geral de software. Eles não
sabem que `Fold` é um verbo no seu domínio, que `Tranche` é um substantivo
preciso, ou que `Fx` é a abreviatura aceita da sua equipe para *foreign
exchange* — e um nome que eles não conhecem pontua como desconhecido, não
como específico de domínio.

Você os ensina com o arquivo de vocabulário que o seu repositório já
carrega: o `glossary.json` commitado (ADR-012). Não existe um segundo
arquivo de dicionário para manter sincronizado.

| Entrada do glossário | Tipo | O que a clareza aprende |
|---|---|---|
| `settle trade` | `verb-phrase` | `settle` é um verbo de domínio; `trade` é um substantivo de domínio |
| `credit tranche` | `noun-phrase` | `credit` e `tranche` são substantivos de domínio |
| `fx` | `word` | `fx` é um substantivo de domínio |

Termos de várias palavras ensinam um token de cada vez, porque os
identificadores são pontuados um token de cada vez. Todo contexto
delimitado contribui: um identificador não carrega namespace, então o
escopo por contexto não pode se aplicar no momento da pontuação.

### A abreviatura aceita é declarada, não inferida

A abreviatura que a sua equipe aceita vive em sua própria seção de nível
raiz, o que eleva o arquivo para `schemaVersion: 2`:

```json
{
  "schemaVersion": 2,
  "contexts": { "trading": { "packages": ["Acme.Trading"] } },
  "abbreviations": { "fx": "foreign exchange", "calc": "calculate" },
  "terms": []
}
```

A um token listado nunca se pede novamente que seja escrito por extenso,
e a expansão é aquilo *com que* as notas de ensino o escrevem por extenso
— `noun 'fx' (foreign exchange)`.

Só essa seção aceita abreviaturas. Um token que apenas aparece dentro de
um termo commitado (`calc` em `calc total`) é ensinado como substantivo de
domínio e continua sendo uma abreviatura, porque ninguém decidiu que ela
era aceita. Aceitar uma é uma decisão que alguém toma e revisa, não um
efeito colateral de uma coleta.

Duas regras do formato de arquivo decorrem disso:

- A seção é **de propriedade humana** — uma coleta nunca a escreve, e um
  merge a carrega intacta, assim como `definition` e `translations`.
- O selo `2` aparece **somente quando a seção tem entradas**, então um
  repositório que nunca usa esse recurso continua escrevendo o mesmo
  arquivo schema 1, byte a byte, que escrevia antes. Os leitores aceitam a
  seção em qualquer versão a partir da 1.

### O que o glossário não pode fazer

Os dicionários integrados mantêm sua autoridade. Um projeto pode ensinar
aos pontuadores uma palavra que eles não conhecem; não pode anular uma que
eles já conhecem.

- **Verbos genéricos continuam genéricos.** Commitar `process` ou `handle`
  não os promove, e o mesmo vale para prefixos booleanos (`is`, `has`).
- **Placeholders sem sentido continuam sem sentido.** `temp`, `foo` e
  companhia não são resgatados por serem escritos.
- **Sinônimos descontinuados nunca são vocabulário.** Um alias existe para
  ser sinalizado; promovê-lo silenciaria o problema `non-canonical-term`
  para o qual ele foi declarado.
- **Termos `stale` não são vocabulário.** Marcar um termo como stale diz
  que a palavra saiu do domínio.

Só conta o arquivo *commitado*. Nada que uma execução coleta realimenta as
pontuações dessa mesma execução — um vocabulário que se autoexpande
tornaria as pontuações não determinísticas e autocertificadas. O commit é
a aprovação humana.

### Onde se aplica

O fixture do xUnit, o relatório de suite do NUnit e o
`dotnet-narrativetrace clarity-scan` encontram o glossário pela mesma
busca ascendente que a coleta usa (`GlossarySettings.ResolveFile`):
`NARRATIVETRACE_GLOSSARY` indica um caminho explícito, `off` desliga o
recurso por completo e, caso contrário, prevalece o `glossary.json` mais
próximo acima do diretório de trabalho.

A leitura é incondicional onde quer que exista um glossário — ela não
muda nada em disco. Um repositório sem `glossary.json` pontua exatamente
como pontuava antes desse recurso existir, e um glossário que não pode ser
lido degrada para os dicionários integrados com um aviso no console, em
vez de fazer a suite falhar.

```csharp
var vocabulary = GlossaryVocabulary.FromFile(
    GlossarySettings.ResolveFile(
        Environment.GetEnvironmentVariable, Directory.GetCurrentDirectory()));

var result = ClarityAnalyzer.Analyze(tree, propertyNames, vocabulary);
```

## Saída do relatório

`ClarityReportRenderer.Render` produz uma tabela de pontuações em
Markdown entre cenários:

```csharp
var report = ClarityReportRenderer.Render(
[
    new ScenarioClarity("Order placement", orderResult),
    new ScenarioClarity("Legacy processing", legacyResult),
]);
```

## Aplicação no build

A clareza se torna um *gate*, não uma sugestão, por meio da CLI
`dotnet-narrativetrace` (ou do pacote `NarrativeTrace.MSBuild` que a
envolve).

```bash
# 1. Escaneia um assembly compilado para clarity-results.json:
dotnet-narrativetrace clarity-scan --assembly bin/Release/net10.0/MyApp.dll

# 2. Falha o build abaixo de um limiar ou acima de um orçamento de problemas HIGH:
dotnet-narrativetrace clarity-check --results clarity-results.json \
    --min-score 0.80 --max-high-issues 0
```

Códigos de saída do `clarity-check`: `0` sucesso (ou `--warn-only`), `1`
falha do gate, `2` erro de uso / resultados malformados. Um arquivo de
resultados ausente é **ignorado** (saída 0), não falha — assim um projeto
que ainda não escaneou não quebra a CI.

Para a integração com o MSBuild (targets `ClarityScan` / `ClarityCheck` e
as propriedades `ClarityMinScore` / `ClarityMaxHighIssues` /
`ClarityWarnOnly`), veja o [Guia de configuração](guia-de-configuracao.md#5-msbuild).

### Contrato JSON

`clarity-results.json` é o contrato entre o scan e o gate — um array de
objetos de cenário:

```json
[
  {
    "scenario": "OrderService",
    "overall": 0.85,
    "method": 0.90,
    "class": 0.95,
    "parameter": 0.80,
    "structural": 1.00,
    "cohesion": 0.70,
    "issues": [
      {
        "severity": "high",
        "description": "Generic verb 'process' in DataProcessor.process",
        "suggestion": "Use a domain-specific verb"
      }
    ]
  }
]
```

O gate conta os problemas de severidade HIGH sem diferenciar maiúsculas
de minúsculas, então tanto `high` (como o scanner emite) quanto `HIGH`
são respeitados.

## Componentes de NLP

O módulo de clareza usa NLP feito à mão, sem dependências externas:

| Componente | Finalidade |
|---|---|
| `IdentifierTokenizer` | Divide `camelCase` / `snake_case` em tokens. |
| `VerbDictionary` | Categoriza verbos (`Domain`, `Standard`, `Boolean`, `Generic`, `Unknown`). |
| `RoleSuffixDictionary` | Classifica sufixos de classe (padrão de projeto, funcional, genérico). |
| `GenericTokenDetector` | Classifica a especificidade dos tokens (sem sentido → específico de domínio). |
| `AbbreviationDictionary` | Pontua abreviaturas por nível (universal, bem conhecida, ambígua). |
| `MorphologyAnalyzer` | Detecta classe gramatical por meio de sufixos (`-tion`, `-ize`, `-able`). |
| `CollocationDictionary` | Reconhece frases de domínio comuns com múltiplos tokens. |
| `CohesionScorer` | Verifica o alinhamento entre o verbo do método e o papel da classe. |
| `MethodNameScorer` / `ClassNameScorer` / `ParameterNameScorer` / `StructuralScorer` | Os cinco pontuadores de componentes. |
| `DomainVocabulary` | As próprias palavras do projeto, lidas do glossário commitado; estende todos os dicionários acima sem sobrescrevê-los. |

## Veja também

- [Guia de instalação](guia-de-instalacao.md) — a ferramenta `dotnet-narrativetrace`
- [Guia de configuração](guia-de-configuracao.md) — o gate de clareza do MSBuild
- [Guia de atributos](guia-de-atributos.md) — nomes limpos primeiro, atributos depois
