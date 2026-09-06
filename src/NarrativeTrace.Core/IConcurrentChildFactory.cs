// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Core;

/// <summary>
/// Optional capability for contexts that can spawn an isolated child context
/// for concurrent work (fork-join or fire-and-forget). The child inherits the
/// parent's trace id, tracing level, service identity, and request/user
/// metadata, but captures into its own independent stack so background work can
/// be grafted back under the parent span while remaining correlated. Mirrors
/// Java's <c>NarrativeContext.snapshot()</c> adapted to .NET's per-fork
/// capture isolation.
/// </summary>
public interface IConcurrentChildFactory
{
    /// <summary>
    /// Creates an isolated child context seeded from this context's identity
    /// and metadata. Generating the trace id eagerly if absent so the child
    /// and parent share it.
    /// </summary>
    INarrativeContext CreateConcurrentChild();
}
