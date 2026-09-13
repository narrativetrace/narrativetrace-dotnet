// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace NarrativeTrace.Build;

/// <summary>One occurrence of a duplicated block, relative to the repository root (<c>src/…/File.cs</c>).</summary>
public sealed record DuplicationOccurrence(string File, int StartLine, int EndLine);

/// <summary>One jscpd clone: an identical (identifiers/literals ignored) token sequence found at every one of <see cref="Occurrences"/>.</summary>
public sealed record DuplicationCluster(int Tokens, int Lines, IReadOnlyList<DuplicationOccurrence> Occurrences);

/// <summary>One source tree's scan (main or test): jscpd's own token count plus this build's clusters/percent.</summary>
public sealed record DuplicationTreeResult(int TokensTotal, int TokensDuplicated, double Percent, IReadOnlyList<DuplicationCluster> Clusters);

/// <summary>The whole <c>duplication.json</c> document: the family-wide schema every NarrativeTrace runtime emits.</summary>
public sealed record DuplicationScanResult(int MinTokens, DuplicationTreeResult Main, DuplicationTreeResult Test);

/// <summary>
/// INTENT: Backs the root <c>DuplicationReport</c> Nuke target — runs jscpd (<c>npx jscpd@&lt;pinned&gt;</c>,
/// the Java runtime's PMD-CPD counterpart for a runtime with no JVM/PMD dependency) over a source tree and
/// normalizes its report into the family-wide <c>duplication.json</c> shape documented in
/// documentation/duplication.md. Identifiers and literals are ignored (<c>--ignore-identifiers
/// --ignore-literals</c> on the jscpd invocation Build.cs assembles from <see cref="JscpdArguments"/>) — jscpd
/// then finds *structural* duplication (the same shape with different names/values), not merely pasted text —
/// and results are ignored below a token floor.
///
/// The actual <c>npx jscpd</c> process invocation lives in Build.cs (<c>DuplicationReport</c>), matching the
/// <c>SecretsScan</c>/<c>Semgrep</c>/<c>OsvScan</c> convention of keeping a live external-tool run out of this
/// class: everything here is pure and unit-tested — <see cref="ParseJscpdReport"/>,
/// <see cref="UnionDuplicatedTokens"/>, <see cref="Aggregate"/>, <see cref="WriteJson"/>,
/// <see cref="ReadJson"/>, <see cref="SummaryLine"/> — the real tool is proven by the actual gate, not a unit
/// test that would just re-run it.
/// </summary>
public static class DuplicationReportSupport
{
    public const string Tool = "jscpd";
    public const string Language = "csharp";

    /// <summary>
    /// The <c>npx jscpd@version …</c> arguments for one tree (main or test), given a single glob
    /// [pattern] rooted at the repository root (e.g. <c>src/**/*.cs</c> or
    /// <c>{tests,benchmarks}/**/*.cs</c> — brace-expanded, since jscpd reports occurrence paths bare
    /// relative-to-each-PATH-argument the moment more than one PATH is passed on the command line;
    /// a single glob rooted at <c>.</c> is the only invocation shape that reports genuinely
    /// repository-root-relative paths, confirmed against a live run). Pure and unit-tested
    /// separately from the process invocation itself (Build.cs).
    /// </summary>
    /// <remarks>
    /// <c>--ignore</c> keeps <c>bin/</c>/<c>obj/</c> build output out of the scan explicitly, rather
    /// than leaning on jscpd's own <c>.gitignore</c> respect: that respect turned out to depend on an
    /// actual <c>.git</c> directory being present at the scan root, present in every developer
    /// checkout and CI clone but deliberately absent from the publish script's <c>git archive</c>
    /// snapshot — there, jscpd silently stopped excluding the still-current <c>obj/**/*.cs</c>
    /// generated sources Compile leaves behind earlier in the same <c>Verify</c> run, measuring real
    /// duplication in generator boilerplate that is not source at all and ratcheting the gate on it
    /// (confirmed live: 137 clusters/197 sources with a git repo present at the scan root, 165
    /// clusters/259 sources without one, identical tree otherwise). An explicit glob is a git-free,
    /// environment-independent floor under the same exclusion <see cref="HeaderAbsenceSupport"/>'s
    /// <c>ExcludedDirectories</c> already applies by walking the filesystem directly instead of
    /// asking a scanned-content tool to infer it.
    /// </remarks>
    public static IReadOnlyList<string> JscpdArguments(string pattern, int minTokens, string outputDir) => new[]
    {
        ".",
        "--pattern", pattern,
        "--format", Language,
        "--min-tokens", minTokens.ToString(CultureInfo.InvariantCulture),
        // jscpd's default --min-lines is 5 — an extra floor this build does not want: the ratchet's
        // only floor is tokens (documentation/duplication.md), so a match under 5 lines but over the
        // token floor must still count.
        "--min-lines", "1",
        "--ignore-identifiers",
        "--ignore-literals",
        "--ignore", "**/bin/**,**/obj/**",
        "--reporters", "json",
        "--output", outputDir,
        "--no-tips",
        "--silent"
    };

    // ---- jscpd's own JSON -> this build's normalized shape -------------------------------------

    /// <summary>
    /// Parses jscpd's own <c>jscpd-report.json</c> (the <c>duplicates[]</c>/<c>statistics</c> shape,
    /// version-stable across the 5.x line) into this build's <see cref="DuplicationCluster"/> list
    /// plus the tree's total token count (<c>statistics.total.tokens</c> — jscpd's own honest count
    /// of every token it scanned, not derived from the clusters). Every jscpd clone is exactly two
    /// occurrences (<c>firstFile</c>/<c>secondFile</c>) — pairwise, unlike PMD CPD's N-ary matches —
    /// so <see cref="DuplicationCluster.Occurrences"/> always has length 2 here, though the shared
    /// <see cref="DuplicationCluster"/>/<see cref="DuplicationOccurrence"/> records stay N-ary for
    /// schema parity with the Java implementation.
    /// </summary>
    public static (int TokensTotal, IReadOnlyList<DuplicationCluster> Clusters) ParseJscpdReport(string jscpdJson)
    {
        using var document = JsonDocument.Parse(jscpdJson);
        var root = document.RootElement;

        var tokensTotal = 0;
        if (root.TryGetProperty("statistics", out var statistics)
            && statistics.TryGetProperty("total", out var total)
            && total.TryGetProperty("tokens", out var tokensElement))
        {
            tokensTotal = tokensElement.GetInt32();
        }

        var clusters = new List<DuplicationCluster>();
        if (root.TryGetProperty("duplicates", out var duplicates))
        {
            foreach (var duplicate in duplicates.EnumerateArray())
            {
                var tokens = duplicate.GetProperty("tokens").GetInt32();
                var lines = duplicate.GetProperty("lines").GetInt32();
                var first = ReadOccurrence(duplicate.GetProperty("firstFile"));
                var second = ReadOccurrence(duplicate.GetProperty("secondFile"));
                clusters.Add(new DuplicationCluster(tokens, lines, new[] { first, second }));
            }
        }

        return (tokensTotal, clusters);
    }

    private static DuplicationOccurrence ReadOccurrence(JsonElement file) =>
        new(
            file.GetProperty("name").GetString()!.Replace('\\', '/'),
            file.GetProperty("start").GetInt32(),
            file.GetProperty("end").GetInt32());

    // ---- the union (never tokens × occurrences) -------------------------------------------------

    /// <summary>
    /// How many tokens are duplicated across [clusters], counting each region once no matter how
    /// many clusters cover it — the union, not the sum.
    /// </summary>
    /// <remarks>
    /// jscpd tokenizes per file rather than into one corpus-wide stream the way PMD's CPD does (the
    /// Java implementation's <c>countCoveredPositions</c> unions raw token-index spans in that single
    /// shared coordinate space), and jscpd's JSON report exposes no per-occurrence token index at
    /// all — only a start/end <em>line</em> per occurrence, plus one token count for the whole
    /// matched fragment (see <see cref="ParseJscpdReport"/>). This union therefore runs over each
    /// file's own line ranges instead: exactly the failure mode the Java implementation's own first
    /// scan hit (a naive per-cluster sum measuring over 300%) reproduces here just as readily — three
    /// near-identical files produce three pairwise clusters, each crediting the same file's lines
    /// again — so occurrences are grouped by file, overlapping or touching line ranges within one
    /// file are merged into runs, and each run is credited once, at the <em>largest</em> token count
    /// among the ranges that formed it, never their sum. Taking the largest (rather than, say,
    /// prorating by line count) never inflates past what a single occurrence in that region could
    /// already contribute on its own — the same "never exceeds what is really there" property
    /// <see cref="Aggregate"/> depends on to keep <c>percent</c> under 100%. It is an exact count
    /// when a file's duplicate ranges are identical or non-overlapping (the overwhelming common
    /// case — including the exact "same span, several partners" scenario the failure mode above
    /// describes) and a conservative one only for a genuinely rare in-file nested/partial overlap
    /// between two *different* clusters.
    /// </remarks>
    public static int UnionDuplicatedTokens(IReadOnlyList<DuplicationCluster> clusters)
    {
        var byFile = new Dictionary<string, List<(int Start, int End, int Tokens)>>();
        foreach (var cluster in clusters)
        {
            foreach (var occurrence in cluster.Occurrences)
            {
                if (!byFile.TryGetValue(occurrence.File, out var spans))
                {
                    spans = new List<(int, int, int)>();
                    byFile[occurrence.File] = spans;
                }
                spans.Add((occurrence.StartLine, occurrence.EndLine, cluster.Tokens));
            }
        }

        var total = 0;
        foreach (var spans in byFile.Values)
        {
            var sorted = spans.OrderBy(s => s.Start).ToList();
            var runEnd = sorted[0].End;
            var runTokens = sorted[0].Tokens;
            for (var i = 1; i < sorted.Count; i++)
            {
                var (start, end, tokens) = sorted[i];
                if (start <= runEnd)
                {
                    runEnd = Math.Max(runEnd, end);
                    runTokens = Math.Max(runTokens, tokens);
                }
                else
                {
                    total += runTokens;
                    runEnd = end;
                    runTokens = tokens;
                }
            }
            total += runTokens;
        }
        return total;
    }

    // ---- aggregate / write / read / summary (mirrors the Java shape) --------------------------

    /// <summary>
    /// Aggregates already-deduplicated totals ([tokensDuplicated] must already be
    /// <see cref="UnionDuplicatedTokens"/>'s result, never a per-cluster sum) and raw clusters into
    /// the tree-level result, sorted largest-first. Pure — no I/O, no jscpd invocation.
    /// </summary>
    public static DuplicationTreeResult Aggregate(int tokensTotal, int tokensDuplicated, IReadOnlyList<DuplicationCluster> clusters)
    {
        var sorted = clusters.OrderByDescending(c => c.Tokens).ToList();
        var percent = tokensTotal == 0 ? 0.0 : (double)tokensDuplicated / tokensTotal * 100.0;
        return new DuplicationTreeResult(tokensTotal, tokensDuplicated, RoundToOneDecimal(percent), sorted);
    }

    private static double RoundToOneDecimal(double value) => Math.Round(value, 1, MidpointRounding.AwayFromZero);

    public static void WriteJson(DuplicationScanResult scan, string filePath)
    {
        var sb = new StringBuilder();
        sb.Append('{');
        sb.Append(CultureInfo.InvariantCulture, $"\"tool\":\"{Tool}\",");
        sb.Append(CultureInfo.InvariantCulture, $"\"language\":\"{Language}\",");
        sb.Append(CultureInfo.InvariantCulture, $"\"minTokens\":{scan.MinTokens},");
        sb.Append("\"main\":");
        AppendTree(sb, scan.Main);
        sb.Append(',');
        sb.Append("\"test\":");
        AppendTree(sb, scan.Test);
        sb.Append('}');

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        File.WriteAllText(filePath, sb.ToString());
    }

    private static void AppendTree(StringBuilder sb, DuplicationTreeResult tree)
    {
        sb.Append('{');
        sb.Append(CultureInfo.InvariantCulture, $"\"tokensTotal\":{tree.TokensTotal},");
        sb.Append(CultureInfo.InvariantCulture, $"\"tokensDuplicated\":{tree.TokensDuplicated},");
        sb.Append(CultureInfo.InvariantCulture, $"\"percent\":{tree.Percent},");
        sb.Append("\"clusters\":[");
        for (var i = 0; i < tree.Clusters.Count; i++)
        {
            if (i > 0) sb.Append(',');
            var cluster = tree.Clusters[i];
            sb.Append('{');
            sb.Append(CultureInfo.InvariantCulture, $"\"tokens\":{cluster.Tokens},");
            sb.Append(CultureInfo.InvariantCulture, $"\"lines\":{cluster.Lines},");
            sb.Append("\"occurrences\":[");
            for (var j = 0; j < cluster.Occurrences.Count; j++)
            {
                if (j > 0) sb.Append(',');
                var occurrence = cluster.Occurrences[j];
                sb.Append(CultureInfo.InvariantCulture,
                    $"{{\"file\":\"{JsonEscape(occurrence.File)}\",\"startLine\":{occurrence.StartLine},\"endLine\":{occurrence.EndLine}}}");
            }
            sb.Append("]}");
        }
        sb.Append("]}");
    }

    private static string JsonEscape(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");

    public static DuplicationScanResult ReadJson(string filePath)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(filePath));
        var root = document.RootElement;
        var minTokens = root.GetProperty("minTokens").GetInt32();
        return new DuplicationScanResult(minTokens, ReadTree(root, "main"), ReadTree(root, "test"));
    }

    private static DuplicationTreeResult ReadTree(JsonElement root, string key)
    {
        var tree = root.GetProperty(key);
        var clusters = tree.GetProperty("clusters").EnumerateArray().Select(cluster =>
        {
            var occurrences = cluster.GetProperty("occurrences").EnumerateArray().Select(occurrence =>
                new DuplicationOccurrence(
                    occurrence.GetProperty("file").GetString()!,
                    occurrence.GetProperty("startLine").GetInt32(),
                    occurrence.GetProperty("endLine").GetInt32())
            ).ToList();
            return new DuplicationCluster(
                cluster.GetProperty("tokens").GetInt32(),
                cluster.GetProperty("lines").GetInt32(),
                occurrences);
        }).ToList();

        return new DuplicationTreeResult(
            tree.GetProperty("tokensTotal").GetInt32(),
            tree.GetProperty("tokensDuplicated").GetInt32(),
            tree.GetProperty("percent").GetDouble(),
            clusters);
    }

    /// <summary>
    /// The one build-log summary line: main's percent/cluster count/largest cluster (the thing
    /// <c>DuplicationCheck</c> acts on), then test's (reported only).
    /// </summary>
    public static string SummaryLine(DuplicationScanResult scan)
    {
        var main = scan.Main;
        var test = scan.Test;
        var largest = main.Clusters.Count == 0 ? null : main.Clusters.OrderByDescending(c => c.Tokens).First();
        var largestDescription = largest is null
            ? "no clusters"
            : string.Create(CultureInfo.InvariantCulture, $"largest {largest.Tokens} tokens ")
              + string.Join(" ↔ ", largest.Occurrences.Select(o => string.Create(CultureInfo.InvariantCulture, $"{o.File}:{o.StartLine}")));

        return string.Create(CultureInfo.InvariantCulture,
            $"duplication: main {FormatPercent(main.Percent)}% of tokens in {main.Clusters.Count} clusters ({largestDescription}) · test {FormatPercent(test.Percent)}% in {test.Clusters.Count} clusters (reported, not gated)");
    }

    private static string FormatPercent(double value) => value.ToString("F1", CultureInfo.InvariantCulture);
}
