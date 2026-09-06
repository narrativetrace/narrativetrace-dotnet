// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.AspNetCore;

/// <summary>
/// The user-tier identity an <see cref="IRequestContextProvider"/> derived
/// from the current request.
/// </summary>
/// <remarks>
/// A plain value: the provider decides identity, the middleware decides where
/// it goes (span metadata and request log fields). Every member is optional —
/// an application that knows only the tenant returns that alone, and the
/// unknown fields are omitted downstream rather than logged blank.
/// </remarks>
/// <param name="EnduserId">Identifier of the acting end user, or null.</param>
/// <param name="SessionId">Identifier of the session, or null.</param>
/// <param name="TenantId">Identifier of the tenant, or null.</param>
public sealed record UserContext(
    string? EnduserId = null,
    string? SessionId = null,
    string? TenantId = null);
