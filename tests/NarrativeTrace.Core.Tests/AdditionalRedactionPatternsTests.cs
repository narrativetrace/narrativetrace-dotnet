// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using Xunit;

namespace NarrativeTrace.Core.Tests;

/// <summary>
/// The .NET mirror of java's <c>AdditionalRedactionPatternsTest</c>: the
/// operator-supplied deny-list widening, read from the environment (the only
/// override channel this port has — there is no JVM-style system property).
/// </summary>
public class AdditionalRedactionPatternsTests
{
    [Fact]
    public void Parse_splits_comma_separated_patterns_and_strips_whitespace()
    {
        var parsed = AdditionalRedactionPatterns.Parse(" betalingskort , clavePropia ,ssn");

        Assert.Equal(
            new HashSet<string> { "betalingskort", "clavePropia", "ssn" },
            parsed.ToHashSet());
    }

    [Fact]
    public void Parse_drops_blank_entries()
    {
        var parsed = AdditionalRedactionPatterns.Parse("a,,b, ,c");

        Assert.Equal(
            new HashSet<string> { "a", "b", "c" },
            parsed.ToHashSet());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_of_null_or_blank_yields_nothing(string? raw)
    {
        Assert.Empty(AdditionalRedactionPatterns.Parse(raw));
    }

    [Fact]
    public void Configured_reads_the_environment_variable()
    {
        var configured = AdditionalRedactionPatterns.Configured(
            key => key == AdditionalRedactionPatterns.EnvironmentVariable ? "betalingskort" : null);

        Assert.Equal(["betalingskort"], configured);
    }

    [Fact]
    public void Configured_is_empty_when_the_environment_variable_is_unset()
    {
        var configured = AdditionalRedactionPatterns.Configured(_ => null);

        Assert.Empty(configured);
    }
}
