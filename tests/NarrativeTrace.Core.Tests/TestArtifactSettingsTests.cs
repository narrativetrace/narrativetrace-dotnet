// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using Xunit;

namespace NarrativeTrace.Core.Tests;

public class TestArtifactSettingsTests
{
    [Fact]
    public void Defaults_to_disabled_markdown_in_default_directory()
    {
        var settings = TestArtifactSettings.Resolve(_ => null);

        Assert.False(settings.Enabled);
        Assert.Equal(
            TestArtifactSettings.DefaultDirectory, settings.Directory);
        Assert.Equal(TraceArtifactFormat.Markdown, settings.Format);
    }

    [Fact]
    public void Reads_output_gate_directory_and_format()
    {
        var env = new Dictionary<string, string?>
        {
            [ConfigResolver.OutputKey] = "true",
            [ConfigResolver.OutputDirKey] = "artifacts/traces",
            [ConfigResolver.FormatKey] = "mermaid",
        };

        var settings = TestArtifactSettings.Resolve(
            key => env.TryGetValue(key, out var value) ? value : null);

        Assert.True(settings.Enabled);
        Assert.Equal("artifacts/traces", settings.Directory);
        Assert.Equal(TraceArtifactFormat.Mermaid, settings.Format);
    }

    [Fact]
    public void The_entry_arrays_are_off_by_default()
    {
        var settings = TestArtifactSettings.Resolve(_ => null);

        Assert.False(settings.EntryArtifacts.Canonical);
        Assert.False(settings.EntryArtifacts.Structural);
        Assert.False(settings.EntryArtifacts.Any);
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("1", true)]
    [InlineData("TRUE", true)]
    [InlineData("yes", false)]
    [InlineData("", false)]
    public void The_canonical_entry_array_reads_its_own_switch(
        string value, bool expected)
    {
        var settings = TestArtifactSettings.Resolve(
            key => key == ConfigResolver.CanonicalJsonKey ? value : null);

        Assert.Equal(expected, settings.EntryArtifacts.Canonical);
        Assert.False(settings.EntryArtifacts.Structural);
    }

    [Fact]
    public void The_structural_entry_array_reads_its_own_switch()
    {
        var settings = TestArtifactSettings.Resolve(
            key => key == ConfigResolver.StructuralJsonKey ? "true" : null);

        Assert.True(settings.EntryArtifacts.Structural);
        Assert.False(settings.EntryArtifacts.Canonical);
        Assert.True(settings.EntryArtifacts.Any);
    }

    [Fact]
    public void Unrecognized_format_degrades_to_markdown()
    {
        var settings = TestArtifactSettings.Resolve(
            key => key == ConfigResolver.FormatKey ? "garbage" : null);

        Assert.Equal(TraceArtifactFormat.Markdown, settings.Format);
    }

    [Fact]
    public void Blank_directory_falls_back_to_default()
    {
        var settings = TestArtifactSettings.Resolve(
            key => key == ConfigResolver.OutputDirKey ? "   " : null);

        Assert.Equal(
            TestArtifactSettings.DefaultDirectory, settings.Directory);
    }
}
