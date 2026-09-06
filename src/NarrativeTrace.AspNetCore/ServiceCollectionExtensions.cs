// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NarrativeTrace.Core;
using NarrativeTrace.Runtime;

namespace NarrativeTrace.AspNetCore;

/// <summary>
/// Dependency-injection registration for the NarrativeTrace ASP.NET Core
/// integration — options, config, the per-request context, a default
/// exporter, and the middleware.
/// </summary>
public static class ServiceCollectionExtensions
{
    private const string ConfigSection = "NarrativeTrace";

    /// <summary>
    /// Registers the NarrativeTrace services with options supplied purely in
    /// code, without binding from configuration.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="configure">Optional delegate to customize the options.</param>
    /// <returns>The same <paramref name="services"/> for chaining.</returns>
    public static IServiceCollection AddNarrativeTrace(
        this IServiceCollection services,
        Action<NarrativeTraceOptions>? configure = null)
    {
        return services.AddNarrativeTrace(
            configuration: null, configure);
    }

    /// <summary>
    /// Registers the NarrativeTrace services: binds
    /// <see cref="NarrativeTraceOptions"/> from the <c>NarrativeTrace</c>
    /// configuration section (then applies <paramref name="configure"/>),
    /// and adds the options, <see cref="NarrativeTraceConfig"/>, a scoped
    /// per-request <see cref="INarrativeContext"/>, a default
    /// <see cref="LoggerTraceExporter"/>, and the middleware.
    /// </summary>
    /// <remarks>
    /// Precedence across sources is deliberate and silent rather than
    /// fail-fast: the section binds first, then <paramref name="configure"/>
    /// overrides in code. That is the standard ASP.NET Core options idiom, so
    /// rejecting a value set in both places would surprise callers more than
    /// it would protect them. <see cref="NarrativeTraceOptions.ExcludedPaths"/>
    /// is the one exception to last-writer-wins: configured paths are
    /// <em>added</em> to the list, so configuration and code accumulate.
    /// The <c>NARRATIVETRACE_*</c> variables read by
    /// <see cref="ConfigResolver"/> are a separate opt-in channel for hosts
    /// without <c>IConfiguration</c>; this registration never consults them,
    /// so a deployment that needs them must surface them through
    /// <paramref name="configuration"/>.
    /// The context and exporter are registered with <c>TryAdd</c>, so any
    /// registration made before this call wins. Pair with
    /// <see cref="ApplicationBuilderExtensions.UseNarrativeTrace"/> to insert
    /// the middleware into the pipeline.
    /// </remarks>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="configuration">Configuration to bind options from; may be
    /// null to skip binding.</param>
    /// <param name="configure">Optional delegate applied after binding to
    /// override options in code.</param>
    /// <returns>The same <paramref name="services"/> for chaining.</returns>
    public static IServiceCollection AddNarrativeTrace(
        this IServiceCollection services,
        IConfiguration? configuration,
        Action<NarrativeTraceOptions>? configure = null)
    {
        var options = new NarrativeTraceOptions();
        BindFromConfiguration(options, configuration);
        configure?.Invoke(options);

        services.AddLogging();
        services.AddSingleton(options);
        services.AddSingleton(
            new NarrativeTraceConfig(
                options.Level, options.ServiceIdentity));
        services.TryAddScoped<INarrativeContext>(
            sp => new SyncNarrativeContext(
                sp.GetRequiredService<NarrativeTraceConfig>()));
        services.TryAddSingleton<ITraceExporter, LoggerTraceExporter>();
        services.AddTransient<NarrativeTraceMiddleware>();
        return services;
    }

    private static void BindFromConfiguration(
        NarrativeTraceOptions options, IConfiguration? configuration)
    {
        var section = configuration?.GetSection(ConfigSection);
        if (section is null)
        {
            return;
        }

        options.Level = TracingLevelExtensions.FromName(
            section["Level"], options.Level);
        options.ServiceIdentity =
            IdentityFrom(section) ?? options.ServiceIdentity;
        foreach (var path in section
            .GetSection("ExcludedPaths").GetChildren())
        {
            if (path.Value is { } value)
            {
                options.ExcludedPaths.Add(value);
            }
        }
    }

    /// <summary>
    /// Builds the service identity from configuration, or null when the
    /// section names none of its keys.
    /// </summary>
    /// <remarks>
    /// Null rather than an empty identity, matching the Java registrar: a
    /// deployment that configures nothing gets no identity stamped, and an
    /// identity supplied in code is left intact rather than overwritten with
    /// blanks. Individual keys are independent — configuring only the service
    /// name leaves version and environment null.
    /// </remarks>
    private static ServiceIdentity? IdentityFrom(IConfiguration section)
    {
        var name = NullIfBlank(section["ServiceName"]);
        var version = NullIfBlank(section["ServiceVersion"]);
        var environment = NullIfBlank(section["Environment"]);
        if (name is null && version is null && environment is null)
        {
            return null;
        }

        return new ServiceIdentity(name, version, environment);
    }

    private static string? NullIfBlank(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
