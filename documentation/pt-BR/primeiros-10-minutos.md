<!-- source: documentation/first-10-minutes.md blob 85c4e7df07b9 | translated: 2026-09-12 | reviewed: - -->
# Veja um trace em 60 segundos

[English](../first-10-minutes.md) | [Español](../es/primeros-10-minutos.md) | **Português** | [简体中文](../zh-CN/前10分钟.md)

Sem instruções de log, sem framework de testes, sem arquivos para abrir: um
app de console, um `dotnet run`, e um trace no seu terminal. Tudo abaixo foi
executado de verdade contra os pacotes publicados — a saída está colada,
não imaginada.

## 1. Novo app de console, adicione os pacotes

```bash
dotnet new console -n Hello && cd Hello
dotnet add package NarrativeTrace.Core
dotnet add package NarrativeTrace.Runtime
dotnet add package NarrativeTrace.Proxy
```

Três pacotes, resolvidos a partir do nuget.org — ainda não existe um único
metapacote que traga tudo de uma vez.

## 2. Substitua o Program.cs

```csharp
using NarrativeTrace.Core;
using NarrativeTrace.Proxy;
using NarrativeTrace.Runtime;

var context = new SyncNarrativeContext(new NarrativeTraceConfig());
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
└── IOrderService.PlaceOrder(customerId: "cust-1", productId: "book-123", quantity: 2) → "confirmed:cust-1:book-123:2" — 9ms
```

(O tempo vai variar de uma execução para outra — o resto é estável.)

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

```diff
+using Microsoft.Extensions.Logging;
 using NarrativeTrace.Core;
+using NarrativeTrace.Logging;
 using NarrativeTrace.Proxy;
 using NarrativeTrace.Runtime;

 var context = new SyncNarrativeContext(new NarrativeTraceConfig());
 var orders = NarrativeTraceProxy.Create<IOrderService>(new OrderService(), context);

 orders.PlaceOrder("cust-1", "book-123", 2);

-Console.WriteLine(IndentedTextRenderer.Render(context.CaptureTrace()));
+var tree = context.CaptureTrace();
+Console.WriteLine(IndentedTextRenderer.Render(tree));
+
+using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
+TraceLogExporter.ExportToLogger(tree, loggerFactory.CreateLogger("NarrativeTrace"));
```

```bash
dotnet run
```

```text
└── IOrderService.PlaceOrder(customerId: "cust-1", productId: "book-123", quantity: 2) → "confirmed:cust-1:book-123:2" — 0ms

info: NarrativeTrace[1]
      IOrderService.PlaceOrder(customerId: "cust-1", productId: "book-123", quantity: 2) -> "confirmed:cust-1:book-123:2"
```

(O tempo vai variar de uma execução para outra — o resto é estável.)

O mesmo trace, dois destinos: o renderizador de console continua exatamente
igual, e o registro de `ILogger` abaixo prova que a árvore chega ao destino
que você já tem — troque `AddConsole()` pelo seu provedor real e mais nada
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
