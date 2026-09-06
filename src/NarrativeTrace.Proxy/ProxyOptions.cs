// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
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
public sealed record ProxyOptions(
    string? ClassName = null,
    bool IncludeReturnValues = true);
