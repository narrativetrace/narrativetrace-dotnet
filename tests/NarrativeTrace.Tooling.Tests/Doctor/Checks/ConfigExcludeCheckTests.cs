// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Tooling.Doctor;
using NarrativeTrace.Tooling.Doctor.Checks;
using Xunit;

namespace NarrativeTrace.Tooling.Tests.Doctor.Checks;

public sealed class ConfigExcludeCheckTests
{
    [Fact]
    public void Unset_passes_and_names_the_shipped_default()
    {
        var finding = ConfigExcludeCheck.Run(DoctorSnapshotFixtures.Empty());

        Assert.True(finding.Passed);
        Assert.Contains("shipped default", finding.Message, StringComparison.Ordinal);
        Assert.Contains("Microsoft.Extensions", finding.Message, StringComparison.Ordinal);
        Assert.Contains("Moq", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Set_passes_and_names_the_effective_list_as_replacing_the_default()
    {
        var finding = ConfigExcludeCheck.Run(WithExclude("Acme.Legacy; Acme.Infra.*"));

        Assert.True(finding.Passed);
        Assert.Contains("replaces the shipped default", finding.Message, StringComparison.Ordinal);
        Assert.Contains("Acme.Legacy", finding.Message, StringComparison.Ordinal);
        Assert.Contains("Acme.Infra", finding.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Moq", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Set_but_empty_passes_and_says_nothing_is_excluded()
    {
        var finding = ConfigExcludeCheck.Run(WithExclude(""));

        Assert.True(finding.Passed);
        Assert.Contains("nothing is excluded", finding.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Acme.A,Acme.B")]
    [InlineData("Acme/Legacy")]
    [InlineData("Acme Legacy")]
    public void A_prefix_that_can_match_no_namespace_fails_with_the_fix(string value)
    {
        var finding = ConfigExcludeCheck.Run(WithExclude(value));

        Assert.False(finding.Passed);
        Assert.Contains(";", finding.Fix, StringComparison.Ordinal);
    }

    [Fact]
    public void The_shipped_default_is_the_one_the_library_ships()
    {
        Assert.Equal(
            NarrativeTrace.DependencyInjection.InstrumentationExclusions.DefaultExclude,
            ConfigExcludeCheck.ShippedDefault);
    }

    private static DoctorSnapshot WithExclude(string value)
    {
        return DoctorSnapshotFixtures.Empty() with
        {
            Env = new Dictionary<string, string?> { ["NARRATIVETRACE_EXCLUDE"] = value },
        };
    }
}
