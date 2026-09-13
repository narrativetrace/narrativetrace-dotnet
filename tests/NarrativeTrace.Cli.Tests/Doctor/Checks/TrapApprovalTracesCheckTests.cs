// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Cli.Doctor;
using NarrativeTrace.Cli.Doctor.Checks;
using Xunit;

namespace NarrativeTrace.Cli.Tests.Doctor.Checks;

public sealed class TrapApprovalTracesCheckTests
{
    [Fact]
    public void Passes_when_nothing_is_configured_yet()
    {
        var finding = TrapApprovalTracesCheck.Run(DoctorSnapshotFixtures.Empty());

        Assert.True(finding.Passed);
    }

    [Fact]
    public void Passes_when_only_approved_traces_exist()
    {
        var snapshot = WithApprovedDir(("Order/place.approved.nt", "..."));

        var finding = TrapApprovalTracesCheck.Run(snapshot);

        Assert.True(finding.Passed);
    }

    [Fact]
    public void Fails_when_a_received_trace_is_pending()
    {
        var snapshot = WithApprovedDir(
            ("Order/place.approved.nt", "..."), ("Order/place.received.nt", "..."));

        var finding = TrapApprovalTracesCheck.Run(snapshot);

        Assert.False(finding.Passed);
        Assert.Contains("place.received.nt", finding.Message, StringComparison.Ordinal);
    }

    private static DoctorSnapshot WithApprovedDir(params (string Path, string Content)[] files)
    {
        return DoctorSnapshotFixtures.Empty() with
        {
            ApprovedDirFiles = files.ToDictionary(f => f.Path, f => f.Content),
        };
    }
}
