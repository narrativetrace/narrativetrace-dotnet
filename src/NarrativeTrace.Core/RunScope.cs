// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Core;

/// <summary>
/// Holds the <see cref="RunIdentity"/> of whatever test-suite execution is
/// currently active, if any.
/// </summary>
/// <remarks>
/// <para>
/// INTENT: the seam that lets an optional module — <c>NarrativeTrace.Logging</c>'s
/// scope keys — and a per-test writer (the xUnit <c>NarrativeFixture</c>, the
/// NUnit <c>NarrativeTestBase</c>) name the enclosing suite's run without
/// either one depending on the suite-owning fixture directly. A test-framework
/// integration's suite fixture/setup begins the scope once, for the whole
/// run's lifetime, and ends it once flushed; every reader in between sees the
/// same <see cref="RunIdentity"/> a suite's own footer and manifest were
/// written with.
/// </para>
/// <para>
/// <b>Ambient by design, not per-thread:</b> unlike a thread-local mechanism
/// (Java's SLF4J MDC), nothing here needs setting on each worker thread —
/// every reader (a log scope being built, a trace file being written) is
/// synchronous with the read, so one shared value, current for the process's
/// one active run, is enough. Multiple genuinely concurrent, unrelated suite
/// runs in the same process are not a scenario this seam disambiguates — the
/// same limitation the Java port accepts for its own per-JVM run identity.
/// </para>
/// </remarks>
public static class RunScope
{
    private static readonly object Lock = new();
    private static RunIdentity? _current;

    /// <summary>The active run's identity, or <see langword="null"/> when no suite has begun one.</summary>
    public static RunIdentity? Current
    {
        get
        {
            lock (Lock)
            {
                return _current;
            }
        }
    }

    /// <summary>Marks <paramref name="identity"/> as the run every subsequent reader observes.</summary>
    public static void Begin(RunIdentity identity)
    {
        if (identity is null)
        {
            throw new ArgumentNullException(nameof(identity));
        }

        lock (Lock)
        {
            _current = identity;
        }
    }

    /// <summary>Clears the active run. Safe to call when none is active.</summary>
    public static void End()
    {
        lock (Lock)
        {
            _current = null;
        }
    }
}
