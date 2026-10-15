// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Tooling.Platforms;
using Xunit;

namespace NarrativeTrace.Tooling.Tests.Platforms;

/// <summary>A row cannot be built half-empty, and a pointer cannot leave the project.</summary>
public sealed class PlatformRowContractTests
{
    private const string Date = "2026-10-10";

    private static PlatformRow Row(
        string id = "some-platform", string name = "Name", string[]? markers = null, Pointer? pointer = null,
        string install = "install", string finds = "finds", string lane = "manual", string verified = Date) =>
        new(id, name, markers ?? [".some"], pointer ?? new Pointer.None(), install, finds, lane, verified);

    [Fact]
    public void A_well_formed_row_is_built()
    {
        Assert.Equal("some-platform", Row().Id);
    }

    [Theory]
    [InlineData("Some_Platform")]
    [InlineData("")]
    [InlineData("-x")]
    public void A_row_id_must_be_kebab_case(string id)
    {
        var error = Assert.Throws<ArgumentException>(() => Row(id: id));

        Assert.Contains("kebab-case", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_row_with_no_marker_path_could_never_be_detected()
    {
        var error = Assert.Throws<ArgumentException>(() => Row(markers: []));

        Assert.Contains("no marker path", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("last week")]
    [InlineData("2026-1-1")]
    [InlineData("")]
    public void A_verification_date_must_be_a_date(string verified)
    {
        var error = Assert.Throws<ArgumentException>(() => Row(verified: verified));

        Assert.Contains("yyyy-mm-dd", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Every_text_column_must_be_filled()
    {
        Assert.Contains("name", Assert.Throws<ArgumentException>(() => Row(name: " ")).Message, StringComparison.Ordinal);
        Assert.Contains("install path", Assert.Throws<ArgumentException>(() => Row(install: "")).Message, StringComparison.Ordinal);
        Assert.Contains("agent-finds", Assert.Throws<ArgumentException>(() => Row(finds: "")).Message, StringComparison.Ordinal);
        Assert.Contains("lane", Assert.Throws<ArgumentException>(() => Row(lane: "Not A Lane")).Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/etc/replit.md")]
    [InlineData("../replit.md")]
    [InlineData("a/../../x")]
    [InlineData(" ")]
    public void A_pointer_path_stays_inside_the_project(string path)
    {
        Assert.Throws<ArgumentException>(() => new Pointer.ManagedSection(path));
        Assert.Throws<ArgumentException>(() => new Pointer.Rule(path, "text"));
    }

    [Fact]
    public void A_rule_pointer_carries_text()
    {
        Assert.Throws<ArgumentException>(() => new Pointer.Rule(".cursor/rules/x.mdc", " "));
    }

    [Fact]
    public void The_none_pointer_points_at_no_file()
    {
        Assert.Equal(string.Empty, new Pointer.None().Path);
    }
}
