// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using Microsoft.Extensions.Logging;

namespace NarrativeTrace.Logging;

/// <summary>
/// Per-event log-level configuration for the logging integrations.
/// Defaults align with the Java <c>Slf4jTraceEventListener</c>: entry and
/// return events at <see cref="LogLevel.Trace"/>, exceptions at
/// <see cref="LogLevel.Warning"/>. Fork/join/graft lifecycle events reuse
/// <see cref="EnterLevel"/>.
/// </summary>
public sealed class TraceLoggingOptions
{
    /// <summary>Level for method-enter and lifecycle events.</summary>
    public LogLevel EnterLevel { get; init; } = LogLevel.Trace;

    /// <summary>Level for successful method-return events.</summary>
    public LogLevel ReturnLevel { get; init; } = LogLevel.Trace;

    /// <summary>Level for thrown-exit events.</summary>
    public LogLevel ExceptionLevel { get; init; } = LogLevel.Warning;

    /// <summary>Shared default instance used when none is supplied.</summary>
    public static TraceLoggingOptions Default { get; } = new();
}
