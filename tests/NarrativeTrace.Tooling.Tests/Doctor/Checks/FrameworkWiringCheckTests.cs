// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Tooling.Doctor;
using NarrativeTrace.Tooling.Doctor.Checks;
using NarrativeTrace.Tooling.Frameworks;
using Xunit;

namespace NarrativeTrace.Tooling.Tests.Doctor.Checks;

/// <summary>
/// One generic check per framework-table row (Phase 6 D3): it fails both ways — the framework is
/// present but its package is not referenced, or the package is in use but its wiring is never (or
/// only partly) applied — passes a project that uses neither, and its fix is the fix it accepts.
/// </summary>
public sealed class FrameworkWiringCheckTests
{
    private const string WebSdk = "<Project Sdk=\"Microsoft.NET.Sdk.Web\"></Project>";
    private const string AspNetCoreReference =
        "<Project Sdk=\"Microsoft.NET.Sdk.Web\"><ItemGroup><PackageReference Include=\"NarrativeTrace.AspNetCore\" Version=\"0.2.5\" /></ItemGroup></Project>";

    private static FrameworkRow Row(string id) => FrameworkTable.Row(id)!;

    private static DoctorFinding Run(string rowId, DoctorSnapshot snapshot) =>
        new FrameworkWiringCheck(Row(rowId)).Run(snapshot);

    private static DoctorSnapshot Project(string manifest, params (string Path, string Content)[] sources) =>
        DoctorSnapshotFixtures.WithProject(manifest) with
        {
            SourceFiles = sources.ToDictionary(s => s.Path, s => s.Content),
        };

    [Fact]
    public void A_project_that_neither_uses_the_framework_nor_references_the_package_passes()
    {
        var finding = Run("aspnetcore", DoctorSnapshotFixtures.WithProject("<Project Sdk=\"Microsoft.NET.Sdk\"></Project>"));

        Assert.True(finding.Passed);
        Assert.Equal("config.registration-pair", finding.Id);
        Assert.Contains("not detected", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_detected_framework_whose_package_is_not_referenced_fails_with_the_add_line_and_the_snippet()
    {
        var finding = Run("aspnetcore", Project(WebSdk, ("Program.cs", "var app = builder.Build();")));

        Assert.False(finding.Passed);
        Assert.Contains("NarrativeTrace.AspNetCore is not referenced", finding.Message, StringComparison.Ordinal);
        Assert.Contains("dotnet add package NarrativeTrace.AspNetCore", finding.Fix, StringComparison.Ordinal);
        Assert.EndsWith(WiringSnippets.Text("aspnetcore"), finding.Fix, StringComparison.Ordinal);
    }

    [Fact]
    public void The_add_line_pins_the_projects_own_family_version_when_the_doctor_can_tell_it()
    {
        var snapshot = Project(WebSdk) with { ProjectResolvedFamilyVersion = "0.2.5" };

        var finding = Run("aspnetcore", snapshot);

        Assert.Contains("dotnet add package NarrativeTrace.AspNetCore --version 0.2.5", finding.Fix, StringComparison.Ordinal);
    }

    [Fact]
    public void A_referenced_package_whose_wiring_is_never_applied_fails_with_the_snippet()
    {
        var finding = Run("aspnetcore", Project(AspNetCoreReference, ("Program.cs", "var app = builder.Build();")));

        Assert.False(finding.Passed);
        Assert.Contains("never applied", finding.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("dotnet add package", finding.Fix, StringComparison.Ordinal);
        Assert.EndsWith(WiringSnippets.Text("aspnetcore"), finding.Fix, StringComparison.Ordinal);
    }

    [Fact]
    public void Add_without_use_fails_even_where_no_manifest_is_visible_as_the_registration_pair_always_did()
    {
        var finding = Run("aspnetcore", DoctorSnapshotFixtures.WithSource(("Startup.cs", "services.AddNarrativeTrace(configuration);")));

        Assert.False(finding.Passed);
        Assert.Contains("UseNarrativeTrace", finding.Fix, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("services.AddNarrativeTrace(configuration); app.UseNarrativeTrace();")]
    [InlineData("services.AddNarrativeTrace(configuration); app.UseMiddleware<NarrativeTraceMiddleware>();")]
    [InlineData("services.AddNarrativeTrace(configuration); app.UseMiddleware<NarrativeTrace.AspNetCore.NarrativeTraceMiddleware>();")]
    public void Add_and_use_in_either_spelling_pass(string source)
    {
        var finding = Run("aspnetcore", Project(AspNetCoreReference, ("Startup.cs", source)));

        Assert.True(finding.Passed);
        Assert.Contains("is wired", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_registration_and_the_middleware_may_live_in_different_files()
    {
        var finding = Run(
            "aspnetcore",
            Project(AspNetCoreReference, ("Services.cs", "services.AddNarrativeTrace(c);"), ("Pipeline.cs", "app.UseNarrativeTrace();")));

        Assert.True(finding.Passed);
    }

    [Fact]
    public void Add_narrative_tracing_is_not_mistaken_for_add_narrative_trace()
    {
        var finding = Run(
            "aspnetcore",
            Project(AspNetCoreReference, ("Program.cs", "services.AddNarrativeTracing(o => o.Namespaces(\"A\")); app.UseNarrativeTrace();")));

        Assert.False(finding.Passed);
    }

    [Fact]
    public void A_row_that_defers_passes_while_the_row_it_defers_to_is_present()
    {
        var finding = Run(
            "di",
            Project("<Project Sdk=\"Microsoft.NET.Sdk.Web\"><PackageReference Include=\"Microsoft.Extensions.DependencyInjection\" /></Project>"));

        Assert.True(finding.Passed);
    }

    [Fact]
    public void A_project_that_builds_narrativetrace_from_source_is_referenced_through_its_project_reference()
    {
        var finding = Run(
            "nunit",
            Project("<ProjectReference Include=\"../src/NarrativeTrace.Testing.NUnit/NarrativeTrace.Testing.NUnit.csproj\" />"));

        Assert.False(finding.Passed);
        Assert.Contains("never applied", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Every_finding_links_to_its_rows_own_guide_on_the_published_snapshot()
    {
        foreach (var row in FrameworkTable.RowsWithWiringChecks())
        {
            var finding = new FrameworkWiringCheck(row).Run(DoctorSnapshotFixtures.Empty());

            Assert.Equal(DoctorDocUrls.ForPage(row.Guide), finding.DocUrl);
        }
    }

    /// <summary>
    /// Cross-port item 3: the fix the doctor prints must be the fix the doctor accepts — every row's own
    /// snippet, dropped into a project that references the package, passes its check.
    /// </summary>
    [Fact]
    public void Every_rows_own_snippet_dropped_into_a_referencing_project_passes_its_check()
    {
        foreach (var row in FrameworkTable.RowsWithWiringChecks())
        {
            var manifest = $"<PackageReference Include=\"{row.Package.PackageId}\" Version=\"0.2.5\" />";
            var finding = new FrameworkWiringCheck(row).Run(Project(manifest, ("Wiring.cs", WiringSnippets.Text(row.Id))));

            Assert.True(finding.Passed, $"{row.Id}: {finding.Message}");
        }
    }

    [Fact]
    public void Every_row_fails_a_project_that_shows_its_framework_and_nothing_else()
    {
        var markers = new Dictionary<string, string>
        {
            ["aspnetcore"] = WebSdk,
            ["di"] = "<PackageReference Include=\"Microsoft.Extensions.DependencyInjection\" />",
            ["xunit"] = "<PackageReference Include=\"xunit\" />",
            ["nunit"] = "<PackageReference Include=\"NUnit\" />",
            ["opentelemetry"] = "<PackageReference Include=\"OpenTelemetry.Extensions.Hosting\" />",
        };

        foreach (var row in FrameworkTable.RowsWithWiringChecks())
        {
            Assert.False(new FrameworkWiringCheck(row).Run(Project(markers[row.Id])).Passed, row.Id);
        }
    }

    [Fact]
    public void A_row_with_no_wiring_check_of_its_own_is_refused()
    {
        Assert.Throws<ArgumentException>(() => new FrameworkWiringCheck(Row("default-logger")));
        Assert.Throws<ArgumentException>(() => new FrameworkWiringCheck(Row("msbuild-clarity")));
    }
}
