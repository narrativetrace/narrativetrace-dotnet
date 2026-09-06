// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Diagnostics;
using NarrativeTrace.Core;
using Xunit;

namespace NarrativeTrace.Core.Tests;

public class MonotonicClockTests
{
    [Fact]
    public void ToIso8601_produces_utc_millisecond_string()
    {
        var text = MonotonicClock.ToIso8601(Stopwatch.GetTimestamp());

        Assert.Matches(
            @"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3}Z$", text);
    }

    [Fact]
    public void Later_timestamp_is_not_before_earlier()
    {
        var earlier = MonotonicClock.ToWallClock(1000);
        var later = MonotonicClock.ToWallClock(
            1000 + Stopwatch.Frequency);

        Assert.True(later > earlier);
    }

    [Fact]
    public void One_frequency_of_ticks_maps_to_one_second()
    {
        var baseline = MonotonicClock.ToWallClock(5_000_000);
        var oneSecondLater = MonotonicClock.ToWallClock(
            5_000_000 + Stopwatch.Frequency);

        Assert.Equal(
            1.0, (oneSecondLater - baseline).TotalSeconds, precision: 3);
    }

    [Fact]
    public void TimeSpan_tick_conversion_agrees_with_timestamp_conversion()
    {
        var timestamp = Stopwatch.GetTimestamp();

        var fromTimestamp = MonotonicClock.ToWallClock(timestamp);
        var fromTicks = MonotonicClock.ToWallClockFromTimeSpanTicks(
            StopwatchTicks.ToTimeSpanTicks(timestamp));

        Assert.True(
            (fromTicks - fromTimestamp).Duration()
                < TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public void TimeSpan_tick_deltas_preserve_durations_exactly()
    {
        var start = MonotonicClock.ToWallClockFromTimeSpanTicks(
            10_000_000);
        var end = MonotonicClock.ToWallClockFromTimeSpanTicks(
            10_000_000 + TimeSpan.TicksPerSecond);

        Assert.Equal(TimeSpan.FromSeconds(1), end - start);
    }
}
