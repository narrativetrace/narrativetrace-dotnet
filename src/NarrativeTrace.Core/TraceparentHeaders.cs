// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Core;

/// <summary>
/// The outgoing <c>traceparent</c> header value for code that makes HTTP calls
/// without <c>IHttpClientFactory</c>, so it can attach the header by hand.
/// </summary>
/// <remarks>
/// Prefer the handler (<c>TraceparentPropagationHandler</c>) where an
/// <c>HttpClient</c> is built for you; this is the same value, read once.
/// </remarks>
public static class TraceparentHeaders
{
    /// <summary>The header value to send for the flow running on <paramref name="context"/>.</summary>
    /// <param name="context">The narrative context of the calling flow; must not be <see langword="null"/>.</param>
    /// <returns>
    /// A version-<c>00</c> <c>traceparent</c> value naming the current trace and the innermost
    /// active span, or <see langword="null"/> when there is no trace to propagate — send no header then.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    public static string? Current(INarrativeContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        return context.OutboundTraceparent()?.Format();
    }
}
