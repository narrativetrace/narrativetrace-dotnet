// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Cli.Doctor.Checks;
using Xunit;

namespace NarrativeTrace.Cli.Tests.Doctor.Checks;

public sealed class ToolchainTargetFrameworkCheckTests
{
    [Fact]
    public void Passes_when_legacy_is_not_referenced()
    {
        var finding = ToolchainTargetFrameworkCheck.Run(DoctorSnapshotFixtures.Empty());

        Assert.True(finding.Passed);
        Assert.Equal(string.Empty, finding.Fix);
    }

    [Fact]
    public void Passes_when_legacy_project_targets_net48()
    {
        var snapshot = DoctorSnapshotFixtures.WithProject(
            "<Project><PropertyGroup><TargetFramework>net48</TargetFramework></PropertyGroup>" +
            "<ItemGroup><PackageReference Include=\"NarrativeTrace.Legacy\" /></ItemGroup></Project>");

        var finding = ToolchainTargetFrameworkCheck.Run(snapshot);

        Assert.True(finding.Passed);
    }

    [Fact]
    public void Fails_when_legacy_project_targets_net10()
    {
        var snapshot = DoctorSnapshotFixtures.WithProject(
            "<Project><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>" +
            "<ItemGroup><PackageReference Include=\"NarrativeTrace.Legacy\" /></ItemGroup></Project>");

        var finding = ToolchainTargetFrameworkCheck.Run(snapshot);

        Assert.False(finding.Passed);
        Assert.NotEqual(string.Empty, finding.Fix);
        Assert.Contains("net48", finding.Fix, StringComparison.Ordinal);
    }
}
