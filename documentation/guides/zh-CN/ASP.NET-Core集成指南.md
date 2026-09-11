<!-- source: documentation/guides/aspnetcore.md blob dcbb1a9ba068 | translated: 2026-09-09 | reviewed: 2026-09-03 -->
# ASP.NET Core 集成指南

[English](../aspnetcore.md) | [Español](../es/guia-de-integracion-con-aspnet-core.md) | [Português](../pt-BR/guia-de-integracao-com-aspnet-core.md) | **简体中文**

本指南介绍如何在 ASP.NET Core 应用中追踪 HTTP 请求：按请求的中间件生命
周期、可插拔的导出、路径排除，以及请求/用户上下文。

## 包

```xml
<PackageReference Include="NarrativeTrace.AspNetCore" Version="0.1.2" />
```

## 1. 注册并接入中间件

`AddNarrativeTrace` 会注册中间件、选项、配置以及一个默认导出器。
`app.UseMiddleware<NarrativeTraceMiddleware>()` 把它放进管道 — 要足够靠
前，才能包住整个请求。

```csharp
using NarrativeTrace.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddNarrativeTrace(builder.Configuration);

var app = builder.Build();
app.UseMiddleware<NarrativeTraceMiddleware>();
```

`NarrativeTraceMiddleware` 是一个 `IMiddleware`，因此从 DI 解析(由
`AddNarrativeTrace` 注册为 transient)。它按请求的生命周期是：

1. **跳过** — 如果路径匹配某个被排除的前缀(调用 `next` 并返回，不创建
   上下文)。
2. **创建** 一个新的 `SyncNarrativeContext` 并存入 `HttpContext.Items`
   (用 `HttpContext.GetNarrativeContext()` 取回)。
3. **标记** 请求层级 — HTTP 方法、路由、客户端 IP — 如果注册了
   `IRequestContextProvider`，还会标记用户层级。
4. **运行** 管道的其余部分(`await next`)。
5. **捕获并导出** 追踪，在 `finally` 块中完成 — 因此抛出异常的请求同样
   会产出追踪，并带上响应状态码与耗时毫秒数。

## 2. 追踪你的服务

中间件持有一个按请求的上下文；把你想追踪的服务包装到**那个**上下文上，
它们的调用才会嵌套进该请求的追踪里。用 `HttpContext.GetNarrativeContext()`
解析它：

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

在未被追踪的请求上(例如被排除的路径)，`GetNarrativeContext()` 返回
`NoopContext.Instance`，因此包装始终是安全的 — 没有活动追踪时它什么也不
记录。

> **注意：** 中间件的按请求上下文与 `AddNarrativeTracing` 的 DI 自动包装
> 是相互独立的(参见[配置指南](配置指南.md#3-依赖注入))。自动包装记录到一个
> 由你自己捕获的 DI scoped 上下文；中间件记录到 `HttpContext.Items` 并
> 自动导出。每条请求路径只选其一 — 不要指望自动包装的服务出现在中间件
> 导出的追踪里。

## 3. 导出

默认情况下 `AddNarrativeTrace` 注册 `LoggerTraceExporter`，它把每份完成
的追踪以结构化输出记录在 `NarrativeTrace.Export` 日志类别下。提供你自己
的 `ITraceExporter` 即可把追踪送往别处：

```csharp
public sealed class OtlpTraceExporter : ITraceExporter
{
    public void Export(TraceTree tree, RequestContext request)
    {
        // request.StatusCode、request.DurationMs
        // 把 `tree` 发送到你的可观测性后端
    }
}

builder.Services.AddSingleton<ITraceExporter, OtlpTraceExporter>();
builder.Services.AddNarrativeTrace(builder.Configuration);
```

`AddNarrativeTrace` 用 `TryAdd` 注册它的默认导出器，因此你在它**之前**
做的注册会胜出。`RequestContext` 携带 `StatusCode` 与 `DurationMs` — 这
些结果事实只有在请求结束后才知道。

## 4. 排除路径

彻底跳过健康检查、指标等噪音 — 被排除的路径不创建上下文，也不产生追踪：

```csharp
builder.Services.AddNarrativeTrace(builder.Configuration, options =>
{
    options.Level = TracingLevel.Detail;
    options.ExcludedPaths.Add("/health");
    options.ExcludedPaths.Add("/metrics");
});
```

或者写在 `appsettings.json` 中：

```json
{
  "NarrativeTrace": {
    "Level": "Detail",
    "ExcludedPaths": [ "/health", "/metrics" ]
  }
}
```

匹配按路径**段**进行，且不区分大小写：`/health` 会排除 `/health` 和
`/health/live`，但不会排除 `/healthcheck`。

同时使用两个来源的行为是明确定义的：配置先绑定，选项委托随后执行，因此
`Level` 和服务标识采用**代码中**设置的值。`ExcludedPaths` 是例外——配置的
路径会*追加*到列表中，因此两个来源是累加而非相互替换。`NARRATIVETRACE_*`
环境变量(参见[配置指南](配置指南.md#2-环境变量configresolver))是独立的可选
通道，`AddNarrativeTrace` 从不读取它们。

## 5. 用户上下文

要把用户身份(终端用户 id、会话、租户)标记到追踪上，请实现
`IRequestContextProvider` 并注册它。该提供程序是一个纯函数 — 它*返回*自
己解析出的身份，由中间件决定这些值的去向：

```csharp
public sealed class ClaimsRequestContextProvider : IRequestContextProvider
{
    public UserContext? ResolveUserContext(HttpContext context)
    {
        var user = context.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            return null;   // 匿名流量是正常现象，不是错误
        }

        return new UserContext(
            EnduserId: user.FindFirst("sub")?.Value,
            TenantId: user.FindFirst("tenant")?.Value);
    }
}

builder.Services.AddSingleton<
    IRequestContextProvider, ClaimsRequestContextProvider>();
```

`UserContext` 的每个成员都是可选的；只返回你知道的部分，请求完全不带身
份时返回 `null`。中间件会把返回的值标记到追踪上(`SetUserContext`)，并把
存在的字段 `enduserId` / `sessionId` / `tenantId` 加入该请求的日志作用
域，与 `traceId` / `traceName` / `httpMethod` / `httpRoute` / `clientIp`
并列。提供程序抛出的异常会被吞掉 — 可观测性绝不会让请求失败。

这些值会流入 JSON 导出、日志作用域以及 OpenTelemetry Span，成为
`enduser.*` / `session.*` / `tenant.*` 属性层级。

## 6. 测试集成

你可以直接用 `DefaultHttpContext` 来驱动中间件 — 无需 Web 宿主：

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

若要更快地对单个服务做单元测试，可以跳过中间件，直接把
[xUnit / NUnit 测试助手](安装指南.md#方式-d--xunit-自动上下文--失败叙事)
与 `NarrativeTraceProxy` 一起使用。

## 另请参阅

- [安装指南](安装指南.md) — 包与集成方式
- [配置指南](配置指南.md) — 级别、排除路径、DI 自动包装
- [特性指南](特性指南.md) — 叙述文本、脱敏、错误上下文
