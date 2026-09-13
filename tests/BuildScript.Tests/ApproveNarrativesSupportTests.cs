// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Build;
using Xunit;

namespace Build.Tests;

/// <summary>Covers the file-promotion logic behind the <c>Approve</c> build target.</summary>
public sealed class ApproveNarrativesSupportTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("nt-approve").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void Missing_root_promotes_nothing()
    {
        var promoted = ApproveNarrativesSupport.PromoteReceived(
            Path.Combine(_root, "does-not-exist"));

        Assert.Empty(promoted);
    }

    [Fact]
    public void No_received_traces_promotes_nothing()
    {
        var promoted = ApproveNarrativesSupport.PromoteReceived(_root);

        Assert.Empty(promoted);
    }

    [Fact]
    public void A_received_trace_is_moved_onto_its_approved_sibling()
    {
        var dir = Path.Combine(_root, "narratives", "OrderTests");
        Directory.CreateDirectory(dir);
        var received = Path.Combine(dir, "places_order.received.nt");
        File.WriteAllText(received, "scenario: Places an order\n\n- Svc.PlacesOrder()\n");

        var promoted = ApproveNarrativesSupport.PromoteReceived(_root);

        var approved = Path.Combine(dir, "places_order.approved.nt");
        Assert.Equal([approved], promoted);
        Assert.True(File.Exists(approved));
        Assert.False(File.Exists(received));
    }

    [Fact]
    public void An_existing_approved_trace_is_overwritten()
    {
        var dir = Path.Combine(_root, "narratives", "OrderTests");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "places_order.approved.nt"), "old");
        File.WriteAllText(Path.Combine(dir, "places_order.received.nt"), "new");

        ApproveNarrativesSupport.PromoteReceived(_root);

        Assert.Equal("new", File.ReadAllText(Path.Combine(dir, "places_order.approved.nt")));
    }

    [Fact]
    public void Every_received_trace_under_the_root_is_promoted_regardless_of_nesting()
    {
        var first = Path.Combine(_root, "a", "one.received.nt");
        var second = Path.Combine(_root, "b", "c", "two.received.nt");
        Directory.CreateDirectory(Path.GetDirectoryName(first)!);
        Directory.CreateDirectory(Path.GetDirectoryName(second)!);
        File.WriteAllText(first, "one");
        File.WriteAllText(second, "two");

        var promoted = ApproveNarrativesSupport.PromoteReceived(_root);

        Assert.Equal(2, promoted.Count);
        Assert.True(File.Exists(Path.Combine(_root, "a", "one.approved.nt")));
        Assert.True(File.Exists(Path.Combine(_root, "b", "c", "two.approved.nt")));
    }
}
