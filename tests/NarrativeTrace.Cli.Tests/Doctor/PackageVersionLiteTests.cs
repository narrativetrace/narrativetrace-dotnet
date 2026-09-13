// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Cli.Doctor;
using Xunit;

namespace NarrativeTrace.Cli.Tests.Doctor;

public sealed class PackageVersionLiteTests
{
    [Theory]
    [InlineData("0.1.3", "0.1.4", true)]
    [InlineData("0.1.4", "0.1.4", false)]
    [InlineData("0.1.5", "0.1.4", false)]
    [InlineData("0.0.9", "0.1.4", true)]
    [InlineData("1.0.0", "0.1.4", false)]
    public void Compares_major_minor_patch(string version, string boundary, bool expectedBelow)
    {
        Assert.Equal(expectedBelow, PackageVersionLite.IsBelow(version, boundary));
    }

    [Theory]
    [InlineData("not-a-version", "0.1.4")]
    [InlineData("0.1.4", "not-a-version")]
    public void Unparseable_input_is_never_reported_as_below(string version, string boundary)
    {
        Assert.False(PackageVersionLite.IsBelow(version, boundary));
    }

    [Fact]
    public void Prerelease_suffix_is_ignored()
    {
        Assert.True(PackageVersionLite.IsBelow("0.1.3-beta.1", "0.1.4"));
    }
}
