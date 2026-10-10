// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using Xunit;

namespace NarrativeTrace.Core.Tests;

/// <summary>
/// The test runner (<c>dotnet</c>'s test verb) runs a test assembly with its working directory at the build output
/// (<c>bin/&lt;configuration&gt;/&lt;tfm&gt;</c>), so a relative approved-trace directory read against
/// that directory would put the committed baselines inside <c>bin/</c> — wiped by a clean, never
/// committed, and not where the doctor looks. A relative directory therefore resolves against the
/// nearest directory, from the run directory up, that holds a project file.
/// </summary>
public sealed class ApprovedDirectoryTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "nt-approved-dir-" + Guid.NewGuid().ToString("N"));

    public ApprovedDirectoryTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private static TestArtifactSettings Settings(string approvedDir) =>
        new(true, TestArtifactSettings.DefaultDirectory, TraceArtifactFormat.Markdown, default, true, approvedDir);

    private string Dir(params string[] parts)
    {
        var path = Path.Combine([_root, .. parts]);
        Directory.CreateDirectory(path);
        return path;
    }

    [Theory]
    [InlineData("App.Tests.csproj")]
    [InlineData("App.Tests.fsproj")]
    [InlineData("App.Tests.vbproj")]
    public void A_relative_directory_resolves_beside_the_test_project_not_inside_bin(string projectFile)
    {
        File.WriteAllText(Path.Combine(Dir("App.Tests"), projectFile), "<Project />");
        var run = Dir("App.Tests", "bin", "Debug", "net10.0");

        Assert.Equal(
            Path.Combine(_root, "App.Tests", "narratives"),
            Settings("narratives").ApprovedDirectoryFrom(run));
    }

    [Fact]
    public void A_run_directory_that_holds_the_project_is_its_own_project_directory()
    {
        var project = Dir("App.Tests");
        File.WriteAllText(Path.Combine(project, "App.Tests.csproj"), "<Project />");

        Assert.Equal(Path.Combine(project, "baselines"), Settings("baselines").ApprovedDirectoryFrom(project));
    }

    [Fact]
    public void An_absolute_directory_is_used_as_given()
    {
        var absolute = Dir("elsewhere");

        Assert.Equal(absolute, Settings(absolute).ApprovedDirectoryFrom(Dir("App.Tests", "bin")));
    }

    [Fact]
    public void Without_a_project_file_in_reach_the_run_directory_is_used()
    {
        var run = Dir("a", "b", "c", "d", "e", "f", "g");
        File.WriteAllText(Path.Combine(_root, "Far.csproj"), "<Project />");

        Assert.Equal(Path.Combine(run, "narratives"), Settings("narratives").ApprovedDirectoryFrom(run));
    }

    [Fact]
    public void A_project_like_name_that_is_not_a_project_file_does_not_count()
    {
        var project = Dir("App.Tests");
        File.WriteAllText(Path.Combine(project, "App.Tests.csproj.bak"), "x");
        Dir("App.Tests", "Fake.csproj");
        var run = Dir("App.Tests", "bin");

        Assert.Equal(Path.Combine(run, "narratives"), Settings("narratives").ApprovedDirectoryFrom(run));
    }

    [Fact]
    public void A_missing_run_directory_is_refused()
    {
        Assert.Throws<ArgumentException>(() => Settings("narratives").ApprovedDirectoryFrom(" "));
    }
}
