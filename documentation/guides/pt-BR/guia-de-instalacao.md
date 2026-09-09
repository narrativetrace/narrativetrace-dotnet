<!-- source: documentation/guides/installation.md blob 305ad017f7d5 | translated: 2026-09-09 | reviewed: 2026-09-03 -->
# NarrativeTrace .NET — Guia de instalação

[English](../installation.md) | [Español](../es/guia-de-instalacion.md) | **Português** | [简体中文](../zh-CN/安装指南.md)

Este guia cobre a instalação e a conexão do NarrativeTrace em um projeto
.NET.

## Pré-requisitos

- SDK do .NET 10 para compilar; as bibliotecas são multi-target `net10.0`
  e `netstandard2.0`, então elas rodam em qualquer runtime .NET compatível
  com netstandard2.0 (.NET Core 2.0+, .NET 5+ e — via o pacote `Legacy`
  — .NET Framework 4.8).
- **Não é necessário nenhum flag do compilador.** Diferente da JVM (que
  precisa de `-parameters`), o .NET mantém os nomes dos parâmetros dos
  métodos nos metadados por padrão, então os traces mostram nomes reais
  prontos para uso. Use o atributo
  [`[Traced]`](guia-de-atributos.md#traced) apenas quando quiser
  sobrescrever um nome.

## Pacotes

Cada projeto em `src/` é publicado como um pacote NuGet com o mesmo id.
Comece com o mínimo e adicione apenas o que você precisar.

```xml
<!-- Mínimo: captura + renderização -->
<PackageReference Include="NarrativeTrace.Core" Version="0.1.1" />
<PackageReference Include="NarrativeTrace.Runtime" Version="0.1.1" />
<PackageReference Include="NarrativeTrace.Proxy" Version="0.1.1" />
```

| Pacote | Quando adicionar |
|---|---|
| `NarrativeTrace.Core` | Sempre — modelo de trace, `INarrativeContext`, configuração, ocultação, renderizadores Markdown/Prose/texto e os atributos `[Narrated]`/`[OnError]`/`[NotTraced]`/`[NarrativeSummary]` (namespace `NarrativeTrace.Core.Annotation`). |
| `NarrativeTrace.Runtime` | Sempre — o motor de captura (`SyncNarrativeContext`, `AsyncNarrativeContext`, exportadores JSON/de capítulos). |
| `NarrativeTrace.Proxy` | Tracing de interfaces via `DispatchProxy`, além da sobrescrita de nome de parâmetro `[Traced]`, específica do proxy. |
| `NarrativeTrace.DependencyInjection` | `AddNarrativeTracing` — encapsulamento automático dos serviços de interface cujo namespace corresponde a um prefixo, no container do MS.DI. |
| `NarrativeTrace.AspNetCore` | Middleware de ciclo de vida do trace por requisição para ASP.NET Core. |
| `NarrativeTrace.Testing.Xunit` | `NarrativeFixture` — contexto por teste e impressão da narrativa de falha (namespace `NarrativeTrace.TestingXunit`). |
| `NarrativeTrace.Testing.NUnit` | `NarrativeTestBase` — o mesmo para NUnit (namespace `NarrativeTrace.TestingNUnit`). |
| `NarrativeTrace.Diagrams` | Renderizadores de diagramas de sequência Mermaid / PlantUML. |
| `NarrativeTrace.Clarity` | Análise e relatórios de clareza de nomes (`ClarityScanner`, `ClarityAnalyzer`). |
| `NarrativeTrace.Observability` | Ponte com OpenTelemetry — exportação em lote e criação de spans ao vivo via `System.Diagnostics.ActivitySource`. |
| `NarrativeTrace.Logging` | Ponte com `Microsoft.Extensions.Logging` (`LoggingNarrativeContext`, `TraceLogExporter`, `AddNarrativeLogging()`). |
| `NarrativeTrace.Cli` | Ferramenta global `dotnet-narrativetrace` — varredura de clareza somente por reflexão + quality gate de CI. |
| `NarrativeTrace.MSBuild` | Pacote somente de build que conecta a CLI a `dotnet build` / `dotnet test`. |

> As versões são pré-1.0 (`0.1.1`). Use a versão que você realmente
> instalou; mantenha todos os pacotes `NarrativeTrace.*` na mesma versão.

## Escolha um caminho de integração

### Opção A — DispatchProxy (funciona em qualquer app .NET)

Encapsule um serviço tipado por interface; cada chamada através do proxy
é registrada no contexto compartilhado.

```csharp
using NarrativeTrace.Core;
using NarrativeTrace.Proxy;
using NarrativeTrace.Runtime;

var context = new SyncNarrativeContext(
    new NarrativeTraceConfig(TracingLevel.Detail));

var orders = NarrativeTraceProxy.Create<IOrderService>(
    new DefaultOrderService(), context);

orders.PlaceOrder("C-1234", "SKU-KB", 2);

Console.WriteLine(IndentedTextRenderer.Render(context.CaptureTrace()));
context.Reset();
```

Use essa opção quando seus serviços forem baseados em interface.
Compartilhe **um único** contexto entre os serviços que colaboram para
que suas chamadas se aninhem em uma única árvore.

### Opção B — Encapsulamento automático por injeção de dependência

O equivalente em `.NET` do encapsulamento automático de beans do
Spring/Micronaut. Registre seus serviços como de costume e, em seguida,
encapsule aqueles cujo namespace de implementação corresponde a um
prefixo:

```csharp
using NarrativeTrace.DependencyInjection;

services.AddScoped<IOrderService, DefaultOrderService>();

services.AddNarrativeTracing(options => options
    .Namespaces("MyApp.Services", "MyApp.Payments"));
```

Somente os serviços registrados por **interface** cujo namespace de
implementação corresponde a um prefixo configurado são encapsulados. Um
`INarrativeContext` scoped é registrado automaticamente. Consulte o
[Guia de configuração](guia-de-configuracao.md#3-injeção-de-dependência)
para ver as opções.

### Opção C — Tracing de requisições no ASP.NET Core

Adicione os serviços e posicione o middleware no início do pipeline.
Cada requisição recebe um trace novo (em `HttpContext`), que o
middleware captura e exporta quando a requisição termina. Trace seus
serviços resolvendo esse contexto por requisição:

```csharp
using NarrativeTrace.AspNetCore;
using NarrativeTrace.Proxy;

builder.Services.AddNarrativeTrace(builder.Configuration);

var app = builder.Build();
app.UseMiddleware<NarrativeTraceMiddleware>();

app.MapGet("/orders/{id}", (string id, HttpContext http) =>
{
    var orders = NarrativeTraceProxy.Create<IOrderService>(
        new DefaultOrderService(), http.GetNarrativeContext());
    return orders.FindOrder(id);
});
```

Consulte o
[Guia de integração com ASP.NET Core](guia-de-integracao-com-aspnet-core.md)
para exportadores, rotas excluídas e contexto de requisição/usuário.

### Opção D — Contexto automático no xUnit + narrativa de falha

```csharp
using NarrativeTrace.Proxy;
using NarrativeTrace.TestingXunit;   // nota: sem ponto antes de Xunit
using Xunit;

public sealed class OrderServiceTests
{
    [Fact]
    public void Customer_places_order()
    {
        using var narrative = new NarrativeFixture();

        narrative.Run("Customer places order", context =>
        {
            var orders = NarrativeTraceProxy.Create<IOrderService>(
                new DefaultOrderService(), context);
            orders.PlaceOrder("C-1234", "SKU-KB", 2);
        });
    }
}
```

Crie um `NarrativeFixture` por teste com `using var` para que cada teste
tenha um contexto limpo (o dispose o reinicia). `NarrativeFixture.Run`
imprime a narrativa capturada no console quando o corpo lança uma
exceção e depois a relança — assim, um teste que falha se explica
sozinho.

### Opção E — Contexto automático no NUnit + narrativa de falha

```csharp
using NarrativeTrace.Proxy;
using NarrativeTrace.TestingNUnit;   // nota: sem ponto antes de NUnit
using NUnit.Framework;

public sealed class OrderServiceTests : NarrativeTestBase
{
    [Test]
    public void Customer_places_order()
    {
        var orders = NarrativeTraceProxy.Create<IOrderService>(
            new DefaultOrderService(), Context);
        orders.PlaceOrder("C-1234", "SKU-KB", 2);
    }
}
```

`NarrativeTestBase` abre um contexto novo por teste (`[SetUp]`) e
imprime a narrativa em caso de falha (`[TearDown]`). Sobrescreva
`OnTraceComplete` para gravar arquivos ou executar a análise de
clareza.

### Opção F — CLI `dotnet-narrativetrace`

Instale a ferramenta global e analise a clareza dos nomes a partir de
um assembly compilado — sem precisar rodar testes:

```bash
dotnet tool install --global NarrativeTrace.Cli

dotnet-narrativetrace clarity-scan --assembly bin/Release/net10.0/MyApp.dll
dotnet-narrativetrace clarity-check --results clarity-results.json --min-score 0.80 --max-high-issues 0
```

Consulte o [Guia de clareza](guia-de-clareza.md) para o modelo de
pontuação e o quality gate de CI.

### Opção G — Integração com MSBuild

Adicione o pacote somente de build para executar o quality gate de
clareza como parte do seu build:

```xml
<PackageReference Include="NarrativeTrace.MSBuild" Version="0.1.1"
                  PrivateAssets="all" />
```

Isso registra os targets `ClarityScan` e `ClarityCheck` e repassa as
configurações `NARRATIVETRACE_*` para o host de testes. Configure os
limiares com propriedades do MSBuild (consulte o
[Guia de configuração](guia-de-configuracao.md#5-msbuild)).

## Configurar a saída de traces

A biblioteca lê quatro variáveis de ambiente `NARRATIVETRACE_*` através
do `ConfigResolver` — o canal de sobrescrita nativo do `.NET`:

| Variável | Valores | Padrão |
|---|---|---|
| `NARRATIVETRACE_LEVEL` | `Off`, `Errors`, `Summary`, `Narrative`, `Detail` | `Detail` |
| `NARRATIVETRACE_OUTPUT` | `true` / `false` (também `1`) | `false` |
| `NARRATIVETRACE_OUTPUT_DIR` | qualquer caminho com permissão de escrita | (nenhum) |
| `NARRATIVETRACE_FORMAT` | `Markdown`, `Text`, `Prose`, `Json` | `Markdown` |

Valores inválidos degradam para o padrão em vez de lançar uma exceção,
então uma configuração incorreta nunca derruba a captura. O parsing do
nível é tolerante a maiúsculas/minúsculas e pontuação (`detail`,
`DETAIL`, `Detail` todos resolvem para o mesmo valor).

## Validar a instalação

Renderize um trace no console:

```csharp
var context = new SyncNarrativeContext(
    new NarrativeTraceConfig(TracingLevel.Detail));
var svc = NarrativeTraceProxy.Create<IGreeter>(new Greeter(), context);
svc.Greet("world");
Console.WriteLine(IndentedTextRenderer.Render(context.CaptureTrace()));
```

Você deve ver uma narrativa aninhada com o nome do método, os valores
dos parâmetros e o valor de retorno.

## Veja também

- [Guia de configuração](guia-de-configuracao.md) — níveis de tracing, variáveis de ambiente, DI, ASP.NET Core, MSBuild
- [Guia de atributos](guia-de-atributos.md) — `[Narrated]`, `[OnError]`, `[NotTraced]`, `[Traced]`, `[NarrativeSummary]`
- [Guia de integração com ASP.NET Core](guia-de-integracao-com-aspnet-core.md) — middleware, exportadores, contexto de requisição/usuário
- [Guia de clareza](guia-de-clareza.md) — modelo de pontuação, scanner, quality gate de CI
