// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;

namespace NarrativeTrace.Runtime;

/// <summary>
/// Shared helpers for the concurrency groups. Keeps the parent-context
/// seeding decision in one place so fork-join and fire-and-forget behave
/// identically.
/// </summary>
internal static class ConcurrencySupport
{
    /// <summary>
    /// Produces the isolated context a forked/launched task captures into.
    /// When the parent supports <see cref="IConcurrentChildFactory"/> the child
    /// inherits its trace id, level, service identity, and request/user
    /// metadata; otherwise it falls back to a fresh default context.
    /// </summary>
    public static INarrativeContext ChildContextOf(
        INarrativeContext parent)
    {
        return parent is IConcurrentChildFactory factory
            ? factory.CreateConcurrentChild()
            : new SyncNarrativeContext(new NarrativeTraceConfig());
    }
}
