// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Core;

/// <summary>
/// Collects the (scenario, trace) pairs recorded across a whole test suite, in
/// insertion order and retaining duplicate scenario names — mirroring the Java
/// reference's deliberate use of an ordered list rather than a map, so two
/// classes sharing a display name both survive into the suite report.
/// </summary>
public sealed class SuiteTraceAccumulator
{
    private readonly List<KeyValuePair<string, TraceTree>> _entries = [];
    private readonly object _lock = new();

    /// <summary>Records one scenario's finished trace.</summary>
    /// <param name="scenario">
    /// The scenario name. Duplicates are kept, not overwritten — two test
    /// classes sharing a display name both survive into the report.
    /// </param>
    /// <param name="tree">The captured trace. Stored by reference; it is already immutable.</param>
    /// <remarks>Thread-safe: test frameworks add from parallel workers.</remarks>
    public void Add(string scenario, TraceTree tree)
    {
        lock (_lock)
        {
            _entries.Add(new KeyValuePair<string, TraceTree>(scenario, tree));
        }
    }

    /// <summary>How many entries have been recorded, counting duplicate scenario names separately.</summary>
    /// <remarks>
    /// A point-in-time reading. Under a parallel test run it can change before
    /// you act on it, so do not use it to index into <see cref="Entries"/> —
    /// take a snapshot from <see cref="Entries"/> and use its own count.
    /// </remarks>
    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _entries.Count;
            }
        }
    }

    /// <summary>
    /// A snapshot of the accumulated entries in insertion order.
    /// </summary>
    public IReadOnlyList<KeyValuePair<string, TraceTree>> Entries
    {
        get
        {
            lock (_lock)
            {
                return _entries.ToArray();
            }
        }
    }
}
