# ASP.NET Core Integration Guide

**English** | [Español](es/guia-de-integracion-con-aspnet-core.md) | [Português](pt-BR/guia-de-integracao-com-aspnet-core.md) | [简体中文](zh-CN/ASP.NET-Core集成指南.md)

This guide covers tracing HTTP requests in an ASP.NET Core application:
the per-request middleware lifecycle, pluggable export, path exclusion,
and request/user context.

## Package

```xml
<PackageReference Include="NarrativeTrace.AspNetCore" Version="0.1.1" />
```

## 1. Register and wire the middleware

`AddNarrativeTrace` registers the middleware, options, config, and a
default exporter. `app.UseMiddleware<NarrativeTraceMiddleware>()` places it
in the pipeline — early, so it wraps the whole request.

```csharp
using NarrativeTrace.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddNarrativeTrace(builder.Configuration);

var app = builder.Build();
app.UseMiddleware<NarrativeTraceMiddleware>();
```

`NarrativeTraceMiddleware` is an `IMiddleware`, so it is resolved from DI
(registered as transient by `AddNarrativeTrace`). Its per-request
lifecycle is:

1. **Skip** if the path matches an excluded prefix (calls `next` and
   returns — no context created).
2. **Create** a fresh `SyncNarrativeContext` and store it in
   `HttpContext.Items` (retrieve it with `HttpContext.GetNarrativeContext()`).
3. **Stamp** the request tier — HTTP method, route, client IP — and, if an
   `IRequestContextProvider` is registered, the user tier.
4. **Run** the rest of the pipeline (`await next`).
5. **Capture and export** the trace in a `finally` block — so a request
   that throws still produces a trace, carrying the response status code
   and elapsed milliseconds.

## 2. Trace your services

The middleware owns a per-request context; wrap the services you want
traced against **that** context so their calls nest into the request's
trace. Resolve it with `HttpContext.GetNarrativeContext()`:

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

`GetNarrativeContext()` returns `NoopContext.Instance` on an untraced
request (e.g. an excluded path), so wrapping is always safe — it simply
records nothing when there is no active trace.

> **Note:** the middleware's per-request context is independent of the
> `AddNarrativeTracing` DI auto-wrap (see the
> [Configuration Guide](configuration.md#3-dependency-injection)). Auto-wrap
> records into a DI-scoped context you capture yourself; the middleware
> records into `HttpContext.Items` and exports automatically. Pick one per
> request path — don't expect auto-wrapped services to appear in the
> middleware's exported trace.

## 3. Export

By default `AddNarrativeTrace` registers `LoggerTraceExporter`, which logs
each completed trace as structured output under the `NarrativeTrace.Export`
logger category. Provide your own `ITraceExporter` to send traces
elsewhere:

```csharp
public sealed class OtlpTraceExporter : ITraceExporter
{
    public void Export(TraceTree tree, RequestContext request)
    {
        // request.StatusCode, request.DurationMs
        // ship `tree` to your observability backend
    }
}

builder.Services.AddSingleton<ITraceExporter, OtlpTraceExporter>();
builder.Services.AddNarrativeTrace(builder.Configuration);
```

`AddNarrativeTrace` registers its default exporter with `TryAdd`, so a
registration you make **before** it wins. `RequestContext` carries
`StatusCode` and `DurationMs` — the outcome facts known only once the
request finishes.

## 4. Exclude paths

Skip health checks, metrics, and other noise entirely — excluded paths
create no context and no trace:

```csharp
builder.Services.AddNarrativeTrace(builder.Configuration, options =>
{
    options.Level = TracingLevel.Detail;
    options.ExcludedPaths.Add("/health");
    options.ExcludedPaths.Add("/metrics");
});
```

Or in `appsettings.json`:

```json
{
  "NarrativeTrace": {
    "Level": "Detail",
    "ExcludedPaths": [ "/health", "/metrics" ]
  }
}
```

Matching is by path **segment**, case-insensitive: `/health` excludes
`/health` and `/health/live`, but not `/healthcheck`.

Using both sources together is well defined: configuration binds first and
the options delegate runs after it, so `Level` and the service identity take
the value set **in code**. `ExcludedPaths` is the exception — configured
paths are *added* to the list, so the two sources accumulate rather than
replace one another. The `NARRATIVETRACE_*` environment variables
(see the [Configuration Guide](configuration.md#2-environment-variables-configresolver))
are a separate opt-in channel that `AddNarrativeTrace` never reads.

## 5. User context

To stamp user identity onto the trace (end-user id, session, tenant),
implement `IRequestContextProvider` and register it. The provider is a pure
function — it *returns* the identity it derived, and the middleware decides
where that goes:

```csharp
public sealed class ClaimsRequestContextProvider : IRequestContextProvider
{
    public UserContext? ResolveUserContext(HttpContext context)
    {
        var user = context.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            return null;   // anonymous traffic is normal, not an error
        }

        return new UserContext(
            EnduserId: user.FindFirst("sub")?.Value,
            TenantId: user.FindFirst("tenant")?.Value);
    }
}

builder.Services.AddSingleton<
    IRequestContextProvider, ClaimsRequestContextProvider>();
```

Every member of `UserContext` is optional; return only what you know, and
`null` when the request carries no identity at all. The middleware stamps
the returned values onto the trace (`SetUserContext`) and adds the
present-only fields `enduserId` / `sessionId` / `tenantId` to the request
log scope, beside `traceId` / `traceName` / `httpMethod` / `httpRoute` /
`clientIp`. A provider that throws is swallowed — observability never fails
the request.

These values flow into JSON export, logging scopes, and OpenTelemetry
spans as the `enduser.*` / `session.*` / `tenant.*` attribute tiers.

## 6. Testing the integration

You can exercise the middleware directly with a `DefaultHttpContext` — no
web host needed:

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

For faster unit tests of individual services, skip the middleware and use
the [xUnit / NUnit test helpers](installation.md#option-d--xunit-auto-context--failure-narrative)
with `NarrativeTraceProxy` directly.

## See also

- [Installation Guide](installation.md) — packages and integration paths
- [Configuration Guide](configuration.md) — levels, excluded paths, DI auto-wrap
- [Annotations Guide](annotations.md) — narration, redaction, error context
