// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Tooling.Frameworks;
using Xunit;

namespace NarrativeTrace.Tooling.Tests.Frameworks;

/// <summary>
/// The framework table's rows (Phase 6 D1): ids and check ids are a cross-port contract, detection
/// reads a manifest the way people write one, and a row defers to a more specific one.
/// </summary>
public sealed class FrameworkTableTests
{
    private const string WebProject = "<Project Sdk=\"Microsoft.NET.Sdk.Web\"></Project>";

    private static FrameworkRow Row(string id) => FrameworkTable.Row(id) ?? throw new InvalidOperationException(id);

    [Fact]
    public void The_rows_are_the_audited_frameworks_in_table_order()
    {
        Assert.Equal(
            ["aspnetcore", "di", "xunit", "nunit", "opentelemetry", "default-logger", "msbuild-clarity"],
            FrameworkTable.Rows.Select(row => row.Id));
    }

    [Fact]
    public void Every_observable_row_names_its_wiring_check_and_aspnetcore_keeps_the_registration_pair()
    {
        Assert.Equal(
            ["config.registration-pair", "config.di-namespaces", "config.xunit-fixture", "config.nunit-base",
                "config.otel-source"],
            FrameworkTable.WiringCheckIds());
    }

    [Fact]
    public void The_default_logger_row_folds_into_the_existing_silent_sink_check()
    {
        Assert.Equal(new CheckBinding.ExistingCheck("trap.silent-sink"), Row("default-logger").Check);
    }

    [Fact]
    public void The_msbuild_clarity_row_is_an_owner_opt_in_no_check_observes()
    {
        var row = Row("msbuild-clarity");

        Assert.IsType<Wiring.OwnerOptIn>(row.Wiring);
        Assert.IsType<CheckBinding.NoCheck>(row.Check);
    }

    [Fact]
    public void Only_the_aspnetcore_row_names_a_tier_b_case()
    {
        Assert.Equal(
            ["aspnetcore"],
            FrameworkTable.Rows.Where(row => row.TierBCase != FrameworkRow.NoTierBCase).Select(row => row.Id));
        Assert.Equal("init-prompt-aspnetcore-project", Row("aspnetcore").TierBCase);
    }

    [Fact]
    public void Row_answers_null_for_an_id_the_table_has_no_row_for()
    {
        Assert.Null(FrameworkTable.Row("spring"));
    }

    [Theory]
    [InlineData(WebProject)]
    [InlineData("<Project Sdk=\"Microsoft.NET.Sdk.Web/10.0.100\"></Project>")]
    [InlineData("<Project><Sdk Name=\"Microsoft.NET.Sdk.Web\" /></Project>")]
    [InlineData("<Project Sdk=\"Microsoft.NET.Sdk\"><ItemGroup><FrameworkReference Include=\"Microsoft.AspNetCore.App\" /></ItemGroup></Project>")]
    public void Aspnetcore_is_detected_by_the_web_sdk_or_the_shared_framework_reference(string manifest)
    {
        Assert.True(FrameworkTable.Detected(Row("aspnetcore"), manifest));
    }

    [Theory]
    [InlineData("<Project Sdk=\"Microsoft.NET.Sdk.WebAssembly\"></Project>")]
    [InlineData("<Project Sdk=\"Microsoft.NET.Sdk.Worker\"></Project>")]
    [InlineData("<Project Sdk=\"Microsoft.NET.Sdk\"><ItemGroup><FrameworkReference Include=\"Microsoft.AspNetCore.App.Internal\" /></ItemGroup></Project>")]
    [InlineData("<Project Sdk=\"Microsoft.NET.Sdk\"><!-- <FrameworkReference Include=\"Microsoft.AspNetCore.App\" /> --></Project>")]
    public void Aspnetcore_is_not_detected_by_a_near_miss_sdk_or_a_commented_out_reference(string manifest)
    {
        Assert.False(FrameworkTable.Detected(Row("aspnetcore"), manifest));
    }

    [Theory]
    [InlineData("<PackageReference Include=\"xunit\" Version=\"2.9.2\" />")]
    [InlineData("<PackageReference Version=\"2.9.2\" Include=\"xunit\" />")]
    [InlineData("<PackageReference Include=\"xUnit\" />")]
    [InlineData("<PackageReference Include=\"xunit.v3\" Version=\"1.0.0\" />")]
    [InlineData("<PackageReference Include=\"Moq;xunit\" />")]
    [InlineData("<PackageReference\n    Include='xunit'\n    Version='2.9.2' />")]
    public void Xunit_is_detected_however_the_package_reference_is_written(string reference)
    {
        Assert.True(FrameworkTable.Detected(Row("xunit"), $"<Project>{reference}</Project>"));
    }

    [Theory]
    [InlineData("<PackageReference Include=\"xunit.runner.visualstudio\" />")]
    [InlineData("<PackageReference Include=\"xunit.assert\" />")]
    [InlineData("<PackageVersion Include=\"xunit\" Version=\"2.9.2\" />")]
    [InlineData("<PackageReference Update=\"xunit\" Version=\"2.9.2\" />")]
    [InlineData("<!-- <PackageReference Include=\"xunit\" /> -->")]
    [InlineData("<Description>works with xunit</Description>")]
    public void Xunit_is_not_detected_by_a_sibling_package_a_version_pin_or_a_comment(string text)
    {
        Assert.False(FrameworkTable.Detected(Row("xunit"), $"<Project>{text}</Project>"));
    }

    [Theory]
    [InlineData("OpenTelemetry", true)]
    [InlineData("OpenTelemetry.Extensions.Hosting", true)]
    [InlineData("OpenTelemetryX", false)]
    [InlineData("MyCompany.OpenTelemetry", false)]
    public void Opentelemetry_is_detected_by_its_package_or_any_package_under_its_id(string id, bool expected)
    {
        Assert.Equal(
            expected,
            FrameworkTable.Detected(Row("opentelemetry"), $"<PackageReference Include=\"{id}\" />"));
    }

    [Fact]
    public void Nunit_is_detected_by_its_own_package_but_not_its_adapter()
    {
        Assert.True(FrameworkTable.Detected(Row("nunit"), "<PackageReference Include=\"NUnit\" />"));
        Assert.False(FrameworkTable.Detected(Row("nunit"), "<PackageReference Include=\"NUnit3TestAdapter\" />"));
    }

    [Fact]
    public void Dependency_injection_defers_to_aspnetcore_whose_own_row_is_the_right_advice_for_a_web_app()
    {
        const string DiReference = "<PackageReference Include=\"Microsoft.Extensions.DependencyInjection\" />";

        Assert.True(FrameworkTable.Detected(Row("di"), $"<Project Sdk=\"Microsoft.NET.Sdk\">{DiReference}</Project>"));
        Assert.False(FrameworkTable.Detected(Row("di"), $"<Project Sdk=\"Microsoft.NET.Sdk.Web\">{DiReference}</Project>"));
    }

    [Fact]
    public void Dependency_injection_is_not_detected_by_the_abstractions_package_every_library_references()
    {
        Assert.False(FrameworkTable.Detected(
            Row("di"), "<PackageReference Include=\"Microsoft.Extensions.DependencyInjection.Abstractions\" />"));
    }

    [Fact]
    public void A_row_whose_presence_is_an_absence_or_an_opt_in_is_never_detected_from_a_manifest()
    {
        Assert.False(FrameworkTable.Detected(Row("default-logger"), WebProject));
        Assert.False(FrameworkTable.Detected(Row("msbuild-clarity"), "<PackageReference Include=\"NarrativeTrace.MSBuild\" />"));
    }

    [Fact]
    public void A_deferral_to_an_id_the_table_has_no_row_for_throws_rather_than_never_standing_down()
    {
        var typo = Row("di") with
        {
            Marker = Row("di").Marker with { DeferTo = ["aspnet-core"] },
        };

        var error = Assert.Throws<InvalidOperationException>(
            () => FrameworkTable.Detected(typo, "<PackageReference Include=\"Microsoft.Extensions.DependencyInjection\" />"));
        Assert.Contains("aspnet-core", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("<PackageReference Include=\"NarrativeTrace.AspNetCore\" Version=\"0.2.5\" />", true)]
    [InlineData("<ProjectReference Include=\"..\\..\\src\\NarrativeTrace.AspNetCore\\NarrativeTrace.AspNetCore.csproj\" />", true)]
    [InlineData("<ProjectReference Include=\"../NarrativeTrace.AspNetCore/NarrativeTrace.AspNetCore.csproj\" />", true)]
    [InlineData("<PackageReference Include=\"NarrativeTrace.AspNetCore.Extras\" />", false)]
    [InlineData("<ProjectReference Include=\"../MyNarrativeTrace.AspNetCore/MyNarrativeTrace.AspNetCore.csproj\" />", false)]
    [InlineData("<!-- <PackageReference Include=\"NarrativeTrace.AspNetCore\" /> -->", false)]
    public void A_package_is_referenced_by_its_package_or_project_reference_only(string text, bool expected)
    {
        Assert.Equal(expected, Row("aspnetcore").Package.ReferencedIn(text));
    }
}
