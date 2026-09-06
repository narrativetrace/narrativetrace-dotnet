// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Core;

/// <summary>
/// Detects concurrent branches that did not actually overlap — the
/// <c>await</c>-in-a-loop pattern that looks parallel but runs one at a time.
/// </summary>
/// <remarks>
/// A diagnosis aid, not a capture concern: it reads the timings already on a
/// finished trace and reports what they imply, so it is safe to run over any
/// <see cref="TraceNode"/> list at any time. The judgement is based purely on
/// observed start/end intervals, so it reports what <i>did</i> happen, not what
/// <i>could</i> happen — branches that were genuinely independent but merely
/// finished quickly enough not to overlap still read as sequential.
/// </remarks>
public static class SequentialAsyncDetector
{
    /// <summary>
    /// Reports whether a group's branches ran one after another, and what
    /// running them concurrently would have cost instead.
    /// </summary>
    /// <param name="members">
    /// The sibling nodes of one concurrent group, typically a
    /// <see cref="ChildSegment.Nodes"/>. Order does not matter — every pair is
    /// compared.
    /// </param>
    /// <returns>
    /// The analysis. For fewer than two members the result is
    /// <c>(false, 0, 0)</c>: a single branch is reported as <b>not</b>
    /// sequential, since there is nothing it could have overlapped with, and the
    /// durations are zeroed rather than measured. Do not read that zero as
    /// "instant".
    /// </returns>
    /// <remarks>
    /// Cost is O(n²) in the number of members — fine for the handful of branches
    /// a fork usually has, worth avoiding on a very wide group in a hot path.
    /// </remarks>
    /// <example>
    /// <code>
    /// var result = SequentialAsyncDetector.Analyze(segment.Nodes);
    /// if (result.IsSequential &amp;&amp; result.TotalMs > result.ParallelizableMs)
    /// {
    ///     Warn($"awaited in sequence: {result.TotalMs} ms, "
    ///        + $"~{result.ParallelizableMs} ms if run concurrently");
    /// }
    /// </code>
    /// </example>
    public static SequentialAsyncResult Analyze(
        IReadOnlyList<TraceNode> members)
    {
        if (members.Count < 2)
        {
            return new SequentialAsyncResult(
                false, 0, 0);
        }

        var isSequential = AreNonOverlapping(members);
        var totalMs = SumDurationsMs(members);
        var parallelizableMs = MaxDurationMs(members);
        return new SequentialAsyncResult(
            isSequential, totalMs, parallelizableMs);
    }

    private static bool AreNonOverlapping(
        IReadOnlyList<TraceNode> members)
    {
        for (var i = 0; i < members.Count; i++)
        {
            for (var j = i + 1; j < members.Count; j++)
            {
                if (Overlaps(members[i], members[j]))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool Overlaps(
        TraceNode a, TraceNode b)
    {
        var aEnd = a.StartTimestamp + a.DurationTicks;
        var bEnd = b.StartTimestamp + b.DurationTicks;
        return a.StartTimestamp < bEnd
            && b.StartTimestamp < aEnd;
    }

    private static long SumDurationsMs(
        IReadOnlyList<TraceNode> members)
    {
        long sum = 0;
        for (var i = 0; i < members.Count; i++)
        {
            sum += members[i].DurationTicks
                / TimeSpan.TicksPerMillisecond;
        }

        return sum;
    }

    private static long MaxDurationMs(
        IReadOnlyList<TraceNode> members)
    {
        long max = 0;
        for (var i = 0; i < members.Count; i++)
        {
            var ms = members[i].DurationTicks
                / TimeSpan.TicksPerMillisecond;
            if (ms > max)
            {
                max = ms;
            }
        }

        return max;
    }
}

/// <summary>
/// The outcome of a <see cref="SequentialAsyncDetector.Analyze"/> pass: whether
/// the branches overlapped, and the time at stake if they did not.
/// </summary>
/// <param name="IsSequential">
/// <see langword="true"/> when no two branches overlapped in time, i.e. the
/// group ran one branch at a time. Also <see langword="false"/> for a group of
/// fewer than two members — see <see cref="SequentialAsyncDetector.Analyze"/>.
/// </param>
/// <param name="TotalMs">
/// The summed duration of every branch: roughly the wall-clock cost when the
/// group ran sequentially. Truncated to whole milliseconds, so a group of
/// sub-millisecond branches reports 0.
/// </param>
/// <param name="ParallelizableMs">
/// The single longest branch: the wall-clock cost the group would approach if
/// the branches ran concurrently. The difference from <paramref name="TotalMs"/>
/// is the time potentially recoverable — meaningful only when
/// <paramref name="IsSequential"/> is <see langword="true"/>, since an already
/// concurrent group has nothing to recover. Also truncated to whole
/// milliseconds.
/// </param>
public sealed record SequentialAsyncResult(
    bool IsSequential,
    long TotalMs,
    long ParallelizableMs);
