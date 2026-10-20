// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;

namespace NarrativeTrace.DependencyInjection;

/// <summary>
/// Options for <see cref="ServiceCollectionExtensions.AddNarrativeTracing"/>.
/// </summary>
public sealed class NarrativeTracingDiOptions
{
    private readonly List<string> _namespaces = [];
    private IReadOnlyList<string>? _excludedNamespaces;

    /// <summary>The tracing level for the shared context.</summary>
    public TracingLevel Level { get; set; } = TracingLevel.Detail;

    /// <summary>
    /// The redaction policy every auto-wrapped proxy renders parameters and
    /// return values with, or <see langword="null"/> for the secure
    /// <see cref="RedactionPolicy.Default"/> every other shipped integration
    /// uses — the same <see cref="NarrativeTrace.Proxy.ProxyOptions.Redaction"/>
    /// hook a directly-constructed proxy accepts, threaded through
    /// <see cref="ServiceCollectionExtensions.AddNarrativeTracing"/> so a
    /// policy set once here reaches every service this call wraps. Given
    /// explicitly, it <em>replaces</em> the default name-based decision
    /// rather than widening it, so <see cref="RedactionPolicy.Disabled"/>
    /// here really does disable name-based redaction end to end;
    /// <c>[NotTraced]</c> always wins regardless. Left unset, a
    /// <see cref="RedactionPolicy"/> registered by the ASP.NET Core
    /// integration's own <c>AddNarrativeTrace(o => o.Redaction = …)</c> is
    /// used instead, when present.
    /// </summary>
    public RedactionPolicy? Redaction { get; set; }

    /// <summary>
    /// The service identity stamped on every span, or <see langword="null"/> to take the one the
    /// <c>NARRATIVETRACE_SERVICE_NAME</c>, <c>NARRATIVETRACE_SERVICE_VERSION</c> and
    /// <c>NARRATIVETRACE_ENVIRONMENT</c> variables name. With neither, spans are attributed to
    /// <c>unknown_service:dotnet</c>.
    /// </summary>
    public ServiceIdentity? ServiceIdentity { get; set; }

    internal IReadOnlyList<string> BaseNamespaces => _namespaces;

    /// <summary>The explicit exclude list, or null when none was given (the environment or default applies).</summary>
    internal IReadOnlyList<string>? ExcludedNamespaces => _excludedNamespaces;

    /// <summary>
    /// Configures the base namespaces whose interface-registered services
    /// are auto-wrapped in tracing proxies. Matching uses dot-boundary
    /// semantics.
    /// </summary>
    public NarrativeTracingDiOptions Namespaces(params string[] namespaces)
    {
        _namespaces.AddRange(namespaces);
        return this;
    }

    /// <summary>
    /// Sets the namespaces that are never traced even when included: a service is left alone when its
    /// interface, its implementation or its resolved instance lives under one of these prefixes
    /// (dot-boundary semantics, so <c>Acme</c> never matches <c>Acmex</c>; a trailing <c>.</c> or
    /// <c>.*</c> is accepted). Exclude wins over include.
    /// </summary>
    /// <remarks>
    /// This <b>replaces</b> the shipped <see cref="InstrumentationExclusions.DefaultExclude"/> and the
    /// <see cref="InstrumentationExclusions.EnvironmentKey"/> variable — passing an empty list switches
    /// the default off. To extend the default, include its prefixes in the call. May be called once;
    /// a second call is a duplicate and throws.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The exclude list was already given.</exception>
    public NarrativeTracingDiOptions ExcludeNamespaces(
        params string[] namespaces)
    {
        if (_excludedNamespaces is not null)
        {
            throw new InvalidOperationException(
                "ExcludeNamespaces was already called; the exclude list replaces the default as a whole, so give it once.");
        }

        _excludedNamespaces = InstrumentationExclusions.Normalise(namespaces);
        return this;
    }
}
