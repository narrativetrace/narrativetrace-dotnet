// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using Xunit;

namespace NarrativeTrace.Core.Tests;

public sealed class ConsoleSummaryReporterDeltaLineTests
{
    private static ScenarioDelta Unchanged(string scenario) =>
        new(scenario, ScenarioDeltaKind.Unchanged, string.Empty, string.Empty);

    private static ScenarioDelta New(string scenario) =>
        new(scenario, ScenarioDeltaKind.New, string.Empty, string.Empty);

    private static ScenarioDelta Changed(string scenario, string summary) =>
        new(scenario, ScenarioDeltaKind.Changed, summary, "diff");

    [Fact]
    public void No_deltas_produce_an_empty_line()
    {
        Assert.Equal(string.Empty, ConsoleSummaryReporter.FormatDeltaLine([]));
    }

    [Fact]
    public void Unchanged_only_uses_the_singular_noun_for_one_scenario()
    {
        var line = ConsoleSummaryReporter.FormatDeltaLine([Unchanged("A")]);

        Assert.Equal("1 scenario unchanged", line);
    }

    [Fact]
    public void Unchanged_only_uses_the_plural_noun_for_more_than_one()
    {
        var line = ConsoleSummaryReporter.FormatDeltaLine([Unchanged("A"), Unchanged("B")]);

        Assert.Equal("2 scenarios unchanged", line);
    }

    [Fact]
    public void New_only_reports_new()
    {
        var line = ConsoleSummaryReporter.FormatDeltaLine([New("A")]);

        Assert.Equal("1 scenario new", line);
    }

    [Fact]
    public void Changed_reports_the_scenario_name_and_summary()
    {
        var line = ConsoleSummaryReporter.FormatDeltaLine(
            [Changed("Weekend trip settles", "+4 calls CurrencyConverter.ToBaseCurrency")]);

        Assert.Equal(
            "1 scenario changed: \"Weekend trip settles\" (+4 calls CurrencyConverter.ToBaseCurrency)",
            line);
    }

    /// <summary>
    /// The noun rides on the first non-empty segment only; later segments are
    /// just the bare count, and segments join with " · ".
    /// </summary>
    [Fact]
    public void All_three_kinds_combine_with_the_noun_only_on_the_first_segment()
    {
        var line = ConsoleSummaryReporter.FormatDeltaLine(
            [Unchanged("A"), Unchanged("B"), New("C"), Changed("D", "+1 call X.y")]);

        Assert.Equal("2 scenarios unchanged · 1 new · 1 changed: \"D\" (+1 call X.y)", line);
    }

    [Fact]
    public void Multiple_changed_scenarios_are_comma_joined()
    {
        var line = ConsoleSummaryReporter.FormatDeltaLine(
            [Changed("A", "+1 call X.y"), Changed("B", "-1 call X.z")]);

        Assert.Equal(
            "2 scenarios changed: \"A\" (+1 call X.y), \"B\" (-1 call X.z)",
            line);
    }

    /// <summary>A changed scenario name over 32 characters is truncated with an ellipsis so it cannot flood the line.</summary>
    [Fact]
    public void A_long_scenario_name_is_truncated()
    {
        var longName = new string('a', 40);

        var line = ConsoleSummaryReporter.FormatDeltaLine([Changed(longName, "+1 call X.y")]);

        Assert.Equal(
            "1 scenario changed: \"" + new string('a', 32) + "…\" (+1 call X.y)",
            line);
    }

    [Fact]
    public void A_scenario_name_at_exactly_the_cap_is_not_truncated()
    {
        var name = new string('a', 32);

        var line = ConsoleSummaryReporter.FormatDeltaLine([Changed(name, "+1 call X.y")]);

        Assert.Equal("1 scenario changed: \"" + name + "\" (+1 call X.y)", line);
    }
}
