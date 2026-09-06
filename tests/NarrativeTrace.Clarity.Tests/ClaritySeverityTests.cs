// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Clarity;
using Xunit;

namespace NarrativeTrace.Clarity.Tests;

public class ClaritySeverityTests
{
    [Theory]
    [InlineData(ClaritySeverity.High, 3)]
    [InlineData(ClaritySeverity.Medium, 2)]
    [InlineData(ClaritySeverity.Low, 1)]
    public void Weight_ranks_high_above_medium_above_low(
        ClaritySeverity severity, int expected)
    {
        Assert.Equal(expected, severity.Weight());
    }

    [Theory]
    [InlineData(ClaritySeverity.High, "HIGH")]
    [InlineData(ClaritySeverity.Medium, "MEDIUM")]
    [InlineData(ClaritySeverity.Low, "LOW")]
    public void Json_name_is_upper_case(ClaritySeverity severity, string expected)
    {
        Assert.Equal(expected, severity.JsonName());
    }

    [Theory]
    [InlineData(0.0, ClaritySeverity.High)]
    [InlineData(0.20, ClaritySeverity.High)]
    [InlineData(0.2001, ClaritySeverity.Medium)]
    [InlineData(0.50, ClaritySeverity.Medium)]
    [InlineData(0.5001, ClaritySeverity.Low)]
    [InlineData(1.0, ClaritySeverity.Low)]
    public void From_score_uses_the_java_thresholds(double score, ClaritySeverity expected)
    {
        Assert.Equal(expected, ClaritySeverityExtensions.FromScore(score));
    }

    [Fact]
    public void Undefined_severity_values_are_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ((ClaritySeverity)99).Weight());
        Assert.Throws<ArgumentOutOfRangeException>(() => ((ClaritySeverity)99).JsonName());
    }
}
