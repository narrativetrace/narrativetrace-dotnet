// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using Microsoft.AspNetCore.Builder;

namespace NarrativeTrace.AspNetCore;

/// <summary>
/// Activation helpers for the NarrativeTrace request middleware.
/// </summary>
public static class ApplicationBuilderExtensions
{
    /// <summary>
    /// Inserts <see cref="NarrativeTraceMiddleware"/> into the request
    /// pipeline. Place this near the outer edge so downstream traced services
    /// contribute to a single request-scoped trace. The .NET equivalent of
    /// Java's self-installing servlet filter / Micronaut <c>@Filter("/**")</c>.
    /// </summary>
    public static IApplicationBuilder UseNarrativeTrace(
        this IApplicationBuilder app)
    {
        return app.UseMiddleware<NarrativeTraceMiddleware>();
    }
}
