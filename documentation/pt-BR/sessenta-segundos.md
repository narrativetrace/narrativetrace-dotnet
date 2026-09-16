<!-- source: documentation/sixty-seconds.md blob 70186c49812a | translated: 2026-09-16 | reviewed: - -->
# Veja um trace em 60 segundos

[English](../sixty-seconds.md) | [Español](../es/sesenta-segundos.md) | **Português** | [简体中文](../zh-CN/60秒.md)

Sem instruções de log, sem framework de testes, sem arquivos para abrir: um
app de console, um `dotnet run`, e um trace no seu terminal. Tudo abaixo foi
executado de verdade — a saída está colada, não imaginada.

## 1. Novo app de console, adicione o pacote

```bash
dotnet new console -n Hello && cd Hello
dotnet add package NarrativeTrace.Proxy
```

Um único pacote: `NarrativeTrace.Proxy` depende de `NarrativeTrace.Runtime`,
que depende de `NarrativeTrace.Core` — `dotnet add package` resolve a cadeia
inteira, então `SyncNarrativeContext` e `IndentedTextRenderer` logo abaixo
ficam disponíveis sem mais dois `dotnet add package`.

## 2. Substitua o Program.cs

`Program.cs` semeia um `Traceparent` fixo através do `NarrativeTraceConfig`
— o mesmo formato de conexão que o `NarrativeTraceMiddleware` adota de um
cabeçalho de requisição `traceparent` recebido — apenas para que a saída
desta página sempre nomeie o mesmo trace. Seu próprio código nunca faz
isso: deixe `initialTraceparent` sem definir e uma execução real gera um id
de trace aleatório a cada vez, e o nome de três palavras abaixo é derivado
dele, nunca de um nome que você escolhe.

```csharp
using NarrativeTrace.Core;
using NarrativeTrace.Proxy;
using NarrativeTrace.Runtime;

// snippet:begin fixedTraceparent
// Um traceparent W3C fixo, semeado através do NarrativeTraceConfig para que a saída incrustada
// desta página sempre nomeie o mesmo trace. Uma execução real não adota nada aqui (ou um
// cabeçalho de requisição recebido real, via NarrativeTraceMiddleware) e obtém um id de trace
// aleatório e novo a cada vez.
const string DemoTraceparent = "00-a1b2c3d4a1b2c3d4a1b2c3d4a1b2c3d4-a1b2c3d4a1b2c3d4-01";
// snippet:end fixedTraceparent

var context = new SyncNarrativeContext(
    new NarrativeTraceConfig(initialTraceparent: Traceparent.Parse(DemoTraceparent)));
var orders = NarrativeTraceProxy.Create<IOrderService>(new OrderService(), context);

orders.PlaceOrder("cust-1", "book-123", 2);

Console.WriteLine(IndentedTextRenderer.Render(context.CaptureTrace()));

public interface IOrderService
{
    string PlaceOrder(string customerId, string productId, int quantity);
}

public sealed class OrderService : IOrderService
{
    public string PlaceOrder(string customerId, string productId, int quantity)
        => $"confirmed:{customerId}:{productId}:{quantity}";
}
```

## 3. Execute

```bash
dotnet run
```

```text
trace: loose hook parks (a1b2c3d)

└── IOrderService.PlaceOrder(customerId: "cust-1", productId: "book-123", quantity: 2) → "confirmed:cust-1:book-123:2" — 10ms
```

(O tempo é a única coisa que vai variar na sua máquina e entre execuções —
o traceparent fixo acima mantém todo o resto, incluindo o nome do trace,
estável.)

Você não escreveu uma única instrução de log. Essa narrativa veio inteira
do nome do seu método, dos nomes dos seus parâmetros e do valor que você
retornou.

## O que acabou de acontecer

- `NarrativeTraceProxy.Create<T>` envolve `OrderService` atrás da sua
  interface `IOrderService` e registra cada chamada feita através do
  wrapper.
- `SyncNarrativeContext` guarda a gravação em memória;
  `NarrativeTraceConfig()` usa por padrão `TracingLevel.Detail` — captura
  completa de parâmetros e valores de retorno, sem nenhum flag para ligar.
- `CaptureTrace()` retorna a árvore gravada; `IndentedTextRenderer` a
  imprimiu acima. `MarkdownRenderer` e `ProseRenderer` renderizam a mesma
  árvore em outros formatos. Envolva um serviço que chama outros serviços
  traçados e a árvore se aninha — uma chamada, uma história.

## Envie para o seu logger

Mais dois pacotes, a mesma árvore capturada, nenhuma lógica de captura nova
— `TraceLogExporter` a reproduz sobre o `ILogger` que seu app já tem
conectado.

```bash
dotnet add package NarrativeTrace.Logging
dotnet add package Microsoft.Extensions.Logging.Console
```

As linhas comentadas abaixo são a mudança — um `Program.cs` completo,
pronto para colar:

```csharp
using Microsoft.Extensions.Logging; // new: send the trace to an ILogger, not only the console
using Microsoft.Extensions.Logging.Console; // new: for LoggerColorBehavior below
using NarrativeTrace.Core;
using NarrativeTrace.Logging; // new: TraceLogExporter replays a captured trace onto an ILogger
using NarrativeTrace.Proxy;
using NarrativeTrace.Runtime;

// A fixed W3C traceparent, seeded through NarrativeTraceConfig so this page's embedded output
// always names the same trace. A real run adopts nothing here (or a real inbound request header,
// via NarrativeTraceMiddleware) and gets a fresh, randomly generated trace id every time.
const string DemoTraceparent = "00-a1b2c3d4a1b2c3d4a1b2c3d4a1b2c3d4-a1b2c3d4a1b2c3d4-01";

var context = new SyncNarrativeContext(
    new NarrativeTraceConfig(initialTraceparent: Traceparent.Parse(DemoTraceparent)));
var orders = NarrativeTraceProxy.Create<IOrderService>(new OrderService(), context);

orders.PlaceOrder("cust-1", "book-123", 2);

var tree = context.CaptureTrace(); // new: capture once, render it to both destinations below
Console.WriteLine(IndentedTextRenderer.Render(tree));

using var loggerFactory = LoggerFactory.Create(builder => // new: a second destination for the same trace
    builder.AddSimpleConsole(options => options.ColorBehavior = LoggerColorBehavior.Disabled)); // new: disable ANSI color codes so captured/piped output stays plain text
TraceLogExporter.ExportToLogger(tree, loggerFactory.CreateLogger("NarrativeTrace")); // new: replay the same tree onto the logger

public interface IOrderService
{
    string PlaceOrder(string customerId, string productId, int quantity);
}

public sealed class OrderService : IOrderService
{
    public string PlaceOrder(string customerId, string productId, int quantity)
        => $"confirmed:{customerId}:{productId}:{quantity}";
}
```

**Por que `AddSimpleConsole(… ColorBehavior.Disabled)` dentro de um
`using var`, e não simplesmente `builder.AddConsole()`.**
`Microsoft.Extensions.Logging.Console` escreve por meio de uma fila em
segundo plano por padrão, então uma linha registrada por ali pode aparecer
depois — ou intercalada de forma estranha com — uma saída síncrona de
`Console.Write*` que logicamente veio depois. Isso é um comportamento da
plataforma, próprio do provedor de console, não um defeito do
NarrativeTrace, e piora quanto mais qualquer um dos dois lados escreve. A
correção que realmente funciona, verificada rodando este exemplo
repetidamente: **liberar (`Dispose`) o `ILoggerFactory` antes que algo
posterior dependa da ordem** — o `using var` acima faz isso
automaticamente quando `Main` termina, e `Dispose()` bloqueia até que a
thread escritora em segundo plano do provedor tenha esvaziado tudo o que
estava na fila, e é exatamente por isso que o bloco abaixo sai na mesma
ordem sempre. `ColorBehavior.Disabled` corrige um problema diferente —
mantém os códigos de escape ANSI fora de uma saída que você planeja
capturar, comparar ou colar em uma página como esta — então vale a pena
manter os dois, mas só a liberação do `using var` corrige a ordem.

```bash
dotnet run
```

```text
trace: loose hook parks (a1b2c3d)

└── IOrderService.PlaceOrder(customerId: "cust-1", productId: "book-123", quantity: 2) → "confirmed:cust-1:book-123:2" — 0ms

info: NarrativeTrace[1]
      IOrderService.PlaceOrder(customerId: "cust-1", productId: "book-123", quantity: 2) -> "confirmed:cust-1:book-123:2"
```

(O tempo é a única coisa que vai variar na sua máquina e entre execuções —
o traceparent fixo acima mantém todo o resto, incluindo o nome do trace,
estável.)

O mesmo trace, dois destinos: o renderizador de console continua exatamente
igual, e o registro de `ILogger` abaixo prova que a árvore chega ao destino
que você já tem — troque `AddSimpleConsole(...)` pelo seu provedor real e mais nada
muda. Veja o [Guia de instalação](../guides/pt-BR/guia-de-instalacao.md)
para os outros pontos de entrada do `NarrativeTrace.Logging`
(`LoggingNarrativeContext` para logging ao vivo por chamada,
`AddNarrativeLogging()` para DI).

## Próximos passos

- **Use em seus testes, ou cablear em DI / ASP.NET Core** —
  [Escolhendo uma integração](escolhendo-uma-integracao.md) é o diagrama de
  decisão; o [Guia de instalação](../guides/pt-BR/guia-de-instalacao.md) tem
  a referência completa de pacotes e conexão.
- **Mantenha um valor fora do trace** — `[NotTraced]`, a lista de bloqueio
  de nomes sempre ativa, e o artefato estrutural `.nt` sem valores são três
  camadas independentes — veja
  [Privacidade e ocultação](privacidade-e-ocultacao.md).
- **Pontue sua nomenclatura** — renomeie `PlaceOrder` para `Process`,
  mantenha tudo o mais igual, e a pontuação de clareza cai — veja o
  [Guia de clareza](../guides/pt-BR/guia-de-clareza.md).
- **Cada opção** — [Guia de configuração](../guides/pt-BR/guia-de-configuracao.md):
  níveis de tracing, formato de saída, cada variável `NARRATIVETRACE_*`.
- **Algo não está funcionando?** — [Solução de problemas](solucao-de-problemas.md):
  sintoma → causa → correção para os modos de falha que as pessoas realmente
  encontram.
