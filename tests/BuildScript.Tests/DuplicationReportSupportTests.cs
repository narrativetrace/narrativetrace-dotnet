// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Build;
using Xunit;

namespace Build.Tests;

/// <summary>
/// Covers <see cref="DuplicationReportSupport"/> against jscpd's own JSON report shape (confirmed
/// field-for-field against a real <c>npx jscpd@5.2.0</c> run over a fixture pair — see
/// documentation/duplication.md) and the pure aggregation/union logic that shape feeds.
/// </summary>
public sealed class DuplicationReportSupportTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("nt-duplication-report").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private static DuplicationCluster Cluster(int tokens, params string[] files) => new(
        Tokens: tokens,
        Lines: tokens / 4,
        Occurrences: files.Select(f => new DuplicationOccurrence(f, 10, 10 + tokens)).ToList());

    // ---- ParseJscpdReport (real jscpd JSON shape) -----------------------------------------------

    [Fact]
    public void ParseJscpdReport_reads_clusters_and_the_tools_own_total_token_count()
    {
        const string json = """
        {
          "duplicates": [
            {
              "format": "csharp",
              "lines": 13,
              "tokens": 44,
              "firstFile": {"name": "a/Foo.cs", "start": 1, "end": 14},
              "secondFile": {"name": "b/Bar.cs", "start": 1, "end": 14}
            }
          ],
          "statistics": {
            "total": {"tokens": 88, "lines": 28, "duplicatedTokens": 88, "duplicatedLines": 13}
          }
        }
        """;

        var (tokensTotal, clusters) = DuplicationReportSupport.ParseJscpdReport(json);

        Assert.Equal(88, tokensTotal);
        var cluster = Assert.Single(clusters);
        Assert.Equal(44, cluster.Tokens);
        Assert.Equal(13, cluster.Lines);
        Assert.Equal(2, cluster.Occurrences.Count);
        Assert.Equal("a/Foo.cs", cluster.Occurrences[0].File);
        Assert.Equal(1, cluster.Occurrences[0].StartLine);
        Assert.Equal(14, cluster.Occurrences[0].EndLine);
        Assert.Equal("b/Bar.cs", cluster.Occurrences[1].File);
    }

    [Fact]
    public void ParseJscpdReport_of_zero_duplicates_is_an_empty_cluster_list_not_an_error()
    {
        const string json = """{"duplicates": [], "statistics": {"total": {"tokens": 120}}}""";

        var (tokensTotal, clusters) = DuplicationReportSupport.ParseJscpdReport(json);

        Assert.Equal(120, tokensTotal);
        Assert.Empty(clusters);
    }

    [Fact]
    public void ParseJscpdReport_normalizes_backslashes_in_windows_style_paths()
    {
        const string json = """
        {
          "duplicates": [
            {
              "lines": 10, "tokens": 61,
              "firstFile": {"name": "src\\A.cs", "start": 1, "end": 10},
              "secondFile": {"name": "src\\B.cs", "start": 1, "end": 10}
            }
          ],
          "statistics": {"total": {"tokens": 200}}
        }
        """;

        var (_, clusters) = DuplicationReportSupport.ParseJscpdReport(json);

        Assert.Equal("src/A.cs", clusters[0].Occurrences[0].File);
    }

    // ---- UnionDuplicatedTokens (never tokens × occurrences) -------------------------------------

    [Fact]
    public void UnionDuplicatedTokens_of_disjoint_files_sums_every_occurrences_own_span()
    {
        // Each side of a clone is real duplicated content on its own — a's copy AND b's copy both
        // count — so a plain two-file, non-overlapping pair contributes 2 × its token count, and two
        // such clusters sum plainly: (60 + 60) + (80 + 80) = 280. The union only ever removes a
        // recount of the exact same (file, span) pair; it never halves an otherwise-disjoint clone.
        var clusters = new[] { Cluster(60, "a/X.cs", "b/Y.cs"), Cluster(80, "c/Z.cs", "d/W.cs") };
        Assert.Equal(280, DuplicationReportSupport.UnionDuplicatedTokens(clusters));
    }

    [Fact]
    public void UnionDuplicatedTokens_credits_the_same_repeated_span_once()
    {
        // The exact scenario a naive per-cluster sum gets wrong: three near-identical files produce
        // two pairwise clusters (a-b, a-c), both anchored on a's identical 1..14 span. Summing both
        // clusters' tokens would credit a's own 44 tokens twice; the union must not.
        var clusters = new[]
        {
            new DuplicationCluster(44, 13, new[]
            {
                new DuplicationOccurrence("a/Foo.cs", 1, 14), new DuplicationOccurrence("b/Bar.cs", 1, 14)
            }),
            new DuplicationCluster(44, 13, new[]
            {
                new DuplicationOccurrence("a/Foo.cs", 1, 14), new DuplicationOccurrence("c/Baz.cs", 1, 14)
            }),
        };

        // a: one 44-token span (shared by both clusters, counted once) = 44
        // b: one 44-token span = 44
        // c: one 44-token span = 44
        Assert.Equal(132, DuplicationReportSupport.UnionDuplicatedTokens(clusters));
    }

    [Fact]
    public void UnionDuplicatedTokens_of_an_overlapping_in_file_run_takes_the_largest_not_the_sum()
    {
        var clusters = new[]
        {
            Cluster(200, "a/Big.cs", "b/Big.cs"), // a/Big.cs lines 10..210
            new DuplicationCluster(60, 15, new[]
            {
                new DuplicationOccurrence("a/Big.cs", 50, 65), // nested inside the 200-token span
                new DuplicationOccurrence("c/Small.cs", 10, 25)
            }),
        };

        // a/Big.cs: one merged run (10..210 overlapping 50..65) credited at its largest, 200.
        // b/Big.cs: 200. c/Small.cs: 60. Never 200 + 60 + 200 + 60.
        Assert.Equal(460, DuplicationReportSupport.UnionDuplicatedTokens(clusters));
    }

    [Fact]
    public void UnionDuplicatedTokens_of_no_clusters_is_zero()
    {
        Assert.Equal(0, DuplicationReportSupport.UnionDuplicatedTokens(Array.Empty<DuplicationCluster>()));
    }

    // ---- Aggregate -------------------------------------------------------------------------------

    [Fact]
    public void Aggregate_computes_percent_and_sorts_clusters_by_tokens_descending()
    {
        var result = DuplicationReportSupport.Aggregate(
            tokensTotal: 1000, tokensDuplicated: 360, clusters: new[] { Cluster(60, "a", "b"), Cluster(120, "c", "d") });

        Assert.Equal(1000, result.TokensTotal);
        Assert.Equal(360, result.TokensDuplicated);
        Assert.Equal(36.0, result.Percent, precision: 3);
        Assert.Equal([120, 60], result.Clusters.Select(c => c.Tokens));
    }

    [Fact]
    public void Aggregate_of_zero_total_tokens_is_zero_percent_not_a_divide_by_zero()
    {
        var result = DuplicationReportSupport.Aggregate(0, 0, Array.Empty<DuplicationCluster>());
        Assert.Equal(0.0, result.Percent);
        Assert.Empty(result.Clusters);
    }

    [Fact]
    public void Aggregate_rounds_percent_to_one_decimal()
    {
        var result = DuplicationReportSupport.Aggregate(3, 1, new[] { Cluster(1, "a", "b") });
        Assert.Equal(33.3, result.Percent, precision: 4);
    }

    // ---- WriteJson / ReadJson ----------------------------------------------------------------

    [Fact]
    public void WriteJson_then_ReadJson_round_trips()
    {
        var scan = new DuplicationScanResult(
            MinTokens: 60,
            Main: DuplicationReportSupport.Aggregate(1000, 284, new[] { Cluster(142, "a", "b") }),
            Test: DuplicationReportSupport.Aggregate(2000, 280, new[] { Cluster(80, "a", "b"), Cluster(60, "c", "d") }));
        var path = Path.Combine(_dir, "duplication.json");

        DuplicationReportSupport.WriteJson(scan, path);
        var text = File.ReadAllText(path);
        Assert.Contains("\"tool\":\"jscpd\"", text);
        Assert.Contains("\"language\":\"csharp\"", text);
        Assert.Contains("\"minTokens\":60", text);

        var roundTripped = DuplicationReportSupport.ReadJson(path);
        Assert.Equal(scan.MinTokens, roundTripped.MinTokens);
        Assert.Equal(scan.Main.TokensTotal, roundTripped.Main.TokensTotal);
        Assert.Equal(scan.Main.Percent, roundTripped.Main.Percent, precision: 3);
        Assert.Equal(scan.Main.Clusters.Select(c => c.Tokens), roundTripped.Main.Clusters.Select(c => c.Tokens));
        Assert.Equal(scan.Main.Clusters[0].Occurrences, roundTripped.Main.Clusters[0].Occurrences);
        Assert.Equal(scan.Test.Clusters.Count, roundTripped.Test.Clusters.Count);
    }

    [Fact]
    public void WriteJson_escapes_quotes_and_backslashes_in_paths()
    {
        var cluster = new DuplicationCluster(61, 10, new[] { new DuplicationOccurrence("weird\"path\\file.cs", 1, 5) });
        var scan = new DuplicationScanResult(
            MinTokens: 60,
            Main: DuplicationReportSupport.Aggregate(100, 61, new[] { cluster }),
            Test: DuplicationReportSupport.Aggregate(0, 0, Array.Empty<DuplicationCluster>()));
        var path = Path.Combine(_dir, "duplication.json");

        DuplicationReportSupport.WriteJson(scan, path);
        var roundTripped = DuplicationReportSupport.ReadJson(path);

        Assert.Equal("weird\"path\\file.cs", roundTripped.Main.Clusters[0].Occurrences[0].File);
    }

    // ---- SummaryLine -----------------------------------------------------------------------------

    [Fact]
    public void SummaryLine_names_the_largest_main_cluster_and_marks_test_ungated()
    {
        var scan = new DuplicationScanResult(
            MinTokens: 60,
            Main: DuplicationReportSupport.Aggregate(1000, 284, new[] { Cluster(142, "module1/src/Thing.cs", "module2/src/Thing.cs") }),
            Test: DuplicationReportSupport.Aggregate(1000, 120, new[] { Cluster(60, "a", "b") }));

        var line = DuplicationReportSupport.SummaryLine(scan);

        Assert.StartsWith("duplication: main ", line);
        Assert.Contains("142 tokens", line);
        Assert.Contains("module1/src/Thing.cs:10", line);
        Assert.Contains("reported, not gated", line);
    }

    [Fact]
    public void SummaryLine_handles_no_clusters_at_all()
    {
        var scan = new DuplicationScanResult(
            MinTokens: 60,
            Main: DuplicationReportSupport.Aggregate(1000, 0, Array.Empty<DuplicationCluster>()),
            Test: DuplicationReportSupport.Aggregate(1000, 0, Array.Empty<DuplicationCluster>()));

        var line = DuplicationReportSupport.SummaryLine(scan);

        Assert.Contains("no clusters", line);
        Assert.Contains("0.0% of tokens in 0 clusters", line);
    }

    // ---- JscpdArguments ---------------------------------------------------------------------------

    [Fact]
    public void JscpdArguments_ignores_identifiers_and_literals_and_floors_only_on_tokens()
    {
        var args = DuplicationReportSupport.JscpdArguments("src/**/*.cs", 60, "artifacts/duplication/main");

        Assert.Contains("--ignore-identifiers", args);
        Assert.Contains("--ignore-literals", args);
        Assert.Contains("--min-lines", args);
        Assert.Equal("1", args[args.ToList().IndexOf("--min-lines") + 1]);
        Assert.Contains("60", args);
        Assert.Contains("src/**/*.cs", args);
    }

    [Fact]
    public void JscpdArguments_excludes_build_output_explicitly_not_via_gitignore()
    {
        // jscpd's own .gitignore respect only fires with a real .git directory at the scan root —
        // absent from the publish script's git-archive snapshot, where bin/**/obj/** generated
        // sources would otherwise silently join the scan. An explicit --ignore glob keeps the
        // exclusion true regardless of whether a .git directory happens to be present.
        var args = DuplicationReportSupport.JscpdArguments("src/**/*.cs", 60, "artifacts/duplication/main");

        var list = args.ToList();
        Assert.Contains("--ignore", list);
        Assert.Equal("**/bin/**,**/obj/**", list[list.IndexOf("--ignore") + 1]);
    }
}
