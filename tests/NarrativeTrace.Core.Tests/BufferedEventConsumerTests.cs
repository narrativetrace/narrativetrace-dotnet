// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using NarrativeTrace.Core;
using NarrativeTrace.Runtime;
using Xunit;

namespace NarrativeTrace.Core.Tests;

public class BufferedEventConsumerTests
{
    private static readonly TraceId Tid =
        new("0af7651916cd43dd8448eb211c80319c");

    private sealed class FakeClock
    {
        public long Now { get; set; }

        public long Read() => Now;
    }

    private static SpanId Span(int n) =>
        new(n.ToString("x16", CultureInfo.InvariantCulture));

    private static EnterEvent Enter(int span, string cls, string method) =>
        new(
            new SpanContext(Tid, Span(span), null), 0L,
            new MethodSignature(cls, method, []));

    [Fact]
    public void Normal_mode_stores_one_event_per_cycle()
    {
        using var consumer = new BufferedEventConsumer(16, startConsumer: false);
        consumer.Publish(Enter(1, "Svc", "Run"));
        consumer.Publish(Enter(2, "Svc", "Run"));

        consumer.DrainOnce();
        Assert.Single(consumer.Events());

        consumer.DrainOnce();
        Assert.Equal(2, consumer.Events().Count);
    }

    [Fact]
    public void Shedding_mode_drains_without_storing_but_counts_dropped()
    {
        using var consumer = new BufferedEventConsumer(16, startConsumer: false);
        for (var i = 0; i < 12; i++)
        {
            consumer.Publish(Enter(
                i + 1, "Svc", "M" + i.ToString(CultureInfo.InvariantCulture)));
        }

        consumer.DrainOnce();

        Assert.Empty(consumer.Events());
        Assert.Equal(12, consumer.DroppedCount);
    }

    [Fact]
    public void Near_full_fill_discards_and_counts_dropped()
    {
        using var consumer = new BufferedEventConsumer(16, startConsumer: false);
        for (var i = 0; i < 15; i++)
        {
            consumer.Publish(Enter(i + 1, "Svc", "Run"));
        }

        Assert.Equal(0, consumer.DroppedCount);
        consumer.DrainOnce();

        Assert.Empty(consumer.Events());
        Assert.Equal(15, consumer.DroppedCount);
    }

    [Fact]
    public void Throwing_subscriber_neither_stalls_drain_nor_counts_dropped()
    {
        using var consumer = new BufferedEventConsumer(16, startConsumer: false);
        consumer.Subscribe(_ =>
            throw new InvalidOperationException("subscriber down"));
        var received = new List<TraceEvent>();
        consumer.Subscribe(received.Add);

        consumer.Publish(Enter(1, "Svc", "Run"));
        consumer.DrainOnce();

        Assert.Single(consumer.Events());
        Assert.Single(received);
        Assert.Equal(0, consumer.DroppedCount);
    }

    [Fact]
    public void Flush_drains_all_remaining_into_store()
    {
        using var consumer = new BufferedEventConsumer(16, startConsumer: false);
        consumer.Publish(Enter(1, "Svc", "Run"));
        consumer.Publish(Enter(2, "Svc", "Run"));

        consumer.Flush();

        Assert.Equal(2, consumer.Events().Count);
    }

    [Fact]
    public void Subscribers_receive_normal_mode_events()
    {
        using var consumer = new BufferedEventConsumer(16, startConsumer: false);
        var received = new List<TraceEvent>();
        consumer.Subscribe(received.Add);

        consumer.Publish(Enter(1, "Svc", "Run"));
        consumer.DrainOnce();

        Assert.Single(received);
    }

    /// <summary>
    /// The consumer is the production event stream a listener attaches to, so
    /// it has to satisfy the abstraction the listening layers depend on —
    /// otherwise nothing outside Runtime can subscribe without inverting the
    /// dependency arrow.
    /// </summary>
    [Fact]
    public void The_consumer_is_subscribable_through_the_core_abstraction()
    {
        using var consumer = new BufferedEventConsumer(16, startConsumer: false);
        var received = new List<TraceEvent>();
        IEventSubscribable subscribable = consumer;

        subscribable.Subscribe(received.Add);
        consumer.Publish(Enter(1, "Svc", "Run"));
        consumer.DrainOnce();

        Assert.Single(received);
    }

    [Fact]
    public void Shedding_mode_does_not_notify_subscribers()
    {
        using var consumer = new BufferedEventConsumer(16, startConsumer: false);
        var received = new List<TraceEvent>();
        consumer.Subscribe(received.Add);
        for (var i = 0; i < 12; i++)
        {
            consumer.Publish(Enter(
                i + 1, "Svc", "M" + i.ToString(CultureInfo.InvariantCulture)));
        }

        consumer.DrainOnce();

        Assert.Empty(received);
    }

    [Fact]
    public void Emergency_mode_does_not_notify_subscribers()
    {
        using var consumer = new BufferedEventConsumer(16, startConsumer: false);
        var received = new List<TraceEvent>();
        consumer.Subscribe(received.Add);
        for (var i = 0; i < 15; i++)
        {
            consumer.Publish(Enter(i + 1, "Svc", "Run"));
        }

        consumer.DrainOnce();

        Assert.Empty(received);
    }

    [Fact]
    public void Multiple_subscribers_each_receive_normal_mode_events()
    {
        using var consumer = new BufferedEventConsumer(16, startConsumer: false);
        var first = new List<TraceEvent>();
        var second = new List<TraceEvent>();
        consumer.Subscribe(first.Add);
        consumer.Subscribe(second.Add);

        consumer.Publish(Enter(1, "Svc", "Run"));
        consumer.DrainOnce();

        Assert.Single(first);
        Assert.Single(second);
    }

    [Fact]
    public async Task Concurrent_dispose_is_idempotent()
    {
        var consumer = new BufferedEventConsumer(1024);
        consumer.Publish(Enter(1, "Svc", "Run"));

        var tasks = Enumerable.Range(0, 8)
            .Select(_ => Task.Run(consumer.Dispose))
            .ToArray();

        var all = Task.WhenAll(tasks);
        Assert.Same(all, await Task.WhenAny(all, Task.Delay(5000)));
        await all;
        Assert.True(consumer.Events().Count >= 1);
    }

    [Fact]
    public void Stale_since_millis_is_zero_before_first_drain()
    {
        using var consumer = new BufferedEventConsumer(16, startConsumer: false);

        Assert.Equal(0, consumer.StaleSinceMillis());
    }

    [Fact]
    public void Stale_since_millis_advances_after_a_gap()
    {
        var clock = new FakeClock();
        using var consumer = new BufferedEventConsumer(
            16, startConsumer: false, clock.Read);
        clock.Now = 1000;
        consumer.Publish(Enter(1, "Svc", "Run"));
        consumer.DrainOnce();

        clock.Now = 1000 + (Stopwatch.Frequency * 2);

        Assert.True(consumer.StaleSinceMillis() >= 2000);
    }

    [Fact]
    public void Heartbeat_advances_on_an_empty_drain_cycle()
    {
        var clock = new FakeClock { Now = 500 };
        using var consumer = new BufferedEventConsumer(
            16, startConsumer: false, clock.Read);

        consumer.DrainOnce();
        clock.Now = 500 + Stopwatch.Frequency;

        Assert.True(consumer.StaleSinceMillis() >= 1000);
    }

    [Fact]
    public void Stale_heartbeat_increments_watchdog_stale_count()
    {
        var clock = new FakeClock();
        using var consumer = new BufferedEventConsumer(
            16, startConsumer: false, clock.Read);
        clock.Now = 1000;
        consumer.Publish(Enter(1, "Svc", "Run"));
        consumer.DrainOnce();

        clock.Now = 1000 + (Stopwatch.Frequency * 10);
        consumer.TriggerWatchdogCheck();

        Assert.Equal(1, consumer.WatchdogStaleCount);
    }

    [Fact]
    public void Fresh_heartbeat_does_not_increment_watchdog_stale_count()
    {
        var clock = new FakeClock();
        using var consumer = new BufferedEventConsumer(
            16, startConsumer: false, clock.Read);
        clock.Now = 1000;
        consumer.Publish(Enter(1, "Svc", "Run"));
        consumer.DrainOnce();

        clock.Now = 1000 + Stopwatch.Frequency;
        consumer.TriggerWatchdogCheck();

        Assert.Equal(0, consumer.WatchdogStaleCount);
    }

    [Fact]
    public void Clear_resets_the_store()
    {
        using var consumer = new BufferedEventConsumer(16, startConsumer: false);
        consumer.Publish(Enter(1, "Svc", "Run"));
        consumer.DrainOnce();
        Assert.Single(consumer.Events());

        consumer.Clear();

        Assert.Empty(consumer.Events());
    }

    [Fact]
    public void Process_exit_drains_remaining_events()
    {
        using var consumer = new BufferedEventConsumer(16, startConsumer: false);
        consumer.Publish(Enter(1, "Svc", "Run"));
        Assert.Empty(consumer.Events());

        consumer.SimulateProcessExit();

        Assert.Single(consumer.Events());
    }

    [Fact]
    public void Process_exit_is_no_op_after_explicit_dispose()
    {
        var consumer = new BufferedEventConsumer(16, startConsumer: false);
        consumer.Publish(Enter(1, "Svc", "Run"));
        consumer.Dispose();
        var afterDispose = consumer.Events().Count;

        consumer.SimulateProcessExit();

        Assert.Equal(afterDispose, consumer.Events().Count);
    }

    [Fact]
    public void Background_consumer_drains_published_events()
    {
        using var consumer = new BufferedEventConsumer(1024);
        for (var i = 0; i < 50; i++)
        {
            consumer.Publish(Enter(i + 1, "Svc", "Run"));
        }

        consumer.Flush();

        Assert.Equal(50, consumer.Events().Count);
    }

    /// <summary>
    /// Deterministic regression for the gap the family parity rule names:
    /// "flush() is a barrier" also has to cover subscriber delivery, not just
    /// the store. Before the drain-lock fix, the background cycle stored the
    /// event and released its lock <em>before</em> notifying subscribers, so
    /// a <see cref="BufferedEventConsumer.Flush"/> landing in that gap
    /// returned while a subscriber had not yet seen the event it just
    /// accounted for (this is what forced
    /// <c>TranslationSubscriberTests.Receives_events_live_from_a_buffered_event_consumer</c>
    /// onto <c>Dispose()</c> as its barrier instead). The <c>beforeNotify</c>
    /// seam pins the background thread exactly in that gap so the race is
    /// reproduced every run, not just under contention.
    /// </summary>
    [Fact]
    public void Flush_waits_out_an_in_flight_background_notify_before_returning()
    {
        using var reachedHook = new ManualResetEventSlim(false);
        using var releaseHook = new ManualResetEventSlim(false);
        using var consumer = new BufferedEventConsumer(
            16, startConsumer: true, beforeNotify: () =>
            {
                reachedHook.Set();
                releaseHook.Wait(TimeSpan.FromSeconds(5));
            });

        var delivered = new List<TraceEvent>();
        consumer.Subscribe(e => { lock (delivered) { delivered.Add(e); } });
        consumer.Publish(Enter(1, "Svc", "Run"));

        Assert.True(
            reachedHook.Wait(TimeSpan.FromSeconds(5)),
            "background drain cycle never reached the pre-notify hook");

        var flush = Task.Run(consumer.Flush);
        Assert.False(
            flush.Wait(TimeSpan.FromMilliseconds(200)),
            "Flush() returned while the background drain step was still storing/notifying -- not a barrier");

        releaseHook.Set();
        Assert.True(
            flush.Wait(TimeSpan.FromSeconds(5)),
            "Flush() never returned after the in-flight drain step completed");

        lock (delivered)
        {
            Assert.Single(delivered);
        }

        Assert.Single(consumer.Events());
    }

    [Fact]
    public async Task When_count_reached_completes_after_enough_events()
    {
        using var consumer = new BufferedEventConsumer(16, startConsumer: false);
        var reached = consumer.WhenCountReached(2);
        Assert.False(reached.IsCompleted);

        consumer.Publish(Enter(1, "Svc", "Run"));
        consumer.Publish(Enter(2, "Svc", "Run"));
        consumer.DrainOnce();
        consumer.DrainOnce();

        await reached.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(reached.IsCompletedSuccessfully);
    }

    [Fact]
    public void When_count_reached_is_already_complete_when_threshold_met()
    {
        using var consumer = new BufferedEventConsumer(16, startConsumer: false);
        consumer.Publish(Enter(1, "Svc", "Run"));
        consumer.DrainOnce();

        Assert.True(consumer.WhenCountReached(1).IsCompleted);
    }

    [Fact]
    public async Task Dispose_completes_pending_count_waiters()
    {
        var consumer = new BufferedEventConsumer(16, startConsumer: false);
        var reached = consumer.WhenCountReached(5);

        consumer.Dispose();

        await reached.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(reached.IsCompleted);
    }

    [Fact]
    public void Dispose_flushes_pending_events_and_is_idempotent()
    {
        var consumer = new BufferedEventConsumer(1024);
        consumer.Publish(Enter(1, "Svc", "Run"));

        consumer.Dispose();
        consumer.Dispose();

        Assert.True(consumer.Events().Count >= 1);
    }

    // Cross-runtime defaults contract (owner, 2026-08-31): default cap is
    // 65,536 (2^16), not an arbitrarily larger power of two.
    [Fact]
    public void Default_capacity_matches_the_cross_port_contract()
    {
        Assert.Equal(1 << 16, BufferedEventConsumer.DefaultCapacity);
    }

    [Fact]
    public void Background_consumer_thread_never_roots_the_process_alive()
    {
        using var consumer = new BufferedEventConsumer(16);

        Assert.True(consumer.IsBackgroundDrainThread);
    }

    [Fact]
    public void Manual_watchdog_construction_has_no_thread_to_root_the_process_either()
    {
        using var consumer = new BufferedEventConsumer(
            16, startConsumer: false);

        Assert.True(consumer.IsBackgroundDrainThread);
    }

    [Fact]
    public void Drain_interval_defaults_to_the_cross_port_value()
    {
        using var consumer = new BufferedEventConsumer(16);

        Assert.Equal(
            BufferedEventConsumer.DefaultDrainIntervalMillis,
            consumer.DrainIntervalMillis);
    }

    [Fact]
    public void Drain_interval_is_configurable()
    {
        using var consumer = new BufferedEventConsumer(
            16, drainIntervalMillis: 25);

        Assert.Equal(25, consumer.DrainIntervalMillis);
    }
}
