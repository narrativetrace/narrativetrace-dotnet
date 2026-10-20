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
        return services.AddNarrativeTracing(
            configure, Environment.GetEnvironmentVariable, Console.Error);
    }

    /// <summary>
    /// The seam: reads <see cref="InstrumentationExclusions.EnvironmentKey"/> through an injected reader
    /// and says what it left alone to an injected writer, so a test needs neither process state nor stderr.
    /// </summary>
    internal static IServiceCollection AddNarrativeTracing(
        this IServiceCollection services,
        Action<NarrativeTracingDiOptions> configure,
        Func<string, string?> readEnvironment,
        TextWriter diagnostics)
    {
        var options = new NarrativeTracingDiOptions();
        configure(options);

        var config = new NarrativeTraceConfig(
            options.Level, options.ServiceIdentity ?? ConfigResolver.Resolve(readEnvironment).Identity);
        services.TryAddSingleton(config);
        services.TryAddScoped<INarrativeContext>(
            sp => new SyncNarrativeContext(
                sp.GetRequiredService<NarrativeTraceConfig>()));

        var plan = new WrapPlan(
            new NamespaceMatcher(options.BaseNamespaces),
            new NamespaceMatcher(EffectiveExclusions(options, readEnvironment)),
            options.Redaction,
            new ExclusionReport(diagnostics));
        DecorateMatchingServices(services, plan);
        return services;
    }

    // The option beats the environment beats the shipped default; whichever is present is the whole list.
    private static IReadOnlyList<string> EffectiveExclusions(
        NarrativeTracingDiOptions options, Func<string, string?> readEnvironment)
    {
        if (options.ExcludedNamespaces is { } given)
        {
            return given;
        }

        return readEnvironment(InstrumentationExclusions.EnvironmentKey) is { } fromEnvironment
            ? InstrumentationExclusions.Parse(fromEnvironment)
            : InstrumentationExclusions.DefaultExclude;
    }

    private sealed record WrapPlan(
        NamespaceMatcher Include,
        NamespaceMatcher Exclude,
        RedactionPolicy? Redaction,
        ExclusionReport Report);

    private static void DecorateMatchingServices(
        IServiceCollection services, WrapPlan plan)
    {
        for (var i = 0; i < services.Count; i++)
        {
            if (TryBuildWrappedDescriptor(services[i], plan) is { } wrapped)
            {
                services[i] = wrapped;
            }
        }
    }

    private static ServiceDescriptor? TryBuildWrappedDescriptor(
        ServiceDescriptor descriptor, WrapPlan plan)
    {
        if (!ShouldWrap(descriptor, plan))
        {
            return null;
        }

        var serviceType = descriptor.ServiceType;
        return ServiceDescriptor.Describe(
            serviceType,
            sp => WrapInstance(
                sp, serviceType,
                ResolveOriginal(sp, descriptor), plan),
            descriptor.Lifetime);
    }

    private static bool ShouldWrap(ServiceDescriptor descriptor, WrapPlan plan)
    {
        // Keyed descriptors expose their implementation only through the
        // Keyed* members; the non-keyed getters throw. Skip them with
        // tolerance (no crash) — wrapping keyed services is a later item.
        return !descriptor.IsKeyedService
            && descriptor.ServiceType.IsInterface
            && !descriptor.ServiceType.IsGenericTypeDefinition
            && !GeneratedTypes.IsGenerated(descriptor.ServiceType)
            && !IsGeneratedImplementation(descriptor)
            && plan.Include.Matches(ImplementationNamespace(descriptor))
            && !IsExcluded(descriptor, plan);
    }

    // Reaches the include list's selection, so a match here is a carve-out worth saying once.
    private static bool IsExcluded(ServiceDescriptor descriptor, WrapPlan plan)
    {
        var prefix = plan.Exclude.MatchingPrefix(descriptor.ServiceType.Namespace)
            ?? plan.Exclude.MatchingPrefix(ImplementationNamespace(descriptor));
        if (prefix is null)
        {
            return false;
        }

        plan.Report.Excluded(prefix, DeclaredImplementationType(descriptor) ?? descriptor.ServiceType);
        return true;
    }

    private static bool IsGeneratedImplementation(ServiceDescriptor descriptor)
    {
        var implType = DeclaredImplementationType(descriptor);
        return implType is not null && GeneratedTypes.IsGenerated(implType);
    }

    private static Type? DeclaredImplementationType(
        ServiceDescriptor descriptor)
    {
        return descriptor.ImplementationType
            ?? descriptor.ImplementationInstance?.GetType();
    }

    private static string? ImplementationNamespace(
        ServiceDescriptor descriptor)
    {
        // Prefer the concrete implementation namespace; for a
        // factory-registered service the impl type is opaque at
        // registration, so fall back to the (interface) service-type
        // namespace, which is near-universally co-located with the impl.
        return DeclaredImplementationType(descriptor)?.Namespace
            ?? descriptor.ServiceType.Namespace;
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
        IServiceProvider sp, Type serviceType, object original, WrapPlan plan)
    {
        // A factory hides its product's type until resolve time; a generated or
        // excluded product (a mock, a closure) is handed back untouched.
        if (GeneratedTypes.IsGenerated(original.GetType()) || IsExcludedInstance(original, plan))
        {
            return original;
        }

        return TryCreateProxy(sp, serviceType, original, plan) ?? original;
    }

    private static bool IsExcludedInstance(object original, WrapPlan plan)
    {
        var type = original.GetType();
        var prefix = plan.Exclude.MatchingPrefix(type.Namespace);
        if (prefix is not null)
        {
            plan.Report.Excluded(prefix, type);
        }

        return prefix is not null;
    }

    // The B-125 principle: a target that cannot be proxied is skipped and said so once, never thrown
    // into the host's own GetService call.
    private static object? TryCreateProxy(
        IServiceProvider sp, Type serviceType, object original, WrapPlan plan)
    {
        try
        {
            return NarrativeTraceProxy.Create(
                serviceType, original,
                sp.GetRequiredService<INarrativeContext>(),
                new ProxyOptions(Redaction: EffectiveRedaction(sp, plan.Redaction)));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException
            or NotSupportedException or TypeLoadException or System.Reflection.TargetInvocationException)
        {
            plan.Report.CannotReach(serviceType, ex);
            return null;
        }
    }

    // This call's own o.Redaction wins when set; otherwise fall back to
    // whatever RedactionPolicy the ASP.NET Core integration's own
    // AddNarrativeTrace(o => o.Redaction = …) registered in the same
    // container — the two options types live in separate packages with no
    // reference between them, so the container is the one place a policy
    // configured on either side can reach a proxy the other side
    // constructs. Optional: a plain service lookup, never required, so a
    // solo AddNarrativeTracing (no ASP.NET Core integration at all) is
    // unaffected.
    private static RedactionPolicy? EffectiveRedaction(
        IServiceProvider sp, RedactionPolicy? redaction)
    {
        return redaction ?? sp.GetService<RedactionPolicy>();
    }
}
