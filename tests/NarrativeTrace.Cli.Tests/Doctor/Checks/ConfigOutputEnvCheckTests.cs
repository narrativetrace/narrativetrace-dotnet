// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Cli.Doctor;
using NarrativeTrace.Cli.Doctor.Checks;
using Xunit;

namespace NarrativeTrace.Cli.Tests.Doctor.Checks;

public sealed class ConfigOutputEnvCheckTests
{
    [Fact]
    public void Passes_when_unset()
    {
        var finding = ConfigOutputEnvCheck.Run(DoctorSnapshotFixtures.Empty());

        Assert.True(finding.Passed);
    }

    [Theory]
    [InlineData("true")]
    [InlineData("false")]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("TRUE")]
    public void Passes_for_recognized_values(string value)
    {
        var snapshot = WithOutputEnv(value);

        var finding = ConfigOutputEnvCheck.Run(snapshot);

        Assert.True(finding.Passed);
    }

    [Fact]
    public void Fails_for_an_unrecognized_value()
    {
        var snapshot = WithOutputEnv("no");

        var finding = ConfigOutputEnvCheck.Run(snapshot);

        Assert.False(finding.Passed);
        Assert.Contains("no", finding.Message, StringComparison.Ordinal);
    }

    private static DoctorSnapshot WithOutputEnv(string value)
    {
        return DoctorSnapshotFixtures.Empty() with
        {
            Env = new Dictionary<string, string?> { ["NARRATIVETRACE_OUTPUT"] = value },
        };
    }
}
