// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using Xunit;

namespace NarrativeTrace.Core.Tests;

/// <summary>
/// Adversarial probes of <see cref="TestArtifactSettings.ApprovedDirectoryFrom"/>: the nearest project
/// file search, its five-level bound, and the shapes of run directory a test run can present.
/// </summary>
public sealed class AdversarialPhase7ApprovedDirTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "nt-approved-dir-" + Guid.NewGuid().ToString("N"));

    public AdversarialPhase7ApprovedDirTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static TestArtifactSettings Settings(string approvedDir = "narratives") =>
        new(true, "TestResults/narrativetrace", TraceArtifactFormat.Markdown, ApprovedDir: approvedDir);

    private string Dir(params string[] parts)
    {
        var path = Path.Combine([_root, .. parts]);
        Directory.CreateDirectory(path);
        return path;
    }

    [Fact]
    public void Relative_approved_dir_resolves_beside_the_project_file_above_a_bin_run_directory()
    {
        var project = Dir("app");
        File.WriteAllText(Path.Combine(project, "app.csproj"), "<Project/>");
        var run = Dir("app", "bin", "Debug", "net10.0");

        Assert.Equal(Path.Combine(project, "narratives"), Settings().ApprovedDirectoryFrom(run));
    }

    [Theory]
    [InlineData("app.csproj")]
    [InlineData("app.fsproj")]
    [InlineData("app.vbproj")]
    public void Each_project_flavour_is_recognised_as_the_project_directory(string projectFile)
    {
        var project = Dir("lang");
        File.WriteAllText(Path.Combine(project, projectFile), "<Project/>");
        var run = Dir("lang", "bin");

        Assert.Equal(Path.Combine(project, "narratives"), Settings().ApprovedDirectoryFrom(run));
    }

    [Fact]
    public void Project_file_exactly_five_levels_above_the_run_directory_is_found()
    {
        var project = Dir("depth5");
        File.WriteAllText(Path.Combine(project, "app.csproj"), "<Project/>");
        var run = Dir("depth5", "b", "c", "d", "e", "f");

        Assert.Equal(Path.Combine(project, "narratives"), Settings().ApprovedDirectoryFrom(run));
    }

    [Fact]
    public void Project_file_six_levels_above_the_run_directory_is_out_of_reach_and_the_run_directory_is_used()
    {
        var project = Dir("depth6");
        File.WriteAllText(Path.Combine(project, "app.csproj"), "<Project/>");
        var run = Dir("depth6", "b", "c", "d", "e", "f", "g");

        Assert.Equal(Path.Combine(run, "narratives"), Settings().ApprovedDirectoryFrom(run));
    }

    [Fact]
    public void A_nearer_project_file_wins_over_an_outer_one()
    {
        var outer = Dir("outer");
        File.WriteAllText(Path.Combine(outer, "outer.csproj"), "<Project/>");
        var inner = Dir("outer", "inner");
        File.WriteAllText(Path.Combine(inner, "inner.csproj"), "<Project/>");
        var run = Dir("outer", "inner", "bin");

        Assert.Equal(Path.Combine(inner, "narratives"), Settings().ApprovedDirectoryFrom(run));
    }

    [Fact]
    public void A_run_directory_that_does_not_exist_resolves_from_the_nearest_existing_project_above()
    {
        var project = Dir("ghost");
        File.WriteAllText(Path.Combine(project, "app.csproj"), "<Project/>");
        var missing = Path.Combine(project, "bin", "never-created");

        Assert.Equal(Path.Combine(project, "narratives"), Settings().ApprovedDirectoryFrom(missing));
    }

    [Fact]
    public void A_directory_named_like_a_project_file_is_not_a_project_file()
    {
        var trap = Dir("trap");
        Dir("trap", "x.csproj");
        var run = Dir("trap", "bin");

        Assert.Equal(Path.Combine(run, "narratives"), Settings().ApprovedDirectoryFrom(run));
    }

    [Fact]
    public void A_dot_dot_in_the_approved_dir_still_names_the_same_place_as_a_sibling_of_the_project()
    {
        var project = Dir("dots");
        File.WriteAllText(Path.Combine(project, "app.csproj"), "<Project/>");
        var run = Dir("dots", "bin");

        var resolved = Settings("../shared").ApprovedDirectoryFrom(run);

        Assert.Equal(Path.Combine(_root, "shared"), Path.GetFullPath(resolved));
    }

    [Fact]
    public void An_absolute_approved_dir_is_returned_as_given_regardless_of_the_run_directory()
    {
        var absolute = Path.Combine(_root, "absolute-approved");
        var run = Dir("anywhere");

        Assert.Equal(absolute, Settings(absolute).ApprovedDirectoryFrom(run));
    }

    [Fact]
    public void A_run_directory_given_with_a_trailing_separator_resolves_the_same_as_without()
    {
        var project = Dir("slash");
        File.WriteAllText(Path.Combine(project, "app.csproj"), "<Project/>");
        var run = Dir("slash", "bin");

        Assert.Equal(
            Settings().ApprovedDirectoryFrom(run),
            Settings().ApprovedDirectoryFrom(run + Path.DirectorySeparatorChar));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_blank_run_directory_is_rejected(string runDirectory)
    {
        Assert.Throws<ArgumentException>(() => Settings().ApprovedDirectoryFrom(runDirectory));
    }

    [Fact]
    public void A_null_run_directory_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => Settings().ApprovedDirectoryFrom(null!));
    }
}
