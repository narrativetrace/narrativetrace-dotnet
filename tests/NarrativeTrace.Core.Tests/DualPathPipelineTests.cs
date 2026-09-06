// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using NarrativeTrace.Runtime;
using Xunit;

namespace NarrativeTrace.Core.Tests;

public class DualPathPipelineTests
{
    private static EnterEvent Event() => new(
        new SpanContext(
            new TraceId("0af7651916cd43dd8448eb211c80319c"),
            new SpanId("b7ad6b7169203331"),
            null),
        0L,
        new MethodSignature("Svc", "Run", []));

    [Fact]
    public void Publish_fans_out_to_both_paths()
    {
        var received = new List<TraceEvent>();
        using var consumer =
            new BufferedEventConsumer(16, startConsumer: false);
        var pipeline = new DualPathPipeline(received.Add, consumer);

        pipeline.Publish(Event());
        consumer.Flush();

        Assert.Single(received);
        Assert.Single(consumer.Events());
    }

    [Fact]
    public void Throwing_synchronous_listener_is_isolated()
    {
        using var consumer =
            new BufferedEventConsumer(16, startConsumer: false);
        var pipeline = new DualPathPipeline(
            _ => throw new InvalidOperationException("boom"), consumer);

        pipeline.Publish(Event());
        consumer.Flush();

        // Listener threw but the exception was swallowed and the best-effort
        // path still received the event.
        Assert.Single(consumer.Events());
    }

    [Fact]
    public void Throwing_best_effort_consumer_does_not_propagate()
    {
        var received = new List<TraceEvent>();
        Action<TraceEvent> failing = _ =>
            throw new InvalidOperationException("best-effort path blew up");
        var pipeline = new DualPathPipeline(received.Add, failing);
        var evt = Event();

        pipeline.Publish(evt);

        Assert.Single(received);
        Assert.Same(evt, received[0]);
    }

    [Fact]
    public void Action_best_effort_path_exposes_no_store()
    {
        var pipeline = new DualPathPipeline(_ => { }, _ => { });

        pipeline.Publish(Event());
        pipeline.Flush();

        Assert.False(pipeline.HasStore);
        Assert.Empty(pipeline.Events());
    }

    [Fact]
    public void Has_store_is_true_only_with_a_consumer()
    {
        using var consumer =
            new BufferedEventConsumer(16, startConsumer: false);

        Assert.True(new DualPathPipeline(null, consumer).HasStore);
        Assert.False(new DualPathPipeline(_ => { }).HasStore);
    }

    [Fact]
    public void Store_operations_no_op_without_a_consumer()
    {
        var pipeline = new DualPathPipeline(_ => { });

        pipeline.Publish(Event());
        pipeline.Flush();
        pipeline.Clear();

        Assert.Empty(pipeline.Events());
    }

    [Fact]
    public void Events_and_clear_delegate_to_the_consumer()
    {
        using var consumer =
            new BufferedEventConsumer(16, startConsumer: false);
        var pipeline = new DualPathPipeline(null, consumer);

        pipeline.Publish(Event());
        pipeline.Flush();
        Assert.Single(pipeline.Events());

        pipeline.Clear();
        Assert.Empty(pipeline.Events());
    }
}
