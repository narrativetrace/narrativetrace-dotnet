// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NarrativeTrace.Core;
using NarrativeTrace.Proxy;
using NarrativeTrace.Runtime;

namespace NarrativeTrace.DependencyInjection;

/// <summary>
/// Registers NarrativeTrace tracing proxies over interface-registered
/// services whose implementation namespace matches a configured prefix —
/// the .NET equivalent of Spring/Micronaut bean auto-wrapping.
/// </summary>
/// <remarks>
/// <para>
/// Multi-interface divergence: each interface descriptor is wrapped in its
/// own <see cref="System.Reflection.DispatchProxy"/>, which supports exactly
/// one interface per proxy. A class registered under two interfaces therefore
/// resolves to two distinct proxy objects (the underlying target instance is
/// still shared when the registration shares one implementation instance).
/// Spring/Micronaut build a single proxy implementing all of a bean's
/// interfaces; matching that in .NET would require Castle.DynamicProxy. This
/// divergence is intentional and pinned by
/// <c>Multi_interface_singleton_wraps_each_interface_independently</c>.
/// </para>
/// <para>
/// Keyed services are skipped (not wrapped) rather than throwing; see DI-1.
/// </para>
/// </remarks>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers narrative tracing and wraps already-registered services whose
    /// implementation namespace matches the configured prefixes.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">
    /// Sets the tracing level and the namespace prefixes to include and exclude.
    /// Invoked immediately, not deferred.
    /// </param>
    /// <returns>The same collection, for chaining.</returns>
    /// <remarks>
    /// <para>
    /// <b>Call it last.</b> Wrapping is applied to the descriptors present at the
    /// moment of the call, so any service registered afterwards is not traced.
    /// </para>
    /// <para>
    /// Registers <see cref="INarrativeContext"/> as <b>scoped</b>, so each
    /// request or scope gets its own trace. Resolving one from the root provider
    /// yields a context shared by everything using the root — the usual captive
    /// dependency hazard applies: a singleton that takes an
    /// <see cref="INarrativeContext"/> pins one scope's context forever.
    /// </para>
    /// <para>
    /// Only interface registrations are wrapped, and each interface gets its own
    /// proxy — a class registered under two interfaces resolves to two distinct
    /// proxy objects. Keyed services are skipped silently rather than throwing.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// services.AddScoped&lt;IOrderService, OrderService&gt;();
    /// services.AddNarrativeTracing(o =>
    /// {
    ///     o.Level = TracingLevel.Narrative;
    ///     o.BaseNamespaces = ["MyApp.Services"];
    /// });   // last: earlier registrations are what get wrapped
    /// </code>
    /// </example>
    public static IServiceCollection AddNarrativeTracing(
        this IServiceCollection services,
        Action<NarrativeTracingDiOptions> configure)
    {
        var options = new NarrativeTracingDiOptions();
        configure(options);

        var config = new NarrativeTraceConfig(options.Level);
        services.TryAddSingleton(config);
        services.TryAddScoped<INarrativeContext>(
            sp => new SyncNarrativeContext(
                sp.GetRequiredService<NarrativeTraceConfig>()));

        var matcher = new NamespaceMatcher(options.BaseNamespaces);
        var exclusions = new NamespaceMatcher(options.ExcludedNamespaces);
        DecorateMatchingServices(services, matcher, exclusions);
        return services;
    }

    private static void DecorateMatchingServices(
        IServiceCollection services,
        NamespaceMatcher matcher, NamespaceMatcher exclusions)
    {
        for (var i = 0; i < services.Count; i++)
        {
            if (TryBuildWrappedDescriptor(services[i], matcher, exclusions)
                is { } wrapped)
            {
                services[i] = wrapped;
            }
        }
    }

    private static ServiceDescriptor? TryBuildWrappedDescriptor(
        ServiceDescriptor descriptor,
        NamespaceMatcher matcher, NamespaceMatcher exclusions)
    {
        if (!ShouldWrap(descriptor, matcher, exclusions))
        {
            return null;
        }

        var serviceType = descriptor.ServiceType;
        return ServiceDescriptor.Describe(
            serviceType,
            sp => WrapInstance(
                sp, serviceType,
                ResolveOriginal(sp, descriptor)),
            descriptor.Lifetime);
    }

    private static bool ShouldWrap(
        ServiceDescriptor descriptor,
        NamespaceMatcher matcher, NamespaceMatcher exclusions)
    {
        // Keyed descriptors expose their implementation only through the
        // Keyed* members; the non-keyed getters throw. Skip them with
        // tolerance (no crash) — wrapping keyed services is a later item.
        return !descriptor.IsKeyedService
            && descriptor.ServiceType.IsInterface
            && !descriptor.ServiceType.IsGenericTypeDefinition
            && !exclusions.Matches(descriptor.ServiceType.Namespace)
            && matcher.Matches(
                ImplementationNamespace(descriptor));
    }

    private static string? ImplementationNamespace(
        ServiceDescriptor descriptor)
    {
        // Prefer the concrete implementation namespace; for a
        // factory-registered service the impl type is opaque at
        // registration, so fall back to the (interface) service-type
        // namespace, which is near-universally co-located with the impl.
        var implType = descriptor.ImplementationType
            ?? descriptor.ImplementationInstance?.GetType();
        return implType?.Namespace ?? descriptor.ServiceType.Namespace;
    }

    private static object ResolveOriginal(
        IServiceProvider sp, ServiceDescriptor descriptor)
    {
        if (descriptor.ImplementationInstance is { } instance)
        {
            return instance;
        }

        if (descriptor.ImplementationFactory is { } factory)
        {
            return factory(sp);
        }

        return ActivatorUtilities.CreateInstance(
            sp, descriptor.ImplementationType!);
    }

    private static object WrapInstance(
        IServiceProvider sp, Type serviceType, object original)
    {
        return NarrativeTraceProxy.Create(
            serviceType, original,
            sp.GetRequiredService<INarrativeContext>());
    }
}
