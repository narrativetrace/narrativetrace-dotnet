// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Core;

/// <summary>
/// Writes the suite-level artifacts once all tests have run: a
/// <c>clarity-results.json</c> holding every accumulated scenario, the
/// human-facing <c>clarity-report.md</c> beside it, and a printed footer
/// summarizing the scenario count and clarity split. An empty suite writes
/// nothing and prints nothing — a short-circuit on an empty trace set. The
/// clarity scorer and both renderers are injected so Core needs no
/// dependency on the clarity engine.
/// </summary>
public static class SuiteReportWriter
{
    /// <summary>
    /// The machine-readable suite results, written into the output directory.
    /// Consumed by tooling — treat the name as part of the contract.
    /// </summary>
    public const string ResultsFileName = "clarity-results.json";

    /// <summary>The human-facing suite report written beside the results JSON.</summary>
    public const string ReportFileName = "clarity-report.md";

    /// <param name="entries">Accumulated (scenario, tree) pairs; an empty suite writes nothing.</param>
    /// <param name="outputDir">Directory both artifacts are written to.</param>
    /// <param name="console">Sink for the summary footer.</param>
    /// <param name="clarityScorer">Scores one trace for the footer's clarity split.</param>
    /// <param name="jsonRenderer">Renders <see cref="ResultsFileName"/>.</param>
    /// <param name="markdownRenderer">Renders <see cref="ReportFileName"/>.</param>
    /// <param name="loss">
    /// What the run lost, if the caller tracked it. A lossless run — and a
    /// caller that passes <see langword="null"/> — prints no loss line.
    /// </param>
    /// <param name="deltas">
    /// Every scenario's structural delta against its last green artifact, if
    /// the caller tracked them. Prints a trailing "Since last green" line;
    /// omitted entirely when null, empty, or every delta line renders empty.
    /// </param>
    /// <param name="run">
    /// The test-suite run this suite executed as, named in the footer's
    /// leading <c>run:</c> line; <see langword="null"/> when the caller has
    /// not adopted <see cref="RunIdentity"/>.
    /// </param>
    public static void Write(
        IReadOnlyList<KeyValuePair<string, TraceTree>> entries,
        string outputDir,
        TextWriter console,
        Func<TraceTree, double> clarityScorer,
        Func<IReadOnlyList<KeyValuePair<string, TraceTree>>, string> jsonRenderer,
        Func<IReadOnlyList<KeyValuePair<string, TraceTree>>, string> markdownRenderer,
        TraceLoss? loss = null,
        IReadOnlyList<ScenarioDelta>? deltas = null,
        RunIdentity? run = null)
    {
        if (entries.Count == 0)
        {
            return;
        }

        Directory.CreateDirectory(outputDir);
        // outputDir is a caller-supplied report directory (test/build configuration), not
        // externally-facing input, and both filenames below are compile-time constants — semgrep's
        // unsafe-path-combine rule cannot see that trust boundary.
        File.WriteAllText(
            Path.Combine(outputDir, ResultsFileName), jsonRenderer(entries)); // nosemgrep: csharp.lang.security.filesystem.unsafe-path-combine.unsafe-path-combine
        File.WriteAllText(
            Path.Combine(outputDir, ReportFileName), markdownRenderer(entries)); // nosemgrep: csharp.lang.security.filesystem.unsafe-path-combine.unsafe-path-combine

        PrintFooter(entries, outputDir, console, clarityScorer, loss, deltas, run);
    }

    private static void PrintFooter(
        IReadOnlyList<KeyValuePair<string, TraceTree>> entries,
        string outputDir,
        TextWriter console,
        Func<TraceTree, double> clarityScorer,
        TraceLoss? loss,
        IReadOnlyList<ScenarioDelta>? deltas,
        RunIdentity? run)
    {
        var scores = new List<double>(entries.Count);
        for (var i = 0; i < entries.Count; i++)
        {
            scores.Add(clarityScorer(entries[i].Value));
        }

        console.WriteLine(
            ConsoleSummaryReporter.FormatSuiteFooter(
                entries.Count, outputDir, scores, loss ?? TraceLoss.None, run));

        var deltaLine = ConsoleSummaryReporter.FormatDeltaLine(deltas ?? []);
        if (deltaLine.Length > 0)
        {
            console.WriteLine("  Since last green: " + deltaLine);
        }
    }
}
