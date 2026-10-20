<!-- source: documentation/guides/dependency-injection.md blob 44d0598f0add | translated: 2026-10-19 | reviewed: - -->
# Guia de encapsulamento automático com injeção de dependências

[English](../dependency-injection.md) | [Español](../es/guia-de-inyeccion-de-dependencias.md) | **Português** | [简体中文](../zh-CN/依赖注入指南.md)

Este guia cobre o `AddNarrativeTracing` — a integração do NarrativeTrace
com `Microsoft.Extensions.DependencyInjection`. Ele encapsula
automaticamente os serviços registrados por interface cujo namespace de
implementação corresponde a um prefixo configurado, de modo que suas
chamadas entram no trace sem tocar nos pontos de chamada. Você aponta
para os namespaces dos seus serviços, e ele decora no local os serviços
correspondentes.

## Pacote

```xml
<PackageReference Include="NarrativeTrace.DependencyInjection" Version="0.4.1" />
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
| `ExcludeNamespaces(params string[])` | fluente | a [lista padrão fornecida](#exclusões-de-instrumentação) | Namespaces nunca rastreados, mesmo quando incluídos. Informá-lo **substitui** o padrão; chame-o uma única vez. |
| `Redaction` | `RedactionPolicy?` | `null` | A política de ocultação com a qual cada serviço que essa chamada encapsula renderiza parâmetros e valores de retorno. Veja abaixo. |

`Namespaces` é aditivo; `ExcludeNamespaces` é informado uma única vez e
substitui a lista padrão como um todo. Ambos são encadeáveis e devolvem a
própria instância de opções:

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
  **interface** (o tipo de serviço), contra o namespace da
  **implementação** e, no momento da resolução, contra o namespace da
  instância que uma factory devolveu. Isso permite incluir um namespace
  amplamente e, depois, excluir os tipos de framework/SPI que vivem em
  outro. Uma exclusão sempre vence uma inclusão.

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
- nem seu namespace de interface nem o de implementação estão **excluídos**
  (`ExcludeNamespaces`, ou a lista padrão fornecida);
- ele **não é código gerado** — tipos gerados pelo compilador, tipos de
  assembly dinâmico (mocks, proxies de `DispatchProxy`) e qualquer coisa que
  já seja um `DispatchProxy` nunca são encapsulados, veja
  [Exclusões de instrumentação](#exclusões-de-instrumentação);
- seu **namespace de implementação está incluído** por `Namespaces`.

Tudo que falhar em uma verificação é deixado exatamente como foi
registrado, então resolvê-lo devolve a implementação bruta. Um serviço que
passa por todas as verificações, mas não pode receber um proxy (por exemplo,
uma interface com um `[NotTraced]` em nível de método), também é devolvido
sem encapsulamento, em vez de fazer o `GetService` do host falhar; uma linha
na saída de erro padrão o nomeia, uma única vez.

> **Divergência com múltiplas interfaces.** O `DispatchProxy` suporta uma
> interface por proxy. Uma classe registrada sob duas interfaces (por
> exemplo, `AddSingleton<IAlpha>(impl)` e `AddSingleton<IBeta>(impl)`) é
> encapsulada **duas vezes** — dois objetos proxy distintos sobre o mesmo
> alvo compartilhado, em vez de um único proxy combinado implementando
> todas as interfaces. Essa forma de proxy único não é tentada aqui de
> propósito; construí-la em .NET exigiria o Castle.DynamicProxy em vez do
> `DispatchProxy` embutido.

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

**A ocultação continua conectando os dois lados, mesmo sendo caminhos de
trace independentes**: uma `RedactionPolicy`
configurada via `services.AddNarrativeTrace(o => o.Redaction = …)` é
registrada na mesma coleção de serviços, então o encapsulamento automático
de `AddNarrativeTracing` a pega como alternativa para cada serviço que
encapsula — sem que essa chamada precise repetir seu próprio `Redaction`.
Veja o [Guia de configuração §6](guia-de-configuracao.md#6-ocultação).

## Exclusões de instrumentação

Duas listas decidem o que o encapsulamento automático nunca rastreia, e elas têm donos diferentes.

1. **Embutida, não configurável: código gerado.** Um tipo que nenhum humano
   escreveu nunca é uma fronteira de negócio, então é ignorado seja qual for
   a lista de inclusão, e nenhuma opção desliga isso. O teste é estrutural,
   nunca um nome de fornecedor: o tipo (ou um tipo que o contém) é
   `[CompilerGenerated]` (closures, display classes, máquinas de estado de
   async e de iteradores, tipos anônimos); seu nome contém `<`, que nenhum
   identificador de código-fonte pode conter; seu assembly é dinâmico (um
   proxy de `DispatchProxy`, um mock do Castle DynamicProxy); ou ele mesmo é
   um `DispatchProxy`. Serviços keyed e genéricos abertos são ignorados da
   mesma forma, em silêncio.
2. **Configurável, com um padrão fornecido: internos de bibliotecas e
   frameworks.** Prefixos de namespace que são infraestrutura, não o seu
   código. Defina sua própria lista com `ExcludeNamespaces(...)`, ou com a
   variável de ambiente `NARRATIVETRACE_EXCLUDE` (prefixos separados por
   `;`). **Informar qualquer um dos dois substitui a lista padrão como um
   todo** — a presença da chave decide, um valor vazio desliga o padrão, e a
   opção vence a variável de ambiente. Para estender o padrão, copie seus
   prefixos para a sua própria lista. Uma exclusão sempre vence uma inclusão,
   e a correspondência é por fronteira de ponto (`Acme` nunca corresponde a
   `Acmex`).

Inclusão e exclusão juntas:

```csharp
services.AddNarrativeTracing(options => options
    .Namespaces("MyApp")
    .ExcludeNamespaces("MyApp.Generated", "Moq", "Xunit"));
```

Cada prefixo de exclusão que retira algo que sua lista de inclusão
selecionou é reportado uma única vez na saída de erro padrão — nunca uma vez
por tipo. A verificação `config.exclude` do doctor imprime a lista efetiva.

A lista padrão fornecida (publicada literalmente a partir de `InstrumentationExclusions.DefaultExclude`):

| Prefixo de namespace | Por que é excluído |
|---|---|
| `Microsoft.Extensions` | DI, logging, options, hosting e configuração: os tipos do próprio contêiner |
| `Microsoft.AspNetCore` | Serviços do próprio framework web e sua cadeia de interceptadores |
| `Microsoft.AI` | SDK do Application Insights |
| `Microsoft.ApplicationInsights` | SDK do Application Insights |
| `Microsoft.Build` | Tipos do próprio MSBuild |
| `Microsoft.CSharp` | Runtime do dynamic binder |
| `System` | A biblioteca de classes base |
| `Datadog` | Runtime de outro tracer de APM; evita instrumentação recursiva quando implantado em conjunto |
| `OpenTelemetry` | O outro APM com maior chance de ser implantado em conjunto |
| `Moq` | Biblioteca de mocking |
| `NSubstitute` | Biblioteca de mocking |
| `FakeItEasy` | Biblioteca de mocking |
| `Castle` | Castle.Core e DynamicProxy, por baixo de Moq, NSubstitute e FakeItEasy |
| `Xunit` | Auxiliares do test runner |
| `NUnit` | Auxiliares do test runner |
| `Microsoft.VisualStudio.TestPlatform` | Host do VSTest |
| `Microsoft.VisualStudio.TestTools` | MSTest |

## Veja também

- [Guia de instalação](guia-de-instalacao.md#opção-b--encapsulamento-automático-por-injeção-de-dependências) — os caminhos de integração em um relance
- [Guia de configuração](guia-de-configuracao.md#3-injeção-de-dependências) — resumo das opções ao lado das demais superfícies de configuração
- [Guia de integração com ASP.NET Core](guia-de-integracao-com-aspnet-core.md) — a alternativa do middleware por requisição
- [Guia de atributos](guia-de-atributos.md) — `[Narrated]`, `[NotTraced]`, ocultação nas chamadas encapsuladas
