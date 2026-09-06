// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Diagnostics;

namespace NarrativeTrace.Core;

/// <summary>
/// Converts raw <see cref="Stopwatch.GetTimestamp()"/> tick quantities — whose
/// scale is <see cref="Stopwatch.Frequency"/> (e.g. nanoseconds on Linux/.NET,
/// 100 ns units on some Windows QPC configurations) — into <see cref="TimeSpan"/>
/// ticks (100 ns units).
/// </summary>
/// <remarks>
/// Trace events capture <see cref="Stopwatch.GetTimestamp()"/>; durations and
/// start offsets derived from them are therefore in Stopwatch units. Downstream
/// renderers and exporters divide by <see cref="TimeSpan.TicksPerMillisecond"/>
/// to obtain milliseconds, which is only correct when the value is already in
/// TimeSpan ticks. Normalising here — once, at the point Stopwatch ticks are
/// baked into the tree — keeps every consumer correct on all platforms without
/// per-consumer conversion. When <see cref="Stopwatch.Frequency"/> equals the
/// TimeSpan tick rate (10 MHz) the conversion is the identity.
/// </remarks>
public static class StopwatchTicks
{
    private static readonly double TimeSpanTicksPerStopwatchTick =
        (double)TimeSpan.TicksPerSecond / Stopwatch.Frequency;

    /// <summary>
    /// Converts a Stopwatch-tick quantity to TimeSpan ticks (100 ns units).
    /// </summary>
    public static long ToTimeSpanTicks(long stopwatchTicks)
    {
        return (long)(stopwatchTicks * TimeSpanTicksPerStopwatchTick);
    }
}
