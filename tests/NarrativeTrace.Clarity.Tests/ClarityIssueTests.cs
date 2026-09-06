// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Clarity;
using Xunit;

namespace NarrativeTrace.Clarity.Tests;

public class ClarityIssueTests
{
    [Fact]
    public void Defaults_to_medium_severity_and_one_occurrence()
    {
        var issue = new ClarityIssue("method-name", "Svc.Do", "Use a domain verb");

        Assert.Equal("method-name", issue.Category);
        Assert.Equal("Svc.Do", issue.Element);
        Assert.Equal("Use a domain verb", issue.Suggestion);
        Assert.Equal(ClaritySeverity.Medium, issue.Severity);
        Assert.Equal(1, issue.Occurrences);
        Assert.Equal(2.0, issue.ImpactScore);
    }

    [Fact]
    public void Impact_score_is_severity_weight_times_occurrences()
    {
        var high = new ClarityIssue(
            "method-name", "Svc.Do", "s", ClaritySeverity.High, 4);

        Assert.Equal(12.0, high.ImpactScore);
    }

    [Fact]
    public void With_occurrences_rescales_impact_and_keeps_identity()
    {
        var issue = new ClarityIssue(
            "param-name", "x", "Use a domain name", ClaritySeverity.Low);

        var repeated = issue.WithOccurrences(5);

        Assert.Equal(5, repeated.Occurrences);
        Assert.Equal(5.0, repeated.ImpactScore);
        Assert.Equal(issue.Category, repeated.Category);
        Assert.Equal(issue.Element, repeated.Element);
        Assert.Equal(issue.Severity, repeated.Severity);
        Assert.Equal(1, issue.Occurrences);
    }

    [Theory]
    [InlineData(null, "el", "sug")]
    [InlineData("", "el", "sug")]
    [InlineData("  ", "el", "sug")]
    [InlineData("cat", null, "sug")]
    [InlineData("cat", "", "sug")]
    [InlineData("cat", "el", null)]
    public void Blank_fields_are_rejected(string? category, string? element, string? suggestion)
    {
        Assert.Throws<ArgumentException>(
            () => new ClarityIssue(category!, element!, suggestion!));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Non_positive_occurrences_are_rejected(int occurrences)
    {
        Assert.Throws<ArgumentException>(() => new ClarityIssue(
            "cat", "el", "sug", ClaritySeverity.Medium, occurrences));
        Assert.Throws<ArgumentException>(() => new ClarityIssue(
            "cat", "el", "sug").WithOccurrences(occurrences));
    }

    [Fact]
    public void Undefined_severity_is_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ClarityIssue(
            "cat", "el", "sug", (ClaritySeverity)99));
    }

    [Fact]
    public void Equality_is_structural()
    {
        Assert.Equal(
            new ClarityIssue("c", "e", "s", ClaritySeverity.High, 2),
            new ClarityIssue("c", "e", "s", ClaritySeverity.High, 2));
        Assert.NotEqual(
            new ClarityIssue("c", "e", "s", ClaritySeverity.High, 2),
            new ClarityIssue("c", "e", "s", ClaritySeverity.High, 3));
    }
}
