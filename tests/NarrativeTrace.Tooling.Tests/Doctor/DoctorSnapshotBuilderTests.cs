// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Tooling.Doctor;
using NarrativeTrace.Tooling.Init;
using Xunit;

namespace NarrativeTrace.Tooling.Tests.Doctor;

public sealed class DoctorSnapshotBuilderTests : IDisposable
{
    /// <summary>A single-skill in-memory carrier, pure — no disk I/O, mirroring the check tests' style.</summary>
    private static Carrier StubCarrier(string version = "1.0.0") => Carrier.FromSource(version, name => name switch
    {
        "catalogue.json" =>
            "{\"runtime\": \"dotnet\", \"skills\": [{\"name\": \"narrativetrace-doctor\", "
            + "\"description\": \"d\", \"agents\": \"agents/narrativetrace-doctor/SKILL.md\", "
            + "\"claude\": \"claude/narrativetrace-doctor/SKILL.md\"}]}",
        "agents/narrativetrace-doctor/SKILL.md" => "---\nname: narrativetrace-doctor\n---\n",
        "claude/narrativetrace-doctor/SKILL.md" => "---\nname: narrativetrace-doctor\n---\n",
        _ => null,
    });

    private static Carrier ThrowingCarrier() =>
        throw new ArgumentException("no carrier for this test");

    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "nt-doctor-" + Guid.NewGuid().ToString("N"));

    public DoctorSnapshotBuilderTests()
    {
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    /// <summary>
    /// A framework marker or a package reference can live in any MSBuild manifest — another language's
    /// project file, or a Directory.Build file that adds items to every project — but a central version
    /// pin references nothing.
    /// </summary>
    [Fact]
    public void Reads_every_msbuild_manifest_a_reference_can_live_in_but_not_the_central_version_pins()
    {
        File.WriteAllText(Path.Combine(_root, "Directory.Build.props"), "<Project />");
        File.WriteAllText(Path.Combine(_root, "Directory.Build.targets"), "<Project />");
        File.WriteAllText(Path.Combine(_root, "Directory.Packages.props"), "<Project />");
        WriteUnder("app", "App.fsproj");
        WriteUnder("lib", "Lib.vbproj");
        WriteUnder("web", "Web.csproj");

        var snapshot = DoctorSnapshotBuilder.Build(_root, _ => null);

        Assert.Equal(
            ["App.fsproj", "Directory.Build.props", "Directory.Build.targets", "Lib.vbproj", "Web.csproj"],
            snapshot.ProjectFiles.Keys.Select(Path.GetFileName).Order());
    }

    [Fact]
    public void Reads_csproj_and_source_files_but_skips_bin_and_obj()
    {
        File.WriteAllText(Path.Combine(_root, "app.csproj"), "<Project />");
        File.WriteAllText(Path.Combine(_root, "Program.cs"), "// program");
        WriteUnder("bin", "Ignored.cs");
        WriteUnder("obj", "Ignored.cs");

        var snapshot = DoctorSnapshotBuilder.Build(_root, _ => null);

        Assert.Single(snapshot.ProjectFiles);
        Assert.Single(snapshot.SourceFiles);
    }

    [Fact]
    public void Reads_run_settings_files_but_skips_bin_and_obj()
    {
        Directory.CreateDirectory(Path.Combine(_root, "tests"));
        Directory.CreateDirectory(Path.Combine(_root, "bin"));
        File.WriteAllText(Path.Combine(_root, "tests", "approval.runsettings"), "<RunSettings />");
        File.WriteAllText(Path.Combine(_root, "bin", "copied.runsettings"), "<RunSettings />");

        var snapshot = DoctorSnapshotBuilder.Build(_root, _ => null);

        Assert.Single(snapshot.RunSettingsFiles);
        Assert.Contains(snapshot.RunSettingsFiles.Keys, path => path.EndsWith("approval.runsettings", StringComparison.Ordinal));
    }

    /// <summary>
    /// A relative approved dir sits beside each test project (the test run resolves it there), so a
    /// doctor run from the solution root reads every project's baselines, not only its own folder's.
    /// </summary>
    [Fact]
    public void Reads_the_approved_traces_beside_every_project_as_well_as_its_own()
    {
        Directory.CreateDirectory(Path.Combine(_root, "App.Tests", "narratives", "OrderTests"));
        File.WriteAllText(Path.Combine(_root, "App.Tests", "App.Tests.csproj"), "<Project />");
        File.WriteAllText(Path.Combine(_root, "App.Tests", "narratives", "OrderTests", "a.approved.nt"), "x");
        Directory.CreateDirectory(Path.Combine(_root, "narratives"));
        File.WriteAllText(Path.Combine(_root, "narratives", "root.received.nt"), "y");
        Directory.CreateDirectory(Path.Combine(_root, "Lib", "baselines"));
        File.WriteAllText(Path.Combine(_root, "Lib", "Lib.csproj"), "<Project />");

        var snapshot = DoctorSnapshotBuilder.Build(_root, _ => null);

        Assert.Equal(2, snapshot.ApprovedDirFiles.Count);
        Assert.Contains(snapshot.ApprovedDirFiles.Keys, path => path.EndsWith("a.approved.nt", StringComparison.Ordinal));
        Assert.Contains(snapshot.ApprovedDirFiles.Keys, path => path.EndsWith("root.received.nt", StringComparison.Ordinal));
    }

    [Fact]
    public void An_absolute_approved_dir_is_read_once_whatever_the_projects()
    {
        var absolute = Path.Combine(_root, "elsewhere");
        Directory.CreateDirectory(absolute);
        File.WriteAllText(Path.Combine(absolute, "a.approved.nt"), "x");
        File.WriteAllText(Path.Combine(_root, "App.csproj"), "<Project />");

        var snapshot = DoctorSnapshotBuilder.Build(_root, key => key == "NARRATIVETRACE_APPROVED_DIR" ? absolute : null);

        Assert.Single(snapshot.ApprovedDirFiles);
    }

    [Fact]
    public void Reads_the_narrativetrace_environment_variables()
    {
        var snapshot = DoctorSnapshotBuilder.Build(_root, key =>
            key == "NARRATIVETRACE_OUTPUT" ? "false" : null);

        Assert.Equal("false", snapshot.Env["NARRATIVETRACE_OUTPUT"]);
        Assert.Null(snapshot.Env["NARRATIVETRACE_LEVEL"]);
    }

    [Fact]
    public void Reads_installed_packages_from_project_assets_json()
    {
        var objDir = Path.Combine(_root, "obj");
        Directory.CreateDirectory(objDir);
        File.WriteAllText(
            Path.Combine(objDir, "project.assets.json"),
            """{"libraries":{"NarrativeTrace.Core/0.1.4":{"type":"package"}}}""");

        var snapshot = DoctorSnapshotBuilder.Build(_root, _ => null);

        Assert.Equal("0.1.4", snapshot.InstalledPackages["NarrativeTrace.Core"]);
    }

    [Fact]
    public void Build_on_a_missing_directory_returns_an_empty_snapshot_without_throwing()
    {
        var snapshot = DoctorSnapshotBuilder.Build(
            Path.Combine(_root, "does-not-exist"), _ => null);

        Assert.Empty(snapshot.ProjectFiles);
        Assert.Empty(snapshot.InstalledPackages);
    }

    [Fact]
    public void Ignores_a_project_assets_json_with_no_libraries_property()
    {
        var objDir = Path.Combine(_root, "obj");
        Directory.CreateDirectory(objDir);
        File.WriteAllText(Path.Combine(objDir, "project.assets.json"), """{"version":3}""");

        var snapshot = DoctorSnapshotBuilder.Build(_root, _ => null);

        Assert.Empty(snapshot.InstalledPackages);
    }

    [Fact]
    public void Ignores_a_malformed_project_assets_json_without_throwing()
    {
        var objDir = Path.Combine(_root, "obj");
        Directory.CreateDirectory(objDir);
        File.WriteAllText(Path.Combine(objDir, "project.assets.json"), "{not valid json");

        var snapshot = DoctorSnapshotBuilder.Build(_root, _ => null);

        Assert.Empty(snapshot.InstalledPackages);
    }

    [Fact]
    public void Reads_approved_dir_files_under_the_default_narratives_directory()
    {
        WriteUnder("narratives/Order", "place.approved.nt");

        var snapshot = DoctorSnapshotBuilder.Build(_root, _ => null);

        Assert.Contains(snapshot.ApprovedDirFiles.Keys, k => k.EndsWith("place.approved.nt", StringComparison.Ordinal));
    }

    [Fact]
    public void Reads_installed_skills_through_the_installers_own_project_state_reader()
    {
        var directory = Path.Combine(_root, ".agents", "skills", "narrativetrace-doctor");
        Directory.CreateDirectory(directory);
        File.WriteAllText(
            Path.Combine(directory, "SKILL.md"),
            "---\nname: narrativetrace-doctor\n---\n\n"
            + "<!-- installed by narrativetrace init from NarrativeTrace.Skills/1.0.0 — edit the catalogue, not this file -->\n");

        var snapshot = DoctorSnapshotBuilder.Build(_root, _ => null, () => StubCarrier());

        var skill = Assert.Single(snapshot.InstalledSkills);
        Assert.Equal("narrativetrace-doctor", skill.Name);
        Assert.Equal(SkillPresence.Ours, skill.Presence);
        Assert.Equal("NarrativeTrace.Skills/1.0.0", skill.Coordinate);
    }

    [Fact]
    public void Resolves_the_projects_family_version_from_restore_output()
    {
        var objDir = Path.Combine(_root, "obj");
        Directory.CreateDirectory(objDir);
        File.WriteAllText(
            Path.Combine(objDir, "project.assets.json"),
            """{"libraries":{"NarrativeTrace.Core/0.1.4":{"type":"package"}}}""");

        var snapshot = DoctorSnapshotBuilder.Build(_root, _ => null, () => StubCarrier());

        Assert.Equal("0.1.4", snapshot.ProjectResolvedFamilyVersion);
    }

    [Fact]
    public void Project_resolved_family_version_is_null_with_no_restore_output()
    {
        var snapshot = DoctorSnapshotBuilder.Build(_root, _ => null, () => StubCarrier());

        Assert.Null(snapshot.ProjectResolvedFamilyVersion);
    }

    [Fact]
    public void Reads_the_carrier_catalogues_skill_names_from_the_given_opener()
    {
        var snapshot = DoctorSnapshotBuilder.Build(_root, _ => null, () => StubCarrier());

        Assert.Equal(["narrativetrace-doctor"], snapshot.CarrierCatalogueSkills);
    }

    [Fact]
    public void A_carrier_that_cannot_be_opened_leaves_the_catalogue_empty_without_throwing()
    {
        var snapshot = DoctorSnapshotBuilder.Build(_root, _ => null, ThrowingCarrier);

        Assert.Empty(snapshot.CarrierCatalogueSkills);
    }

    [Fact]
    public void The_two_arg_overload_builds_a_snapshot_with_no_carrier_configured()
    {
        var snapshot = DoctorSnapshotBuilder.Build(_root, _ => null);

        Assert.Empty(snapshot.CarrierCatalogueSkills);
    }

    [Fact]
    public void Build_on_a_missing_directory_leaves_skill_fields_empty_without_throwing()
    {
        var snapshot = DoctorSnapshotBuilder.Build(
            Path.Combine(_root, "does-not-exist"), _ => null, () => StubCarrier());

        Assert.Empty(snapshot.InstalledSkills);
        Assert.Null(snapshot.ProjectResolvedFamilyVersion);
    }

    private void WriteUnder(string relativeDir, string fileName)
    {
        var dir = Path.Combine(_root, relativeDir);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, fileName), "content");
    }
}
