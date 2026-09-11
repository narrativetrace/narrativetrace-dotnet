// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Build;
using Xunit;

namespace Build.Tests;

/// <summary>
/// Covers <see cref="TrxSupport"/> — the VSTest <c>.trx</c> reader <c>VerifyAll</c> uses to slice
/// one <c>./build.sh Test</c> sweep into the schema's <c>unit-tests</c>/<c>property</c>/
/// <c>fuzz-tier-a</c>/<c>architecture</c>/<c>conformance</c>/<c>stress-short</c> rows.
/// </summary>
public sealed class TrxSupportTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("nt-trx").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private const string TrxTemplate = """
        <?xml version="1.0" encoding="utf-8"?>
        <TestRun id="a" name="run" xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
          <Results>
            <UnitTestResult testId="id-1" testName="My.Namespace.FooTests.Passes" outcome="Passed" duration="00:00:01.5000000" />
            <UnitTestResult testId="id-2" testName="My.Namespace.FooTests.Fails" outcome="Failed" duration="00:00:00.2500000" />
            <UnitTestResult testId="id-3" testName="My.Namespace.BarTests.Skipped" outcome="NotExecuted" duration="00:00:00.0000000" />
            <UnitTestResult testId="id-missing" testName="My.Namespace.BazTests.NoDefinition" outcome="Passed" duration="00:00:00.1000000" />
          </Results>
          <TestDefinitions>
            <UnitTest id="id-1"><TestMethod className="My.Namespace.FooTests" name="Passes" /></UnitTest>
            <UnitTest id="id-2"><TestMethod className="My.Namespace.FooTests" name="Fails" /></UnitTest>
            <UnitTest id="id-3"><TestMethod className="My.Namespace.BarTests" name="Skipped" /></UnitTest>
          </TestDefinitions>
        </TestRun>
        """;

    private string WriteTrx(string content)
    {
        var path = Path.Combine(_dir, "sample.trx");
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void ReadResults_resolves_class_name_from_TestDefinitions_by_testId()
    {
        var results = TrxSupport.ReadResults(WriteTrx(TrxTemplate));

        var passed = Assert.Single(results, r => r.TestName.EndsWith("Passes", StringComparison.Ordinal));
        Assert.Equal("My.Namespace.FooTests", passed.ClassName);
        Assert.True(passed.Passed);
        Assert.Equal(1.5, passed.DurationSeconds, precision: 3);
    }

    [Fact]
    public void ReadResults_falls_back_to_parsing_the_class_out_of_testName_when_no_definition_matches()
    {
        var results = TrxSupport.ReadResults(WriteTrx(TrxTemplate));

        var orphan = Assert.Single(results, r => r.TestName.EndsWith("NoDefinition", StringComparison.Ordinal));
        Assert.Equal("My.Namespace.BazTests", orphan.ClassName);
    }

    [Fact]
    public void Failed_and_Passed_are_mutually_exclusive_and_NotExecuted_is_neither()
    {
        var results = TrxSupport.ReadResults(WriteTrx(TrxTemplate));

        var skipped = Assert.Single(results, r => r.TestName.EndsWith("Skipped", StringComparison.Ordinal));
        Assert.False(skipped.Passed);
        Assert.False(skipped.Failed);
    }

    [Fact]
    public void Summarize_counts_passed_failed_skipped_and_distinct_classes()
    {
        var results = TrxSupport.ReadResults(WriteTrx(TrxTemplate));

        var metrics = TrxSupport.Summarize(results);

        Assert.Equal(2, metrics["tests_passed"]);
        Assert.Equal(1, metrics["tests_failed"]);
        Assert.Equal(1, metrics["tests_skipped"]);
        Assert.Equal(3, metrics["test_classes"]);
    }

    [Fact]
    public void AllGreen_is_true_only_when_every_result_passed()
    {
        var results = TrxSupport.ReadResults(WriteTrx(TrxTemplate));

        Assert.False(TrxSupport.AllGreen(results));
        Assert.True(TrxSupport.AllGreen(results.Where(r => r.Passed).ToList()));
    }

    [Fact]
    public void AllGreen_is_vacuously_true_for_an_empty_slice()
    {
        Assert.True(TrxSupport.AllGreen([]));
    }

    [Fact]
    public void TotalSeconds_sums_every_results_own_duration()
    {
        var results = TrxSupport.ReadResults(WriteTrx(TrxTemplate));

        Assert.Equal(1.85, TrxSupport.TotalSeconds(results), precision: 3);
    }

    [Fact]
    public void ReadByProject_returns_an_empty_list_for_a_project_whose_trx_file_is_absent()
    {
        var byProject = TrxSupport.ReadByProject(_dir, ["DoesNotExist"]);

        Assert.Empty(byProject["DoesNotExist"]);
    }

    [Fact]
    public void ReadByProject_reads_the_matching_file_by_project_name_and_suffix()
    {
        File.WriteAllText(Path.Combine(_dir, "MyProject.trx"), TrxTemplate);

        var byProject = TrxSupport.ReadByProject(_dir, ["MyProject"]);

        Assert.Equal(4, byProject["MyProject"].Count);
    }
}
