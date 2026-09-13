// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Cli.Doctor.Checks;
using Xunit;

namespace NarrativeTrace.Cli.Tests.Doctor.Checks;

public sealed class ToolchainPackageVersionsAlignedCheckTests
{
    [Fact]
    public void Passes_when_fewer_than_two_narrativetrace_packages_installed()
    {
        var snapshot = DoctorSnapshotFixtures.WithPackages(("NarrativeTrace.Core", "0.1.4"));

        var finding = ToolchainPackageVersionsAlignedCheck.Run(snapshot);

        Assert.True(finding.Passed);
    }

    [Fact]
    public void Passes_when_all_installed_versions_match()
    {
        var snapshot = DoctorSnapshotFixtures.WithPackages(
            ("NarrativeTrace.Core", "0.1.4"), ("NarrativeTrace.Runtime", "0.1.4"));

        var finding = ToolchainPackageVersionsAlignedCheck.Run(snapshot);

        Assert.True(finding.Passed);
    }

    [Fact]
    public void Fails_when_installed_versions_drift()
    {
        var snapshot = DoctorSnapshotFixtures.WithPackages(
            ("NarrativeTrace.Core", "0.1.3"), ("NarrativeTrace.Runtime", "0.1.4"));

        var finding = ToolchainPackageVersionsAlignedCheck.Run(snapshot);

        Assert.False(finding.Passed);
        Assert.Contains("NarrativeTrace.Core@0.1.3", finding.Message, StringComparison.Ordinal);
        Assert.Contains("NarrativeTrace.Runtime@0.1.4", finding.Message, StringComparison.Ordinal);
    }
}
