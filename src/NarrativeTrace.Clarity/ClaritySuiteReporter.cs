// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;

namespace NarrativeTrace.Clarity;

/// <summary>
/// Bridges the accumulated suite traces to the clarity engine: scores every
/// scenario with <see cref="ClarityAnalyzer"/> and serializes them with
/// <see cref="ClarityJsonExporter"/>, delegating file/footer emission to
/// <see cref="SuiteReportWriter"/>. This is the wired entry point the test
/// integrations call at suite completion.
/// </summary>
public static class ClaritySuiteReporter
{
    /// <summary>Scores every accumulated scenario and writes the suite's clarity artifacts.</summary>
    /// <param name="entries">
    /// The (scenario, trace) pairs collected across the suite. Duplicate
    /// scenario names are expected and are reported separately, not merged.
    /// An empty suite writes nothing.
    /// </param>
    /// <param name="outputDir">Directory to write <c>clarity-results.json</c> and <c>clarity-report.md</c> into. Created if missing.</param>
    /// <param name="console">Where the summary footer is echoed. Pass <see cref="TextWriter.Null"/> to stay quiet.</param>
    /// <exception cref="IOException">The output directory or a report file could not be written.</exception>
    /// <remarks>
    /// Called once at suite completion by the test integrations. Analysis cost
    /// scales with the total size of the accumulated traces, so it is deliberately
    /// deferred to the end of the run rather than done per test.
    /// </remarks>
    /// <param name="vocabulary">
    /// The project's committed glossary vocabulary, so the suite scores in the
    /// repository's own language; null uses the built-in dictionaries alone.
    /// </param>
    /// <param name="loss">
    /// What the run lost, if the integration tracked it. Adds one
    /// <c>Incomplete: …</c> line to the footer, omitted entirely at zero loss.
    /// </param>
    public static void Write(
        IReadOnlyList<KeyValuePair<string, TraceTree>> entries,
        string outputDir,
        TextWriter console,
        DomainVocabulary? vocabulary = null,
        TraceLoss? loss = null)
    {
        SuiteReportWriter.Write(
            entries,
            outputDir,
            console,
            tree => Score(tree, vocabulary),
            scored => RenderJson(scored, vocabulary),
            scored => RenderMarkdown(scored, vocabulary),
            loss);
    }

    private static double Score(TraceTree tree, DomainVocabulary? vocabulary)
    {
        return ClarityAnalyzer.Analyze(tree, NoPropertyNames, vocabulary).Overall;
    }

    // netstandard2.0 has no IReadOnlySet<T>, so the contract is ISet<string>.
    private static readonly HashSet<string> NoPropertyNames =
        new(StringComparer.Ordinal);

    private static string RenderJson(
        IReadOnlyList<KeyValuePair<string, TraceTree>> entries,
        DomainVocabulary? vocabulary)
    {
        return ClarityJsonExporter.ExportReport(Analyze(entries, vocabulary));
    }

    private static string RenderMarkdown(
        IReadOnlyList<KeyValuePair<string, TraceTree>> entries,
        DomainVocabulary? vocabulary)
    {
        return ClarityReportRenderer.Render(Analyze(entries, vocabulary));
    }

    private static List<ScenarioClarity> Analyze(
        IReadOnlyList<KeyValuePair<string, TraceTree>> entries,
        DomainVocabulary? vocabulary)
    {
        var results = new List<ScenarioClarity>(entries.Count);
        for (var i = 0; i < entries.Count; i++)
        {
            results.Add(new ScenarioClarity(
                entries[i].Key,
                ClarityAnalyzer.Analyze(
                    entries[i].Value, NoPropertyNames, vocabulary)));
        }

        return results;
    }
}
