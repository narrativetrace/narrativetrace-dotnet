// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Build;
using Xunit;

namespace Build.Tests;

/// <summary>
/// Guards the project selection behind the <c>Test</c> and <c>Coverage</c>
/// targets. A test project that falls out of that selection does not go red —
/// it stops running, silently, and whatever it enforced stops being enforced.
/// Only a check that looks at the repository can notice.
/// </summary>
public sealed class TestProjectSelectionTests : IDisposable
{
    private readonly string _repo = Directory.CreateTempSubdirectory("nt-test-selection").FullName;

    public void Dispose() => Directory.Delete(_repo, recursive: true);

    /// <summary>Writes a project file under <c>tests/</c> of the throwaway repository.</summary>
    private void WriteTestProject(string name, string extension = ".csproj")
    {
        var directory = Path.Combine(_repo, "tests", name);
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, name + extension), "<Project />\n");
    }

    /// <summary>Writes a solution listing exactly the named project files.</summary>
    private void WriteSolution(params string[] projectFileNames) =>
        File.WriteAllText(
            Path.Combine(_repo, "NarrativeTrace.sln"),
            string.Join("\n", projectFileNames) + "\n");

    [Fact]
    public void Every_project_under_tests_joins_the_test_and_coverage_sweeps()
    {
        var skipped = TestProjectSelection.Check(RepositoryPath.Root());

        Assert.Empty(skipped);
    }

    [Fact]
    public void Project_whose_name_lacks_the_suffix_is_reported_as_skipped()
    {
        WriteTestProject("NarrativeTrace.ArchChecks");
        WriteSolution("NarrativeTrace.ArchChecks.csproj");

        var skipped = TestProjectSelection.Check(_repo);

        var problem = Assert.Single(skipped);
        Assert.Contains("NarrativeTrace.ArchChecks", problem, StringComparison.Ordinal);
        Assert.Contains("does not end in", problem, StringComparison.Ordinal);
    }

    [Fact]
    public void Correctly_named_project_missing_from_the_solution_is_reported()
    {
        WriteTestProject("NarrativeTrace.Orphan.Tests");
        WriteSolution("NarrativeTrace.Core.Tests.csproj");

        var skipped = TestProjectSelection.Check(_repo);

        var problem = Assert.Single(skipped);
        Assert.Contains("NarrativeTrace.Orphan.Tests", problem, StringComparison.Ordinal);
        Assert.Contains("NarrativeTrace.sln", problem, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(".csproj")]
    [InlineData(".fsproj")]
    public void A_named_and_listed_project_of_either_language_is_selected(string extension)
    {
        WriteTestProject("NarrativeTrace.Sample.Tests", extension);
        WriteSolution("NarrativeTrace.Sample.Tests" + extension);

        Assert.Empty(TestProjectSelection.Check(_repo));
    }

    [Fact]
    public void A_repository_without_a_tests_directory_has_nothing_to_report()
    {
        WriteSolution();

        Assert.Empty(TestProjectSelection.Check(_repo));
    }
}
