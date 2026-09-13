// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Cli.Doctor.Checks;
using Xunit;

namespace NarrativeTrace.Cli.Tests.Doctor.Checks;

public sealed class ConfigRegistrationPairCheckTests
{
    [Fact]
    public void Passes_when_not_used()
    {
        var finding = ConfigRegistrationPairCheck.Run(DoctorSnapshotFixtures.Empty());

        Assert.True(finding.Passed);
    }

    [Fact]
    public void Passes_when_both_add_and_use_are_present()
    {
        var snapshot = DoctorSnapshotFixtures.WithSource(
            ("Startup.cs", "services.AddNarrativeTrace(configuration); app.UseNarrativeTrace();"));

        var finding = ConfigRegistrationPairCheck.Run(snapshot);

        Assert.True(finding.Passed);
    }

    [Fact]
    public void Passes_when_middleware_is_registered_via_UseMiddleware()
    {
        var snapshot = DoctorSnapshotFixtures.WithSource(
            ("Startup.cs",
             "services.AddNarrativeTrace(configuration); app.UseMiddleware<NarrativeTraceMiddleware>();"));

        var finding = ConfigRegistrationPairCheck.Run(snapshot);

        Assert.True(finding.Passed);
    }

    [Fact]
    public void Fails_when_add_is_called_without_use()
    {
        var snapshot = DoctorSnapshotFixtures.WithSource(
            ("Startup.cs", "services.AddNarrativeTrace(configuration);"));

        var finding = ConfigRegistrationPairCheck.Run(snapshot);

        Assert.False(finding.Passed);
        Assert.Contains("UseNarrativeTrace", finding.Fix, StringComparison.Ordinal);
    }
}
