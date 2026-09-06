// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Clarity;
using NarrativeTrace.Core;
using NarrativeTrace.Glossary;

namespace NarrativeTrace.TestingNUnit;

/// <summary>
/// Accumulates the narratives recorded across a whole NUnit suite and, when
/// flushed, writes a <c>clarity-results.json</c> and prints the summary footer.
/// Duplicate scenario names are retained in order; an empty suite writes nothing.
/// </summary>
/// <remarks>
/// When the repository has a committed <c>glossary.json</c> (found via
/// <see cref="GlossarySettings"/>), the flush additionally harvests the
/// suite's traces into it and prints the vocabulary summary line (ADR-012).
/// A glossary failure is reported to the console but never fails the suite —
/// test results outrank vocabulary governance.
/// </remarks>
public sealed class NarrativeSuiteReport
{
    private readonly SuiteTraceAccumulator _accumulator = new();
    private readonly HashSet<ITraceLossSource> _lossSources = [];
    private readonly string _outputDir;
    private readonly Func<string, string?> _readEnv;

    /// <summary>Creates a suite report writing into the given directory.</summary>
    /// <param name="outputDir">Where <c>clarity-results.json</c> and the report are written. Created if missing.</param>
    public NarrativeSuiteReport(string outputDir)
        : this(outputDir, Environment.GetEnvironmentVariable)
    {
    }

    internal NarrativeSuiteReport(string outputDir, Func<string, string?> readEnv)
    {
        _outputDir = outputDir;
        _readEnv = readEnv;
    }

    /// <summary>Adds one test's captured trace under its scenario name.</summary>
    /// <param name="scenario">The scenario name. Duplicates across classes are retained in order, not merged.</param>
    /// <param name="tree">The captured trace.</param>
    public void Record(string scenario, TraceTree tree)
    {
        _accumulator.Add(scenario, tree);
    }

    /// <summary>
    /// Registers a capture whose loss the suite footer should account for.
    /// </summary>
    /// <param name="source">
    /// The capture — typically a context — to read at flush time. Registering
    /// the same one twice counts it once, so calling this per test with a
    /// shared context is safe.
    /// </param>
    /// <remarks>
    /// Sources are read at the <i>end</i> of the run rather than summed as they
    /// are reported, because a context's counters are cumulative: adding up
    /// per-test readings of one context would multiply the same loss by the
    /// number of tests that saw it.
    /// </remarks>
    public void ReportLoss(ITraceLossSource source)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        _lossSources.Add(source);
    }

    /// <summary>Every registered source's final reading, added up once each.</summary>
    private TraceLoss AccumulatedLoss()
    {
        return _lossSources
            .Select(source => source.TraceLoss)
            .Aggregate(TraceLoss.None, Combine);
    }

    private static TraceLoss Combine(TraceLoss total, TraceLoss loss)
    {
        return new TraceLoss(
            total.DroppedEvents + loss.DroppedEvents,
            total.RefusedScopes + loss.RefusedScopes,
            total.RefusedSpans + loss.RefusedSpans);
    }

    /// <summary>Writes the clarity artifacts, harvests the glossary, and prints the summary footer.</summary>
    /// <param name="console">Where the summary is written. Pass <see cref="TextWriter.Null"/> to stay quiet.</param>
    /// <remarks>
    /// Unlike the xUnit suite fixture this is <b>not</b> guarded against being
    /// called twice — a second call re-writes the artifacts from the same
    /// accumulated entries. A glossary failure is reported to
    /// <paramref name="console"/> and swallowed, never failing the suite.
    /// </remarks>
    public void Flush(TextWriter console)
    {
        ClaritySuiteReporter.Write(
            _accumulator.Entries, _outputDir, console, ProjectVocabulary(console),
            AccumulatedLoss());
        HarvestGlossary(console);
    }

    /// <summary>
    /// The repository's committed glossary, read as the vocabulary clarity
    /// scores with — the same rule the xUnit fixture applies, so one repository
    /// scores identically under either framework.
    /// </summary>
    /// <remarks>
    /// Reading is unconditional wherever a glossary exists: it changes nothing
    /// on disk. Only the committed file counts, so nothing this run harvests
    /// feeds back into its own scores. An unreadable glossary degrades to the
    /// built-in dictionaries with a console note rather than failing the suite.
    /// </remarks>
    private DomainVocabulary ProjectVocabulary(TextWriter console)
    {
        try
        {
            return GlossaryVocabulary.FromFile(
                GlossarySettings.ResolveFile(
                    _readEnv, Directory.GetCurrentDirectory()));
        }
        catch (Exception e) when (e is ArgumentException or IOException)
        {
            console.WriteLine(
                "Committed glossary could not be read, scoring with the "
                + $"built-in dictionaries only: {e.Message}");
            return DomainVocabulary.Empty;
        }
    }

    private void HarvestGlossary(TextWriter console)
    {
        try
        {
            GlossarySuiteReporter.Write(
                _accumulator.Entries,
                GlossarySettings.ResolveFile(_readEnv, Directory.GetCurrentDirectory()),
                _outputDir,
                console);
        }
        catch (ArgumentException e)
        {
            console.WriteLine($"Glossary harvest skipped: {e.Message}");
        }
    }
}
