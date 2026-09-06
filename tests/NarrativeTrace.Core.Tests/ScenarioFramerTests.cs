// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using Xunit;

namespace NarrativeTrace.Core.Tests;

public class ScenarioFramerTests
{
    [Fact]
    public void Prefixes_with_Scenario_label()
    {
        Assert.StartsWith(
            "Scenario: ",
            ScenarioFramer.Frame("PlaceOrder"));
    }

    [Fact]
    public void Strips_Test_prefix()
    {
        Assert.Equal(
            "Scenario: Places an order",
            ScenarioFramer.Frame("Test_places_an_order"));
    }

    [Fact]
    public void Strips_Should_prefix()
    {
        Assert.Equal(
            "Scenario: Return total",
            ScenarioFramer.Frame("Should_return_total"));
    }

    [Fact]
    public void Replaces_underscores_with_spaces()
    {
        Assert.Equal(
            "Scenario: Places an order",
            ScenarioFramer.Frame("places_an_order"));
    }

    [Fact]
    public void Splits_PascalCase_and_capitalizes_first()
    {
        Assert.Equal(
            "Scenario: Place order",
            ScenarioFramer.Frame("PlaceOrder"));
    }

    [Fact]
    public void Humanizes_camel_case_display_name()
    {
        Assert.Equal(
            "Scenario: Customer places order",
            ScenarioFramer.Frame("customerPlacesOrder"));
    }

    [Fact]
    public void Strips_trailing_parenthesized_arguments()
    {
        Assert.Equal(
            "Scenario: Customer places order",
            ScenarioFramer.Frame("customerPlacesOrder()"));
    }

    [Fact]
    public void Preserves_already_spaced_display_names()
    {
        Assert.Equal(
            "Scenario: Customer places order",
            ScenarioFramer.Frame("Customer places order"));
    }
}
