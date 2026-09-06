// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Core;

/// <summary>
/// Request route or path template associated with a span. A Pro-tier
/// privacy extension point; <see cref="ToString"/> returns the raw value
/// for transparent use in export, MDC, and OTel.
/// </summary>
public sealed record HttpRoute(string Value)
{
    /// <summary>
    /// The route template, e.g. <c>/orders/{id}</c>. Keep it the low-cardinality
    /// pattern rather than the filled-in path, or every request becomes a distinct
    /// value downstream.
    /// </summary>
    public string Value { get; } = Value;

    /// <summary>Returns <see cref="Value"/> unchanged, for transparent use in export, log scope and OTel attributes.</summary>
    public override string ToString() => Value;
}

/// <summary>
/// Client network address captured at span creation. A Pro-tier privacy
/// extension point.
/// </summary>
public sealed record ClientIp(string Value)
{
    /// <summary>
    /// The caller's address as captured, before any anonymization.
    /// </summary>
    public string Value { get; } = Value;

    /// <summary>Returns <see cref="Value"/> unchanged, for transparent use in export, log scope and OTel attributes.</summary>
    public override string ToString() => Value;
}

/// <summary>
/// End-user identity captured at span creation. A Pro-tier privacy
/// extension point.
/// </summary>
public sealed record EnduserId(string Value)
{
    /// <summary>
    /// The end user's identifier as captured, before any pseudonymization.
    /// </summary>
    public string Value { get; } = Value;

    /// <summary>Returns <see cref="Value"/> unchanged, for transparent use in export, log scope and OTel attributes.</summary>
    public override string ToString() => Value;
}

/// <summary>
/// Session identity captured at span creation. A Pro-tier privacy
/// extension point.
/// </summary>
public sealed record SessionId(string Value)
{
    /// <summary>
    /// The session identifier as captured.
    /// </summary>
    public string Value { get; } = Value;

    /// <summary>Returns <see cref="Value"/> unchanged, for transparent use in export, log scope and OTel attributes.</summary>
    public override string ToString() => Value;
}

/// <summary>
/// Tenant identity captured at span creation, for multi-tenant traces.
/// </summary>
public sealed record TenantId(string Value)
{
    /// <summary>
    /// The tenant identifier as captured.
    /// </summary>
    public string Value { get; } = Value;

    /// <summary>Returns <see cref="Value"/> unchanged, for transparent use in export, log scope and OTel attributes.</summary>
    public override string ToString() => Value;
}
