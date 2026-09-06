// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using NarrativeTrace.Runtime;
using Xunit;

namespace NarrativeTrace.Core.Tests;

/// <summary>
/// The one loss surface: what it counts, how it reads, and the rule that a
/// clean run says nothing at all.
/// </summary>
public class TraceLossTests
{
    [Fact]
    public void A_run_that_lost_nothing_is_lossless_and_describes_nothing()
    {
        Assert.True(TraceLoss.None.IsLossless);
        Assert.Null(TraceLoss.None.Describe());
    }

    [Theory]
    [InlineData(1, 0, 0)]
    [InlineData(0, 1, 1)]
    [InlineData(0, 0, 1)]
    public void Any_loss_at_all_stops_it_being_lossless(
        long dropped, long scopes, long spans)
    {
        var loss = new TraceLoss(dropped, scopes, spans);

        Assert.False(loss.IsLossless);
        Assert.NotNull(loss.Describe());
    }

    [Fact]
    public void Dropped_events_alone_read_as_a_buffer_problem()
    {
        var loss = new TraceLoss(1204, 0, 0);

        Assert.Equal(
            "Incomplete: 1,204 events dropped (buffer full)", loss.Describe());
    }

    [Fact]
    public void Refusals_alone_read_as_an_adoption_cap_problem()
    {
        var loss = new TraceLoss(0, 3, 4100);

        Assert.Equal(
            "Incomplete: 3 async scopes not adopted (cap, 4,100 spans)",
            loss.Describe());
    }

    [Fact]
    public void Both_halves_are_named_in_one_line()
    {
        var loss = new TraceLoss(1204, 3, 4100);

        Assert.Equal(
            "Incomplete: 1,204 events dropped (buffer full) · "
            + "3 async scopes not adopted (cap, 4,100 spans)",
            loss.Describe());
    }

    [Fact]
    public void Grouping_is_culture_invariant()
    {
        using var culture = new CultureScope("de-DE");

        var loss = new TraceLoss(1204, 0, 0);

        Assert.Contains("1,204", loss.Describe(), StringComparison.Ordinal);
    }

    [Fact]
    public void A_context_that_lost_nothing_reports_none()
    {
        var context = new SyncNarrativeContext(new NarrativeTraceConfig());
        context.EnterMethod("Svc", "Run", []);
        context.ExitMethodWithReturn("true");

        Assert.Equal(TraceLoss.None, ((ITraceLossSource)context).TraceLoss);
    }

    [Fact]
    public void A_scoped_context_forwards_the_active_scopes_loss()
    {
        var context = new AsyncNarrativeContext(new NarrativeTraceConfig());

        Assert.Equal(TraceLoss.None, context.TraceLoss);

        context.Run(() =>
        {
            context.EnterMethod("Svc", "Run", []);
            context.ExitMethodWithReturn("true");
            Assert.Equal(TraceLoss.None, context.TraceLoss);
        });
    }

    [Fact]
    public void A_buffered_sinks_drops_are_counted_as_lost_events()
    {
        using var sink = new CountingLossPipeline(dropped: 7);
        var context = new SyncNarrativeContext(
            new NarrativeTraceConfig(), sink);

        Assert.Equal(7, ((ITraceLossSource)context).TraceLoss.DroppedEvents);
    }

    /// <summary>A sink that reports a fixed number of shed events.</summary>
    private sealed class CountingLossPipeline
        : IEventPipeline, IEventLossCounter
    {
        internal CountingLossPipeline(long dropped)
        {
            DroppedEventCount = dropped;
        }

        public long DroppedEventCount { get; }

        public void Publish(TraceEvent traceEvent) { }

        public void Flush() { }

        public IReadOnlyList<TraceEvent> Events() => [];

        public void Clear() { }

        public void Dispose() { }
    }
}
