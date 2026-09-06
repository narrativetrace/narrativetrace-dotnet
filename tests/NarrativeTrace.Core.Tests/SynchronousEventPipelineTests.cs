// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using NarrativeTrace.Runtime;
using Xunit;

namespace NarrativeTrace.Core.Tests;

public class SynchronousEventPipelineTests
{
    private static readonly MethodSignature Signature =
        new("Svc", "Run", []);

    [Fact]
    public void Publish_retains_events_in_order()
    {
        using var pipeline = new SynchronousEventPipeline();

        pipeline.Publish(TestEvents.Enter(0, -1, 10, Signature));
        pipeline.Publish(TestEvents.Enter(1, 0, 20, Signature));

        var events = pipeline.Events();
        Assert.Equal(2, events.Count);
        Assert.Equal(10, events[0].TimestampTicks);
        Assert.Equal(20, events[1].TimestampTicks);
    }

    [Fact]
    public void Seed_constructor_copies_events()
    {
        var seed = new TraceEvent[]
        {
            TestEvents.Enter(0, -1, 10, Signature),
        };

        using var pipeline = new SynchronousEventPipeline(seed);

        Assert.Single(pipeline.Events());
    }

    [Fact]
    public void Clear_removes_all_retained_events()
    {
        using var pipeline = new SynchronousEventPipeline();
        pipeline.Publish(TestEvents.Enter(0, -1, 10, Signature));

        pipeline.Clear();

        Assert.Empty(pipeline.Events());
    }

    [Fact]
    public void Flush_is_a_safe_no_op()
    {
        using var pipeline = new SynchronousEventPipeline();
        pipeline.Publish(TestEvents.Enter(0, -1, 10, Signature));

        pipeline.Flush();

        Assert.Single(pipeline.Events());
    }
}
