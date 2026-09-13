// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;

namespace NarrativeTrace.AspNetCore;

/// <summary>
/// Configuration for the ASP.NET Core integration — tracing level, exporter
/// logger category, service identity, and excluded request paths. Bound from
/// the <c>NarrativeTrace</c> configuration section and refined via the
/// <c>configure</c> delegate on <c>AddNarrativeTrace</c>.
/// </summary>
public sealed class NarrativeTraceOptions
{
    /// <summary>
    /// How much detail the trace captures. Defaults to
    /// <see cref="TracingLevel.Detail"/>.
    /// </summary>
    public TracingLevel Level { get; set; } =
        TracingLevel.Detail;

    /// <summary>
    /// Logger category the built-in <see cref="LoggerTraceExporter"/> writes
    /// to. Defaults to <c>NarrativeTrace.Export</c> (Java's
    /// <c>narrativetrace.export</c>).
    /// </summary>
    public string LoggerName { get; set; } = "NarrativeTrace.Export";

    /// <summary>
    /// Optional service identity (name, version, environment) stamped onto
    /// every exported span context.
    /// </summary>
    public ServiceIdentity? ServiceIdentity { get; set; }

    /// <summary>
    /// Request path prefixes excluded from tracing (e.g. <c>/health</c>,
    /// <c>/metrics</c>). Matched by segment with
    /// <see cref="Microsoft.AspNetCore.Http.PathString.StartsWithSegments(Microsoft.AspNetCore.Http.PathString, System.StringComparison)"/>.
    /// </summary>
    public IList<string> ExcludedPaths { get; } = new List<string>();

    /// <summary>
    /// The redaction policy every service this app auto-wraps with
    /// <c>AddNarrativeTracing</c> renders parameters and return values
    /// with, or <see langword="null"/> for the secure
    /// <see cref="RedactionPolicy.Default"/> every other shipped
    /// integration uses. Registered into the same service collection so a
    /// policy configured once here — <c>services.AddNarrativeTrace(o =>
    /// o.Redaction = …)</c> — reaches every proxy
    /// <c>NarrativeTrace.DependencyInjection</c>'s auto-wrap constructs in
    /// this app, without that package's own options needing to repeat it
    /// *(since 0.1.4, unreleased)*. <c>NarrativeTracingDiOptions.Redaction</c>,
    /// when also set, takes precedence for that call. Given explicitly, the
    /// policy <em>replaces</em> the default name-based decision rather than
    /// widening it; <c>[NotTraced]</c> always wins regardless.
    /// </summary>
    public RedactionPolicy? Redaction { get; set; }
}
