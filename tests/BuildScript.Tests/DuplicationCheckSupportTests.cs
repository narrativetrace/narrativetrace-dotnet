// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Build;
using Xunit;

namespace Build.Tests;

/// <summary>Covers <see cref="DuplicationCheckSupport"/>'s ratchet: baseline/exemption parsing and the decide() logic.</summary>
public sealed class DuplicationCheckSupportTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("nt-duplication-check").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private static DuplicationCluster Cluster(int tokens, params string[] files) => new(
        Tokens: tokens,
        Lines: tokens / 4,
        Occurrences: files.Select(f => new DuplicationOccurrence(f, 10, 10 + tokens)).ToList());

    private static DuplicationTreeResult Tree(double percent, IReadOnlyList<DuplicationCluster> clusters) =>
        new(TokensTotal: 1000, TokensDuplicated: 0, Percent: percent, Clusters: clusters);

    private static readonly DuplicationBaseline Baseline = new(MainPercent: 3.0, MainLargestCluster: 100, Recorded: "x", Commit: "y");

    // ---- baseline.properties -----------------------------------------------------------------

    [Fact]
    public void ReadBaseline_parses_all_fields()
    {
        var path = Path.Combine(_dir, "baseline.properties");
        File.WriteAllText(path, """
            recorded=2026-09-12
            commit=2026-09-12-first-scan
            main.percent=3.1
            main.largestCluster=142
            """);

        var baseline = DuplicationCheckSupport.ReadBaseline(path);

        Assert.Equal(3.1, baseline.MainPercent, precision: 3);
        Assert.Equal(142, baseline.MainLargestCluster);
        Assert.Equal("2026-09-12", baseline.Recorded);
        Assert.Equal("2026-09-12-first-scan", baseline.Commit);
    }

    [Fact]
    public void ReadBaseline_missing_file_throws() =>
        Assert.Throws<ArgumentException>(() => DuplicationCheckSupport.ReadBaseline(Path.Combine(_dir, "missing.properties")));

    [Fact]
    public void ReadBaseline_missing_required_key_throws()
    {
        var path = Path.Combine(_dir, "baseline.properties");
        File.WriteAllText(path, "recorded=2026-09-12\ncommit=abc\nmain.percent=3.1\n");
        Assert.Throws<ArgumentException>(() => DuplicationCheckSupport.ReadBaseline(path));
    }

    // ---- exemptions.txt ------------------------------------------------------------------------

    [Fact]
    public void ReadExemptions_parses_reasoned_pairs()
    {
        var path = Path.Combine(_dir, "exemptions.txt");
        File.WriteAllText(path, """
            # deliberate flat/structured renderer twins
            src/NarrativeTrace.Core/**/ValueRenderer.cs :: src/NarrativeTrace.Core/**/StructuredValueRenderer.cs

            # hostile corpus builders share fixture scaffolding on purpose
            tests/NarrativeTrace.SecurityTests/**/*CorpusBuilder.cs :: tests/NarrativeTrace.SecurityTests/**/*CorpusBuilder.cs
            """);

        var exemptions = DuplicationCheckSupport.ReadExemptions(path);

        Assert.Equal(2, exemptions.Count);
        Assert.Equal("deliberate flat/structured renderer twins", exemptions[0].Reason);
        Assert.EndsWith("ValueRenderer.cs", exemptions[0].GlobA);
        Assert.EndsWith("StructuredValueRenderer.cs", exemptions[0].GlobB);
    }

    [Fact]
    public void ReadExemptions_missing_file_returns_empty() =>
        Assert.Empty(DuplicationCheckSupport.ReadExemptions(Path.Combine(_dir, "missing.txt")));

    [Fact]
    public void ReadExemptions_pair_without_reason_throws()
    {
        var path = Path.Combine(_dir, "exemptions.txt");
        File.WriteAllText(path, "a/File.cs :: b/File.cs\n");
        Assert.Throws<ArgumentException>(() => DuplicationCheckSupport.ReadExemptions(path));
    }

    [Fact]
    public void ReadExemptions_malformed_pair_throws()
    {
        var path = Path.Combine(_dir, "exemptions.txt");
        File.WriteAllText(path, "# reason\nonly-one-glob.cs\n");
        Assert.Throws<ArgumentException>(() => DuplicationCheckSupport.ReadExemptions(path));
    }

    [Fact]
    public void ReadExemptions_concatenates_a_multi_line_reason()
    {
        var path = Path.Combine(_dir, "exemptions.txt");
        File.WriteAllText(path, "# first line\n# second line\na/File.cs :: b/File.cs\n");

        var exemptions = DuplicationCheckSupport.ReadExemptions(path);

        Assert.Equal("first line second line", exemptions[0].Reason);
    }

    // ---- IsExempt --------------------------------------------------------------------------------

    [Fact]
    public void IsExempt_matches_either_order_of_the_pair()
    {
        var exemption = new DuplicationExemption("a/*.cs", "b/*.cs", "twins");
        var clusterAB = Cluster(80, "a/X.cs", "b/Y.cs");
        var clusterBA = Cluster(80, "b/Y.cs", "a/X.cs");

        Assert.True(DuplicationCheckSupport.IsExempt(clusterAB, [exemption]));
        Assert.True(DuplicationCheckSupport.IsExempt(clusterBA, [exemption]));
    }

    [Fact]
    public void IsExempt_false_when_no_occurrence_matches()
    {
        var exemption = new DuplicationExemption("a/*.cs", "b/*.cs", "twins");
        Assert.False(DuplicationCheckSupport.IsExempt(Cluster(80, "c/X.cs", "d/Y.cs"), [exemption]));
    }

    [Fact]
    public void IsExempt_false_for_a_three_way_cluster_with_one_uncovered_occurrence()
    {
        var exemption = new DuplicationExemption("a/*.cs", "b/*.cs", "twins");
        Assert.False(DuplicationCheckSupport.IsExempt(Cluster(80, "a/X.cs", "b/Y.cs", "c/Z.cs"), [exemption]));
    }

    // ---- Decide ------------------------------------------------------------------------------

    [Fact]
    public void Decide_passes_when_within_tolerance_and_no_larger_cluster()
    {
        var result = DuplicationCheckSupport.Decide(Tree(3.2, [Cluster(90, "a/X.cs", "b/Y.cs")]), Baseline, []);
        Assert.True(result.Passed);
    }

    [Fact]
    public void Decide_fails_when_percent_rises_past_tolerance()
    {
        var result = DuplicationCheckSupport.Decide(Tree(3.4, []), Baseline, []);
        Assert.False(result.Passed);
        Assert.Contains("rose to", result.Message);
    }

    [Fact]
    public void Decide_passes_at_exactly_the_tolerance()
    {
        var result = DuplicationCheckSupport.Decide(Tree(3.3, []), Baseline, []);
        Assert.True(result.Passed);
    }

    [Fact]
    public void Decide_fails_on_a_new_cluster_larger_than_baseline()
    {
        var result = DuplicationCheckSupport.Decide(Tree(3.0, [Cluster(150, "a/X.cs", "b/Y.cs")]), Baseline, []);
        Assert.False(result.Passed);
        Assert.Contains("new cluster 150 tokens", result.Message);
    }

    [Fact]
    public void Decide_passes_when_the_larger_cluster_is_exempted()
    {
        var result = DuplicationCheckSupport.Decide(
            Tree(3.0, [Cluster(150, "a/X.cs", "b/Y.cs")]), Baseline, [new DuplicationExemption("a/*.cs", "b/*.cs", "deliberate twins")]);
        Assert.True(result.Passed);
    }

    [Fact]
    public void Decide_never_considers_the_test_tree()
    {
        // decide() only ever receives the main tree — the signature itself enforces "tests never
        // gate"; this documents that a huge test-tree percentage cannot even be passed in.
        var result = DuplicationCheckSupport.Decide(Tree(3.0, []), Baseline, []);
        Assert.True(result.Passed);
    }
}
