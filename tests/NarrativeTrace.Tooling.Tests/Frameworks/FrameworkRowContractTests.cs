// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Text.RegularExpressions;
using NarrativeTrace.Tooling.Frameworks;
using Xunit;

namespace NarrativeTrace.Tooling.Tests.Frameworks;

/// <summary>
/// A row cannot be built half-empty or with a check the doctor could never run, and a snippet's
/// evidence means "every pattern, each in any source file".
/// </summary>
public sealed class FrameworkRowContractTests
{
    private static readonly FrameworkMarker Marker = new("a marker", [], []);
    private static readonly IntegrationPackage Package = new("NarrativeTrace.Example");

    private static Wiring.Snippet Snippet(params string[] evidence) =>
        new("wiring", "tests/Fixture.cs", "wiring", "csharp", evidence.Select(e => new Regex(e)).ToList());

    private static FrameworkRow Row(string id = "row", Wiring? wiring = null, CheckBinding? check = null) =>
        new(id, "Row", Marker, Package, wiring ?? Snippet("x"), check ?? new CheckBinding.WiringCheck("config.row-wired"),
            FrameworkRow.NoTierBCase, "documentation/guide.md");

    [Theory]
    [InlineData("Row")]
    [InlineData("row_id")]
    [InlineData("-row")]
    [InlineData("row-")]
    [InlineData("row--id")]
    [InlineData("")]
    public void A_row_id_that_is_not_kebab_case_is_refused(string id)
    {
        Assert.Throws<ArgumentException>(() => Row(id));
    }

    [Fact]
    public void A_wiring_check_on_a_row_whose_wiring_no_source_shows_is_refused()
    {
        var error = Assert.Throws<ArgumentException>(() => Row(wiring: new Wiring.OwnerOptIn("an opt-in")));

        Assert.Contains("source", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void An_owner_opt_in_row_with_no_check_is_a_decision_the_table_accepts()
    {
        var row = Row(wiring: new Wiring.OwnerOptIn("an opt-in"), check: new CheckBinding.NoCheck("runtime-only"));

        Assert.IsType<CheckBinding.NoCheck>(row.Check);
    }

    [Theory]
    [InlineData("config.registration")]
    [InlineData("trap.row-wired")]
    [InlineData("config.Row-Wired")]
    [InlineData("config.-wired")]
    [InlineData("config.")]
    public void A_wiring_check_id_must_be_config_framework_dash_thing(string id)
    {
        Assert.Throws<ArgumentException>(() => new CheckBinding.WiringCheck(id));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_names_descriptions_fixtures_and_cases_are_refused(string blank)
    {
        Assert.Throws<ArgumentException>(() => new FrameworkMarker(blank, [], []));
        Assert.Throws<ArgumentException>(() => new FrameworkRow(
            "row", blank, Marker, Package, Snippet("x"), new CheckBinding.WiringCheck("config.row-wired"),
            FrameworkRow.NoTierBCase, "g"));
        Assert.Throws<ArgumentException>(() => new Wiring.Snippet("w", blank, null, "csharp", [new Regex("x")]));
        Assert.Throws<ArgumentException>(() => new FrameworkRow(
            "row", "Row", Marker, Package, Snippet("x"), new CheckBinding.WiringCheck("config.row-wired"), blank, "g"));
    }

    [Fact]
    public void A_snippet_without_evidence_is_refused()
    {
        Assert.Throws<ArgumentException>(() => Snippet());
    }

    [Theory]
    [InlineData("Example.Orders")]
    [InlineData("Microsoft.Extensions.DependencyInjection")]
    public void A_package_outside_the_narrativetrace_family_is_refused(string id)
    {
        Assert.Throws<ArgumentException>(() => new IntegrationPackage(id));
    }

    [Fact]
    public void Evidence_needs_every_pattern_each_found_in_any_source()
    {
        var snippet = Snippet("Add\\(", "Use\\(");

        Assert.True(snippet.AppliedIn(["services.Add();", "app.Use();"]));
        Assert.True(snippet.AppliedIn(["services.Add(); app.Use();"]));
        Assert.False(snippet.AppliedIn(["services.Add();"]));
        Assert.False(snippet.AppliedIn([]));
    }

    [Fact]
    public void The_add_command_pins_the_projects_family_version_only_when_it_is_known()
    {
        Assert.Equal("dotnet add package NarrativeTrace.Example --version 1.2.3", Package.AddCommand("1.2.3"));
        Assert.Equal("dotnet add package NarrativeTrace.Example", Package.AddCommand(null));
    }
}
