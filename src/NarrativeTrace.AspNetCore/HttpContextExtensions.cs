// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using Microsoft.AspNetCore.Http;
using NarrativeTrace.Core;

namespace NarrativeTrace.AspNetCore;

/// <summary>
/// Extensions for retrieving the per-request narrative context that
/// <see cref="NarrativeTraceMiddleware"/> stores on the current request.
/// </summary>
public static class HttpContextExtensions
{
    /// <summary>
    /// Returns the request-scoped <see cref="INarrativeContext"/> the
    /// middleware placed in <see cref="HttpContext.Items"/>, so services can
    /// nest their calls into the request's trace.
    /// </summary>
    /// <remarks>
    /// Returns <see cref="NoopContext.Instance"/> when no context is present
    /// — for example on an excluded path or a request that did not pass
    /// through the middleware — so wrapping services is always safe.
    /// </remarks>
    /// <param name="context">The current HTTP context.</param>
    public static INarrativeContext
        GetNarrativeContext(this HttpContext context)
    {
        if (context.Items.TryGetValue(
                NarrativeTraceMiddleware.ContextKey,
                out var value)
            && value is INarrativeContext ctx)
        {
            return ctx;
        }

        return NoopContext.Instance;
    }
}
