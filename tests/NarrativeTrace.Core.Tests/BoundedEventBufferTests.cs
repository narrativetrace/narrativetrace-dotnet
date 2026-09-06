// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Globalization;
using FsCheck;
using FsCheck.Xunit;
using NarrativeTrace.Core;
using NarrativeTrace.Runtime;
using Xunit;

namespace NarrativeTrace.Core.Tests;

public class BoundedEventBufferTests
{
    private static readonly SpanContext Sc = new(
        new TraceId("0af7651916cd43dd8448eb211c80319c"),
        new SpanId("b7ad6b7169203331"),
        null);

    private static EnterEvent Event(int id) => new(
        Sc, id,
        new MethodSignature(
            "Svc", id.ToString(CultureInfo.InvariantCulture), []));

    private static int IdOf(TraceEvent e) => int.Parse(
        ((EnterEvent)e).Signature.MethodName, CultureInfo.InvariantCulture);

    [Fact]
    public void Put_then_poll_returns_the_event()
    {
        var buffer = new BoundedEventBuffer(4);
        var evt = Event(7);

        buffer.Put(evt);

        Assert.Same(evt, buffer.Poll());
    }

    [Fact]
    public void Poll_on_empty_returns_null()
    {
        var buffer = new BoundedEventBuffer(4);

        Assert.Null(buffer.Poll());
    }

    [Fact]
    public void Events_are_polled_in_fifo_order()
    {
        var buffer = new BoundedEventBuffer(8);
        for (var i = 0; i < 5; i++)
        {
            buffer.Put(Event(i));
        }

        for (var i = 0; i < 5; i++)
        {
            Assert.Equal(i, IdOf(buffer.Poll()!));
        }

        Assert.Null(buffer.Poll());
    }

    [Fact]
    public void Capacity_is_rounded_up_to_a_power_of_two()
    {
        Assert.Equal(1024, new BoundedEventBuffer(1000).Capacity);
        Assert.Equal(1024, new BoundedEventBuffer(1024).Capacity);
        Assert.Equal(2048, new BoundedEventBuffer(1025).Capacity);
        Assert.Equal(2, new BoundedEventBuffer(1).Capacity);
    }

    [Fact]
    public void Overflow_overwrites_oldest_events()
    {
        var buffer = new BoundedEventBuffer(4);
        for (var i = 0; i < 6; i++)
        {
            buffer.Put(Event(i));
        }

        // capacity 4: events 0 and 1 were overwritten by 4 and 5.
        Assert.Equal(2, IdOf(buffer.Poll()!));
        Assert.Equal(3, IdOf(buffer.Poll()!));
        Assert.Equal(4, IdOf(buffer.Poll()!));
        Assert.Equal(5, IdOf(buffer.Poll()!));
        Assert.Null(buffer.Poll());
        Assert.Equal(2, buffer.OverwrittenCount);
    }

    [Fact]
    public void No_overwrite_leaves_the_count_at_zero()
    {
        var buffer = new BoundedEventBuffer(4);
        buffer.Put(Event(0));
        buffer.Put(Event(1));

        Assert.Equal(0, buffer.OverwrittenCount);
    }

    [Fact]
    public void Overwrite_count_is_reconciled_lazily_at_the_next_poll_or_drain()
    {
        var buffer = new BoundedEventBuffer(4);
        for (var i = 0; i < 12; i++)
        {
            buffer.Put(Event(i));
        }

        // The ring only reconciles overwritten slots when the consumer next
        // looks — matching _consumerIndex's own lazy-advance discipline.
        Assert.Equal(0, buffer.OverwrittenCount);

        buffer.Poll();

        // capacity 4, 12 published: events 0..7 were overwritten before this
        // poll ever ran, all reconciled by the single Poll() call above.
        Assert.Equal(8, buffer.OverwrittenCount);
    }

    [Fact]
    public void Size_reflects_pending_events_and_is_bounded_by_capacity()
    {
        var buffer = new BoundedEventBuffer(4);
        Assert.Equal(0, buffer.Size);

        buffer.Put(Event(1));
        buffer.Put(Event(2));
        Assert.Equal(2, buffer.Size);

        for (var i = 0; i < 10; i++)
        {
            buffer.Put(Event(i));
        }

        Assert.Equal(4, buffer.Size);
    }

    [Fact]
    public void IsEmpty_tracks_pending_events()
    {
        var buffer = new BoundedEventBuffer(4);
        Assert.True(buffer.IsEmpty);

        buffer.Put(Event(1));
        Assert.False(buffer.IsEmpty);

        buffer.Poll();
        Assert.True(buffer.IsEmpty);
    }

    [Fact]
    public void Drain_consumes_all_available_events_in_order()
    {
        var buffer = new BoundedEventBuffer(8);
        for (var i = 0; i < 5; i++)
        {
            buffer.Put(Event(i));
        }

        var drained = new List<int>();
        buffer.Drain(e => drained.Add(IdOf(e)));

        Assert.Equal([0, 1, 2, 3, 4], drained);
        Assert.True(buffer.IsEmpty);
    }

    [Fact]
    public async Task Poll_returns_null_while_producer_is_between_claim_and_publish()
    {
        using var claimed = new ManualResetEventSlim(false);
        using var mayPublish = new ManualResetEventSlim(false);
        var buffer = new BoundedEventBuffer(4, () =>
        {
            claimed.Set();
            mayPublish.Wait();
        });
        var evt = Event(42);

        var producer = Task.Run(() => buffer.Put(evt));
        claimed.Wait();

        // Slot is claimed but not yet published — must not be visible.
        Assert.Null(buffer.Poll());

        mayPublish.Set();
        await producer;

        Assert.Same(evt, buffer.Poll());
    }

    [Fact]
    public void Concurrent_producers_lose_no_events_within_capacity()
    {
        for (var iteration = 0; iteration < 200; iteration++)
        {
            AssertNoLoss(producers: 4, perProducer: 64);
        }
    }

    [Property(MaxTest = 30)]
    public void No_loss_for_arbitrary_producer_shape(
        PositiveInt producersRaw, PositiveInt perProducerRaw)
    {
        var producers = 1 + (producersRaw.Get % 6);
        var perProducer = 1 + (perProducerRaw.Get % 40);

        AssertNoLoss(producers, perProducer);
    }

    // Runs `producers` concurrent producers (each emitting a disjoint block of
    // ids) against a single consumer, with capacity chosen to exceed the total
    // so no overwrite occurs, then asserts every id is consumed exactly once.
    private static void AssertNoLoss(int producers, int perProducer)
    {
        var total = producers * perProducer;
        var buffer = new BoundedEventBuffer(Math.Max(total * 2, 8));
        var consumed = new List<int>(total);
        using var done = new CountdownEvent(producers);

        var producerTasks = new Task[producers];
        for (var p = 0; p < producers; p++)
        {
            var start = p * perProducer;
            producerTasks[p] = Task.Run(() =>
            {
                for (var i = 0; i < perProducer; i++)
                {
                    buffer.Put(Event(start + i));
                }

                done.Signal();
            });
        }

        var consumer = Task.Run(() =>
        {
            while (!done.IsSet || !buffer.IsEmpty)
            {
                if (buffer.Poll() is { } e)
                {
                    consumed.Add(IdOf(e));
                }
            }
        });

        Task.WaitAll(producerTasks);
        consumer.Wait();
        buffer.Drain(e => consumed.Add(IdOf(e)));

        Assert.Equal(total, consumed.Count);
        Assert.Equal(Enumerable.Range(0, total), consumed.OrderBy(x => x));
    }

    // The seqlock fix (mirrors java's BoundedEventBufferPublicationSeamTest):
    // the sequence check proves the producer
    // HAD finished writing the slot, not that it still holds that event. A
    // producer that laps the ring between the check and the read replaces
    // the event underneath the consumer -- delivering a later generation's
    // event under this index, and delivering it a second time when the
    // index catches up, while the event that belonged here vanishes
    // uncounted. The second (consumer-side) test seam pauses a drain/poll
    // in exactly that window, making a race jcstress measured at 123 in 2.2
    // million infinitely wide and deterministic.
    [Fact]
    public async Task Drain_skips_a_slot_overwritten_between_the_check_and_the_read()
    {
        using var atSlotRead = new ManualResetEventSlim(false);
        using var releaseRead = new ManualResetEventSlim(false);
        var buffer = new BoundedEventBuffer(2, afterClaim: () => { }, afterSequenceCheck: () =>
        {
            atSlotRead.Set();
            releaseRead.Wait();
        });
        buffer.Put(Event(0));
        var firstPass = new List<TraceEvent>();
        var consumer = Task.Run(() => buffer.Drain(firstPass.Add));

        Assert.True(atSlotRead.Wait(TimeSpan.FromSeconds(5)),
            "the consumer seam must fire between the sequence check and the slot read");
        var later = LapTheRing(buffer);
        releaseRead.Set();
        await consumer;

        Assert.Empty(firstPass);
        Assert.Equal(1, buffer.OverwrittenCount);

        var secondPass = new List<TraceEvent>();
        buffer.Drain(secondPass.Add);
        Assert.Equal(later, secondPass);
        Assert.Equal(1, buffer.OverwrittenCount);
    }

    [Fact]
    public async Task Poll_answers_null_and_counts_the_loss_for_a_slot_overwritten_mid_read()
    {
        using var atSlotRead = new ManualResetEventSlim(false);
        using var releaseRead = new ManualResetEventSlim(false);
        var buffer = new BoundedEventBuffer(2, afterClaim: () => { }, afterSequenceCheck: () =>
        {
            atSlotRead.Set();
            releaseRead.Wait();
        });
        buffer.Put(Event(0));
        TraceEvent? polled = null;
        var consumer = Task.Run(() => polled = buffer.Poll());

        Assert.True(atSlotRead.Wait(TimeSpan.FromSeconds(5)),
            "the consumer seam must fire between the sequence check and the slot read");
        var later = LapTheRing(buffer);
        releaseRead.Set();
        await consumer;

        Assert.Null(polled);
        Assert.Equal(1, buffer.OverwrittenCount);
        Assert.Same(later[0], buffer.Poll());
        Assert.Same(later[1], buffer.Poll());
    }

    // Publishes two more events, wrapping the two-slot ring onto the slot being read.
    private static List<TraceEvent> LapTheRing(BoundedEventBuffer buffer)
    {
        var later = new List<TraceEvent> { Event(1), Event(2) };
        foreach (var e in later)
        {
            buffer.Put(e);
        }

        return later;
    }
}
