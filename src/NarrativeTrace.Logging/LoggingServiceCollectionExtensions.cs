// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

using NarrativeTrace.Core;

namespace NarrativeTrace.Logging;

/// <summary>
/// Composes the <see cref="LoggingTraceEventListener"/> into a host's
/// dependency-injection container, so the narrative reaches the log stream in
/// production rather than only in tests.
/// </summary>
/// <remarks>
/// <para>
/// The .NET answer to Java's <c>PipelineBootstrap</c>, which does not port.
/// SLF4J's <c>LoggerFactory</c> is a static global, so the JVM edition can take
/// the module's presence on the classpath as the activation signal and
/// manufacture a logger reflectively. .NET's <see cref="ILogger"/> comes from
/// the container: a type probe cannot conjure one, and it would be hostile to
/// trimming and AOT besides. <b>"An <see cref="ILoggerFactory"/> is registered"
/// is the .NET activation signal</b>, and this extension is where the host
/// states it.
/// </para>
/// <para>
/// It lives in this package rather than in
/// <c>NarrativeTrace.DependencyInjection</c> on purpose: that package would
/// otherwise need a reference to this one, inverting the dependency arrow that
/// <c>ArchTests</c> enforces.
/// </para>
/// </remarks>
public static class LoggingServiceCollectionExtensions
{
    /// <summary>The logger category the bridge writes under.</summary>
    public const string LoggerCategory = "NarrativeTrace";

    /// <summary>
    /// Registers the event-stream logging bridge and attaches it to a
    /// registered event stream.
    /// </summary>
    /// <param name="services">The service collection to add to; must not be null.</param>
    /// <param name="options">
    /// Per-event-type levels, or <see langword="null"/> for
    /// <see cref="TraceLoggingOptions.Default"/>.
    /// </param>
    /// <returns>The same <paramref name="services"/>, for chaining.</returns>
    /// <remarks>
    /// <para>
    /// <b>Two conditions, both silent when unmet.</b> Nothing is registered when
    /// no <see cref="ILoggerFactory"/> is registered — the bridge would have
    /// nowhere to write — or when <c>NARRATIVETRACE_NARRATION=off</c> vetoes
    /// narration. Both are configuration statements, not faults, so they
    /// no-op rather than throw: an application that removes its logging
    /// provider should stop narrating, not fail to start.
    /// </para>
    /// <para>
    /// <b>Call it last</b>, like <c>AddNarrativeTracing</c>. The listener is
    /// attached to <see cref="IEventSubscribable"/> registrations present at the
    /// moment of the call, and to nothing registered afterwards. When no event
    /// stream is registered the listener is still registered and resolvable —
    /// the host can subscribe it by hand — but nothing is attached.
    /// </para>
    /// <para>
    /// The listener is a singleton and registered with <c>TryAdd</c>, so a
    /// listener the host registered first wins.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
    public static IServiceCollection AddNarrativeLogging(
        this IServiceCollection services, TraceLoggingOptions? options = null)
    {
        return services.AddNarrativeLogging(
            options, Environment.GetEnvironmentVariable);
    }

    /// <summary>
    /// Registers the bridge, reading the narration veto through an injected
    /// variable reader instead of the process environment.
    /// </summary>
    /// <param name="services">The service collection to add to; must not be null.</param>
    /// <param name="options">Per-event-type levels, or <see langword="null"/> for the default.</param>
    /// <param name="readEnvironment">
    /// Reads a <c>NARRATIVETRACE_*</c> variable by name, returning
    /// <see langword="null"/> when unset — the seam that lets a test state the
    /// veto without mutating process state.
    /// </param>
    /// <returns>The same <paramref name="services"/>, for chaining.</returns>
    /// <exception cref="ArgumentNullException">Either argument is null.</exception>
    public static IServiceCollection AddNarrativeLogging(
        this IServiceCollection services,
        TraceLoggingOptions? options,
        Func<string, string?> readEnvironment)
    {
        if (services is null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        if (readEnvironment is null)
        {
            throw new ArgumentNullException(nameof(readEnvironment));
        }

        if (!ConfigResolver.Resolve(readEnvironment).Narration
            || !IsRegistered<ILoggerFactory>(services))
        {
            return services;
        }

        services.TryAddSingleton(
            provider => Listener(provider, options ?? TraceLoggingOptions.Default));
        AttachToEventStreams(services);
        return services;
    }

    private static LoggingTraceEventListener Listener(
        IServiceProvider provider, TraceLoggingOptions options)
    {
        return new LoggingTraceEventListener(
            provider.GetRequiredService<ILoggerFactory>()
                .CreateLogger(LoggerCategory),
            options);
    }

    private static bool IsRegistered<T>(IServiceCollection services)
    {
        foreach (var descriptor in services)
        {
            if (descriptor.ServiceType == typeof(T))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Rewrites every <see cref="IEventSubscribable"/> registration so the
    /// resolved stream arrives with the listener already attached.
    /// </summary>
    /// <remarks>
    /// Subscription cannot happen at registration time — neither object exists
    /// yet — and a hosted service would drag in the hosting abstractions for
    /// one call. Decorating the descriptor keeps the wiring where the host
    /// asked for it and costs nothing when the stream is never resolved.
    /// </remarks>
    private static void AttachToEventStreams(IServiceCollection services)
    {
        for (var i = 0; i < services.Count; i++)
        {
            if (Attached(services[i]) is { } decorated)
            {
                services[i] = decorated;
            }
        }
    }

    private static ServiceDescriptor? Attached(ServiceDescriptor descriptor)
    {
        if (descriptor.IsKeyedService
            || descriptor.ServiceType != typeof(IEventSubscribable))
        {
            return null;
        }

        return ServiceDescriptor.Describe(
            descriptor.ServiceType,
            provider => Subscribed(provider, Resolve(provider, descriptor)),
            descriptor.Lifetime);
    }

    private static object Subscribed(IServiceProvider provider, object stream)
    {
        ((IEventSubscribable)stream).Subscribe(
            provider.GetRequiredService<LoggingTraceEventListener>().OnEvent);
        return stream;
    }

    private static object Resolve(
        IServiceProvider provider, ServiceDescriptor descriptor)
    {
        if (descriptor.ImplementationInstance is { } instance)
        {
            return instance;
        }

        return descriptor.ImplementationFactory is { } factory
            ? factory(provider)
            : ActivatorUtilities.CreateInstance(
                provider, descriptor.ImplementationType!);
    }
}
