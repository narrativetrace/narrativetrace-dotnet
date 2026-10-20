// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Diagnostics;
using NarrativeTrace.Core;
using NarrativeTrace.Runtime;
using Xunit;

namespace NarrativeTrace.Observability.Tests;

/// <summary>
/// Proves the live listener sees a fork group's branch under the span that launched it, not as a
/// root: it follows parent span ids, so the id has to be right on the branch's first event.
/// </summary>
public sealed class ForkedBranchLiveExportTests : IDisposable
{
    private readonly ActivitySource _source = new("NarrativeTrace.ForkedBranchLiveExportTests");
    private readonly ActivityListener _listener;
    private readonly List<Activity> _stopped = [];

    public ForkedBranchLiveExportTests()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = s => s == _source,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = a => { lock (_stopped) { _stopped.Add(a); } },
        };
        ActivitySource.AddActivityListener(_listener);
    }

    public void Dispose()
    {
        _listener.Dispose();
        _source.Dispose();
    }

    [Fact]
    public async Task A_joined_branch_is_exported_as_a_child_of_the_launching_span()
    {
        var live = new OtelTraceEventListener(_source);
        var context = new SyncNarrativeContext(
            new NarrativeTraceConfig(), new DualPathPipeline(live.OnEvent));
        var launching = context.EnterMethod("Checkout", "PlaceOrder", []);
        var group = ForkJoinGroup.Create(context);
        group.Fork(branch =>
        {
            branch.EnterMethod("Inventory", "Reserve", []);
            branch.ExitMethodWithReturn(null);
            return 1;
        });
        await group.JoinAsync();
        context.ExitMethodWithReturn(null, launching);

        var root = Assert.Single(_stopped, a => a.DisplayName == "Checkout.PlaceOrder");
        var reserve = Assert.Single(_stopped, a => a.DisplayName == "Inventory.Reserve");
        Assert.Equal(root.SpanId, reserve.ParentSpanId);
    }
}
