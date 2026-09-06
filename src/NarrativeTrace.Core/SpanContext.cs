// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Core;

/// <summary>
/// Self-describing trace context carried on every event and node.
/// </summary>
public sealed record SpanContext(
    TraceId TraceId,
    SpanId SpanId,
    SpanId? ParentSpanId,
    string? ServiceName = null,
    string? ServiceVersion = null,
    string? Environment = null)
{
    /// <summary>W3C trace flags (bit 0 = sampled).</summary>
    public int TraceFlags { get; init; }

    /// <summary>W3C tracestate vendor data.</summary>
    public string? TraceState { get; init; }

    /// <summary>Span-tier operation name.</summary>
    public string? SpanName { get; init; }

    /// <summary>Story this span belongs to (typically derived from the trace id).</summary>
    public string? StoryId { get; init; }

    /// <summary>Chapter this span belongs to (one service's contribution).</summary>
    public string? ChapterId { get; init; }

    /// <summary>Trace-tier: HTTP method of the originating request.</summary>
    public string? HttpMethod { get; init; }

    /// <summary>Trace-tier: request route/path template.</summary>
    public HttpRoute? HttpRoute { get; init; }

    /// <summary>Trace-tier: originating client IP.</summary>
    public ClientIp? ClientIp { get; init; }

    /// <summary>Trace-tier: authenticated end-user identity.</summary>
    public EnduserId? EnduserId { get; init; }

    /// <summary>Trace-tier: session identity.</summary>
    public SessionId? SessionId { get; init; }

    /// <summary>Trace-tier: tenant identity.</summary>
    public TenantId? TenantId { get; init; }


    /// <summary>
    /// Creates a SpanContext, flattening service identity fields.
    /// </summary>
    public static SpanContext Create(
        TraceId traceId,
        SpanId spanId,
        SpanId? parentSpanId,
        ServiceIdentity? identity)
    {
        return new SpanContext(
            traceId,
            spanId,
            parentSpanId,
            identity?.ServiceName,
            identity?.ServiceVersion,
            identity?.Environment);
    }
}
