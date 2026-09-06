// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Diagnostics;
using System.Globalization;

namespace NarrativeTrace.Core;

/// <summary>
/// Translates monotonic <see cref="Stopwatch.GetTimestamp()"/> readings —
/// correct for durations but meaningless as an epoch — into wall-clock time.
/// The monotonic clock is anchored to <see cref="DateTimeOffset.UtcNow"/>
/// once at first use; drift over the process lifetime is bounded and
/// acceptable for trace timestamps.
/// </summary>
public static class MonotonicClock
{
    private static readonly DateTimeOffset AnchorWallClock =
        DateTimeOffset.UtcNow;

    private static readonly long AnchorTimestamp =
        Stopwatch.GetTimestamp();

    /// <summary>Converts a monotonic timestamp to wall-clock time.</summary>
    public static DateTimeOffset ToWallClock(long timestamp)
    {
        var elapsedSeconds =
            (double)(timestamp - AnchorTimestamp) / Stopwatch.Frequency;
        return AnchorWallClock.AddSeconds(elapsedSeconds);
    }

    /// <summary>
    /// Converts a <see cref="TimeSpan"/>-tick reading (a monotonic
    /// <see cref="Stopwatch"/> timestamp already normalised via
    /// <see cref="StopwatchTicks.ToTimeSpanTicks"/>, as stored on
    /// <c>TraceNode.StartTimestamp</c>) to wall-clock time, anchored to the same
    /// instant as <see cref="ToWallClock"/>.
    /// </summary>
    public static DateTimeOffset ToWallClockFromTimeSpanTicks(long timeSpanTicks)
    {
        var anchorTimeSpanTicks =
            StopwatchTicks.ToTimeSpanTicks(AnchorTimestamp);
        return AnchorWallClock.AddTicks(timeSpanTicks - anchorTimeSpanTicks);
    }

    /// <summary>
    /// Formats a monotonic timestamp as an ISO-8601 UTC string with
    /// millisecond precision (e.g. <c>2026-07-05T12:34:56.789Z</c>).
    /// </summary>
    public static string ToIso8601(long timestamp)
    {
        return ToWallClock(timestamp).UtcDateTime.ToString(
            "yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
    }
}
