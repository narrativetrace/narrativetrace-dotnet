// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using Xunit;

namespace NarrativeTrace.Core.Tests;

public sealed class TraceArtifactFormatTests
{
    [Theory]
    [InlineData("text", TraceArtifactFormat.Text)]
    [InlineData("MERMAID", TraceArtifactFormat.Mermaid)]
    [InlineData("plantuml", TraceArtifactFormat.PlantUml)]
    [InlineData("markdown", TraceArtifactFormat.Markdown)]
    public void Parses_known_names_case_insensitively(
        string name, TraceArtifactFormat expected)
    {
        Assert.Equal(
            expected,
            TraceArtifactFormatExtensions.FromName(
                name, TraceArtifactFormat.Text));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("garbage")]
    public void Unknown_or_blank_degrades_to_the_fallback(string? name)
    {
        Assert.Equal(
            TraceArtifactFormat.Markdown,
            TraceArtifactFormatExtensions.FromName(
                name, TraceArtifactFormat.Markdown));
    }
}
