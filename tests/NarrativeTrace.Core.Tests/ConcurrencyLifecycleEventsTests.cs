// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using NarrativeTrace.Runtime;
using Xunit;

namespace NarrativeTrace.Core.Tests;

public class ConcurrencyLifecycleEventsTests
{
    [Fact]
    public void ForkJoinGroup_Create_publishes_fork_created_event()
    {
        using var sink = new SynchronousEventPipeline();
        var ctx = new SyncNarrativeContext(
            new NarrativeTraceConfig(), sink);

        var group = ForkJoinGroup.Create(ctx);

        var evt = Assert.Single(
            sink.Events().OfType<ForkCreatedEvent>());
        Assert.Equal(group.GroupId, evt.GroupId);
    }

    [Fact]
    public async Task JoinAsync_publishes_merge_event_with_member_count()
    {
        using var sink = new SynchronousEventPipeline();
        var ctx = new SyncNarrativeContext(
            new NarrativeTraceConfig(), sink);
        var group = ForkJoinGroup.Create(ctx);

        group.Fork(_ => 1);
        group.Fork(_ => 2);
        await group.JoinAsync();

        var evt = Assert.Single(
            sink.Events().OfType<MergeEvent>());
        Assert.Equal(group.GroupId, evt.GroupId);
        Assert.Equal(2, evt.MemberCount);
    }

    [Fact]
    public void FireAndForgetGroup_publishes_fire_and_forget_event()
    {
        using var sink = new SynchronousEventPipeline();
        var ctx = new SyncNarrativeContext(
            new NarrativeTraceConfig(), sink);

        var group = FireAndForgetGroup.Create(ctx);
        group.Launch(_ => { });

        var evt = Assert.Single(
            sink.Events().OfType<FireAndForgetEvent>());
        Assert.Equal(group.GroupId, evt.GroupId);
    }
}
