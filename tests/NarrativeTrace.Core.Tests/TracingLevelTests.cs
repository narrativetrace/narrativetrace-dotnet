// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using Xunit;

namespace NarrativeTrace.Core.Tests;

public class TracingLevelTests
{
    [Fact]
    public void Off_is_not_active()
    {
        Assert.False(TracingLevel.Off.IsActive());
    }

    [Theory]
    [InlineData(TracingLevel.Errors)]
    [InlineData(TracingLevel.Summary)]
    [InlineData(TracingLevel.Narrative)]
    [InlineData(TracingLevel.Detail)]
    public void Active_levels_are_active(TracingLevel level)
    {
        Assert.True(level.IsActive());
    }

    [Theory]
    [InlineData(TracingLevel.Detail, TracingLevel.Summary)]
    [InlineData(TracingLevel.Narrative, TracingLevel.Narrative)]
    [InlineData(TracingLevel.Detail, TracingLevel.Off)]
    public void IsEnabled_returns_true_when_current_gte_required(
        TracingLevel current,
        TracingLevel required)
    {
        Assert.True(current.IsEnabled(required));
    }

    [Theory]
    [InlineData(TracingLevel.Off, TracingLevel.Summary)]
    [InlineData(TracingLevel.Errors, TracingLevel.Detail)]
    [InlineData(TracingLevel.Summary, TracingLevel.Detail)]
    public void IsEnabled_returns_false_when_current_lt_required(
        TracingLevel current,
        TracingLevel required)
    {
        Assert.False(current.IsEnabled(required));
    }

    [Theory]
    [InlineData("Detail", TracingLevel.Detail)]
    [InlineData("detail", TracingLevel.Detail)]
    [InlineData("  NARRATIVE  ", TracingLevel.Narrative)]
    [InlineData("OFF", TracingLevel.Off)]
    public void FromName_parses_known_names_case_insensitively(
        string name, TracingLevel expected)
    {
        Assert.Equal(
            expected,
            TracingLevelExtensions.FromName(name, TracingLevel.Errors));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("bogus")]
    [InlineData("5")]
    [InlineData("-1")]
    public void FromName_falls_back_on_null_blank_or_unknown(string? name)
    {
        Assert.Equal(
            TracingLevel.Summary,
            TracingLevelExtensions.FromName(name, TracingLevel.Summary));
    }
}
