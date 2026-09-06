// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Core;

/// <summary>
/// Classifies <see cref="SpanContext"/> fields by lifecycle and ownership,
/// aligned with OpenTelemetry's attribute model. Exporters and MDC bridges
/// query this to decide which fields to emit at which point. Internally
/// every span still carries all fields; the optimization is at export.
/// </summary>
public enum AttributeTier
{
    /// <summary>Set once per service lifetime: service name/version/environment.</summary>
    Resource,

    /// <summary>
    /// Set once per request at the entry point (HTTP method/route, client
    /// IP, end-user, session, tenant, story/chapter). Lives on the root
    /// span only in exports.
    /// </summary>
    Trace,

    /// <summary>
    /// Set per operation (trace/span/parent id, flags, state, span name).
    /// Every span carries these.
    /// </summary>
    Span,
}
