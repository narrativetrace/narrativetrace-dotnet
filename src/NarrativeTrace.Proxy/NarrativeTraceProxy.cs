// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Linq;
using System.Reflection;
using NarrativeTrace.Core;
using NarrativeTrace.Core.Annotation;

namespace NarrativeTrace.Proxy;

/// <summary>
/// Factory for creating tracing proxies that intercept interface calls and
/// record them into an <see cref="INarrativeContext"/>.
/// </summary>
/// <remarks>
/// A proxy transparently implements the target interface, forwards each
/// call to the wrapped instance, and captures parameters, return values,
/// exceptions, and any <see cref="NarratedAttribute"/> or
/// <see cref="OnErrorAttribute"/> narration via the
/// <see cref="NarrativeInterceptor"/>. When the context is inactive the
/// proxy degrades to a plain pass-through.
/// </remarks>
public static class NarrativeTraceProxy
{
    /// <summary>
    /// Creates a tracing proxy for a compile-time-known interface type.
    /// </summary>
    /// <typeparam name="T">The interface type to proxy.</typeparam>
    /// <param name="target">The instance whose calls are forwarded and traced.</param>
    /// <param name="context">The context that records the captured trace.</param>
    /// <param name="options">Optional per-proxy settings.</param>
    /// <returns>A proxy implementing <typeparamref name="T"/>.</returns>
    public static T Create<T>(
        T target,
        INarrativeContext context,
        ProxyOptions? options = null) where T : class
    {
        RejectMethodLevelNotTraced(typeof(T));
        var proxy = DispatchProxy.Create<T, NarrativeInterceptor>();
        var interceptor = (NarrativeInterceptor)(object)proxy!;
        interceptor.Initialize(target, context, options);
        return proxy;
    }

    /// <summary>
    /// Creates a tracing proxy for a runtime-known interface type. Used by
    /// DI auto-wrapping, where the interface type is not known at compile
    /// time.
    /// </summary>
    /// <param name="interfaceType">The interface type to proxy.</param>
    /// <param name="target">The instance whose calls are forwarded and traced.</param>
    /// <param name="context">The context that records the captured trace.</param>
    /// <param name="options">Optional per-proxy settings.</param>
    /// <returns>A proxy implementing <paramref name="interfaceType"/>.</returns>
    public static object Create(
        Type interfaceType,
        object target,
        INarrativeContext context,
        ProxyOptions? options = null)
    {
        RejectMethodLevelNotTraced(interfaceType);
        // Reflect over the generic DispatchProxy.Create<T, TProxy>() —
        // the non-generic Create(Type, Type) overload does not exist on
        // netstandard2.0.
        var proxy = CreateProxyMethod
            .MakeGenericMethod(
                interfaceType, typeof(NarrativeInterceptor))
            .Invoke(null, null)!;
        var interceptor = (NarrativeInterceptor)proxy;
        interceptor.Initialize(target, context, options);
        return proxy;
    }

    private static readonly MethodInfo CreateProxyMethod =
        typeof(DispatchProxy).GetMethod(
            nameof(DispatchProxy.Create),
            System.Reflection.BindingFlags.Public
            | System.Reflection.BindingFlags.Static,
            binder: null, Type.EmptyTypes, modifiers: null)!;

    // [NotTraced] has no METHOD-level meaning (JVM-edition parity: its
    // @NotTraced has no METHOD target either — see the attribute's own
    // remarks), so a proxy created over an interface carrying it fails
    // fast here, at creation, rather than silently ignoring the misplaced
    // attribute or leaving the caller to a generic reflection error the
    // first time that method is invoked. Checked against every method the
    // interface declares AND inherits — GetInterfaces() returns the full
    // transitive closure for an interface type — since DispatchProxy
    // intercepts calls arriving through any of them.
    private static void RejectMethodLevelNotTraced(Type interfaceType)
    {
        var offender = interfaceType.GetInterfaces()
            .Append(interfaceType)
            .SelectMany(candidate => candidate.GetMethods())
            .FirstOrDefault(method =>
                method.GetCustomAttribute<NotTracedAttribute>() is not null);
        if (offender is null)
        {
            return;
        }

        throw new InvalidOperationException(
            $"[NotTraced] is not supported on a method (found on " +
            $"{offender.DeclaringType?.Name}.{offender.Name}); it has no " +
            "whole-call meaning here. Annotate each sensitive parameter, " +
            "property, or record component individually instead.");
    }
}
