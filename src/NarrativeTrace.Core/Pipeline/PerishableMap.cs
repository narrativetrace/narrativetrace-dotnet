// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Collections.Concurrent;
using System.Diagnostics;

namespace NarrativeTrace.Core;

/// <summary>
/// Bounded map with TTL-based and capacity-based eviction. General-purpose
/// store for pipeline consumers that hold intermediate state (e.g. active
/// spans awaiting completion); entries that exceed the TTL or are evicted for
/// capacity are passed to an eviction callback so the caller can clean up
/// (e.g. ending orphaned spans with error status).
/// </summary>
/// <remarks>
/// Eviction is lazy — it happens on <see cref="Put"/>, not on a timer, which
/// avoids background-thread management. Thread-safe via
/// <see cref="ConcurrentDictionary{TKey,TValue}"/>; eviction scans are not
/// atomic across the whole map, so under high concurrency the size may briefly
/// exceed capacity by a small margin. Re-putting a key overwrites the value and
/// resets its insertion time without triggering eviction for that key.
/// </remarks>
public sealed class PerishableMap<TKey, TValue>
    where TKey : notnull
{
    private readonly ConcurrentDictionary<TKey, TimedEntry> _entries = new();
    private readonly int _maxCapacity;
    private readonly long _ttlTicks;
    private readonly Action<TValue> _onEvict;
    private readonly Func<long> _clock;

    /// <summary>
    /// Creates a perishable map with the given capacity, TTL, and eviction
    /// callback, using <see cref="Stopwatch.GetTimestamp()"/> as the clock.
    /// </summary>
    public PerishableMap(int maxCapacity, TimeSpan ttl, Action<TValue> onEvict)
        : this(maxCapacity, ToStopwatchTicks(ttl), onEvict, Stopwatch.GetTimestamp)
    {
    }

    /// <summary>Test seam: injectable clock and TTL in the clock's own units.</summary>
    internal PerishableMap(
        int maxCapacity, long ttlTicks, Action<TValue> onEvict, Func<long> clock)
    {
        if (maxCapacity < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxCapacity),
                maxCapacity,
                "maxCapacity must be positive");
        }

        _maxCapacity = maxCapacity;
        _ttlTicks = ttlTicks;
        _onEvict = onEvict;
        _clock = clock;
    }

    /// <summary>
    /// Inserts or overwrites an entry, evicting expired and over-capacity
    /// entries first.
    /// </summary>
    /// <remarks>
    /// Intentional divergence from Java <c>PerishableMap.put</c>: capacity
    /// eviction runs only when the key is new. Re-putting an existing key at
    /// capacity evicts nothing (Java would evict the oldest other entry and
    /// fire <c>onEvict</c>). This avoids spuriously ending an unrelated active
    /// span during an in-flight OTel re-put.
    /// </remarks>
    public void Put(TKey key, TValue value)
    {
        var isNew = !_entries.ContainsKey(key);
        EvictExpired();
        if (isNew)
        {
            EvictOverCapacity();
        }

        _entries[key] = new TimedEntry(value, _clock());
    }

    /// <summary>Returns the value for the key, or <c>default</c> if absent.</summary>
    public TValue? Get(TKey key)
    {
        return _entries.TryGetValue(key, out var entry)
            ? entry.Value
            : default;
    }

    /// <summary>Removes and returns the value for the key, or <c>default</c> if absent.</summary>
    public TValue? Remove(TKey key)
    {
        return _entries.TryRemove(key, out var entry)
            ? entry.Value
            : default;
    }

    /// <summary>Current number of entries.</summary>
    public int Count => _entries.Count;

    private void EvictExpired()
    {
        var now = _clock();
        foreach (var pair in _entries)
        {
            if (now - pair.Value.CreatedTicks > _ttlTicks
                && _entries.TryRemove(pair.Key, out var removed))
            {
                _onEvict(removed.Value);
            }
        }
    }

    private void EvictOverCapacity()
    {
        while (_entries.Count >= _maxCapacity && TryFindOldest(out var key))
        {
            if (_entries.TryRemove(key, out var removed))
            {
                _onEvict(removed.Value);
            }
        }
    }

    private bool TryFindOldest(out TKey oldest)
    {
        oldest = default!;
        var found = false;
        var oldestTime = long.MaxValue;
        foreach (var pair in _entries)
        {
            if (pair.Value.CreatedTicks < oldestTime)
            {
                oldestTime = pair.Value.CreatedTicks;
                oldest = pair.Key;
                found = true;
            }
        }

        return found;
    }

    private static long ToStopwatchTicks(TimeSpan ttl)
    {
        return (long)(ttl.TotalSeconds * Stopwatch.Frequency);
    }

    private readonly struct TimedEntry(TValue value, long createdTicks)
    {
        public TValue Value { get; } = value;

        public long CreatedTicks { get; } = createdTicks;
    }
}
