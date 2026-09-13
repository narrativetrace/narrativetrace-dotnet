// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Cli.Doctor.Checks;
using Xunit;

namespace NarrativeTrace.Cli.Tests.Doctor.Checks;

public sealed class TrapLlmsBeforeYouStartCheckTests
{
    [Fact]
    public void Passes_when_no_packages_are_installed()
    {
        var finding = TrapLlmsBeforeYouStartCheck.Run(DoctorSnapshotFixtures.Empty());

        Assert.True(finding.Passed);
    }

    [Fact]
    public void Passes_when_installed_packages_are_0_1_4_or_newer()
    {
        var snapshot = DoctorSnapshotFixtures.WithPackages(("NarrativeTrace.Core", "0.1.4"));

        var finding = TrapLlmsBeforeYouStartCheck.Run(snapshot);

        Assert.True(finding.Passed);
    }

    [Fact]
    public void Fails_when_an_installed_package_predates_0_1_4()
    {
        var snapshot = DoctorSnapshotFixtures.WithPackages(("NarrativeTrace.Core", "0.1.3"));

        var finding = TrapLlmsBeforeYouStartCheck.Run(snapshot);

        Assert.False(finding.Passed);
        Assert.Contains("NarrativeTrace.Core@0.1.3", finding.Message, StringComparison.Ordinal);
    }
}
