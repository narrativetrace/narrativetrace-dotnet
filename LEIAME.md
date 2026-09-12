<!-- source: README.md blob b8eda103f5c0 | translated: 2026-09-12 | reviewed: - -->
# NarrativeTrace .NET

[English](README.md) | [Español](LEAME.md) | **Português** | [简体中文](自述文件.md)

## Comece aqui

[Veja um trace em 60 segundos](documentation/pt-BR/sessenta-segundos.md)
— um aplicativo de console, uma execução, e o trace está no seu terminal.

## Demo

Clone o repositório e execute `./demo.sh`.

## Exemplos

Veja [os exemplos](examples/) — o NarrativeTrace em aplicações realistas.

## O código é o log

**Transforme o que seu código *fez* em uma história legível.** O
NarrativeTrace registra a execução de métodos como um trace narrativo — um
relato aninhado e legível por humanos das chamadas, argumentos, resultados e
tempos por trás de uma unidade de trabalho — e pontua a *clareza* da sua
nomenclatura para que código ilegível seja sinalizado antes de ir para
produção.

Uma arquitetura migration-first: a mesma biblioteca roda no .NET moderno,
`netstandard2.0` e no legado `net48`.

## O problema

Metade deste método é ruído de logging:

```csharp
public OrderResult PlaceOrder(string customerId, string productId, int quantity)
{
    _logger.LogInformation("Placing order for {Customer} product {Product} qty {Qty}",
        customerId, productId, quantity);

    var customer = _customers.FindCustomer(customerId);
    var unitPrice = _catalog.LookupPrice(productId);
    _logger.LogDebug("Priced {Product} at {Price}", productId, unitPrice);

    _inventory.Reserve(productId, quantity);
    var payment = _payments.Charge(customerId, unitPrice * quantity, $"tok_{customer.Id}");
    _logger.LogInformation("Payment processed: {Txn}", payment.TransactionId);
    return new OrderResult(NextOrderId(), payment.TransactionId, unitPrice * quantity, quantity);
}
```

A lógica de negócio é um punhado de linhas; o logging é outro punhado. Cada
desenvolvedor escreve esses logs de um jeito diferente — mensagens, níveis e
valores incluídos diferentes. O resultado é inconsistente, verboso e
emaranhado com o código que descreve.

O NarrativeTrace elimina isso. Escreva lógica de negócio pura:

```csharp
public OrderResult PlaceOrder(string customerId, string productId, int quantity)
{
    var customer = _customers.FindCustomer(customerId);
    var unitPrice = _catalog.LookupPrice(productId);
    _inventory.Reserve(productId, quantity);
    var payment = _payments.Charge(customerId, unitPrice * quantity, $"tok_{customer.Id}");
    return new OrderResult(NextOrderId(), payment.TransactionId, unitPrice * quantity, quantity);
}
```

O trace é gerado automaticamente a partir dos nomes de métodos, nomes de
parâmetros e valores de retorno — a informação que já estava lá:

```
OrderService.PlaceOrder(customerId: "cust-1", productId: "book-123", quantity: 2)
  CustomerService.FindCustomer(customerId: "cust-1") → Customer { Id = cust-1, Name = Ada Lovelace, Tier = Premium }
  ProductCatalogService.LookupPrice(productId: "book-123") → 9.99
  InventoryService.Reserve(productId: "book-123", quantity: 2)
  PaymentService.Charge(customerId: "cust-1", amount: 19.98) → PaymentConfirmation { TransactionId = txn-00001, Amount = 19.98 }
→ OrderResult { OrderId = ORD-00001, TransactionId = txn-00001, Total = 19.98, Quantity = 2 }
```

Esse fluxo é o exemplo real
[`NarrativeTrace.Examples.ECommerce`](examples/NarrativeTrace.Examples.ECommerce),
protegido contra regressão por seus testes de caracterização.

## Quando algo dá errado

O trace torna os bugs visíveis. Cobre um cliente acima do limite dele e a
história para exatamente onde quebrou:

```
OrderService.PlaceOrder(customerId: "cust-1", productId: "book-123", quantity: 2)
  CustomerService.FindCustomer(customerId: "cust-1") → Customer { Id = cust-1, … }
  ProductCatalogService.LookupPrice(productId: "book-123") → 9.99
  InventoryService.Reserve(productId: "book-123", quantity: 2)
  PaymentService.Charge(customerId: "cust-1", amount: 19.98) !! PaymentDeclinedException: Amount 19.98 exceeds approval limit 10
!! PaymentDeclinedException: Amount 19.98 exceeds approval limit 10
```

`Reserve` foi chamado, mas nenhum `Release` compensatório aparece — a
reserva vazada fica visível no trace, não enterrada em um arquivo de log.

## O trace só é tão bom quanto os seus nomes

O mesmo fluxo "jogador entra no mundo", traçado duas vezes — uma com nomes de
domínio, outra com nomes genéricos (a
[demo de nomenclatura do Minecraft](examples/NarrativeTrace.Examples.Minecraft)):

**Refatorado (nomes limpos):**
```
WorldServer.PlayerJoined(playerName: "Steve")
  WorldGenerator.GenerateChunk(x: 0, z: 0) → Chunk { X = 0, Z = 0, Biome = plains }
  PlayerInventory.AddItem(item: "wooden_pickaxe", count: 1) → true
  CraftingTable.Craft(recipe: "wooden_pickaxe") → "wooden_pickaxe"
  CreatureSpawner.SpawnHostile(creatureType: "zombie", x: 10, y: 64, z: 20) → "zombie"
```

**Não refatorado (nomes genéricos):**
```
GameManager.Handle(input: "Steve")
  DataProcessor.Process(a: 0, b: 0) → "0,0"
  StateManager.Update(key: "wooden_pickaxe", value: 1) → true
  ThingFactory.Create(spec: "wooden_pickaxe") → "wooden_pickaxe"
  EntityHandler.Execute(type: "zombie", a: 10, b: 64, c: 20) → "zombie"
```

Grafo de chamadas idêntico, valores de retorno idênticos — só os nomes
mudam. O analisador de clareza pontua a versão limpa estritamente mais alto
(um teste de caracterização fixa tanto a estrutura idêntica quanto a
diferença de pontuação). Se seu código não consegue contar a própria
história, ele precisa de refatoração.

## Por que isso importa para o desenvolvimento assistido por IA

Cada linha `_logger.LogInformation(...)` é uma linha que ferramentas de
codificação com IA — Claude Code, Copilot, Cursor — precisam analisar, gastar
tokens e raciocinar em cima. Em uma classe de serviço típica, o logging é
30–50% das linhas. Remova-as e você ganha:

- **Mais lógica de negócio por janela de contexto** — o mesmo orçamento de
  tokens cobre mais do seu código real.
- **Raciocínio mais limpo** — o modelo vê o que o código *faz*, não como ele
  registra o que faz.
- **Diffs só de sinal** — pull requests mostram mudanças de lógica, não
  mudanças misturadas de lógica e logging.

## Como se compara

- **Logging estruturado** (`Microsoft.Extensions.Logging`) exige que você
  *escreva* instruções de log. O NarrativeTrace gera a narrativa a partir da
  estrutura do seu código — sem chamadas a `LogInformation`, sem templates de
  mensagem, sem conectar escopos manualmente. Quando o tracing de requisição
  está ativo, ele também preenche um `traceId` e um nome de trace legível e
  determinístico.
- **Tracing distribuído** (OpenTelemetry, Jaeger) acompanha o fluxo de
  requisições entre serviços com spans. O NarrativeTrace captura árvores de
  chamadas em *nível de método*, com valores completos de parâmetros e
  retorno — um detalhe que tracers baseados em span não registram. O pacote
  `NarrativeTrace.Observability` faz a ponte entre os dois: exporta árvores
  como spans `Activity` (em lote) ou ao vivo via `OtelTraceEventListener`.
- **Logging por interceptador/AOP** (`DispatchProxy`, Castle) registra
  automaticamente entrada/saída, mas produz uma saída plana e mecânica. O
  NarrativeTrace produz árvores de chamadas aninhadas e ainda pontua a
  qualidade da sua nomenclatura.

Ele não substitui alertas de produção nem mapas de topologia de serviços;
entrega o que nenhum dos dois oferece — uma narrativa de execução legível
por humanos que também funciona como diagnóstico de qualidade de código.

## Não substitui o seu framework de logging

O NarrativeTrace não tem sink, provedor nem pipeline de envio próprios. Sua
configuração do `Microsoft.Extensions.Logging` — Serilog, NLog,
Application Insights, os sinks e enrichers que você já usa — continua
funcionando sem alterações.

O que ele substitui são as *instruções* de narração escritas à mão —
linhas como `_logger.LogInformation("Placing order for {Customer}...",
customerId)`. Um método rastreado produz essa narrativa
automaticamente, emitida através da mesma abstração `ILogger` que essas
linhas usariam: conecte a ponte do `NarrativeTrace.Logging`
(`AddNarrativeLogging()`, ou o decorador `LoggingNarrativeContext`) e
cada entrada/saída vira um registro de log estruturado comum — a mesma
maquinaria `LoggerMessage`, o mesmo `BeginScope`.

O logging manual e o NarrativeTrace se misturam livremente — aponte os
dois para o mesmo `ILogger` e eles compartilham cada sink, filtro e
enricher que seu host já tiver configurado. Adicione um
`_logger.LogWarning(...)` onde ainda quiser um.

## O que você ganha

- **Narrativas de execução** — envolva um serviço em um proxy de tracing e
  cada chamada é capturada como uma árvore de eventos `entrada → resultado`
  com parâmetros, valores de retorno, exceções e durações.
- **Múltiplas renderizações** — o mesmo trace como texto indentado,
  Markdown, prosa, diagramas de sequência Mermaid / PlantUML, JSON canônico
  ou spans OpenTelemetry.
- **Pontuação de clareza** — um analisador orientado a NLP avalia nomes de
  classes/métodos/parâmetros (verbos, abreviações, coesão, tokens genéricos)
  e traz os problemas à tona.
- **Integrações com frameworks** — middleware de requisição do ASP.NET
  Core, auto-wrap de DI, escopos de `ILogger`, OpenTelemetry (lote + ao
  vivo), e helpers de teste xUnit / NUnit que imprimem a narrativa quando um
  teste falha.
- **Ferramental** — uma CLI `dotnet-narrativetrace` (varre assemblies, faz
  gate por clareza) e um pacote `NarrativeTrace.MSBuild` que conecta isso ao
  seu build.

## Adicione a um teste

O caminho de menor cerimônia entre "biblioteca interessante" e "vi um trace
do meu próprio código": envolva o corpo do teste em um fixture que imprime a
história quando o teste falha.

**xUnit** — envolva o corpo (o xUnit não entrega o resultado do teste aos
fixtures):

```csharp
public class OrderTests : IClassFixture<NarrativeFixture>
{
    private readonly NarrativeFixture _fixture;
    public OrderTests(NarrativeFixture fixture) => _fixture = fixture;

    [Fact]
    public void Places_an_order() => _fixture.Run(nameof(Places_an_order), ctx =>
    {
        var orders = NarrativeTraceProxy.Create<IOrderService>(new OrderService(), ctx);
        Assert.Equal("confirmed", orders.PlaceOrder("book-123", 2));
    });
}
```

**NUnit** — derive de `NarrativeTestBase`; as falhas são detectadas e
narradas automaticamente no teardown via `TestContext`.

Ambos escrevem arquivos reais em disco por padrão, sem nenhuma opção para
ativar *(since 0.1.4, unreleased)*:
`TestResults/narrativetrace/traces/<Class>/<slug>.md`, um `.json`
irmão, um diagrama `.mmd` e um `.nt` sem valores — no diretório efêmero e
ignorado pelo Git que o `dotnet test` já trata como saída descartável.
Defina `NARRATIVETRACE_OUTPUT=false` para desativar. Veja
[Veja um trace em 60 segundos](documentation/pt-BR/sessenta-segundos.md)
para o caminho mais rápido até um trace, e o
[Guia de clareza](documentation/guides/pt-BR/guia-de-clareza.md) para
renomear um método e ver a pontuação cair.

## Escolha sua integração

| Você quer | Comece por |
|---|---|
| Controle explícito sobre o que é envolvido, em .NET puro | `NarrativeTraceProxy.Create<T>` abaixo |
| Todo serviço com interface registrado por namespace, traçado automaticamente | Injeção de dependências, abaixo |
| Ciclo de vida de requisições HTTP em produção no ASP.NET Core | Middleware do ASP.NET Core, abaixo |
| Traces em testes, com o mínimo de conexão manual | Adicione a um teste, acima |
| Um gate de qualidade de nomenclatura em CI, sem rodar testes | [CLI](#cli-dotnet-narrativetrace) / MSBuild |

Matriz completa e um diagrama de decisão:
[Escolhendo uma integração](documentation/pt-BR/escolhendo-uma-integracao.md).

**Proxy — controle explícito, funciona em qualquer app .NET:**

```csharp
using NarrativeTrace.Core;
using NarrativeTrace.Proxy;
using NarrativeTrace.Runtime;

var context = new SyncNarrativeContext(new NarrativeTraceConfig(TracingLevel.Detail));
IOrderService orders = NarrativeTraceProxy.Create<IOrderService>(new OrderService(), context);
orders.PlaceOrder("book-123", quantity: 2);

Console.WriteLine(IndentedTextRenderer.Render(context.CaptureTrace()));
```

```
└── IOrderService.PlaceOrder(sku: "book-123", quantity: 2) → "confirmed" — 6ms
```

**Injeção de dependências — envolve automaticamente toda interface cuja
implementação vive sob um prefixo de namespace**, o equivalente .NET do
tracing de beans do Spring/Micronaut (chame por último, depois que todo
serviço que ela deve enxergar já estiver registrado):

```csharp
services.AddNarrativeTracing(o =>
{
    o.Level = TracingLevel.Detail;
    o.Namespaces("MyApp.Services", "MyApp.Domain");
});
```

**ASP.NET Core — trace por requisição, exportado quando a requisição
termina:**

```csharp
builder.Services.AddNarrativeTrace(builder.Configuration);

var app = builder.Build();
app.UseMiddleware<NarrativeTraceMiddleware>(); // perto da borda externa do pipeline

app.MapGet("/orders/{id}", (string id, HttpContext http) =>
    NarrativeTraceProxy.Create<IOrderService>(new OrderService(), http.GetNarrativeContext())
        .FindOrder(id));
```

Veja o [Guia de integração com ASP.NET Core](documentation/guides/pt-BR/guia-de-integracao-com-aspnet-core.md)
para exportadores e contexto de usuário, e o
[Guia de instalação](documentation/guides/pt-BR/guia-de-instalacao.md) para a
CLI, o MSBuild e as pontes de logging/OpenTelemetry.

**Formatos de renderização** — escolha independentemente de como você
capturou o trace:

```csharp
MarkdownRenderer.Render(trace);          // relatório Markdown
IndentedTextRenderer.Render(trace);      // amigável para console / log
ProseRenderer.Render(trace);             // prosa narrativa
MermaidSequenceRenderer.Render(trace);   // diagrama de sequência Mermaid
PlantUmlSequenceRenderer.Render(trace);  // diagrama de sequência PlantUML
JsonExporter.Export(trace, metadata);    // JSON canônico (validado por schema)
```

## Privacidade e segurança, em uma tela

Esta biblioteca roda dentro do seu processo e escreve arquivos que sua
equipe vai compartilhar. Verificado contra o código, não presumido:

| Garantia | Como se sustenta |
|---|---|
| **A ocultação é incondicional em toda integração distribuída** | `[NotTraced]` e uma lista de negação de nomes sempre ativa e multilíngue (mais detecção de forma de valor para JWT/cartão de pagamento/`Set-Cookie`/checksums de identificação nacional, independente do nome do campo) se aplicam à captura por proxy, ao auto-wrap de DI, ao middleware do ASP.NET Core, à saída de testes e aos marcadores de template igualmente. Nenhum flag os desativa. |
| **`[NotTraced]` vence sempre** | Em uma propriedade ou campo, o getter nunca sequer é invocado; em um parâmetro, o valor nunca é renderizado. Ele supera em prioridade um `ToString()` com curadoria e vence uma saída de emergência `RedactionPolicy.Disabled` que existe, mas que nenhuma integração distribuída conecta. |
| **O artefato estrutural seguro para IA não carrega nenhum valor** | O arquivo `.nt` (e o `.structural.json` opt-in) contêm apenas nomes, hierarquia e tipo de resultado — um teste de propriedade semeia conteúdo hostil em todo campo de valor e falha se algo disso sobreviver. |
| **Falhas de tracing não podem derrubar sua aplicação** | Captura e renderização são isoladas de exceção em todo ponto de risco — um `ToString()` que lança exceção, um membro `[NarrativeSummary]` ou um getter de caminho de template degradam para um placeholder sem tocar no resultado da sua chamada de negócio. |
| **O uso de recursos é limitado** | Tamanho de string, tamanho de coleção, largura de objeto e profundidade de aninhamento têm todos um teto, com detecção de ciclos independente da profundidade. |

Dois limites honestos: a detecção é baseada em nome/forma, não estatística
(sem heurísticas de "parece aleatório" — um segredo em um campo com nome
inocente não é capturado), e a resolução de marcadores de template
(`[Narrated]`/`[OnError]`) sempre usa a lista de negação padrão, mesmo que
você tenha conectado uma `RedactionPolicy` personalizada no `ValueRenderer`
em outro lugar.

→ [Privacidade e ocultação](documentation/pt-BR/privacidade-e-ocultacao.md)
para o contrato completo, linha a linha.

## Performance

O tracing faz trabalho e custa algo — nenhuma alegação de "overhead zero".
O gate de regressão (BenchmarkDotNet) derruba o build em **mais de 15% mais
lento** ou em **qualquer** aumento de alocação em relação a uma baseline
registrada. Números de baseline registrados, nível `Detail` (o padrão), de
[`benchmarks/benchmark-baseline.json`](benchmarks/benchmark-baseline.json):

| Benchmark | Média | Alocado |
|---|---|---|
| `CoreBenchmarks.EnterExitCycle` | ~2.5 µs | 2368 B |
| `CoreBenchmarks.RenderObject` | ~819 ns | 1611 B |
| `RendererBenchmarks.MarkdownSmall` | ~595 ns | 2704 B |
| `ConcurrencyBenchmarks.ForkJoin_TwoTasks` | ~3.9 µs | 5602 B |

Só nativo do host: um runner de CI compartilhado não consegue sustentar
esses limiares (provado na primeira execução pública — todos os outros
gates verdes, e depois dezenas de "regressões" espúrias por vizinhos
ruidosos), então tanto o GitHub Actions (`./build.sh Verify --skip
Benchmark`) quanto o push na branch padrão do GitLab pulam esse passo. Ele
ainda roda de verdade, sem supervisão, como um target NUKE comum em
hardware equivalente: `./build.sh Benchmark` restaura, compila, executa os
16 benchmarks (o job `medium` do BenchmarkDotNet) e faz gate deles contra
[`benchmarks/benchmark-baseline.json`](benchmarks/benchmark-baseline.json),
escrevendo resultados em JSON legíveis por máquina em
`artifacts/benchmarks/`. Esse único comando é o ponto de entrada que um job
noturno/de host invoca; regenere a baseline depois de uma mudança de
performance intencional com `./build.sh BenchmarkBaseline`.

Em `TracingLevel.Off` o contexto interrompe o fluxo antecipadamente e não
captura nada — um teste de caracterização fixa que um contexto em nível Off
produz um trace vazio — mas esse caminho ainda não é medido separadamente
em benchmark, então trate "Off é arquiteturalmente zero-captura" como
verificado por teste, não como um número medido de ns/op.

## O que é Free e o que é Pro

**Free** é tudo neste repositório — disponível como código-fonte sob a BSL
1.1, gratuito em produção: todo o pipeline de captura, cada renderizador e
formato de exportação, a pontuação de clareza, cada integração acima e o
artefato estrutural sem valores. As funcionalidades hoje restritas ao Pro
(não distribuídas neste repositório, realocadas para
`narrative-trace-dotnet-enterprise`): agregação de fluxo de eventos entre
execuções (`EventAggregator`), e — planejadas, ainda não construídas —
resumos de fluxo, diffs de migração, grafos de dependência em tempo de
execução e handlers de ferramentas MCP para agentes de IA. O
[Guia de funcionalidades](documentation/feature-guide.md) (em inglês) é a
tabela de status oficial: cada funcionalidade é rotulada Free, Pro, In
development ou Planned, com o código por trás de cada linha já publicada.

## Pacotes

| Pacote | O que fornece |
|---|---|
| `NarrativeTrace.Core` | Modelo de trace, contextos, renderizadores, tipos de clareza, configuração e os atributos `NarrativeTrace.Core.Annotation` |
| `NarrativeTrace.Runtime` | Implementações de contexto, pipeline de eventos, exportadores JSON/chapter |
| `NarrativeTrace.Proxy` | Proxy de tracing baseado em `DispatchProxy`, além de `[Traced]` |
| `NarrativeTrace.DependencyInjection` | Auto-wrap por namespace via `AddNarrativeTracing` |
| `NarrativeTrace.AspNetCore` | Middleware de ciclo de vida de requisição + SPI `ITraceExporter` |
| `NarrativeTrace.Observability` | Exportação `Activity` do OpenTelemetry — listener em lote + ao vivo |
| `NarrativeTrace.Logging` | Exportação de narrativa via `ILogger` + escopos de correlação |
| `NarrativeTrace.Diagrams` | Renderizadores de sequência Mermaid / PlantUML |
| `NarrativeTrace.Clarity` | Analisador de clareza de nomenclatura, pontuadores, dicionários, scanner |
| `NarrativeTrace.Testing.Xunit` / `.NUnit` | Fixtures de teste que narram falhas |
| `NarrativeTrace.Cli` | Ferramenta global `dotnet-narrativetrace` |
| `NarrativeTrace.MSBuild` | Targets de build para scan/gate de clareza |
| `NarrativeTrace.Legacy` | Superfície de compatibilidade com `net48` |

## OpenTelemetry

```csharp
// Em lote: exporta uma árvore de trace concluída como spans Activity.
TraceActivityExporter.Export(context.CaptureTrace());

// Ao vivo: transforma os eventos de entrada/saída de um pipeline em spans conforme acontecem.
var listener = new OtelTraceEventListener(new ActivitySource("MyApp"));
bufferedConsumer.Subscribe(listener.OnEvent);
```

## Suporte a concorrência

O NarrativeTrace acompanha a execução concorrente — paralelismo fork-join e
tarefas fire-and-forget — como cidadãos de primeira classe na árvore de
trace. Como no .NET o contexto flui através de `AsyncLocal` (o análogo do
`ThreadLocal` do Java), cada ramo paralelo roda contra um contexto filho
isolado, cujo trace é mesclado de volta ao pai com metadados de thread.

**Fork-join** — `ForkJoinGroup` roda ramos em paralelo, junta-os, e enxerta
seus traces sob o pai com um `groupId` compartilhado:

```csharp
var fork = ForkJoinGroup.Create(context);
_ = fork.Fork(iso => NarrativeTraceProxy.Create<IProductCatalogService>(catalog, iso)
    .LookupPrice("book-123"));
_ = fork.Fork(iso => { NarrativeTraceProxy.Create<IInventoryService>(inventory, iso)
    .Reserve("book-123", 2); return true; });
await fork.JoinAsync();
```

**Fire-and-forget** — `FireAndForgetGroup` enxerta um nó lançador no pai e
liga o trace filho em segundo plano pelo `groupId`; o pai continua sem
esperar:

```csharp
var group = FireAndForgetGroup.Create(context, "OrderService");
group.Launch(iso => NarrativeTraceProxy.Create<INotificationService>(notifier, iso)
    .NotifyOrderPlaced("cust-1", "ORD-00001"));
```

O Markdown renderizado inclui marcadores `⑂ fork [n tasks]` / `⑃ join`, um
lançador `⤳ fire-and-forget`, nomes de thread e o tempo de parede do join. O
cenário completo — precificação/estoque em paralelo seguidos de uma
notificação assíncrona — é
[`ConcurrencyScenario`](examples/NarrativeTrace.Examples.ECommerce/ConcurrencyScenario.cs),
com testes de caracterização que verificam o `groupId` compartilhado, o nó
lançador e os campos de concorrência na exportação JSON. Para propagação
ambiente através de um `await`, `AsyncNarrativeContext.RunAsync` anexa o
trabalho de continuação ao mesmo trace.

## CLI: `dotnet-narrativetrace`

```bash
dotnet tool install --global NarrativeTrace.Cli

# Pontua a clareza de nomenclatura de um assembly compilado (somente reflexão — nunca o executa):
dotnet-narrativetrace clarity-scan --assembly bin/MyApp.dll --output-dir clarity

# Derruba o CI quando a clareza cai abaixo de um limiar:
dotnet-narrativetrace clarity-check --results clarity/clarity-scan-results.json \
    --min-score 0.7 --max-high-issues 0
```

### Integração com MSBuild

Referencie `NarrativeTrace.MSBuild` e conduza o mesmo gate a partir do seu
build:

```xml
<PropertyGroup>
  <ClarityMinScore>0.7</ClarityMinScore>
  <ClarityMaxHighIssues>0</ClarityMaxHighIssues>
  <ClarityWarnOnly>false</ClarityWarnOnly>
</PropertyGroup>
```

```bash
dotnet build /t:ClarityCheck   # varre + faz gate; incremental via arquivo de stamp
```

## Configuração

Todo ajuste pode ser resolvido a partir de variáveis de ambiente
`NARRATIVETRACE_*` (`NARRATIVETRACE_LEVEL`, `NARRATIVETRACE_OUTPUT`,
`NARRATIVETRACE_FORMAT`, …). Níveis de captura, do menos ao mais detalhado:
`Off`, `Errors`, `Summary`, `Narrative`, `Detail`.

## Documentação

Comece aqui:

- [Veja um trace em 60 segundos](documentation/pt-BR/sessenta-segundos.md) — um app de console, `dotnet run`, saída real no seu terminal
- [Escolhendo uma integração](documentation/pt-BR/escolhendo-uma-integracao.md) — qual módulo você precisa, como um diagrama de decisão
- [Solução de problemas](documentation/pt-BR/solucao-de-problemas.md) — sintoma → causa → correção para os modos de falha que as pessoas realmente encontram
- [O que incluir no commit](documentation/pt-BR/o-que-incluir-no-commit.md) — quais arquivos gerados são descartáveis e quais são revisados
- [Privacidade e ocultação](documentation/pt-BR/privacidade-e-ocultacao.md) — o contrato de ocultação linha a linha, verificado contra o código

Guias orientados a tarefas vivem em [`documentation/guides/`](documentation/guides/pt-BR/guia-de-usuario.md):

- [Instalação](documentation/guides/pt-BR/guia-de-instalacao.md) — pacotes e caminhos de integração
- [Configuração](documentation/guides/pt-BR/guia-de-configuracao.md) — níveis, variáveis de ambiente, DI, MSBuild, ocultação
- [Atributos](documentation/guides/pt-BR/guia-de-atributos.md) — `[Narrated]`, `[OnError]`, `[NotTraced]`, `[Traced]`, `[NarrativeSummary]`
- [Injeção de dependências](documentation/guides/pt-BR/guia-de-injecao-de-dependencias.md) — auto-wrap por namespace no container do MS.DI
- [Integração com ASP.NET Core](documentation/guides/pt-BR/guia-de-integracao-com-aspnet-core.md) — middleware de requisição, exportadores, contexto de usuário
- [Clareza](documentation/guides/pt-BR/guia-de-clareza.md) — modelo de pontuação e gate de CI
- [MSBuild e CLI](documentation/guides/pt-BR/guia-de-msbuild-e-cli.md) — verbos do `dotnet-narrativetrace` e o gate de clareza em tempo de build

Indo mais fundo:

- [Guia de funcionalidades](documentation/feature-guide.md) (em inglês) — tabela de status oficial (Free/Pro/In development/Planned) para cada funcionalidade, com o código por trás de cada linha já publicada
- [Ferramental de segurança](documentation/security-tooling.md) (em inglês) — o conjunto de scanners e o que faz gate do build vs. o que roda por agendamento
- [Testes de segurança](documentation/security-testing.md) (em inglês) — a suíte de fuzzing/propriedades espelhada em toda implementação do NarrativeTrace

Para consumidores de IA: [`llms.txt`](documentation/guides/llms.txt) e
[`llms-full.md`](documentation/guides/llms-full.md).

## Frameworks de destino

- Runtime moderno: `net10.0`
- Compatibilidade: `netstandard2.0`
- Legado: `net48` (em `NarrativeTrace.Legacy`)

## Build e testes

```bash
dotnet build NarrativeTrace.sln
dotnet test NarrativeTrace.sln
```

Ou via [NUKE](https://nuke.build) (`./build.sh`, `build.ps1`, `build.cmd`):

```bash
./build.sh Test        # roda a suíte completa
./build.sh Verify      # clean + format + analyze + test + coverage + metrics + benchmark
./build.sh VerifyAll   # toda verificação que este repo tem, gate e pesada igualmente — veja abaixo
./build.sh Coverage    # relatórios cobertura via Coverlet
./build.sh Mutation    # teste de mutação com Stryker.NET
./build.sh Pack        # produz pacotes NuGet
```

### Gates de qualidade (obrigatórios)

Formatação (`dotnet format`), análise estática (Roslyn + SonarAnalyzer,
incl. um teto de 20 linhas por método via S138, e toda a categoria de regras
de segurança CA5xxx), varredura de segredos (gitleaks, hook pre-commit no
diff staged mais uma varredura completa do histórico), testes (xUnit /
NUnit), cobertura (Coverlet) e regressão de benchmark (BenchmarkDotNet)
fazem gate do build. O teste de mutação (Stryker.NET, `./build.sh
Mutation`) está definido e é executável — inclusive no espelho público via
[`.github/workflows/mutation.yml`](.github/workflows/mutation.yml) — mas
**não** é uma dependência do `Verify`: assim como o ruleset OSS em C# do
Semgrep e a varredura de vulnerabilidades de dependências (OSV-Scanner,
`dotnet list package --vulnerable`), ele roda numa camada agendada/manual,
nunca por commit — veja
[`documentation/security-tooling.md`](documentation/security-tooling.md)
(em inglês). `tests/BuildScript.Tests` valida o próprio comportamento do
build. O CI roda `./build.sh Verify` no GitHub Actions
(`.github/workflows/ci.yml`).

`./build.sh VerifyAll` é o único comando que roda toda verificação que este
repositório tem, gate e pesada igualmente — testes unitários, cobertura,
mutação, testes de propriedade, os dois tiers de fuzz, arquitetura,
conformidade, benchmarks/alocação, stress de concorrência, e as checagens de
secrets/SAST/SCA/lint/formato/complexidade/tradução — de uma vez só,
coletando o resultado de cada categoria em vez de parar no primeiro
problema. Ele escreve `reports/verification/<date>.json` e seu gêmeo `.md`
renderizado, na forma cross-runtime que o repositório Java da família define
(`reports/verification/SCHEMA.md`): os mesmos nomes de campo, os mesmos
quatro status (`passed`/`failed`/`skipped`/`not-implemented`), os mesmos 21
ids de categoria em todo runtime do NarrativeTrace. Longo por design — a
mutação em todo módulo do Stryker e o sweep de stress com iterações elevadas
são, historicamente, minutos, não segundos — então é uma execução
agendada/manual, nunca por commit.

## FAQ

**Quanto overhead isso adiciona, e o que acontece sob alta concorrência?** Não
vamos afirmar "overhead zero" — veja [Performance](#performance) acima para os
números datados que esta resposta resume (BenchmarkDotNet, `TracingLevel.Detail`,
o padrão, de [`benchmarks/benchmark-baseline.json`](benchmarks/benchmark-baseline.json),
atualizados em 2026-08-12): um ciclo completo de entrada/saída custa ~2,5 µs e
aloca 2368 B; renderizar um objeto custa ~819 ns/1611 B; uma renderização
Markdown pequena custa ~595 ns/2704 B. Ainda não há um benchmark separado para
`TracingLevel.Off` — melhor dizer isso claramente do que insinuar um número
que não existe: um contexto no nível off é verificado por teste de
caracterização para produzir um trace vazio, arquiteturalmente sem captura,
mas esse caminho ainda não é uma cifra medida em ns/op.

O que o NarrativeTrace em si adiciona é essa captura — interceptar a chamada,
ler os argumentos, construir a árvore de trace. Tudo depois da captura (a
escrita do `ILogger`, o collector, o disco ou a rede) é o mesmo custo que sua
stack de logging já paga; o NarrativeTrace não adiciona um segundo destino.
Para uma equipe substituindo instruções de log escritas à mão, o lado do
destino fica quase no zero a zero: N chamadas de log por método viram uma
escrita de trace, e essas instruções deixam de ser escritas, revisadas e
mantidas sincronizadas com o código.

Sob concorrência, os dois caminhos do pipeline têm garantias diferentes. Um
listener síncrono — a exportação para `ILogger` do `NarrativeTrace.Logging`,
se você conectar — roda em linha: a escrita é concluída antes do método
retornar, então é exatamente tão durável — e custa exatamente o mesmo — quanto
uma chamada de log já custa. O caminho com buffer, de melhor esforço, é um
anel limitado que descarta acima de 70% de ocupação em vez de bloquear quem
chama, e cada evento descartado é **contado** — sobrescritas do anel e
descartes do dreno adaptativo igualmente, via
`BufferedEventConsumer.DroppedCount` — e mostrado no próprio rodapé
`TraceLoss` do trace ("N events dropped (buffer full)"), nunca em silêncio.

**O limite honesto:** hoje não existe sampling nesta implementação, nem em
nenhuma implementação do NarrativeTrace — toda chamada traçada é capturada por
completo no `TracingLevel` configurado. Um amostrador por porcentagem ou por
taxa está no roadmap, mas não foi lançado. Se você precisa limitar o volume de
captura agora, use `TracingLevel.Off` ou restrinja o escopo traçado ao limite
que importa.

**Como sei que um parâmetro com PII ou credenciais não vai vazar em um
trace?** Quatro camadas independentes, não uma única promessa geral — o
contrato linha a linha é [Privacidade e
ocultação](documentation/pt-BR/privacidade-e-ocultacao.md):

1. `[NotTraced]` em um parâmetro, propriedade ou campo — ocultação explícita
   que você controla. Em uma propriedade ou campo o getter nem sequer é
   invocado; prevalece sobre um `ToString()` cuidadosamente escrito, e é
   incondicional — nenhum flag o desliga.
2. Uma lista de negação por nome, sempre ativa e multilíngue — compara nomes
   de campos e parâmetros com padrões em inglês, espanhol, português,
   francês, alemão e chinês para senhas, tokens, identificações nacionais e
   afins, ampliável (nunca substituível) via
   `NARRATIVETRACE_REDACTION_ADDITIONALPATTERNS`. Ativa por padrão, não
   opcional.
3. Correspondência pela forma do valor, independente do nome do campo — uma
   string com forma de JWT, um número de cartão válido por Luhn, um valor com
   forma de `Set-Cookie`, ou um checksum de identificação nacional ou uma
   regra estrutural (RUT chileno, CPF/CNPJ brasileiro, DNI/NIE espanhol, NIR
   francês, carteira de identidade chinesa, ou um número de Seguro Social
   americano escrito com hifens) é ocultado mesmo que chegue sob um nome
   inocente como `data` ou `value`.
4. O artefato estrutural `.nt` sem valores (mais o `.structural.json`
   opcional) — a garantia categórica. Só nomes, hierarquia e tipo de
   resultado, zero valores em tempo de execução; um teste de propriedades
   semeia conteúdo hostil em cada campo de valor e falha se algum sobreviver.

Dois limites honestos, já nomeados na [tabela de privacidade e
segurança](#privacidade-e-segurança-em-uma-tela) acima: a detecção é por
nome/forma, não estatística — um segredo em um campo com nome inocente, em
uma forma que nenhuma das checagens reconhece, não é capturado — e a
resolução de placeholders de template (`[Narrated]`/`[OnError]`) sempre usa a
lista de negação padrão, mesmo que você tenha conectado um `RedactionPolicy`
personalizado no `ValueRenderer` em outro lugar. As camadas 1–3 são
heurísticas e extensíveis; a camada 4, o artefato estrutural, é a única
*categórica* — recorra a ela se seu modelo de ameaça exigir que nenhum valor
possa jamais sair do processo.

**Os IDs de trace podem se correlacionar com um ID de correlação padrão entre
serviços, ou o tracing é só local?** Só local, hoje — e preferimos dizer isso
claramente a deixar o pacote `NarrativeTrace.Observability` sugerir o
contrário. Esta implementação não analisa um cabeçalho W3C `traceparent` de
entrada, nem anexa um nas chamadas HTTP de saída; é uma decisão explícita e
testada de não fazer isso agora, não um descuido — um teste de propriedades
stub na suíte de segurança afirma que ainda não existe nenhum tipo
`Traceparent` em `NarrativeTrace.Core`, exatamente para que um futuro parser
chegue com seus casos de fuzzing já prontos. `TraceId` é gerado localmente e
tem forma W3C (32 caracteres hexadecimais minúsculos, comparável byte a byte
com os IDs que o runtime Java ou um cabeçalho `traceparent` real
produziriam) — mas nunca é *derivado* de um cabeçalho de entrada, então um ID
de trace do NarrativeTrace não vai ser igual ao de um trace distribuído
anterior.

O que existe: `TraceActivityExporter` (em lote) e `OtelTraceEventListener`
(ao vivo) do `NarrativeTrace.Observability` transformam uma árvore do
NarrativeTrace concluída ou em andamento em spans de
`System.Diagnostics.Activity` sob um `ActivitySource("NarrativeTrace")` — mas
isso é uma repetição, não instrumentação ao vivo: os spans carregam a
hierarquia própria do trace e não são filhos do que quer que
`Activity.Current` valha no momento da exportação. Se a correlação entre
serviços for um requisito obrigatório hoje, propague seu próprio ID de
correlação através dos scopes de `ILogger` do `NarrativeTrace.Logging` junto
com a saída do NarrativeTrace; tratar a propagação de contexto W3C como algo
já lançado aqui seria impreciso — é uma lacuna no roadmap, não um recurso
publicado.

**Valores de parâmetro e retorno são serializados de forma eager?** Sim —
os valores são renderizados para string no momento da captura, no nível
configurado. Isso é deliberado: o trace registra o que o valor *era* no
momento da chamada, não uma visão posterior, possivelmente mutada. Veja
[Privacidade e segurança](#privacidade-e-segurança-em-uma-tela) acima e a
página [Privacidade e ocultação](documentation/pt-BR/privacidade-e-ocultacao.md)
para o contrato de ocultação completo e verificado.

**O tracing pode disparar efeitos colaterais no meu código?** A
renderização pode invocar um conjunto pequeno e documentado de membros:
getters de propriedade durante introspecção reflexiva, um `ToString()`
personalizado, um membro `[NarrativeSummary]`, e caminhos de propriedade
nomeados em templates `[Narrated]`/`[OnError]`. Mantenha-os puros (getters
sem efeito colateral já são uma diretriz do .NET Framework Design
Guidelines) — ou marque o membro com `[NotTraced]`, e nesse caso o valor
dele nunca é sequer lido. Toda invocação é limitada (teto de
tamanho/profundidade/itens), isolada de exceção (um getter que lança
exceção nunca derruba sua chamada de negócio), e acontece de forma eager no
ponto de chamada; em `TracingLevel.Off` nenhuma renderização acontece. Veja
o contrato de pureza no [guia de atributos](documentation/guides/pt-BR/guia-de-atributos.md).

**Ele consegue traçar métodos privados ou que não são de interface?** O
proxy `DispatchProxy` traça chamadas de *interface*, então o tracing
acontece nas fronteiras de interface — chamadas a métodos privados e
internos dentro de uma implementação não são traçadas individualmente.
Isso mantém a narrativa no nível de colaboração (serviço a serviço), que
costuma ser o nível que você quer ler. Para mais granularidade, separe o
comportamento atrás de uma interface.

**Como o NarrativeTrace interage com outras bibliotecas que envolvem
métodos (AOP, proxies, bibliotecas de contratos)?** Ele narra travessias de
fronteira de negócio, não a maquinaria de implementação — um decorador
gerado, um método ponte, ou o stub de proxy de outra biblioteca nunca vira
um frame de trace por direito próprio. Quando uma biblioteca de contrato ou
validação rejeita uma chamada em uma interface que o `NarrativeTraceProxy`
também envolve, a rejeição sempre chega ao trace como uma exceção,
independentemente de qual wrapper for registrado primeiro — só *a qual*
frame ela se anexa (interno ou externo) depende dessa ordem. Um ponto de
entrada dedicado para eventos de violação que não lançam exceção está
planejado, mas ainda não construído. As opções de exclusão de hoje são
limitadas: `ExcludeNamespaces` do `AddNarrativeTracing` exclui namespaces
de interface do auto-wrap de DI com correspondência por limite de ponto, e
o proxy bruto só envolve o que você passa explicitamente para
`NarrativeTraceProxy.Create`. Ainda não existe uma lista de exclusão padrão
para formas de maquinaria conhecidas (decoradores gerados, tipos gerados
pelo compilador), e o design de `DispatchProxy` de uma interface por proxy
significa que uma instância envolvida não expõe nenhuma *outra* interface
marcadora que ela implemente — ambos continuam sendo itens em aberto no
roadmap.

## Status

A versão 0.1.0 está publicada — os pacotes estão no NuGet. O pipeline de
captura, o schema JSON canônico, o modelo de atributos de duas camadas, o ciclo
de vida do ASP.NET Core, a conexão de DI, o OpenTelemetry (lote + ao vivo), o
ferramental de clareza + CLI, as integrações com frameworks de teste e os
exemplos executáveis com seu lançador de demo já estão no lugar. Trabalho
restante: validação em runtime `net48`.

## Licença

A API e o formato de saída do NarrativeTrace são padrões abertos (Apache
2.0). Seu runtime é gratuito e disponível como código-fonte (BSL 1.1,
convertendo para Apache 2.0 quatro anos depois de cada release). Pro é
comercial.

Tudo neste repositório é o runtime, licenciado sob a
[Business Source License 1.1](LICENSE). Você pode usá-lo em produção para
qualquer finalidade, inclusive internamente e em produtos e serviços que
você fornece aos seus próprios clientes — a única exclusão é oferecer o
próprio NarrativeTrace, ou um produto ou serviço cujo valor derive
substancialmente dele, a terceiros como um produto ou serviço de logging,
tracing ou narrativa de código. Quatro anos depois que uma versão é
publicada, essa versão vira Apache 2.0 — a concessão é automática e por
versão, então o que você adota hoje está em um relógio que você pode ler.

A prosa da documentação é [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/); os arquivos de schema JSON são
Apache 2.0.

<!-- legal:trademark:begin -->
NarrativeTrace é uma marca registrada da Empower Agile. A licença não
concede nenhum direito de marca.
<!-- legal:trademark:end -->

### A licença, em palavras simples

Tudo neste repositório é Business Source License 1.1 hoje; a camada de
contrato aberta chega com a separação da api.

<!-- legal:plain-words:begin -->
**Grátis para rodar.** O runtime é de código disponível sob a Business Source
License 1.1: você pode lê-lo, auditá-lo, corrigi-lo e usá-lo em produção sem
custo — inclusive dentro dos produtos e serviços que você vende aos seus
próprios clientes.

**Uma única exclusão.** Você não pode oferecer o próprio NarrativeTrace — ou um
produto ou serviço cujo valor derive substancialmente dele — a terceiros como
produto ou serviço de logging, tracing ou narrativa de código.

**Ela se abre em uma data.** Cada versão lançada se converte para Apache 2.0
quatro anos após ser publicada; a data exata é impressa no LICENSE daquela
versão.

*Este resumo é uma cortesia, não uma licença. O arquivo LICENSE é o único texto
vinculante; onde os dois divergirem, o LICENSE prevalece.*
<!-- legal:plain-words:end -->
