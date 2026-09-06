// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using Xunit;

namespace NarrativeTrace.Core.Tests;

public class SequentialAsyncDetectorTests
{
    [Fact]
    public void Analyze_returns_false_for_overlapping_tasks()
    {
        // Task A: start=100, duration=200 (ends 300)
        // Task B: start=150, duration=200 (ends 350)
        // Overlapping → not sequential
        var nodes = new[]
        {
            MakeNode(100, 200),
            MakeNode(150, 200),
        };

        var result =
            SequentialAsyncDetector.Analyze(nodes);

        Assert.False(result.IsSequential);
    }

    [Fact]
    public void Analyze_returns_true_for_non_overlapping_tasks()
    {
        // Task A: start=100, duration=200 (ends 300)
        // Task B: start=300, duration=200 (ends 500)
        // Non-overlapping → sequential
        var nodes = new[]
        {
            MakeNode(100, 200),
            MakeNode(300, 200),
        };

        var result =
            SequentialAsyncDetector.Analyze(nodes);

        Assert.True(result.IsSequential);
    }

    [Fact]
    public void Analyze_computes_totalMs_as_sum_of_durations()
    {
        var ms = TimeSpan.TicksPerMillisecond;
        var nodes = new[]
        {
            MakeNode(0, 100 * ms),
            MakeNode(100 * ms, 200 * ms),
        };

        var result =
            SequentialAsyncDetector.Analyze(nodes);

        Assert.Equal(300, result.TotalMs);
    }

    [Fact]
    public void Analyze_computes_parallelizableMs_as_max_duration()
    {
        var ms = TimeSpan.TicksPerMillisecond;
        var nodes = new[]
        {
            MakeNode(0, 100 * ms),
            MakeNode(100 * ms, 200 * ms),
        };

        var result =
            SequentialAsyncDetector.Analyze(nodes);

        Assert.Equal(200, result.ParallelizableMs);
    }

    [Fact]
    public void Analyze_returns_false_for_single_member()
    {
        var nodes = new[] { MakeNode(0, 100) };

        var result =
            SequentialAsyncDetector.Analyze(nodes);

        Assert.False(result.IsSequential);
    }

    [Fact]
    public void Analyze_returns_false_for_empty_list()
    {
        var result = SequentialAsyncDetector.Analyze(
            Array.Empty<TraceNode>());

        Assert.False(result.IsSequential);
    }

    private static TraceNode MakeNode(
        long startTimestamp, long durationTicks)
    {
        return new TraceNode(
            new MethodSignature("Svc", "Do", []),
            new Returned(null), [],
            durationTicks, startTimestamp);
    }
}
