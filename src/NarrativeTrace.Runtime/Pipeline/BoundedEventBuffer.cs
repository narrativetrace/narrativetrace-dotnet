// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Threading;
using NarrativeTrace.Core;

namespace NarrativeTrace.Runtime;

/// <summary>
/// Lock-free bounded ring buffer for trace events. Multiple producers, single
/// consumer (MPSC).
/// </summary>
/// <remarks>
/// <para>Design (JCTools-inspired):</para>
/// <list type="bullet">
///   <item>Power-of-two capacity with a bitmask — branchless index wrapping.</item>
///   <item><see cref="Interlocked.Increment(ref long)"/> for the atomic
///   producer claim.</item>
///   <item>Per-slot sequence numbers published with release/acquire
///   (<see cref="Volatile"/>) so the consumer never reads a slot the producer
///   has claimed but not yet written.</item>
/// </list>
/// <para>On overflow the oldest events are overwritten rather than the newest
/// rejected: under backpressure you keep the most recent activity. Every
/// overwritten event is counted in <see cref="OverwrittenCount"/> so loss
/// accounting stays exact even when the ring, not the consumer's drain
/// modes, is what discarded it.</para>
/// <para>
/// The slot protocol is a seqlock (the same fix every runtime carries —
/// see <see cref="Put"/> and <see cref="SteppedOverLappedSlot"/> for the
/// two halves). A one-sequence-store-guards-one-data-field design — check
/// the sequence, then read the event — proves only that the producer HAD
/// finished writing the slot, not that it still holds that event: a
/// producer that laps the ring in the window between the check and the
/// read replaces the event underneath the consumer, which then delivers a
/// later generation's event under the wrong index and delivers it a second
/// time when the index catches up, while the event that belonged there
/// vanishes uncounted. jcstress measured this at 1,732 forbidden samples in
/// 62M on a four-slot ring with neither half of the fix, 42 in 46M with the
/// claim marker alone, 0 in 45M with both.
/// </para>
/// </remarks>
public sealed class BoundedEventBuffer
{
    private sealed class Slot
    {
        public long Sequence;
        public TraceEvent? Event;
    }

    private readonly Slot[] _slots;
    private readonly int _mask;
    private readonly Action _afterClaim;
    private readonly Action _afterSequenceCheck;

    // PIPE-6: Java pads _producerIndex/_consumerIndex onto separate cache lines
    // (JCTools Pad0/Pad1) to avoid false sharing. Intentionally NOT ported —
    // correctness is unaffected, the gain is unmeasured on this runtime, and
    // spacer fields would trip the unused-field analyzer (severity=error). Guard
    // with a BenchmarkDotNet throughput comparison before adding padding.
    private long _producerIndex;
    private long _consumerIndex;
    private long _overwrittenCount;

    /// <summary>Creates a ring buffer with the given slot capacity.</summary>
    /// <param name="capacity">
    /// Slot count. Rounded up to a power of two so index wrapping stays a
    /// bitmask — read <see cref="Capacity"/> back for the value actually used
    /// rather than assuming this one.
    /// </param>
    public BoundedEventBuffer(int capacity)
        : this(capacity, static () => { })
    {
    }

    /// <summary>Test seam: <paramref name="afterClaim"/> fires after the
    /// producer claims a slot, before it publishes the event.</summary>
    internal BoundedEventBuffer(int capacity, Action afterClaim)
        : this(capacity, afterClaim, static () => { })
    {
    }

    /// <summary>
    /// Test seam with both hooks: <paramref name="afterClaim"/> (the
    /// producer's — see the one-argument overload) and
    /// <paramref name="afterSequenceCheck"/>, which fires after a slot's
    /// sequence has been accepted and before its event is read — the window
    /// a lapping producer can overwrite the slot in.
    /// </summary>
    internal BoundedEventBuffer(int capacity, Action afterClaim, Action afterSequenceCheck)
    {
        var rounded = NextPowerOfTwo(capacity);
        _slots = new Slot[rounded];
        _mask = rounded - 1;
        _afterClaim = afterClaim;
        _afterSequenceCheck = afterSequenceCheck;
        for (var i = 0; i < rounded; i++)
        {
            _slots[i] = new Slot { Sequence = i };
        }
    }

    /// <summary>
    /// Enqueues an event. Always succeeds — overwrites oldest on overflow.
    /// </summary>
    /// <remarks>
    /// Publication is three steps, not two. The slot's sequence moves to
    /// this generation's <em>claim</em> value before the event is written
    /// and to its <em>published</em> value after, so the interval in which
    /// the slot holds a new event under an old sequence does not exist.
    /// Without the claim, a consumer that had already accepted the previous
    /// generation's sequence could read the new event out of the slot and
    /// deliver it under the wrong index — see
    /// <see cref="SteppedOverLappedSlot"/>, which is the check the claim
    /// makes effective.
    /// <para>
    /// <see cref="Thread.MemoryBarrier"/> between the claim and the event
    /// write is the writer's half of that. <see cref="Volatile.Write(ref long, long)"/>
    /// (release semantics) orders what comes <em>before</em> it, so it does
    /// not stop the event write from being hoisted above the claim; the
    /// full fence does, and the trailing release write keeps the event
    /// write from sinking below the publication. .NET has no
    /// store-store-only fence, so this uses the full (strictly stronger)
    /// barrier — both are compiler barriers rather than instructions on a
    /// strongly ordered CPU, which is precisely the point: the reordering
    /// this closes is the compiler's, not the processor's.
    /// </para>
    /// </remarks>
    public void Put(TraceEvent traceEvent)
    {
        var idx = Interlocked.Increment(ref _producerIndex) - 1;
        _afterClaim();
        var slot = _slots[(int)(idx & _mask)];
        Volatile.Write(ref slot.Sequence, idx);
        Thread.MemoryBarrier();
        slot.Event = traceEvent;
        Volatile.Write(ref slot.Sequence, idx + 1);
    }

    /// <summary>Dequeues the oldest event, or <c>null</c> if empty. Single-consumer only.</summary>
    public TraceEvent? Poll()
    {
        var producerIndex = Volatile.Read(ref _producerIndex);
        if (_consumerIndex >= producerIndex)
        {
            return null;
        }

        SkipOverwritten(producerIndex);
        var slot = _slots[(int)(_consumerIndex & _mask)];
        if (Volatile.Read(ref slot.Sequence) != _consumerIndex + 1)
        {
            return null;
        }

        _afterSequenceCheck();
        var traceEvent = slot.Event;
        if (SteppedOverLappedSlot(slot))
        {
            return null;
        }

        _consumerIndex++;
        return traceEvent;
    }

    /// <summary>
    /// Whether a producer replaced this slot's event between the sequence
    /// check and the read, and if so, counts it as shed and steps over it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The sequence check proves the producer <em>had</em> finished writing
    /// the slot, not that it still holds that event. A producer that laps
    /// the ring in the two instructions between the check and the read
    /// replaces the event underneath the consumer, which would then deliver
    /// a later generation's event under this index — and deliver it a
    /// second time when the index catches up, while the event that
    /// belonged here vanished without being counted. Overflow may lose
    /// events; it may not invent them, duplicate them, or lose them
    /// silently.
    /// </para>
    /// <para>
    /// Re-reading the sequence is what makes the read a claim rather than a
    /// hope, and it works only because <see cref="Put"/> marks the slot as
    /// claimed <em>before</em> it writes the event: the sequence therefore
    /// changes no later than the event does, never after it. When it
    /// changed, this index's event is definitively gone — counting one
    /// overwrite and advancing keeps the accounting exact, and the
    /// generation that took the slot is delivered when the consumer
    /// reaches its own index.
    /// </para>
    /// <para>
    /// The full fence before the re-read is not decoration. Acquire
    /// semantics (<see cref="Volatile.Read(ref long)"/>) order what comes
    /// <em>after</em> a load, not what comes before, so nothing otherwise
    /// stops the compiler from issuing this second sequence read ahead of
    /// the event read it is supposed to validate — which puts both
    /// sequence reads on one side of the data read and restores exactly
    /// the race this method exists to close. This is a seqlock, and it
    /// needs the reader's fence for the same reason every other seqlock
    /// does.
    /// </para>
    /// <para>
    /// <b>@edgeCase</b> The window is a two-instruction one and needs a
    /// producer to publish a whole ring's worth inside it, so it is
    /// vanishingly rare on the 65,536-slot default and reachable on a
    /// deliberately small one. Java's jcstress measured 123 samples in 2.2
    /// million on a four-slot ring (<c>DrainRacingPublishTest</c>).
    /// </para>
    /// </remarks>
    private bool SteppedOverLappedSlot(Slot slot)
    {
        Thread.MemoryBarrier();
        if (Volatile.Read(ref slot.Sequence) == _consumerIndex + 1)
        {
            return false;
        }

        Interlocked.Increment(ref _overwrittenCount);
        _consumerIndex++;
        return true;
    }

    /// <summary>Drains all currently-available events. Single-consumer only.</summary>
    public void Drain(Action<TraceEvent> action)
    {
        var producerIndex = Volatile.Read(ref _producerIndex);
        SkipOverwritten(producerIndex);
        while (_consumerIndex < producerIndex)
        {
            var slot = _slots[(int)(_consumerIndex & _mask)];
            if (Volatile.Read(ref slot.Sequence) != _consumerIndex + 1)
            {
                break;
            }

            _afterSequenceCheck();
            var traceEvent = slot.Event;
            if (SteppedOverLappedSlot(slot))
            {
                continue;
            }

            action(traceEvent!);
            _consumerIndex++;
        }
    }

    /// <summary>Approximate unconsumed count (may briefly exceed capacity under races).</summary>
    public int Size
    {
        get
        {
            var pending = Volatile.Read(ref _producerIndex)
                - Volatile.Read(ref _consumerIndex);
            return (int)Math.Min(Math.Max(0, pending), _slots.Length);
        }
    }

    /// <summary>
    /// The buffer's real slot count: the constructor argument rounded up to a
    /// power of two, so it may exceed what was requested.
    /// </summary>
    public int Capacity => _slots.Length;

    /// <summary>
    /// Whether the consumer has caught up with the producers at this instant.
    /// </summary>
    /// <remarks>
    /// Racy by nature on a concurrent buffer: a producer may publish between the
    /// read and the caller acting on it, so use it for diagnostics and never to
    /// decide that draining is finished.
    /// </remarks>
    public bool IsEmpty =>
        Volatile.Read(ref _consumerIndex)
            >= Volatile.Read(ref _producerIndex);

    /// <summary>
    /// Events overwritten by a producer before the (single) consumer ever
    /// read them — the ring-overwrite loss <see cref="Poll"/> and
    /// <see cref="Drain"/> silently skip past.
    /// </summary>
    /// <remarks>
    /// Read with <see cref="Interlocked.Read(ref long)"/> since producer
    /// threads may read it concurrently with the consumer's writes; the
    /// consumer itself is the only writer, matching the single-consumer
    /// contract <see cref="Poll"/>/<see cref="Drain"/> already rely on.
    /// </remarks>
    public long OverwrittenCount => Interlocked.Read(ref _overwrittenCount);

    private void SkipOverwritten(long producerIndex)
    {
        var target = producerIndex - _slots.Length;
        if (target > _consumerIndex)
        {
            Interlocked.Add(ref _overwrittenCount, target - _consumerIndex);
            _consumerIndex = target;
        }
    }

    private static int NextPowerOfTwo(int value)
    {
        return HighestOneBit(Math.Max(1, value - 1)) << 1;
    }

    private static int HighestOneBit(int value)
    {
        value |= value >> 1;
        value |= value >> 2;
        value |= value >> 4;
        value |= value >> 8;
        value |= value >> 16;
        return value - (int)((uint)value >> 1);
    }
}
