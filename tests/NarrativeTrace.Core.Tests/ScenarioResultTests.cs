// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using Xunit;

namespace NarrativeTrace.Core.Tests;

public class ScenarioResultTests
{
    [Theory]
    [InlineData(ScenarioResult.Success, "success")]
    [InlineData(ScenarioResult.Error, "error")]
    public void Wire_name_is_the_schema_legal_spelling(
        ScenarioResult result, string expected)
    {
        Assert.Equal(expected, result.WireName());
    }

    [Theory]
    [InlineData(ScenarioResult.Success, "PASSED")]
    [InlineData(ScenarioResult.Error, "FAILED")]
    public void Display_name_is_the_human_facing_spelling(
        ScenarioResult result, string expected)
    {
        Assert.Equal(expected, result.DisplayName());
    }

    [Theory]
    [InlineData(true, ScenarioResult.Error)]
    [InlineData(false, ScenarioResult.Success)]
    public void Of_maps_the_test_outcome_flag(bool failed, ScenarioResult expected)
    {
        Assert.Equal(expected, ScenarioResultExtensions.Of(failed));
    }

    [Theory]
    [InlineData("success", ScenarioResult.Success)]
    [InlineData("error", ScenarioResult.Error)]
    [InlineData("PASSED", ScenarioResult.Success)]
    [InlineData("FAILED", ScenarioResult.Error)]
    public void From_parses_both_spellings(string value, ScenarioResult expected)
    {
        Assert.Equal(expected, ScenarioResultExtensions.From(value));
    }

    [Theory]
    [InlineData("SUCCESS")]
    [InlineData("Error")]
    [InlineData("passed")]
    [InlineData("  FAILED  ")]
    public void From_is_case_insensitive_and_trims(string value)
    {
        // Every one of these is a legal spelling in some casing; none may throw.
        var parsed = ScenarioResultExtensions.From(value);

        Assert.True(parsed is ScenarioResult.Success or ScenarioResult.Error);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ok")]
    [InlineData("failure")]
    [InlineData("Success!")]
    [InlineData("0")]
    public void From_rejects_anything_the_schema_would_refuse(string value)
    {
        var ex = Assert.Throws<ArgumentException>(
            () => ScenarioResultExtensions.From(value));

        Assert.Contains("success, error, PASSED, FAILED", ex.Message);
    }

    [Fact]
    public void From_rejects_null()
    {
        Assert.Throws<ArgumentException>(
            () => ScenarioResultExtensions.From(null!));
    }

    [Theory]
    [InlineData(ScenarioResult.Success)]
    [InlineData(ScenarioResult.Error)]
    public void Wire_name_round_trips_through_From(ScenarioResult result)
    {
        Assert.Equal(result, ScenarioResultExtensions.From(result.WireName()));
    }

    [Theory]
    [InlineData(ScenarioResult.Success)]
    [InlineData(ScenarioResult.Error)]
    public void Display_name_round_trips_through_From(ScenarioResult result)
    {
        Assert.Equal(result, ScenarioResultExtensions.From(result.DisplayName()));
    }

    [Theory]
    [InlineData(ScenarioResult.Success)]
    [InlineData(ScenarioResult.Error)]
    public void Member_name_is_never_a_legal_wire_spelling(ScenarioResult result)
    {
        // The rule the whole enum exists for: ToString() must never reach an
        // artifact. Pin that it is in fact different from the wire spelling,
        // so a careless ToString() cannot silently pass schema validation.
        Assert.NotEqual(result.WireName(), result.ToString());
    }
}
