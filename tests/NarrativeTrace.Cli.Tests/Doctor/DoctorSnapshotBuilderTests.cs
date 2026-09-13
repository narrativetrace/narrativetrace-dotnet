// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Cli.Doctor;
using Xunit;

namespace NarrativeTrace.Cli.Tests.Doctor;

public sealed class DoctorSnapshotBuilderTests : IDisposable
{
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

    private void WriteUnder(string relativeDir, string fileName)
    {
        var dir = Path.Combine(_root, relativeDir);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, fileName), "content");
    }
}
