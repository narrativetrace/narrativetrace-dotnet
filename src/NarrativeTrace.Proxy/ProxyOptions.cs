// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;

namespace NarrativeTrace.Proxy;

/// <summary>
/// Optional per-proxy settings applied by
/// <see cref="NarrativeTraceProxy"/> when wrapping a target.
/// </summary>
/// <param name="ClassName">
/// Overrides the class name reported for traced methods. When
/// <see langword="null"/>, the declaring type's name is used.
/// </param>
/// <param name="IncludeReturnValues">
/// Whether return values are rendered into the trace. When
/// <see langword="false"/>, method exits are recorded without a rendered
/// return value. Defaults to <see langword="true"/>.
/// </param>
/// <param name="Redaction">
/// The redaction policy this proxy renders parameters and return values
/// with, or <see langword="null"/> for the secure
/// <see cref="RedactionPolicy.Default"/> every other shipped integration
/// uses. This is the hook: before it existed, nothing between
/// <see cref="NarrativeTraceProxy.Create{T}"/> and the renderer accepted a
/// <see cref="RedactionPolicy"/> at all, so <c>RedactionPolicy.OfPatterns</c>
/// — documented in the Configuration Guide — was reachable only by an
/// application calling <c>ValueRenderer.Render</c> itself, bypassing the
/// proxy entirely. Set here, it governs both axes: a proxy parameter or
/// return value whose own name matches the policy, and a name matched
/// during the reflective walk of a nested object's properties (a
/// <c>Policy.HolderName</c> field two levels down). Given explicitly, it
/// <em>replaces</em> the default name-based decision rather than adding to
/// it — the same "replace, not widen" contract <see cref="RedactionPolicy.OfPatterns"/>
/// documents — so <see cref="RedactionPolicy.Disabled"/> here really does
/// disable name-based redaction end to end. <c>[NotTraced]</c> always wins
/// regardless of what policy is in force.
/// </param>
public sealed record ProxyOptions(
    string? ClassName = null,
    bool IncludeReturnValues = true,
    RedactionPolicy? Redaction = null);
