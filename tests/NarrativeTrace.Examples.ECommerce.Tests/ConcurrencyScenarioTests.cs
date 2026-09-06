// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Text.Json;
using NarrativeTrace.Core;
using NarrativeTrace.Examples.ECommerce;
using NarrativeTrace.Runtime;
using Xunit;

namespace NarrativeTrace.Examples.ECommerce.Tests;

public sealed class ConcurrencyScenarioTests
{
    private static SyncNarrativeContext NewContext()
    {
        return new SyncNarrativeContext(new NarrativeTraceConfig(TracingLevel.Detail));
    }

    [Fact]
    public async Task Forked_pricing_and_inventory_children_share_one_fork_group_id()
    {
        var context = NewContext();

        var result = await ConcurrencyScenario.RunAsync(context);

        var forked = result.Trace.Roots[0].Children
            .Where(c => c.Concurrency is { Kind: ConcurrencyKind.ForkJoin })
            .ToList();
        Assert.Equal(2, forked.Count);
        Assert.All(forked, c =>
            Assert.Equal(result.ForkGroupId, c.Concurrency!.GroupId));
    }

    [Fact]
    public async Task Fire_and_forget_notification_records_a_launcher_node()
    {
        var context = NewContext();

        var result = await ConcurrencyScenario.RunAsync(context);

        var launcher = Assert.Single(result.Trace.Roots[0].Children
, c => c.Concurrency is { Kind: ConcurrencyKind.FireAndForget });
        Assert.Equal("fire-and-forget", launcher.Signature.MethodName);
        Assert.Equal(result.NotificationGroupId, launcher.Concurrency!.GroupId);
    }

    [Fact]
    public async Task Json_export_of_the_scenario_carries_concurrency_fields()
    {
        var context = NewContext();

        var result = await ConcurrencyScenario.RunAsync(context);

        var json = JsonExporter.Export(
            result.Trace, new TraceMetadata("Concurrency", ScenarioResult.Success));
        using var doc = JsonDocument.Parse(json);
        Assert.Contains("\"concurrency\"", json, StringComparison.Ordinal);
        Assert.Contains(result.ForkGroupId, json, StringComparison.Ordinal);
        Assert.Contains(result.NotificationGroupId, json, StringComparison.Ordinal);
    }
}
