<!-- source: documentation/guides/configuration.md blob da0becfadddb | translated: 2026-09-13 | reviewed: - -->
# NarrativeTrace .NET — Guia de configuração

[English](../configuration.md) | [Español](../es/guia-de-configuracion.md) | **Português** | [简体中文](../zh-CN/配置指南.md)

Este guia documenta a configuração de runtime e de build do NarrativeTrace.

## Superfície de configuração

| Caminho | Mecanismo | Ideal para |
|---|---|---|
| Programática | Construtor de `NarrativeTraceConfig` | Qualquer app — controle direto |
| Ambiente | Variáveis `NARRATIVETRACE_*` (`ConfigResolver`) | CI, containers, hosts de teste |
| Injeção de dependências | `AddNarrativeTracing(options => …)` | Apps com MS.DI |
| ASP.NET Core | `AddNarrativeTrace(configuration)` + seção `"NarrativeTrace"` | Apps web |
| MSBuild | Propriedades de MSBuild `NarrativeTrace*` / `Clarity*` | Gate de clareza em tempo de build |

## 1. Níveis de tracing (`NarrativeTraceConfig`)

Um contexto é criado a partir de um `NarrativeTraceConfig`, que usa `Detail`
por padrão.

```csharp
using NarrativeTrace.Core;
using NarrativeTrace.Runtime;

var config = new NarrativeTraceConfig(TracingLevel.Narrative);
var context = new SyncNarrativeContext(config);
```

Níveis disponíveis (enum `TracingLevel`, do mais baixo ao mais alto):

| Nível | Comportamento |
|---|---|
| `Off` | Nenhum tracing é capturado. |
| `Errors` | Somente os caminhos com exceção são capturados. |
| `Summary` | Entrada raiz, folha mais profunda e cadeias de exceção completas. |
| `Narrative` | Fluxo de chamadas completo, com os valores de parâmetros suprimidos. |
| `Detail` | Fluxo de chamadas completo com valores de parâmetros e de retorno. |

O nível é mutável em runtime (`config.Level = TracingLevel.Errors;`).
`IsActive()` é `true` em `Errors` e acima (somente `Off` desativa a
captura); `CapturesParameterValues` é `true` apenas em `Detail`.

### Identidade do serviço

Estampe uma identidade de processo estável em cada span para correlação
entre serviços:

```csharp
var identity = new ServiceIdentity(
    ServiceName: "order-service",
    ServiceVersion: "2.0.0",
    Environment: "production");

var config = new NarrativeTraceConfig(TracingLevel.Detail, identity);
```

Eles aparecem como `service.name` / `service.version` /
`service.environment` na exportação JSON, nos escopos de logging e nos
spans do OpenTelemetry.

### Semeadura de traceparent *(since 0.1.4, unreleased)*

Semeie um [`traceparent`](https://www.w3.org/TR/trace-context/#traceparent-header)
W3C inicial para que todo contexto construído a partir de uma configuração
continue o trace de quem chamou em vez de iniciar o seu próprio — o
equivalente sem cabeçalho HTTP do que `NarrativeTraceMiddleware` adota de
uma requisição recebida (§4 abaixo):

```csharp
var config = new NarrativeTraceConfig(
    initialTraceparent: Traceparent.Parse("00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01"));
var context = new SyncNarrativeContext(config);
```

`Traceparent.Parse` nunca lança exceção — um valor malformado ou ausente
retorna `null`, que `NarrativeTraceConfig` trata como "sem semente", então
um contexto construído a partir dele recai para um trace novo, gerado
aleatoriamente. Fixo no momento da construção como `ServiceIdentity`: não
há setter, e ressemear um contexto em execução precisa de
`context.AdoptTraceparent(...)` diretamente em vez disso.

Deixe sem definir para tráfego de produção — todo contexto construído a
partir de uma `NarrativeTraceConfig` compartilhada adota o mesmo valor
fixo, o que é correto para um único contexto de nível superior (uma demo,
um script de execução única) mas anula todo o propósito do
`AsyncNarrativeContext` de dar a cada escopo seu próprio trace distinto.
Não combine os dois.

## 2. Variáveis de ambiente (`ConfigResolver`)

`ConfigResolver` lê seis variáveis — o canal de substituição nativo do
`.NET` que mantém o `Core` livre de dependências. Valores inválidos
**regridem para o padrão em vez de lançar uma exceção**, então uma
configuração incorreta nunca derruba a captura.

| Variável | Valores | Padrão |
|---|---|---|
| `NARRATIVETRACE_LEVEL` | `Off`, `Errors`, `Summary`, `Narrative`, `Detail` | `Detail` |
| `NARRATIVETRACE_OUTPUT` | `true` / `false` (ou `1` / `0`) | `true` |
| `NARRATIVETRACE_OUTPUT_DIR` | qualquer caminho com permissão de escrita | `TestResults/narrativetrace` |
| `NARRATIVETRACE_FORMAT` | `Markdown`, `Text`, `Prose`, `Json` | `Markdown` |
| `NARRATIVETRACE_CANONICAL_JSON` | `true` / `false` (ou `1`) | `false` |
| `NARRATIVETRACE_STRUCTURAL_JSON` | `true` / `false` (ou `1`) | `false` |
| `NARRATIVETRACE_APPROVAL` | `true` / `false` (ou `1`) | `false` |
| `NARRATIVETRACE_APPROVED_DIR` | qualquer caminho com permissão de escrita | `narratives` |

O parsing de nível e formato é tolerante (insensível a maiúsculas/minúsculas
e pontuação: `detail`, `DETAIL` e `Detail` resolvem igualmente).

`NARRATIVETRACE_OUTPUT` está **ativado por padrão** (decisão do
responsável, 2026-09-11) *(since 0.1.4, unreleased)*: os artefatos por teste que o fixture do xUnit e a
base do NUnit escrevem são a recompensa de adotar esta biblioteca, então a
escrita acontece sem nenhuma flag. Só um `NARRATIVETRACE_OUTPUT=false`
explícito (ou `0`) desativa; `true`/`1` são aceitos como no-op para scripts
que ainda os definem explicitamente. Sem uma sobrescrita de
`NARRATIVETRACE_OUTPUT_DIR`, os arquivos caem em
`TestResults/narrativetrace/` — a convenção do `.NET` que `dotnet test
--results-directory` e o Visual Studio/Rider já tratam como descartável, e
que o próprio `.gitignore` deste repositório já exclui.

```csharp
var resolved = ConfigResolver.Resolve();          // lê o ambiente do processo
var config = new NarrativeTraceConfig(resolved.Level);
```

`Resolve()` retorna um
`ResolvedConfig(Level, Output, OutputDir, Format, CanonicalJson, StructuralJson)`.

As duas últimas ativam os artefatos legíveis por máquina, gerados por
teste, escritos ao lado do arquivo de trace, seja qual for o formato
principal:

- `<test>.canonical.json` — o trace achatado em entradas do esquema
  canônico (um `method_enter` e um `method_exit` por chamada), para
  consumidores do esquema e para as fixtures de conformidade entre implementações.
- `<test>.structural.json` — o mesmo array com todos os valores de runtime
  elididos (ADR-002 nível 1), pensado para ser entregue a um consumidor de
  IA: os nomes de parâmetros sobrevivem; os valores de parâmetros, os
  valores de retorno e as mensagens de exceção não.

Ambos ficam desativados por padrão; são artefatos de máquina, não algo que
você lê ao lado do trace.

`NARRATIVETRACE_APPROVAL` ativa o modo de aprovação *(since 0.1.4,
unreleased)*: depois de um teste que **passa**, a estrutura sem valores
do cenário (o mesmo render do artefato `.nt`) é verificada contra a trace
aprovada commitada
`<approvedDir>/<TestClassSimpleName>/<artifact_name>.approved.nt` — a
mesma identidade de artefato de qualquer outro arquivo por teste, então
um método que roda mais de uma vez tem uma trace aprovada por invocação.
Uma trace aprovada ausente ou uma diferença estrutural falha o teste com
um diff legível e escreve a estrutura atual ao lado dela como
`*.received.nt`; revise-a e aceite-a via o build target `Approve`
(`./build.sh Approve`) ou renomeie-a manualmente. A estrutura de um teste
que falhou nunca é verificada — ela está no meio do caminho e não deve
agitar as traces recebidas. `NARRATIVETRACE_APPROVED_DIR` nomeia o
diretório onde essas linhas de base vivem (padrão `narratives`). Veja
[Formato de trace estrutural](../../structural-trace-format.md) para o
comportamento completo, e [O que incluir no commit](../../pt-BR/o-que-incluir-no-commit.md)
para saber quais desses arquivos commitar.

## 3. Injeção de dependências

`AddNarrativeTracing` envolve serviços registrados por interface cujo
namespace de implementação corresponde a um prefixo configurado (o
equivalente em `.NET` do empacotamento automático de beans do
Spring/Micronaut).

```csharp
using NarrativeTrace.DependencyInjection;

services.AddNarrativeTracing(options =>
{
    options.Level = TracingLevel.Detail;           // padrão: Detail
    options.Namespaces("MyApp.Orders", "MyApp.Payments");
});
```

Comportamento:

- Somente tipos de serviço que são **interface** são considerados;
  generics abertos são ignorados (uma limitação do `DispatchProxy`).
- A correspondência de namespace usa semântica de **fronteira por ponto**:
  `MyApp.Orders` corresponde a `MyApp.Orders` e a `MyApp.Orders.Sub`, mas
  não a `MyApp.OrdersLegacy`.
- O **tempo de vida** do registro original é preservado (um singleton
  continua singleton, etc.).
- Um `INarrativeContext` **scoped** é registrado automaticamente; todos os
  serviços envolvidos no mesmo scope o compartilham, então suas chamadas se
  aninham em uma única árvore.
- Para um serviço registrado por factory ou por instância, é usado o
  namespace da implementação concreta; se não estiver disponível, o
  namespace do tipo de serviço (a interface) é usado como fallback.

## 4. ASP.NET Core

`AddNarrativeTrace` vincula uma seção opcional de `IConfiguration` chamada
`NarrativeTrace` e depois deixa que um delegate a sobrescreva.

```csharp
builder.Services.AddNarrativeTrace(builder.Configuration, options =>
{
    options.Level = TracingLevel.Detail;
    options.ExcludedPaths.Add("/health");
    options.ExcludedPaths.Add("/metrics");
});
```

`appsettings.json`:

```json
{
  "NarrativeTrace": {
    "Level": "Detail",
    "ExcludedPaths": [ "/health", "/metrics" ]
  }
}
```

| Chave | Tipo | Finalidade |
|---|---|---|
| `Level` | `TracingLevel` | Nível de captura do contexto da requisição. |
| `ExcludedPaths` | `string[]` | Prefixos de rota totalmente ignorados (correspondência por segmento). |

`NarrativeTraceMiddleware` adota um cabeçalho de requisição `traceparent`
recebido automaticamente *(since 0.1.4, unreleased)* — sem opção para
desativar; um cabeçalho ausente, malformado ou de versão proibida é
ignorado e a requisição recebe um trace recém-gerado, exatamente como a
via semeada por configuração acima, mas conduzida pelo cabeçalho de quem
chamou em vez de um valor fixo.

Consulte o
[Guia de integração com ASP.NET Core](guia-de-integracao-com-aspnet-core.md)
para a fiação do middleware e do exportador.

## 5. MSBuild

O pacote `NarrativeTrace.MSBuild` é uma camada fina sobre a CLI
`dotnet-narrativetrace` — toda a lógica de limiares vive na CLI. Ele
declara estas propriedades sobrescrevíveis:

| Propriedade | Padrão | Finalidade |
|---|---|---|
| `NarrativeTraceOutput` | `false` | Emite `NARRATIVETRACE_OUTPUT=true` para o host de testes. |
| `NarrativeTraceOutputDir` | `$(MSBuildProjectDirectory)/narrativetrace` | Onde resultados/traces são escritos. |
| `NarrativeTraceFormat` | `markdown` | Formato de saída do trace. |
| `NarrativeTraceLevel` | `DETAIL` | Nível de captura do host de testes. |
| `ClarityMinScore` | `0.0` | Falha o build abaixo desta pontuação geral. |
| `ClarityMaxHighIssues` | `2147483647` | Falha o build acima desta contagem de problemas HIGH. |
| `ClarityWarnOnly` | `false` | Rebaixa a falha de um gate para um aviso. |

Ele também registra dois targets:

- **`ClarityScan`** (depende de `Build`) — escaneamento somente por
  reflexão do assembly compilado, gerando `clarity-results.json`.
- **`ClarityCheck`** (depende de `ClarityScan`) — executa o gate; é
  incremental via um arquivo `.stamp`, então uma nova verificação é
  ignorada quando `clarity-results.json` não mudou.

```bash
# Impõe um limiar como parte do build:
dotnet build /t:ClarityCheck /p:ClarityMinScore=0.80 /p:ClarityMaxHighIssues=0
```

A ferramenta precisa estar instalada
(`dotnet tool install --global NarrativeTrace.Cli`, ou por meio de um
manifesto de ferramentas local).

## 6. Ocultação

A renderização reflexiva de valores **oculta por padrão, não vaza por
padrão**. Quando um objeto traçado não tem um resumo dedicado, o
`ValueRenderer` reflete sobre suas propriedades — mas uma lista de
negação baseada em nome (`RedactionPolicy.Default`) substitui por
`[REDACTED]`, antes da renderização, os valores cujo nome de campo
corresponde a um padrão sensível (`password`, `secret`, `token`, `apikey`,
`cvv`, `ssn`, `authorization`, `credential`, `privatekey`, `cardNumber`,
`jwt`, `cookie`, `setCookie`, `sessionId`, `accountNumber`,
`routingNumber`, `pan`, `iban`, …).

```csharp
var options = new RenderOptions(
    MaxStringLength: 200,
    MaxArrayItems: 5,
    MaxObjectKeys: 5,
    MaxDepth: 4,
    Redaction: RedactionPolicy.OfPatterns(["ssn", "pan"]));  // lista de negação personalizada
```

- `RedactionPolicy.Default` — a lista de negação embutida.
- `RedactionPolicy.OfPatterns(...)` — seus próprios padrões de substring,
  insensíveis a maiúsculas/minúsculas.
- `RedactionPolicy.Disabled` — desativa a ocultação (os valores são
  renderizados literalmente, e também desativa o mascaramento por forma de
  valor descrito abaixo).

A correspondência é por substring para todo padrão **exceto `pan` e
`iban`**, que são comparados por limites de token do identificador — como
substring, `pan` também ocultaria `companyName`, `planId` e `spanCount`, e
`iban` capturaria campos de negócio comuns da mesma forma. `cardPan` e
`ibanNumber` continuam correspondendo (o padrão é um token inteiro dentro
do nome); `companyName` não.

**O mascaramento por forma de valor é um segundo eixo, independente do
nome.** Não importa como um campo se chama (ou se tem nome — um valor de
retorno direto, um item de lista), uma string é ocultada quando sua
estrutura se parece com um JWT (três segmentos base64url, o primeiro
começando com `eyJ`), um número de cartão de pagamento válido pelo
algoritmo de Luhn (13–19 dígitos, separadores `[ -]` tolerados), ou um
valor de cabeçalho HTTP `Set-Cookie` (um par `nome=valor` seguido de um
atributo nomeado como `Path=` ou `Secure`). É deliberadamente estreito —
sem heurísticas de entropia — então um identificador numérico comum que
por acaso falha na verificação de Luhn permanece visível.
`RedactionPolicy.OfPatterns(...)` mantém o mascaramento por forma de valor
ativo (uma lista de nomes personalizada não é uma opinião sobre se essas
formas de bytes são uma credencial); somente `RedactionPolicy.Disabled` o
desativa.

Para **parâmetros** sensíveis, prefira o atributo
[`[NotTraced]`](guia-de-atributos.md#nottraced) — ele oculta por posição,
independentemente do nome.

**Conectando uma política personalizada ao caminho do proxy.** O
`RenderOptions` acima é o que `ValueRenderer.Render` recebe quando você o
chama diretamente; alcançar o caminho de captura *distribuído* do
`NarrativeTraceProxy` é um passo separado, através de
`ProxyOptions.Redaction` *(since 0.1.4, unreleased)*:

```csharp
var proxy = NarrativeTraceProxy.Create<IOrderService>(
    new OrderService(), context,
    new ProxyOptions(Redaction: RedactionPolicy.OfPatterns(["ssn", "holderName"])));
```

Dada explicitamente, a política *substitui* a decisão padrão baseada em
nome — tanto para o nome do próprio parâmetro do proxy quanto para os
nomes de propriedade refletidos de um objeto aninhado — em vez de
ampliá-la, então `RedactionPolicy.Disabled` aqui realmente desativa a
ocultação baseada em nome de ponta a ponta (`[NotTraced]` ainda oculta de
qualquer forma). Deixe `Redaction` sem definir (o padrão) e um proxy se
comporta exatamente como antes.

O encapsulamento automático de DI e a integração ASP.NET Core expõem o
mesmo gancho *(since 0.1.4, unreleased)*: `NarrativeTracingDiOptions.Redaction`
em [`AddNarrativeTracing`](guia-de-injecao-de-dependencias.md) alcança
cada serviço que essa chamada encapsula, e `NarrativeTraceOptions.Redaction`
em [`AddNarrativeTrace`](guia-de-integracao-com-aspnet-core.md) também
alcança os proxies auto-encapsulados quando os dois estão registrados no
mesmo app — uma política configurada de um dos lados fica visível para o
outro, já que os dois pacotes compartilham uma única coleção de serviços.
O próprio `Redaction` de `AddNarrativeTracing`, quando definido, tem
prioridade para os serviços que encapsula. Veja
[Privacidade e ocultação](../../pt-BR/privacidade-e-ocultacao.md) para o
quadro completo, superfície por superfície.

**Ampliando todas as superfícies de uma vez, sem mudar código.**
`NARRATIVETRACE_REDACTION_ADDITIONALPATTERNS` é uma lista de padrões de
nome de campo separados por vírgula que
é unida a `RedactionPolicy.Default` em si:

```bash
NARRATIVETRACE_REDACTION_ADDITIONALPATTERNS=holderName,betalingskort
```

Diferente de `ProxyOptions.Redaction`, isso só pode adicionar padrões,
nunca remover os embutidos, e alcança toda superfície da tabela acima —
incluindo o encapsulamento automático de DI e o middleware do ASP.NET
Core, que não têm nenhum gancho por chamada. É lido uma única vez, em um
campo `static readonly`, então precisa ser definido antes de qualquer
coisa no processo tocar em `RedactionPolicy` pela primeira vez (o próprio
código de inicialização de um app, ou um teste que o define em tempo de
execução, chegam tarde demais).

## 7. Ponte de logging (`Microsoft.Extensions.Logging`)

Encaminhe eventos de trace pelo seu logging existente com o pacote
`NarrativeTrace.Logging`. `LoggingNarrativeContext` decora qualquer
contexto e registra cada entrada/retorno/exceção via um `ILogger`:

```csharp
using NarrativeTrace.Logging;

var inner = new SyncNarrativeContext(new NarrativeTraceConfig());
INarrativeContext context = new LoggingNarrativeContext(inner, logger);
```

Para exportar uma árvore concluída uma única vez, use `TraceLogExporter`:

```csharp
TraceLogExporter.ExportToLogger(context.CaptureTrace(), logger);
```

**Uma nota sobre a ordem com o logger de console padrão.** Se o `logger`
acima é apoiado por `Microsoft.Extensions.Logging.Console`, suas linhas são
escritas por meio de uma fila em segundo plano por padrão, então podem
aparecer depois, ou intercaladas de forma estranha com, uma saída que seu
processo escreve de forma síncrona (`Console.Write*`, outro provedor, um
test runner capturando stdout) — um comportamento da plataforma próprio do
provedor de console, não um defeito do NarrativeTrace. A correção que
realmente funciona: libere (`Dispose`) o `ILoggerFactory` (ou o provedor de
console) — mais simplesmente com `using var loggerFactory =
LoggerFactory.Create(...)` — antes que algo dependa da ordem; `Dispose()`
bloqueia até que a thread escritora em segundo plano do provedor tenha
esvaziado tudo o que estava na fila. Veja o passo ["Envie para o seu
logger" do tutorial de sessenta segundos](../../pt-BR/sessenta-segundos.md#envie-para-o-seu-logger)
para um exemplo verificado e trabalhado.

Em uma aplicação hospedada, conecte a ponte do **fluxo de eventos** via DI
em vez de construí-la você mesmo:

```csharp
services.AddNarrativeLogging();   // chame por último, assim como AddNarrativeTracing
```

Ele registra `LoggingTraceEventListener` como um singleton construído a
partir do `ILoggerFactory` registrado, e o conecta a qualquer fluxo de
eventos `IEventSubscribable` registrado, de modo que o fluxo chega já
narrando. Duas condições o silenciam, ambas deliberadamente sem lançar
exceção: nenhum `ILoggerFactory` está registrado (a ponte não teria onde
escrever), ou `NARRATIVETRACE_NARRATION=off` veta a narração — a grafia
.NET do `narrativetrace.narration=off` do Java. `off` é o único valor que
veta, então um erro de digitação deixa você narrando em vez de silenciado
sem aviso.

Código bem estruturado — métodos pequenos com nomes claros, valores
calculados retornados em vez de logados — precisa de poucas chamadas de
log manuais; o NarrativeTrace captura a história a partir das assinaturas
e dos valores de retorno. Misture chamadas a `ILogger` apenas para decisões
que não afloram nas fronteiras dos métodos, e remova-as à medida que você
refatora.

**Uma execução da suíte de testes também tem um nome**
*(desde 0.1.4, não lançado)*: enquanto `NarrativeTrace.Testing.Xunit` ou
`NarrativeTrace.Testing.NUnit` tiver uma execução de suíte ativa, ambas as
pontes acima adicionam também `nt.runName` ao escopo — a frase de três
palavras da própria execução, ao lado de `nt.traceId`/`nt.traceName`, de
modo que um único grep encontra as linhas de log de uma execução.
Totalmente ausente fora de uma execução rastreada. A mesma identidade
também nomeia o rodapé de console da suíte (`run: bold elk soars`) e o
objeto `run` de nível superior do `manifest.json`, e prefixa o
frontmatter do documento Markdown do trace (`run:`) e a linha de abertura
dos renderizadores de texto/prosa — nunca o artefato estrutural `.nt`, sem
valores (veja
[Formato de Trace Estrutural](../../structural-trace-format.md)).

## 8. TracingLevel versus nível de logging

São dois filtros independentes. O **TracingLevel** controla o que é
*registrado* na árvore de trace (e, portanto, o overhead da captura). O
**nível de logging** controla o que uma ponte de logging *emite*. Uma
chamada filtrada pelo TracingLevel nunca chega à árvore, aos
renderizadores, nem a nenhum logger.

| Objetivo | Ajuste |
|---|---|
| Reduzir o ruído de logs | Aumente o nível do logger (a árvore continua sendo capturada para arquivos/renderizadores). |
| Reduzir o tamanho do trace | Diminua o `TracingLevel` (`Narrative` → `Summary`). |
| Reduzir CPU/memória | Diminua o `TracingLevel` — o nível de logging não afeta o custo de captura. |

## 9. Valores padrão recomendados por ambiente

| Ambiente | Nível | Saída |
|---|---|---|
| Trabalho local em funcionalidades | `Detail` | ativada por padrão, `FORMAT=Markdown` |
| Execuções de teste em CI | `Narrative` ou `Summary` | ativada por padrão, `FORMAT=Markdown` |
| Produção sensível a desempenho | `Errors` (ou `Off`) | nenhum fixture de teste roda aqui — sem saída em arquivo |

## 10. Exemplos práticos das variáveis de ambiente

Todas as variáveis `NARRATIVETRACE_*` lidas pelo código, em um só
lugar — o que cada uma configura, seu valor padrão e uma demonstração
executável do efeito. As variáveis do `ConfigResolver` são apresentadas
no §2 acima; as demais são apresentadas onde vivem (ocultação no §6,
ponte de logging no §7); esta seção é a companheira com exemplos
práticos de todas elas.

| Variável | Configura | Padrão |
|---|---|---|
| `NARRATIVETRACE_LEVEL` | O `TracingLevel` com o qual um contexto captura. | `Detail` |
| `NARRATIVETRACE_OUTPUT` | Se os artefatos de trace por teste são gravados em disco. | `true` |
| `NARRATIVETRACE_OUTPUT_DIR` | O diretório sob o qual os artefatos de trace são gravados. | `TestResults/narrativetrace` |
| `NARRATIVETRACE_FORMAT` | O formato principal do artefato (`Markdown`/`Text`/`Prose`/`Json`). | `Markdown` |
| `NARRATIVETRACE_CANONICAL_JSON` | Se o array de entradas canônico por teste também é gravado. | `false` |
| `NARRATIVETRACE_STRUCTURAL_JSON` | Se o array de entradas sem valores por teste também é gravado. | `false` |
| `NARRATIVETRACE_APPROVAL` | Se a estrutura de um teste que passa é conferida contra sua linha de base `*.approved.nt` versionada. | `false` |
| `NARRATIVETRACE_APPROVED_DIR` | O diretório onde vivem as linhas de base de aprovação. | `narratives` |
| `NARRATIVETRACE_REDACTION_ADDITIONALPATTERNS` | Padrões de nome de campo, separados por vírgula, unidos ao `RedactionPolicy.Default`. | *(nenhum)* |
| `NARRATIVETRACE_NARRATION` | Veta a ponte de logging quando definida como `off`; qualquer outro valor a mantém ativa. | *(não definida — narrando)* |
| `NARRATIVETRACE_GLOSSARY` | Um caminho de arquivo de glossário explícito para a coleta da suíte, ou `off` para desabilitá-la completamente. | Busca ascendente a partir da execução do teste por `glossary.json` |
| `NARRATIVETRACE_GLOSSARY_PATH` | Um arquivo de glossário a carregar para tradução ao vivo, substituindo o arquivo ao lado da app implantada. | O `glossary.json` ao lado da app, se existir |

Cada exemplo prático abaixo roda através da mesma sobrecarga de leitor
injetado que `ConfigResolver`/`GlossarySettings`/`GlossaryLoader`/
`AddNarrativeLogging` já expõem para os testes (`Resolve(Func<string,
string?> read, …)` e semelhantes) — o mecanismo que permite à
demonstração definir exatamente uma variável sem tocar no ambiente real
do processo. No seu próprio shell, defina a variável de verdade; o
efeito observável é idêntico em ambos os casos.

### `NARRATIVETRACE_LEVEL`

```bash
export NARRATIVETRACE_LEVEL=Narrative
```

```csharp
var resolved = ConfigResolver.Resolve(key => key == "NARRATIVETRACE_LEVEL" ? "Narrative" : null);
Console.WriteLine($"resolved.Level == TracingLevel.{resolved.Level}");
```

Efeito observável — o nível resolvido reflete a variável, interpretada de forma tolerante:

```text
resolved.Level == TracingLevel.Narrative
```

### `NARRATIVETRACE_OUTPUT`

```bash
export NARRATIVETRACE_OUTPUT=false
```

```csharp
var resolved = ConfigResolver.Resolve(key => key == "NARRATIVETRACE_OUTPUT" ? "false" : null);
Console.WriteLine($"resolved.Output == {resolved.Output}");
```

Efeito observável — apenas um `false`/`0` explícito desativa a gravação (o §2 acima detalha toda a tolerância):

```text
resolved.Output == False
```

### `NARRATIVETRACE_OUTPUT_DIR`

```bash
export NARRATIVETRACE_OUTPUT_DIR=artifacts/traces
```

```csharp
var resolved = ConfigResolver.Resolve(key => key == "NARRATIVETRACE_OUTPUT_DIR" ? "artifacts/traces" : null);
Console.WriteLine($"resolved.OutputDir == \"{resolved.OutputDir}\"");
```

Efeito observável — o caminho configurado passa tal como está (aparado; em branco é tratado como não definido):

```text
resolved.OutputDir == "artifacts/traces"
```

### `NARRATIVETRACE_FORMAT`

```bash
export NARRATIVETRACE_FORMAT=Json
```

```csharp
var resolved = ConfigResolver.Resolve(key => key == "NARRATIVETRACE_FORMAT" ? "Json" : null);
Console.WriteLine($"resolved.Format == OutputFormat.{resolved.Format}");
```

Efeito observável — o formato principal do artefato muda em relação ao padrão `Markdown`:

```text
resolved.Format == OutputFormat.Json
```

### `NARRATIVETRACE_CANONICAL_JSON`

```bash
export NARRATIVETRACE_CANONICAL_JSON=true
```

```csharp
var resolved = ConfigResolver.Resolve(key => key == "NARRATIVETRACE_CANONICAL_JSON" ? "true" : null);
Console.WriteLine($"resolved.CanonicalJson == {resolved.CanonicalJson}");
```

Efeito observável — o artefato de máquina `<test>.canonical.json` por teste passa a ser gravado junto ao formato principal:

```text
resolved.CanonicalJson == True
```

### `NARRATIVETRACE_STRUCTURAL_JSON`

```bash
export NARRATIVETRACE_STRUCTURAL_JSON=true
```

```csharp
var resolved = ConfigResolver.Resolve(key => key == "NARRATIVETRACE_STRUCTURAL_JSON" ? "true" : null);
Console.WriteLine($"resolved.StructuralJson == {resolved.StructuralJson}");
```

Efeito observável — o array sem valores `<test>.structural.json` por teste passa a ser gravado:

```text
resolved.StructuralJson == True
```

### `NARRATIVETRACE_APPROVAL`

```bash
export NARRATIVETRACE_APPROVAL=true
```

```csharp
var resolved = ConfigResolver.Resolve(key => key == "NARRATIVETRACE_APPROVAL" ? "true" : null);
Console.WriteLine($"resolved.Approval == {resolved.Approval}");
```

Efeito observável — a estrutura de um teste que passa agora é conferida contra sua linha de base versionada (veja [Formato de Trace Estrutural](../../structural-trace-format.md)):

```text
resolved.Approval == True
```

### `NARRATIVETRACE_APPROVED_DIR`

```bash
export NARRATIVETRACE_APPROVED_DIR=baselines
```

```csharp
var resolved = ConfigResolver.Resolve(key => key == "NARRATIVETRACE_APPROVED_DIR" ? "baselines" : null);
Console.WriteLine($"resolved.ApprovedDir == \"{resolved.ApprovedDir}\"");
```

Efeito observável — as linhas de base de aprovação agora são lidas e gravadas em `baselines/` em vez do padrão `narratives`:

```text
resolved.ApprovedDir == "baselines"
```

### `NARRATIVETRACE_REDACTION_ADDITIONALPATTERNS`

Diferente das variáveis acima, não há código para mudar no ponto de
chamada — o `RedactionPolicy.Default` lê essa variável sozinho, uma
única vez, em um campo `static readonly` (§6 acima). Isso significa que
a demonstração precisa rodar em seu **próprio processo**, iniciado com a
variável já definida, em vez de através do mecanismo de leitor injetado
que os outros exemplos usam — então este é verificado manualmente em vez
de conferido contra um arquivo-fonte versionado, seguindo a mesma regra
8 que sustenta todos os outros exemplos desta página (uma execução real
e compilada, não um palpite digitado):

```bash
export NARRATIVETRACE_REDACTION_ADDITIONALPATTERNS=holderName,betalingskort
```

```csharp
using NarrativeTrace.Core;

Console.WriteLine(RedactionPolicy.Default.ShouldRedact("holderName"));
Console.WriteLine(RedactionPolicy.Default.ShouldRedact("password"));
```

Efeito observável — rode uma vez com a variável não definida e outra com
ela definida (`dotnet run` duas vezes, em duas invocações de processo
separadas):

```text
# não definida
False
True

# NARRATIVETRACE_REDACTION_ADDITIONALPATTERNS=holderName,betalingskort
True
True
```

`password` é ocultado nos dois casos (já está na lista de bloqueio
embutida); `holderName` só é ocultado depois que a variável a amplia.
Definir a variável *depois* que algo no processo já tocou
`RedactionPolicy` não tem efeito — veja o §6 acima.

### `NARRATIVETRACE_NARRATION`

```bash
export NARRATIVETRACE_NARRATION=off
```

```csharp
var services = new ServiceCollection();
services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);

services.AddNarrativeLogging(null, key => key == "NARRATIVETRACE_NARRATION" ? "off" : null);

var listener = services.BuildServiceProvider().GetService<LoggingTraceEventListener>();
Console.WriteLine($"listener is null == {listener is null}");
```

Efeito observável — `off` (sem diferenciar maiúsculas/minúsculas) veta o
registro completamente, mesmo com um `ILoggerFactory` presente; qualquer
outro valor, incluindo um erro de digitação, mantém a narração ativa:

```text
listener is null == True
```

### `NARRATIVETRACE_GLOSSARY`

```bash
export NARRATIVETRACE_GLOSSARY=off
```

```csharp
var resolved = GlossarySettings.ResolveFile(
    key => key == "NARRATIVETRACE_GLOSSARY" ? "off" : null, root);
Console.WriteLine($"resolved == {(resolved is null ? "null (harvesting disabled)" : resolved)}");
```

(`root` acima é o diretório a partir do qual a busca ascendente
começaria — na prática, o diretório de trabalho de uma execução de
teste.) Efeito observável — o valor literal `off` desabilita a coleta
completamente, sobrepondo a busca ascendente por `glossary.json` mesmo
quando um arquivo seria encontrado de outra forma:

```text
resolved == null (harvesting disabled)
```

### `NARRATIVETRACE_GLOSSARY_PATH`

```bash
export NARRATIVETRACE_GLOSSARY_PATH=/srv/app/committed-glossary.json
```

```csharp
var loaded = GlossaryLoader.Load(
    key => key == "NARRATIVETRACE_GLOSSARY_PATH" ? overridePath : null, root);
Console.WriteLine($"loaded from override == {loaded is not null}");
```

(`overridePath` acima é o arquivo nomeado pela variável; `root` é o
diretório base da aplicação implantada.) Efeito observável — a
substituição vence qualquer `glossary.json` ao lado da aplicação
implantada:

```text
loaded from override == True
```

## Veja também

- [Guia de instalação](guia-de-instalacao.md) — pacotes e caminhos de integração
- [Guia de atributos](guia-de-atributos.md) — atributos de ocultação e narração
- [Guia de integração com ASP.NET Core](guia-de-integracao-com-aspnet-core.md) — middleware e exportadores
- [Guia de clareza](guia-de-clareza.md) — o gate `Clarity*` de MSBuild/CLI
