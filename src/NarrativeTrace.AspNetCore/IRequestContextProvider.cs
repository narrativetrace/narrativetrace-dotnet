// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using Microsoft.AspNetCore.Http;

namespace NarrativeTrace.AspNetCore;

/// <summary>
/// Derives user-tier context (end-user, session, tenant identity) from the
/// current <see cref="HttpContext"/> — typically from claims. Applications
/// implement this to feed identity into traces.
/// </summary>
/// <remarks>
/// A pure function by design, mirroring the Java edition's
/// <c>RequestContextProvider.resolveUserContext</c>: the provider decides
/// <em>who</em> the caller is and the middleware decides what to do with that
/// — stamp it onto the span and correlate the request's logs with it. An
/// earlier version of this interface applied the values itself and returned
/// nothing, which left the middleware unable to see what had been resolved.
/// </remarks>
public interface IRequestContextProvider
{
    /// <summary>
    /// Derives the caller's identity from <paramref name="context"/>.
    /// </summary>
    /// <param name="context">
    /// The current HTTP context, typically the source of claims or headers
    /// identifying the caller.
    /// </param>
    /// <returns>
    /// The resolved identity, or null when the request carries none —
    /// anonymous traffic is normal, not an error.
    /// </returns>
    /// <remarks>
    /// Invoked by the middleware during request stamping; faults are swallowed
    /// so observability never fails the request.
    /// </remarks>
    UserContext? ResolveUserContext(HttpContext context);
}
