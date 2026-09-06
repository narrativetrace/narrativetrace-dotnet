// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Diagnostics;
using NarrativeTrace.Runtime;
using Xunit;

namespace NarrativeTrace.Core.Tests;

public class ConsumerWatchdogTests
{
    [Fact]
    public void Fires_when_heartbeat_is_stale_beyond_threshold()
    {
        using var fired = new ManualResetEventSlim(false);
        var staleTimestamp =
            Stopwatch.GetTimestamp() - (Stopwatch.Frequency * 10);
        using var watchdog = new ConsumerWatchdog(
            () => staleTimestamp,
            staleThresholdMillis: 50,
            checkIntervalMillis: 20,
            () => fired.Set());

        Assert.True(fired.Wait(2000));
    }

    [Fact]
    public void Does_not_fire_before_the_first_heartbeat()
    {
        using var fired = new ManualResetEventSlim(false);
        using var watchdog = new ConsumerWatchdog(
            heartbeatTimestamp: () => 0,
            staleThresholdMillis: 10,
            checkIntervalMillis: 20,
            () => fired.Set());

        Assert.False(fired.Wait(200));
    }

    [Fact]
    public void Does_not_fire_when_heartbeat_is_fresh()
    {
        using var fired = new ManualResetEventSlim(false);
        using var watchdog = new ConsumerWatchdog(
            () => Stopwatch.GetTimestamp(),
            staleThresholdMillis: 5000,
            checkIntervalMillis: 20,
            () => fired.Set());

        Assert.False(fired.Wait(200));
    }
}
