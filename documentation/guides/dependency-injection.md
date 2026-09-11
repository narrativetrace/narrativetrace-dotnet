# Dependency Injection Auto-Wrapping Guide

**English** | [Español](es/guia-de-inyeccion-de-dependencias.md) | [Português](pt-BR/guia-de-injecao-de-dependencias.md) | [简体中文](zh-CN/依赖注入指南.md)

This guide covers `AddNarrativeTracing` — NarrativeTrace's
`Microsoft.Extensions.DependencyInjection` integration. It auto-wraps
interface-registered services whose implementation namespace matches a
configured prefix, so their calls are traced without touching call sites.
It is the `.NET` equivalent of Spring/Micronaut bean tracing: you point it
at your service namespaces and it decorates the matching beans in place.

## Package

```xml
<PackageReference Include="NarrativeTrace.DependencyInjection" Version="0.1.2" />
```

## 1. Register and auto-wrap

Register your services as usual, then call `AddNarrativeTracing` **after**
them — it rewrites descriptors already in the collection, so registrations
added later are not seen.

```csharp
using NarrativeTrace.Core;
using NarrativeTrace.DependencyInjection;

services.AddScoped<IOrderService, DefaultOrderService>();
services.AddScoped<IPaymentGateway, StripePaymentGateway>();

services.AddNarrativeTracing(options => options
    .Namespaces("MyApp.Orders", "MyApp.Payments"));
```

Each matched interface descriptor is replaced by one that resolves the
original implementation and returns a `NarrativeTraceProxy` over it. The
proxy records every call into a shared, DI-scoped `INarrativeContext`.

`AddNarrativeTracing` also registers, with `TryAdd` (so a prior
registration of yours wins):

- a singleton `NarrativeTraceConfig` built from `options.Level`;
- a **scoped** `INarrativeContext` (a `SyncNarrativeContext` over that
  config).

Capture the trace yourself from the scope you ran the work in:

```csharp
using var scope = provider.CreateScope();
scope.ServiceProvider.GetRequiredService<IOrderService>()
    .PlaceOrder("C-1234", "SKU-KB", 2);

var trace = scope.ServiceProvider
    .GetRequiredService<INarrativeContext>()
    .CaptureTrace();
```

## 2. Options

`AddNarrativeTracing(Action<NarrativeTracingDiOptions>)` takes a required
configuration delegate. `NarrativeTracingDiOptions` exposes:

| Member | Type | Default | Purpose |
|---|---|---|---|
| `Level` | `TracingLevel` | `Detail` | Capture level for the shared scoped context. |
| `Namespaces(params string[])` | fluent | (empty) | Base namespaces to auto-wrap. |
| `ExcludeNamespaces(params string[])` | fluent | (empty) | Interface namespaces to carve out, even when their implementation namespace is included. |

`Namespaces` and `ExcludeNamespaces` are additive and chainable, and each
returns the options instance:

```csharp
services.AddNarrativeTracing(options =>
{
    options.Level = TracingLevel.Narrative;
    options
        .Namespaces("MyApp.Orders", "MyApp.Payments")
        .ExcludeNamespaces("MyApp.Payments.Spi");
});
```

With no base namespace configured, nothing matches — auto-wrapping is
strictly opt-in per namespace.

## 3. Namespace-matching semantics

Two distinct namespaces are involved, and they are matched against
different rule sets:

- **Inclusion** (`Namespaces`) is tested against the **implementation**
  namespace — the concrete type's namespace (see [section 5](#5-how-the-implementation-namespace-is-resolved)).
- **Exclusion** (`ExcludeNamespaces`) is tested against the **interface**
  (service-type) namespace. This lets you include an implementation
  namespace broadly, then carve out the framework/SPI interfaces that live
  under a different namespace — the analogue of Spring's SPI/configuration
  exclusion.

Both use the same **dot-boundary** rule: a candidate matches a base when it
equals the base exactly, or starts with the base **followed by a dot**. A
sibling that merely shares a character prefix does not match.

Given base `MyApp`:

| Candidate namespace | Matches? | Why |
|---|---|---|
| `MyApp` | yes | exact |
| `MyApp.Orders` | yes | sub-namespace |
| `MyApp.Orders.Internal` | yes | deep sub-namespace |
| `MyApp2` | no | sibling, no dot boundary |
| `MyAppImpl` | no | shared prefix, no dot boundary |
| `My` | no | shorter than the base |
| `Other` | no | unrelated |
| `null` / `""` | no | an unknown namespace never matches |

Matching is **ordinal / case-sensitive** and any one configured base is
enough to match.

## 4. What gets wrapped (and what doesn't)

A descriptor is wrapped only when **all** of these hold:

- the **service type is an interface** (concrete-class registrations are
  left alone);
- it is **not an open generic** (e.g. `IRepository<>`) — `DispatchProxy`
  cannot wrap open generics;
- it is **not a keyed** registration — keyed services are tolerated and
  left unwrapped (no exception), not yet supported;
- its **interface namespace is not excluded** by `ExcludeNamespaces`;
- its **implementation namespace is included** by `Namespaces`.

Anything failing a check is left exactly as registered, so resolving it
returns the raw implementation.

> **Multi-interface divergence.** `DispatchProxy` supports one interface
> per proxy. A class registered under two interfaces (e.g.
> `AddSingleton<IAlpha>(impl)` and `AddSingleton<IBeta>(impl)`) is wrapped
> **twice** — two distinct proxy objects over the same shared target. This
> differs from Spring/Micronaut, which build a single proxy implementing
> all of a bean's interfaces. The divergence is intentional; matching it in
> .NET would require Castle.DynamicProxy.

## 5. How the implementation namespace is resolved

The inclusion check needs a concrete namespace. It is taken, in order,
from:

1. the descriptor's `ImplementationType` (type-registered services);
2. otherwise the runtime type of its `ImplementationInstance`
   (instance-registered services);
3. otherwise a fallback to the **service-type (interface) namespace** —
   used for **factory**-registered services (`AddScoped<IFoo>(sp => …)`),
   whose implementation type is opaque at registration time.

Because interfaces and their implementations are almost always
co-located, the factory fallback wraps the common case correctly; if your
factory returns a type from a different namespace than the interface, it
will match on the interface namespace instead.

## 6. Lifetime and scope behavior

- The original registration's **lifetime is preserved**. A singleton stays
  a singleton (the same proxy instance is returned on every resolve), a
  scoped service stays scoped, and so on.
- The shared `INarrativeContext` is **scoped**. Every wrapped service
  resolved within the same DI scope records into the **same** context, so
  their calls nest into a single trace tree. Create a scope per unit of
  work (per request, per job) and capture the trace from that scope.
- Because the context is scoped but a wrapped **singleton** outlives any
  scope, a singleton proxy resolves the current scope's context on each
  call via the service provider — it does not capture a stale context.

## 7. Interaction with the ASP.NET Core middleware

The `NarrativeTrace.AspNetCore` middleware and the `AddNarrativeTracing`
auto-wrap are **two independent tracing paths** — do not combine them on
the same request path:

| | Middleware (`NarrativeTraceMiddleware`) | DI auto-wrap (`AddNarrativeTracing`) |
|---|---|---|
| Context lives in | `HttpContext.Items` (per request) | a DI-scoped `INarrativeContext` |
| You trace by | wrapping against `HttpContext.GetNarrativeContext()` | resolving auto-wrapped services from the scope |
| Capture & export | automatic, in the middleware's `finally` | you call `CaptureTrace()` yourself |

**Pick one per request path.** Auto-wrapped services record into the
DI-scoped context, not into `HttpContext.Items`, so they will **not**
appear in the middleware's exported trace. If you want the middleware's
automatic per-request capture and export, wrap your services against
`HttpContext.GetNarrativeContext()` instead (see the
[ASP.NET Core Integration Guide](aspnetcore.md#2-trace-your-services)).
Reach for the DI auto-wrap in non-web hosts (workers, console apps,
message consumers) where you own the scope and capture the trace directly.

## See also

- [Installation Guide](installation.md#option-b--dependency-injection-auto-wrapping) — the integration paths at a glance
- [Configuration Guide](configuration.md#3-dependency-injection) — options summary alongside the other configuration surfaces
- [ASP.NET Core Integration Guide](aspnetcore.md) — the per-request middleware alternative
- [Annotations Guide](annotations.md) — `[Narrated]`, `[NotTraced]`, redaction on the wrapped calls
