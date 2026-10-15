// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Build;
using Xunit;

namespace Build.Tests;

/// <summary>
/// Covers <see cref="RecipeArtifactsSupport"/> — the pure half of the <c>RecipeArtifacts</c>
/// target that runs the recipe fixtures and keeps only runs whose verdict is the one a page claims.
/// </summary>
public sealed class RecipeArtifactsSupportTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("nt-recipes").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string WriteTrx(string results)
    {
        var path = Path.Combine(_dir, "run.trx");
        File.WriteAllText(path,
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>"
            + "<TestRun xmlns=\"http://microsoft.com/schemas/VisualStudio/TeamTest/2010\"><Results>"
            + results + "</Results></TestRun>");
        return path;
    }

    [Fact]
    public void FailureMessages_keys_each_failed_test_by_its_name_and_skips_passes()
    {
        var trx = WriteTrx(
            "<UnitTestResult testName=\"A.Passes\" outcome=\"Passed\" />"
            + "<UnitTestResult testName=\"A.Fails\" outcome=\"Failed\"><Output><ErrorInfo>"
            + "<Message>Expected: 1\nActual:   2</Message></ErrorInfo></Output></UnitTestResult>");

        var messages = TrxSupport.FailureMessages(trx);

        Assert.Equal("Expected: 1\nActual:   2", Assert.Single(messages).Value);
        Assert.Equal("A.Fails", Assert.Single(messages).Key);
    }

    [Fact]
    public void FailureMessages_normalises_windows_line_endings()
    {
        var trx = WriteTrx(
            "<UnitTestResult testName=\"A.Fails\" outcome=\"Failed\"><Output><ErrorInfo>"
            + "<Message>one&#13;\ntwo</Message></ErrorInfo></Output></UnitTestResult>");

        Assert.Equal("one\ntwo", TrxSupport.FailureMessages(trx)["A.Fails"]);
    }

    [Fact]
    public void Verdict_problems_are_empty_when_exactly_the_expected_tests_failed()
    {
        var results = new[]
        {
            new TrxTestResult("C", "C.Good", "Passed", 0),
            new TrxTestResult("C", "C.Bad", "Failed", 0),
        };

        Assert.Empty(RecipeArtifactsSupport.VerdictProblems("slug", results, ["C.Bad"]));
    }

    [Fact]
    public void Verdict_problems_name_an_expected_failure_that_passed()
    {
        var results = new[] { new TrxTestResult("C", "C.Bad", "Passed", 0) };

        var problem = Assert.Single(RecipeArtifactsSupport.VerdictProblems("slug", results, ["C.Bad"]));

        Assert.Contains("C.Bad", problem, StringComparison.Ordinal);
        Assert.Contains("should have failed", problem, StringComparison.Ordinal);
    }

    [Fact]
    public void Verdict_problems_name_an_unexpected_failure()
    {
        var results = new[] { new TrxTestResult("C", "C.Good", "Failed", 0) };

        var problem = Assert.Single(RecipeArtifactsSupport.VerdictProblems("slug", results, []));

        Assert.Contains("C.Good", problem, StringComparison.Ordinal);
        Assert.Contains("should have passed", problem, StringComparison.Ordinal);
    }

    [Fact]
    public void Verdict_problems_flag_a_run_that_executed_nothing()
    {
        var problem = Assert.Single(RecipeArtifactsSupport.VerdictProblems("slug", [], []));

        Assert.Contains("no test ran", problem, StringComparison.Ordinal);
    }

    [Fact]
    public void Repository_paths_are_made_relative_in_a_captured_message()
    {
        var message = "Received: /work/repo/artifacts/x.received.nt\nand /work/repository/other";

        var relative = RecipeArtifactsSupport.RelativizePaths(message, "/work/repo");

        Assert.Equal("Received: artifacts/x.received.nt\nand /work/repository/other", relative);
    }

    [Fact]
    public void Windows_separators_in_a_repository_path_become_forward_slashes()
    {
        var relative = RecipeArtifactsSupport.RelativizePaths(@"Received: C:\repo\artifacts\x.nt", @"C:\repo");

        Assert.Equal("Received: artifacts/x.nt", relative);
    }
}
