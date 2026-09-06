// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Diagnostics;
using NarrativeTrace.Core;
using Xunit;

namespace NarrativeTrace.Core.Tests;

public class StopwatchTicksTests
{
    [Fact]
    public void One_second_of_stopwatch_ticks_is_one_second_of_timespan_ticks()
    {
        Assert.Equal(
            TimeSpan.TicksPerSecond,
            StopwatchTicks.ToTimeSpanTicks(Stopwatch.Frequency));
    }

    [Fact]
    public void One_millisecond_of_stopwatch_ticks_is_one_ms_of_timespan_ticks()
    {
        Assert.Equal(
            TimeSpan.TicksPerMillisecond,
            StopwatchTicks.ToTimeSpanTicks(Stopwatch.Frequency / 1000));
    }

    [Fact]
    public void Zero_converts_to_zero()
    {
        Assert.Equal(0, StopwatchTicks.ToTimeSpanTicks(0));
    }
}
