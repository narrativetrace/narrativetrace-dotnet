// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Cli.Doctor.Checks;
using Xunit;

namespace NarrativeTrace.Cli.Tests.Doctor.Checks;

public sealed class ToolchainCompanionPackagesCheckTests
{
    [Fact]
    public void Passes_when_proxy_is_not_installed()
    {
        var finding = ToolchainCompanionPackagesCheck.Run(DoctorSnapshotFixtures.Empty());

        Assert.True(finding.Passed);
    }

    [Fact]
    public void Passes_when_proxy_is_0_1_4_or_newer()
    {
        var snapshot = DoctorSnapshotFixtures.WithPackages(("NarrativeTrace.Proxy", "0.1.4"));

        var finding = ToolchainCompanionPackagesCheck.Run(snapshot);

        Assert.True(finding.Passed);
    }

    [Fact]
    public void Passes_when_pre_0_1_4_proxy_has_both_companions_installed_by_hand()
    {
        var snapshot = DoctorSnapshotFixtures.WithPackages(
            ("NarrativeTrace.Proxy", "0.1.3"),
            ("NarrativeTrace.Core", "0.1.3"),
            ("NarrativeTrace.Runtime", "0.1.3"));

        var finding = ToolchainCompanionPackagesCheck.Run(snapshot);

        Assert.True(finding.Passed);
    }

    [Fact]
    public void Fails_when_pre_0_1_4_proxy_is_missing_a_companion()
    {
        var snapshot = DoctorSnapshotFixtures.WithPackages(("NarrativeTrace.Proxy", "0.1.3"));

        var finding = ToolchainCompanionPackagesCheck.Run(snapshot);

        Assert.False(finding.Passed);
        Assert.Contains("NarrativeTrace.Core", finding.Message, StringComparison.Ordinal);
        Assert.Contains("NarrativeTrace.Runtime", finding.Message, StringComparison.Ordinal);
    }
}
