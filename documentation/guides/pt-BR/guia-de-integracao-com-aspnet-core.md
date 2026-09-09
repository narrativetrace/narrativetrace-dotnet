<!-- source: documentation/guides/aspnetcore.md blob d93be958c0a7 | translated: 2026-09-09 | reviewed: 2026-09-03 -->
# Guia de integração com ASP.NET Core

[English](../aspnetcore.md) | [Español](../es/guia-de-integracion-con-aspnet-core.md) | **Português** | [简体中文](../zh-CN/ASP.NET-Core集成指南.md)

Este guia cobre o tracing de requisições HTTP em uma aplicação ASP.NET
Core: o ciclo de vida do middleware por requisição, a exportação plugável,
a exclusão de caminhos e o contexto de requisição/usuário.

## Pacote

```xml
<PackageReference Include="NarrativeTrace.AspNetCore" Version="0.1.1" />
```

## 1. Registrar e conectar o middleware

`AddNarrativeTrace` registra o middleware, as opções, a configuração e um
exportador padrão. `app.UseMiddleware<NarrativeTraceMiddleware>()` o
coloca no pipeline — bem no início, para que ele envolva toda a
requisição.

```csharp
using NarrativeTrace.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddNarrativeTrace(builder.Configuration);

var app = builder.Build();
app.UseMiddleware<NarrativeTraceMiddleware>();
```

`NarrativeTraceMiddleware` é um `IMiddleware`, então é resolvido a partir
do DI (registrado como transient por `AddNarrativeTrace`). Seu ciclo de
vida por requisição é:

1. **Pular** se o caminho corresponder a um prefixo excluído (chama
   `next` e retorna — nenhum contexto é criado).
2. **Criar** um novo `SyncNarrativeContext` e armazená-lo em
   `HttpContext.Items` (recupere-o com `HttpContext.GetNarrativeContext()`).
3. **Estampar** o nível da requisição — método HTTP, rota, IP do cliente —
   e, se um `IRequestContextProvider` estiver registrado, o nível do
   usuário.
4. **Executar** o restante do pipeline (`await next`).
5. **Capturar e exportar** o trace em um bloco `finally` — assim, uma
   requisição que lança uma exceção ainda produz um trace, com o código
   de status da resposta e os milissegundos decorridos.

## 2. Instrumente seus serviços

O middleware possui um contexto por requisição; envolva os serviços que
você quer instrumentar usando esse mesmo contexto, para que as chamadas
deles se aninhem no trace da requisição. Resolva-o com
`HttpContext.GetNarrativeContext()`:

```csharp
app.MapPost("/orders", (OrderRequest request, HttpContext http) =>
{
    var context = http.GetNarrativeContext();
    var orders = NarrativeTraceProxy.Create<IOrderService>(
        new DefaultOrderService(), context);

    return orders.PlaceOrder(
        request.CustomerId, request.ProductId, request.Quantity);
});
```

`GetNarrativeContext()` retorna `NoopContext.Instance` em uma requisição
sem trace (por exemplo, um caminho excluído), então envolver é sempre
seguro — ele simplesmente não registra nada quando não há um trace ativo.

> **Nota:** o contexto por requisição do middleware é independente do
> envolvimento automático de DI do `AddNarrativeTracing` (veja o
> [Guia de configuração](guia-de-configuracao.md#3-injeção-de-dependências)).
> O envolvimento automático registra em um contexto com escopo de DI que
> você mesmo captura; o middleware registra em `HttpContext.Items` e
> exporta automaticamente. Escolha um por caminho de requisição — não
> espere que serviços envolvidos automaticamente apareçam no trace
> exportado pelo middleware.

## 3. Exportar

Por padrão, `AddNarrativeTrace` registra o `LoggerTraceExporter`, que loga
cada trace concluído como saída estruturada sob a categoria de logger
`NarrativeTrace.Export`. Forneça seu próprio `ITraceExporter` para enviar
os traces para outro lugar:

```csharp
public sealed class OtlpTraceExporter : ITraceExporter
{
    public void Export(TraceTree tree, RequestContext request)
    {
        // request.StatusCode, request.DurationMs
        // envie `tree` para o seu backend de observabilidade
    }
}

builder.Services.AddSingleton<ITraceExporter, OtlpTraceExporter>();
builder.Services.AddNarrativeTrace(builder.Configuration);
```

`AddNarrativeTrace` registra seu exportador padrão com `TryAdd`, então um
registro que você fizer **antes** dele prevalece. `RequestContext` carrega
`StatusCode` e `DurationMs` — os fatos do resultado, conhecidos apenas
quando a requisição termina.

## 4. Excluir caminhos

Ignore completamente health checks, métricas e outros ruídos — caminhos
excluídos não criam contexto nem trace:

```csharp
builder.Services.AddNarrativeTrace(builder.Configuration, options =>
{
    options.Level = TracingLevel.Detail;
    options.ExcludedPaths.Add("/health");
    options.ExcludedPaths.Add("/metrics");
});
```

Ou em `appsettings.json`:

```json
{
  "NarrativeTrace": {
    "Level": "Detail",
    "ExcludedPaths": [ "/health", "/metrics" ]
  }
}
```

A correspondência é por **segmento** de caminho, sem diferenciar
maiúsculas de minúsculas: `/health` exclui `/health` e `/health/live`, mas
não `/healthcheck`.

Usar as duas fontes juntas tem comportamento bem definido: a configuração
é vinculada primeiro e o delegate de opções é executado depois, então
`Level` e a identidade do serviço assumem o valor definido **no código**.
`ExcludedPaths` é a exceção — os caminhos configurados são *adicionados* à
lista, então as duas fontes se acumulam em vez de se substituírem. As
variáveis de ambiente `NARRATIVETRACE_*`
(veja o [Guia de configuração](guia-de-configuracao.md#2-variáveis-de-ambiente-configresolver))
são um canal opcional separado que `AddNarrativeTrace` nunca lê.

## 5. Contexto do usuário

Para estampar a identidade do usuário no trace (id do usuário final,
sessão, tenant), implemente `IRequestContextProvider` e registre-o. O
provedor é uma função pura — ele *retorna* a identidade que derivou, e o
middleware decide para onde ela vai:

```csharp
public sealed class ClaimsRequestContextProvider : IRequestContextProvider
{
    public UserContext? ResolveUserContext(HttpContext context)
    {
        var user = context.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            return null;   // tráfego anônimo é normal, não um erro
        }

        return new UserContext(
            EnduserId: user.FindFirst("sub")?.Value,
            TenantId: user.FindFirst("tenant")?.Value);
    }
}

builder.Services.AddSingleton<
    IRequestContextProvider, ClaimsRequestContextProvider>();
```

Todo membro de `UserContext` é opcional; retorne apenas o que você sabe, e
`null` quando a requisição não carregar identidade alguma. O middleware
estampa os valores retornados no trace (`SetUserContext`) e adiciona os
campos presentes `enduserId` / `sessionId` / `tenantId` ao escopo de log
da requisição, ao lado de `traceId` / `traceName` / `httpMethod` /
`httpRoute` / `clientIp`. Um provedor que lança uma exceção é engolido —
a observabilidade nunca faz a requisição falhar.

Esses valores fluem para a exportação JSON, os escopos de logging e os
spans do OpenTelemetry como os níveis de atributos `enduser.*` /
`session.*` / `tenant.*`.

## 6. Testar a integração

Você pode exercitar o middleware diretamente com um `DefaultHttpContext`
— sem precisar de um host web:

```csharp
var middleware = new NarrativeTraceMiddleware(
    new NarrativeTraceConfig(TracingLevel.Detail));

var http = new DefaultHttpContext();
await middleware.InvokeAsync(http, ctx =>
{
    var orders = NarrativeTraceProxy.Create<IOrderService>(
        new DefaultOrderService(), ctx.GetNarrativeContext());
    orders.PlaceOrder("C-1", "SKU-1", 1);
    return Task.CompletedTask;
});
```

Para testes unitários mais rápidos de serviços individuais, pule o
middleware e use os
[helpers de teste do xUnit / NUnit](guia-de-instalacao.md#opção-d--contexto-automático-do-xunit--narrativa-de-falha)
diretamente com `NarrativeTraceProxy`.

## Veja também

- [Guia de instalação](guia-de-instalacao.md) — pacotes e caminhos de integração
- [Guia de configuração](guia-de-configuracao.md) — níveis, caminhos excluídos, envolvimento automático de DI
- [Guia de atributos](guia-de-atributos.md) — narração, ocultação, contexto de erro
