// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Core;

/// <summary>
/// Classifies each <see cref="SpanContext"/>-related field by
/// <see cref="AttributeTier"/>. Field names match the .NET property names.
/// The classification is static and exhaustive; unknown names throw.
/// </summary>
public static class SpanContextFields
{
    private static readonly IReadOnlyDictionary<string, AttributeTier>
        Fields = new Dictionary<string, AttributeTier>(
            StringComparer.Ordinal)
        {
            // Resource — service identity, once per service lifetime
            ["ServiceName"] = AttributeTier.Resource,
            ["ServiceVersion"] = AttributeTier.Resource,
            ["Environment"] = AttributeTier.Resource,
            // Trace — request/user identity, once per request on root span
            ["HttpMethod"] = AttributeTier.Trace,
            ["HttpRoute"] = AttributeTier.Trace,
            ["ClientIp"] = AttributeTier.Trace,
            ["EnduserId"] = AttributeTier.Trace,
            ["SessionId"] = AttributeTier.Trace,
            ["TenantId"] = AttributeTier.Trace,
            ["StoryId"] = AttributeTier.Trace,
            ["ChapterId"] = AttributeTier.Trace,
            // Span — per-operation identity, every span carries these
            ["TraceId"] = AttributeTier.Span,
            ["SpanId"] = AttributeTier.Span,
            ["ParentSpanId"] = AttributeTier.Span,
            ["TraceFlags"] = AttributeTier.Span,
            ["TraceState"] = AttributeTier.Span,
            ["SpanName"] = AttributeTier.Span,
        };

    /// <summary>Returns the tier for a field name, or throws if unknown.</summary>
    public static AttributeTier Tier(string fieldName)
    {
        return Fields.TryGetValue(fieldName, out var tier)
            ? tier
            : throw new ArgumentException(
                $"Unknown SpanContext field: {fieldName}",
                nameof(fieldName));
    }

    /// <summary>All field-name-to-tier classifications.</summary>
    public static IReadOnlyDictionary<string, AttributeTier> All => Fields;
}
