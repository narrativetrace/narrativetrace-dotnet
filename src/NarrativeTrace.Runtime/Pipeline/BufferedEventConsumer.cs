// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using NarrativeTrace.Core;

namespace NarrativeTrace.Runtime;

/// <summary>
/// Best-effort asynchronous <see cref="IEventPipeline"/> backed by a bounded
/// ring buffer and a background drain thread. Keeps an event history without
/// blocking the publisher.
/// </summary>
/// <remarks>
/// <para>Adaptive draining by queue fill level:</para>
/// <list type="bullet">
///   <item><b>Normal (&lt; 70%)</b> — store each drained event.</item>
///   <item><b>Overloaded (&gt; 70%)</b> — batch drain and discard to relieve
///   pressure, counting every discard so loss accounting stays exact.</item>
/// </list>
/// <para>The ring is single-consumer, so every drain (the background cycle and
/// <see cref="Flush"/>) runs under one lock; the publish path stays lock-free.
/// Subscribers are notified outside the lock and isolated from each other.</para>
/// </remarks>
public sealed class BufferedEventConsumer
    : IEventPipeline, IEventSubscribable, IEventLossCounter
{
    /// <summary>
    /// Default buffer capacity: 64K slots (2^16) — the cross-runtime default.
    /// </summary>
    /// <remarks>
    /// Sizing rule for a non-default cap: <c>cap ≈ peak events/s × tolerable
    /// drain stall</c> — the amount of buffered history you can afford to
    /// lose if the drain thread falls behind for that long. Raise it for a
    /// bursty producer with a slow subscriber; lower it to bound memory on a
    /// low-throughput host.
    /// </remarks>
    public const int DefaultCapacity = 1 << 16;

    /// <summary>Default idle-drain poll interval, in milliseconds.</summary>
    public const int DefaultDrainIntervalMillis = 1;

    internal const double SheddingThreshold = 0.70;

    private const long WatchdogStaleThresholdMillis = 5000;
    private const long WatchdogCheckIntervalMillis = 1000;

    private readonly BoundedEventBuffer _queue;
    private readonly EventStore _store = new();
    private readonly List<Action<TraceEvent>> _subscribers = [];
    private readonly List<CountWaiter> _countWaiters = [];
    private readonly object _lock = new();
    private Thread? _consumer;
    private readonly ConsumerWatchdog? _watchdog;
    private readonly EventHandler _processExitHandler;
    private readonly Func<long> _clock;
    private readonly int _drainIntervalMillis;
    private volatile bool _running = true;
    private int _closed;
    private long _droppedCount;
    private long _lastActivityTimestamp;
    private long _watchdogStaleCount;
    private int _storedEventCount;

    /// <summary>Creates a consumer with the default capacity and starts its drain thread.</summary>
    /// <remarks>Owns a background thread — dispose it, or the thread outlives its usefulness.</remarks>
    public BufferedEventConsumer()
        : this(DefaultCapacity, startConsumer: true)
    {
    }

    /// <summary>Creates a consumer with an explicit capacity and starts its drain thread.</summary>
    /// <param name="bufferCapacity">
    /// Ring-buffer slots, rounded up to a power of two. Sizing is a real
    /// tradeoff: too small and a burst sheds events, too large and the buffer
    /// holds memory proportional to capacity for the process lifetime.
    /// </param>
    public BufferedEventConsumer(int bufferCapacity)
        : this(bufferCapacity, startConsumer: true)
    {
    }

    /// <summary>Creates a consumer with an explicit capacity and idle-drain interval.</summary>
    /// <param name="bufferCapacity">See the single-argument constructor.</param>
    /// <param name="drainIntervalMillis">
    /// How long the drain thread sleeps after it finds the queue empty,
    /// before polling again. Lower values reduce store latency at the cost
    /// of more wake-ups while idle; the default matches the cross-runtime
    /// default of 1ms.
    /// </param>
    public BufferedEventConsumer(int bufferCapacity, int drainIntervalMillis)
        : this(
            bufferCapacity, startConsumer: true,
            drainIntervalMillis: drainIntervalMillis)
    {
    }

    internal BufferedEventConsumer(
        int bufferCapacity, bool startConsumer, Func<long>? clock = null,
        int drainIntervalMillis = DefaultDrainIntervalMillis)
    {
        _queue = new BoundedEventBuffer(bufferCapacity);
        _clock = clock ?? Stopwatch.GetTimestamp;
        _drainIntervalMillis = drainIntervalMillis;
        _processExitHandler = (_, _) => Dispose();
        AppDomain.CurrentDomain.ProcessExit += _processExitHandler;
        _watchdog = startConsumer
            ? StartBackgroundConsumer()
            : CreateManualWatchdog();
    }

    // Background drain thread + timer-scheduled watchdog (production path).
    private ConsumerWatchdog StartBackgroundConsumer()
    {
        _consumer = new Thread(DrainLoop)
        {
            IsBackground = true,
            Name = "narrative-trace-consumer",
        };
        _consumer.Start();
        return new ConsumerWatchdog(
            () => Volatile.Read(ref _lastActivityTimestamp),
            WatchdogStaleThresholdMillis,
            WatchdogCheckIntervalMillis,
            OnWatchdogStale);
    }

    // No background thread; a timer-less watchdog whose checks the caller drives
    // via the injected clock and TriggerWatchdogCheck (deterministic tests).
    private ConsumerWatchdog CreateManualWatchdog()
    {
        return new ConsumerWatchdog(
            () => Volatile.Read(ref _lastActivityTimestamp),
            WatchdogStaleThresholdMillis,
            OnWatchdogStale,
            _clock);
    }

    /// <summary>Submits an event. Lock-free; never blocks the caller.</summary>
    public void Publish(TraceEvent traceEvent)
    {
        _queue.Put(traceEvent);
    }

    /// <summary>Registers a real-time subscriber notified on normal-mode events.</summary>
    /// <remarks>
    /// Normal-mode only is the load-shedding contract of
    /// <see cref="IEventSubscribable"/>: under shedding or emergency pressure
    /// events are discarded before subscribers are notified.
    /// </remarks>
    public void Subscribe(Action<TraceEvent> subscriber)
    {
        lock (_lock)
        {
            _subscribers.Add(subscriber);
        }
    }

    /// <summary>Drains all buffered events into the store so queries observe them.</summary>
    public void Flush()
    {
        var processed = new List<TraceEvent>();
        lock (_lock)
        {
            _queue.Drain(e =>
            {
                _store.Add(e);
                _storedEventCount++;
                processed.Add(e);
            });
            SignalCountWaiters();
        }

        NotifySubscribers(processed);
    }

    /// <summary>
    /// A task that completes once at least <paramref name="count"/> events have
    /// been drained into the store, or immediately if already reached. All
    /// outstanding waiters also complete on <see cref="Dispose"/> (mirroring
    /// Java's <c>EventStoreSubscriber</c> onComplete fan-out), so an awaiter
    /// never hangs past shutdown.
    /// </summary>
    public Task WhenCountReached(int count)
    {
        lock (_lock)
        {
            if (_storedEventCount >= count)
            {
                return Task.CompletedTask;
            }

            var waiter = new CountWaiter(
                count,
                new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously));
            _countWaiters.Add(waiter);
            return waiter.Completion.Task;
        }
    }

    /// <summary>Returns a snapshot of the events drained and stored so far.</summary>
    /// <returns>
    /// A copy of the stored events. Best-effort by design: events shed under
    /// backpressure, and events still queued but not yet drained, are absent —
    /// call <see cref="Flush"/> first to include what is merely undrained.
    /// </returns>
    public IReadOnlyList<TraceEvent> Events()
    {
        lock (_lock)
        {
            return _store.Events();
        }
    }

    /// <summary>
    /// Every event this consumer lost: overloaded-fill discards (queue fill
    /// &gt; 70%, drained and counted without storing or notifying) plus the
    /// ring's own <see cref="BoundedEventBuffer.OverwrittenCount"/>, for
    /// events a producer overwrote before any drain ever saw them. This is a
    /// deliberate divergence from Java's <c>droppedCount()</c>, which also
    /// counts reactive backpressure loss to slow subscribers: .NET
    /// subscribers are synchronous and isolated, so a throwing or slow
    /// subscriber never increments this count.
    /// </summary>
    public long DroppedCount =>
        Interlocked.Read(ref _droppedCount) + _queue.OverwrittenCount;

    /// <inheritdoc/>
    /// <remarks>
    /// The same number as <see cref="DroppedCount"/>, under the name a loss
    /// reporter reads it by — a counter nothing reads is how silent loss
    /// survives.
    /// </remarks>
    public long DroppedEventCount => DroppedCount;

    internal long WatchdogStaleCount => Interlocked.Read(ref _watchdogStaleCount);

    /// <summary>Test seam: the idle-drain poll interval this instance was built with.</summary>
    internal int DrainIntervalMillis => _drainIntervalMillis;

    /// <summary>
    /// Test seam: whether the drain thread is a background thread — the
    /// property that keeps it from rooting the process alive without an
    /// explicit <see cref="Dispose"/>. <see langword="true"/> when no
    /// thread was started (the manual-watchdog test constructor), since
    /// there is then nothing to keep the process alive either.
    /// </summary>
    internal bool IsBackgroundDrainThread => _consumer?.IsBackground ?? true;

    /// <summary>Milliseconds since the drain thread last ran a cycle, or 0 if never.</summary>
    public long StaleSinceMillis()
    {
        var last = Volatile.Read(ref _lastActivityTimestamp);
        return last == 0 ? 0 : ElapsedMillis(last);
    }

    /// <summary>Test seam: runs one watchdog staleness check synchronously.</summary>
    internal void TriggerWatchdogCheck()
    {
        _watchdog?.CheckNow();
    }

    /// <summary>Discards the stored events and resets the stored-event count.</summary>
    /// <remarks>Does not drain the ring buffer — events already queued still arrive afterwards.</remarks>
    public void Clear()
    {
        lock (_lock)
        {
            _store.Clear();
            _storedEventCount = 0;
        }
    }

    /// <summary>
    /// Stops the drain thread, performs a final <see cref="Flush"/>, and releases
    /// the process-exit hook.
    /// </summary>
    /// <remarks>
    /// Idempotent, and safe to call from any thread. Waits up to two seconds for
    /// the drain thread to finish; if it does not, disposal proceeds anyway
    /// rather than blocking shutdown, so a small tail of events can be lost.
    /// Stored events survive disposal and stay readable through
    /// <see cref="Events"/>.
    /// </remarks>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _closed, 1) == 1)
        {
            return;
        }

        AppDomain.CurrentDomain.ProcessExit -= _processExitHandler;
        _running = false;
        _consumer?.Join(2000);
        _watchdog?.Dispose();
        Flush();
        CompleteAllCountWaiters();
    }

    internal void DrainOnce()
    {
        RunDrainCycle();
    }

    /// <summary>Test seam: runs the registered process-exit auto-flush.</summary>
    internal void SimulateProcessExit()
    {
        _processExitHandler(this, EventArgs.Empty);
    }

    private void DrainLoop()
    {
        while (_running)
        {
            RunDrainCycle();
            if (_queue.IsEmpty)
            {
                Thread.Sleep(_drainIntervalMillis);
            }
        }
    }

    private void RunDrainCycle()
    {
        List<TraceEvent>? processed;
        lock (_lock)
        {
            Volatile.Write(ref _lastActivityTimestamp, _clock());
            processed = DrainByFillLevel();
            SignalCountWaiters();
        }

        if (processed is not null)
        {
            NotifySubscribers(processed);
        }
    }

    // Must be called under _lock: the ring is single-consumer and this both
    // drains the queue and mutates the store / frequency maps.
    private List<TraceEvent>? DrainByFillLevel()
    {
        var fill = (double)_queue.Size / _queue.Capacity;
        if (fill > SheddingThreshold)
        {
            // Overloaded (shedding or emergency fill): drain the batch
            // without storing or notifying, but still count it — an
            // uncounted discard is a silent loss.
            _queue.Drain(_ => Interlocked.Increment(ref _droppedCount));
        }
        else if (_queue.Poll() is { } e)
        {
            _store.Add(e);
            _storedEventCount++;
            return [e];
        }

        return null;
    }

    private void NotifySubscribers(List<TraceEvent> events)
    {
        if (events.Count == 0)
        {
            return;
        }

        Action<TraceEvent>[] subscribers;
        lock (_lock)
        {
            subscribers = _subscribers.ToArray();
        }

        foreach (var e in events)
        {
            foreach (var subscriber in subscribers)
            {
                Notify(subscriber, e);
            }
        }
    }

    private void OnWatchdogStale()
    {
        Interlocked.Increment(ref _watchdogStaleCount);
    }

    // Must be called under _lock. Completes and removes every waiter whose
    // threshold the stored-event count has now reached.
    private void SignalCountWaiters()
    {
        for (var i = _countWaiters.Count - 1; i >= 0; i--)
        {
            if (_storedEventCount >= _countWaiters[i].Threshold)
            {
                _countWaiters[i].Completion.TrySetResult(true);
                _countWaiters.RemoveAt(i);
            }
        }
    }

    private void CompleteAllCountWaiters()
    {
        lock (_lock)
        {
            foreach (var waiter in _countWaiters)
            {
                waiter.Completion.TrySetResult(true);
            }

            _countWaiters.Clear();
        }
    }

    private long ElapsedMillis(long sinceTimestamp)
    {
        return StopwatchTicks.ToTimeSpanTicks(_clock() - sinceTimestamp)
            / TimeSpan.TicksPerMillisecond;
    }

    private static void Notify(Action<TraceEvent> subscriber, TraceEvent e)
    {
        try
        {
            subscriber(e);
        }
        catch (Exception)
        {
            // Best-effort delivery: one bad subscriber must not stall drain.
        }
    }

    private readonly struct CountWaiter(
        int threshold, TaskCompletionSource<bool> completion)
    {
        public int Threshold { get; } = threshold;

        public TaskCompletionSource<bool> Completion { get; } = completion;
    }
}
