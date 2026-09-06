// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Diagnostics;
using System.Threading;
using NarrativeTrace.Core;

namespace NarrativeTrace.Runtime;

/// <summary>
/// Monitors a heartbeat supplier and fires a callback when the heartbeat is
/// stale beyond a threshold. Uses a single background timer to check
/// periodically.
/// </summary>
internal sealed class ConsumerWatchdog : IDisposable
{
    private readonly Func<long> _heartbeatTimestamp;
    private readonly Func<long> _nowTimestamp;
    private readonly long _staleThresholdMillis;
    private readonly Action _onStale;
    private readonly Timer? _timer;

    /// <summary>Production watchdog: schedules periodic checks on a timer.</summary>
    public ConsumerWatchdog(
        Func<long> heartbeatTimestamp,
        long staleThresholdMillis,
        long checkIntervalMillis,
        Action onStale)
        : this(
            heartbeatTimestamp, staleThresholdMillis, onStale,
            Stopwatch.GetTimestamp)
    {
        _timer = new Timer(
            _ => CheckNow(), null, checkIntervalMillis, checkIntervalMillis);
    }

    /// <summary>
    /// Manual watchdog: no timer, injectable clock. Callers drive checks via
    /// <see cref="CheckNow"/> — used for deterministic wiring tests.
    /// </summary>
    internal ConsumerWatchdog(
        Func<long> heartbeatTimestamp,
        long staleThresholdMillis,
        Action onStale,
        Func<long> nowTimestamp)
    {
        _heartbeatTimestamp = heartbeatTimestamp;
        _staleThresholdMillis = staleThresholdMillis;
        _onStale = onStale;
        _nowTimestamp = nowTimestamp;
    }

    internal void CheckNow()
    {
        var last = _heartbeatTimestamp();
        if (last == 0)
        {
            return;
        }

        var elapsedMillis =
            StopwatchTicks.ToTimeSpanTicks(_nowTimestamp() - last)
                / TimeSpan.TicksPerMillisecond;
        if (elapsedMillis > _staleThresholdMillis)
        {
            RunSafely();
        }
    }

    private void RunSafely()
    {
        try
        {
            _onStale();
        }
        catch (Exception)
        {
            // Best-effort watchdog: never let a stale callback escape.
        }
    }

    public void Dispose()
    {
        _timer?.Dispose();
    }
}
