<!-- source: documentation/guides/dependency-injection.md blob fdd3acbcda54 | translated: 2026-09-09 | reviewed: 2026-09-03 -->
# Guia de encapsulamento automático com injeção de dependências

[English](../dependency-injection.md) | [Español](../es/guia-de-inyeccion-de-dependencias.md) | **Português** | [简体中文](../zh-CN/依赖注入指南.md)

Este guia cobre o `AddNarrativeTracing` — a integração do NarrativeTrace
com `Microsoft.Extensions.DependencyInjection`. Ele encapsula
automaticamente os serviços registrados por interface cujo namespace de
implementação corresponde a um prefixo configurado, de modo que suas
chamadas entram no trace sem tocar nos pontos de chamada. É o equivalente
em `.NET` do tracing de beans do Spring/Micronaut: você aponta para os
namespaces dos seus serviços, e ele decora no local os beans
correspondentes.

## Pacote

```xml
<PackageReference Include="NarrativeTrace.DependencyInjection" Version="0.1.2" />
```

## 1. Registre e encapsule automaticamente

Registre seus serviços como de costume e, depois, chame o
`AddNarrativeTracing` **depois** deles — ele reescreve os descritores já
presentes na coleção, então registros adicionados posteriormente não são
vistos.

```csharp
using NarrativeTrace.Core;
using NarrativeTrace.DependencyInjection;

services.AddScoped<IOrderService, DefaultOrderService>();
services.AddScoped<IPaymentGateway, StripePaymentGateway>();

services.AddNarrativeTracing(options => options
    .Namespaces("MyApp.Orders", "MyApp.Payments"));
```

Cada descritor de interface correspondente é substituído por outro que
resolve a implementação original e devolve um `NarrativeTraceProxy` sobre
ela. O proxy registra cada chamada em um `INarrativeContext` compartilhado,
com escopo de DI.

O `AddNarrativeTracing` também registra, com `TryAdd` (de modo que um
registro seu anterior prevalece):

- um `NarrativeTraceConfig` singleton, construído a partir de
  `options.Level`;
- um `INarrativeContext` **scoped** (um `SyncNarrativeContext` sobre essa
  configuração).

Capture o trace você mesmo a partir do escopo em que executou o trabalho:

```csharp
using var scope = provider.CreateScope();
scope.ServiceProvider.GetRequiredService<IOrderService>()
    .PlaceOrder("C-1234", "SKU-KB", 2);

var trace = scope.ServiceProvider
    .GetRequiredService<INarrativeContext>()
    .CaptureTrace();
```

## 2. Opções

O `AddNarrativeTracing(Action<NarrativeTracingDiOptions>)` recebe um
delegate de configuração obrigatório. O `NarrativeTracingDiOptions` expõe:

| Membro | Tipo | Padrão | Finalidade |
|---|---|---|---|
| `Level` | `TracingLevel` | `Detail` | Nível de captura do contexto scoped compartilhado. |
| `Namespaces(params string[])` | fluente | (vazio) | Namespaces base a encapsular automaticamente. |
| `ExcludeNamespaces(params string[])` | fluente | (vazio) | Namespaces de interface a excluir, mesmo quando o namespace de implementação correspondente estiver incluído. |

`Namespaces` e `ExcludeNamespaces` são aditivos e encadeáveis, e cada um
devolve a própria instância de opções:

```csharp
services.AddNarrativeTracing(options =>
{
    options.Level = TracingLevel.Narrative;
    options
        .Namespaces("MyApp.Orders", "MyApp.Payments")
        .ExcludeNamespaces("MyApp.Payments.Spi");
});
```

Sem nenhum namespace base configurado, nada corresponde — o encapsulamento
automático é estritamente opt-in por namespace.

## 3. Semântica de correspondência de namespaces

Dois namespaces distintos estão envolvidos, e cada um é comparado com um
conjunto de regras diferente:

- A **inclusão** (`Namespaces`) é testada contra o namespace da
  **implementação** — o namespace do tipo concreto (veja a
  [seção 5](#5-como-o-namespace-de-implementação-é-resolvido)).
- A **exclusão** (`ExcludeNamespaces`) é testada contra o namespace da
  **interface** (o tipo de serviço). Isso permite incluir amplamente um
  namespace de implementação e, depois, excluir as interfaces de
  framework/SPI que vivem em outro namespace — o análogo da exclusão de
  SPI/configuração do Spring.

Ambas usam a mesma regra de **fronteira de ponto**: um candidato
corresponde a uma base quando é exatamente igual a ela, ou começa com a
base **seguida de um ponto**. Um irmão que apenas compartilha um prefixo de
caracteres não corresponde.

Dada a base `MyApp`:

| Namespace candidato | Corresponde? | Por quê |
|---|---|---|
| `MyApp` | sim | exato |
| `MyApp.Orders` | sim | sub-namespace |
| `MyApp.Orders.Internal` | sim | sub-namespace profundo |
| `MyApp2` | não | irmão, sem fronteira de ponto |
| `MyAppImpl` | não | prefixo compartilhado, sem fronteira de ponto |
| `My` | não | mais curto que a base |
| `Other` | não | sem relação |
| `null` / `""` | não | um namespace desconhecido nunca corresponde |

A correspondência é **ordinal / sensível a maiúsculas e minúsculas**, e
basta que uma única base configurada corresponda.

## 4. O que é encapsulado (e o que não é)

Um descritor é encapsulado somente quando **todas** estas condições se
cumprem:

- o **tipo de serviço é uma interface** (registros de classes concretas
  são deixados como estão);
- **não é um genérico aberto** (por exemplo, `IRepository<>`) — o
  `DispatchProxy` não consegue encapsular genéricos abertos;
- **não é um registro keyed** — serviços keyed são tolerados e ficam sem
  encapsulamento (sem lançar exceção); ainda não são suportados;
- seu **namespace de interface não está excluído** por `ExcludeNamespaces`;
- seu **namespace de implementação está incluído** por `Namespaces`.

Tudo que falhar em uma verificação é deixado exatamente como foi
registrado, então resolvê-lo devolve a implementação bruta.

> **Divergência com múltiplas interfaces.** O `DispatchProxy` suporta uma
> interface por proxy. Uma classe registrada sob duas interfaces (por
> exemplo, `AddSingleton<IAlpha>(impl)` e `AddSingleton<IBeta>(impl)`) é
> encapsulada **duas vezes** — dois objetos proxy distintos sobre o mesmo
> alvo compartilhado. Isso difere do Spring/Micronaut, que constroem um
> único proxy implementando todas as interfaces de um bean. A divergência é
> intencional; igualá-la em .NET exigiria o Castle.DynamicProxy.

## 5. Como o namespace de implementação é resolvido

A verificação de inclusão precisa de um namespace concreto. Ele é obtido,
em ordem, a partir de:

1. o `ImplementationType` do descritor (serviços registrados por tipo);
2. senão, o tipo em tempo de execução de seu `ImplementationInstance`
   (serviços registrados por instância);
3. senão, um fallback para o **namespace do tipo de serviço (a
   interface)** — usado para serviços registrados por **factory**
   (`AddScoped<IFoo>(sp => …)`), cujo tipo de implementação é opaco no
   momento do registro.

Como interfaces e suas implementações quase sempre estão no mesmo lugar, o
fallback do factory encapsula corretamente o caso comum; se sua factory
devolver um tipo de um namespace diferente do da interface, a
correspondência passará a ser feita pelo namespace da interface.

## 6. Comportamento de tempo de vida e escopo

- O **tempo de vida** do registro original é **preservado**. Um singleton
  continua singleton (a mesma instância de proxy é devolvida em toda
  resolução), um serviço scoped continua scoped, e assim por diante.
- O `INarrativeContext` compartilhado é **scoped**. Todo serviço
  encapsulado resolvido dentro do mesmo escopo de DI registra no **mesmo**
  contexto, então suas chamadas se aninham em uma única árvore de trace.
  Crie um escopo por unidade de trabalho (por requisição, por job) e
  capture o trace a partir desse escopo.
- Como o contexto é scoped, mas um **singleton** encapsulado sobrevive a
  qualquer escopo, um proxy singleton resolve o contexto do escopo atual a
  cada chamada por meio do service provider — ele não fica preso a um
  contexto obsoleto.

## 7. Interação com o middleware do ASP.NET Core

O middleware `NarrativeTrace.AspNetCore` e o encapsulamento automático do
`AddNarrativeTracing` são **dois caminhos de tracing independentes** — não
os combine no mesmo caminho de requisição:

| | Middleware (`NarrativeTraceMiddleware`) | Encapsulamento automático de DI (`AddNarrativeTracing`) |
|---|---|---|
| O contexto vive em | `HttpContext.Items` (por requisição) | um `INarrativeContext` com escopo de DI |
| Você rastreia | encapsulando contra `HttpContext.GetNarrativeContext()` | resolvendo os serviços encapsulados automaticamente a partir do escopo |
| Captura e exportação | automáticas, no `finally` do middleware | você mesmo chama `CaptureTrace()` |

**Escolha um por caminho de requisição.** Os serviços encapsulados
automaticamente registram no contexto com escopo de DI, não em
`HttpContext.Items`, então eles **não** vão aparecer no trace exportado
pelo middleware. Se você quiser a captura e a exportação automáticas por
requisição do middleware, encapsule seus serviços contra
`HttpContext.GetNarrativeContext()` (veja o
[Guia de integração com ASP.NET Core](guia-de-integracao-com-aspnet-core.md#2-trace-seus-serviços)).
Recorra ao encapsulamento automático de DI em hosts que não são web
(workers, apps de console, consumidores de mensagens) onde você controla o
escopo e captura o trace diretamente.

## Veja também

- [Guia de instalação](guia-de-instalacao.md#opção-b--encapsulamento-automático-por-injeção-de-dependências) — os caminhos de integração em um relance
- [Guia de configuração](guia-de-configuracao.md#3-injeção-de-dependências) — resumo das opções ao lado das demais superfícies de configuração
- [Guia de integração com ASP.NET Core](guia-de-integracao-com-aspnet-core.md) — a alternativa do middleware por requisição
- [Guia de atributos](guia-de-atributos.md) — `[Narrated]`, `[NotTraced]`, ocultação nas chamadas encapsuladas
