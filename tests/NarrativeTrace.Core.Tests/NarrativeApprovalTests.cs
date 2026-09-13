// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using Xunit;

namespace NarrativeTrace.Core.Tests;

public sealed class NarrativeApprovalTests : IDisposable
{
    private readonly string _dir = Path.Combine(
        Path.GetTempPath(), "nt-approval-" + Guid.NewGuid().ToString("N"));

    private static TraceTree TreeWithNode(string method = "PlacesOrder")
    {
        return new TraceTree([
            new TraceNode(
                new MethodSignature("Svc", method, []), new Returned(null), [], 0),
        ]);
    }

    [Fact]
    public void ApprovedFile_uses_the_same_class_directory_and_slug_rules_as_every_other_artifact()
    {
        var identity = ArtifactIdentity.OfMethod("Foo.OrderTests", "PlacesOrder");

        var file = NarrativeApproval.ApprovedFile(_dir, identity);

        Assert.Equal(
            Path.Combine(_dir, "OrderTests", "places_order.approved.nt"), file);
    }

    [Fact]
    public void ApprovedFile_three_argument_overload_resolves_via_ArtifactIdentity_OfMethod()
    {
        var direct = NarrativeApproval.ApprovedFile(_dir, "Foo.OrderTests", "PlacesOrder");
        var viaIdentity = NarrativeApproval.ApprovedFile(
            _dir, ArtifactIdentity.OfMethod("Foo.OrderTests", "PlacesOrder"));

        Assert.Equal(viaIdentity, direct);
    }

    [Fact]
    public void ApprovedFile_of_an_invocation_carries_its_own_slug()
    {
        var identity = ArtifactIdentity.OfInvocation("EquipmentTests", "equipmentCanBeFound", 2, "find TENT");

        var file = NarrativeApproval.ApprovedFile(_dir, identity);

        Assert.Equal(
            Path.Combine(_dir, "EquipmentTests", "equipment_can_be_found-002-find_tent.approved.nt"),
            file);
    }

    /// <summary>
    /// No approved trace yet: the run's structure is written as the received
    /// trace for review, and the test fails with a message naming both.
    /// </summary>
    [Fact]
    public void Missing_approved_trace_writes_received_and_throws()
    {
        var approvedFile = Path.Combine(_dir, "OrderTests", "places_order.approved.nt");

        var exception = Assert.Throws<NarrativeApprovalException>(
            () => NarrativeApproval.Verify(TreeWithNode(), "Places an order", approvedFile));

        Assert.Contains("No approved trace", exception.Message, StringComparison.Ordinal);
        var receivedFile = Path.Combine(_dir, "OrderTests", "places_order.received.nt");
        Assert.True(File.Exists(receivedFile));
        Assert.Equal("scenario: Places an order\n\n- Svc.PlacesOrder()\n", File.ReadAllText(receivedFile));
    }

    [Fact]
    public void Matching_approved_trace_passes_and_cleans_up_a_stale_received_trace()
    {
        var approvedFile = Path.Combine(_dir, "OrderTests", "places_order.approved.nt");
        var receivedFile = Path.Combine(_dir, "OrderTests", "places_order.received.nt");
        Directory.CreateDirectory(Path.GetDirectoryName(approvedFile)!);
        File.WriteAllText(approvedFile, "scenario: Places an order\n\n- Svc.PlacesOrder()\n");
        File.WriteAllText(receivedFile, "stale");

        NarrativeApproval.Verify(TreeWithNode(), "Places an order", approvedFile);

        Assert.False(File.Exists(receivedFile));
    }

    [Fact]
    public void Structural_mismatch_writes_received_and_throws_with_the_readable_diff()
    {
        var approvedFile = Path.Combine(_dir, "OrderTests", "places_order.approved.nt");
        Directory.CreateDirectory(Path.GetDirectoryName(approvedFile)!);
        File.WriteAllText(approvedFile, "scenario: Places an order\n\n- Svc.PlacesOrder()\n- Svc.Ship()\n");

        var exception = Assert.Throws<NarrativeApprovalException>(
            () => NarrativeApproval.Verify(TreeWithNode(), "Places an order", approvedFile));

        Assert.Contains("Trace changed against the approved trace", exception.Message, StringComparison.Ordinal);
        Assert.Contains("-1 call Svc.Ship", exception.Message, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(_dir, "OrderTests", "places_order.received.nt")));
    }

    [Fact]
    public void PromoteReceived_moves_every_received_trace_onto_its_approved_trace()
    {
        var receivedFile = Path.Combine(_dir, "OrderTests", "places_order.received.nt");
        Directory.CreateDirectory(Path.GetDirectoryName(receivedFile)!);
        File.WriteAllText(receivedFile, "scenario: Places an order\n\n- Svc.PlacesOrder()\n");

        var promoted = NarrativeApproval.PromoteReceived(_dir);

        var approvedFile = Path.Combine(_dir, "OrderTests", "places_order.approved.nt");
        Assert.Equal([approvedFile], promoted);
        Assert.True(File.Exists(approvedFile));
        Assert.False(File.Exists(receivedFile));
    }

    [Fact]
    public void PromoteReceived_overwrites_an_existing_approved_trace()
    {
        var approvedFile = Path.Combine(_dir, "OrderTests", "places_order.approved.nt");
        var receivedFile = Path.Combine(_dir, "OrderTests", "places_order.received.nt");
        Directory.CreateDirectory(Path.GetDirectoryName(approvedFile)!);
        File.WriteAllText(approvedFile, "old");
        File.WriteAllText(receivedFile, "new");

        NarrativeApproval.PromoteReceived(_dir);

        Assert.Equal("new", File.ReadAllText(approvedFile));
    }

    [Fact]
    public void PromoteReceived_on_a_missing_root_promotes_nothing()
    {
        var promoted = NarrativeApproval.PromoteReceived(Path.Combine(_dir, "does-not-exist"));

        Assert.Empty(promoted);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }
}
