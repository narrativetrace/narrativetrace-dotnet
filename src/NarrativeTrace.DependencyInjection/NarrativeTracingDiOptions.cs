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
    private readonly List<string> _excludedNamespaces = [];

    /// <summary>The tracing level for the shared context.</summary>
    public TracingLevel Level { get; set; } = TracingLevel.Detail;

    internal IReadOnlyList<string> BaseNamespaces => _namespaces;

    internal IReadOnlyList<string> ExcludedNamespaces => _excludedNamespaces;

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
    /// Excludes interfaces whose own namespace matches one of the given
    /// prefixes from auto-wrapping, even when their implementation namespace
    /// is included — the carve-out for framework/infrastructure interfaces
    /// (Spring's SPI/configuration exclusion). Dot-boundary semantics.
    /// </summary>
    public NarrativeTracingDiOptions ExcludeNamespaces(
        params string[] namespaces)
    {
        _excludedNamespaces.AddRange(namespaces);
        return this;
    }
}
