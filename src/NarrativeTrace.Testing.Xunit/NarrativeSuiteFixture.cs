// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Clarity;
using NarrativeTrace.Core;
using NarrativeTrace.Glossary;

namespace NarrativeTrace.TestingXunit;

/// <summary>
/// Suite-wide fixture that accumulates every test's narrative and, once the whole
/// collection has finished, writes a <c>clarity-results.json</c> and prints the
/// summary footer. Register it once via a <c>[CollectionDefinition]</c> that
/// implements <c>ICollectionFixture&lt;NarrativeSuiteFixture&gt;</c>; each test calls
/// <see cref="Record"/> to contribute its captured trace.
/// </summary>
/// <remarks>
/// When the repository has a committed <c>glossary.json</c> (found via
/// <see cref="GlossarySettings"/>), the flush additionally harvests the
/// suite's traces into it and prints the vocabulary summary line (ADR-012).
/// A glossary failure is reported to the console but never fails the suite —
/// test results outrank vocabulary governance.
/// </remarks>
public sealed class NarrativeSuiteFixture : IDisposable
{
    private readonly SuiteTraceAccumulator _accumulator = new();
    private readonly HashSet<ITraceLossSource> _lossSources = [];
    private readonly string _outputDir;
    private readonly TextWriter _console;
    private readonly Func<string, string?> _readEnv;
    private bool _flushed;

    /// <summary>
    /// Creates a suite fixture writing to the configured output directory and the console.
    /// </summary>
    /// <remarks>The constructor xUnit calls for a <c>[CollectionDefinition]</c> fixture.</remarks>
    public NarrativeSuiteFixture()
        : this(Environment.GetEnvironmentVariable, Console.Out)
    {
    }

    internal NarrativeSuiteFixture(
        Func<string, string?> readEnv, TextWriter console)
    {
        _outputDir = TestArtifactSettings.Resolve(readEnv).Directory;
        _console = console;
        _readEnv = readEnv;
    }

    /// <summary>
    /// Adds one test's captured trace under its scenario name. Duplicate names
    /// across classes are retained in order.
    /// </summary>
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

    /// <summary>
    /// Flushes the suite: writes the clarity report, harvests the glossary, and
    /// prints the summary footer.
    /// </summary>
    /// <remarks>
    /// Called by xUnit once the whole collection has finished — this is where
    /// the suite's artifacts are produced, so a run that never disposes the
    /// fixture produces none. Flushing is idempotent: a second call does
    /// nothing. A glossary failure is reported to the console and swallowed, so
    /// disposal never fails the suite.
    /// </remarks>
    public void Dispose()
    {
        Flush();
        GC.SuppressFinalize(this);
    }

    private void Flush()
    {
        if (_flushed)
        {
            return;
        }

        _flushed = true;
        ClaritySuiteReporter.Write(
            _accumulator.Entries, _outputDir, _console, ProjectVocabulary(),
            AccumulatedLoss());
        HarvestGlossary();
    }

    /// <summary>
    /// The repository's committed glossary, read as the vocabulary clarity
    /// scores with.
    /// </summary>
    /// <remarks>
    /// Unlike harvesting, reading is unconditional wherever a glossary exists:
    /// it changes nothing on disk, and a project that curates its ubiquitous
    /// language should not have to opt in to being scored in it. Only the
    /// committed file counts — nothing this run harvests feeds back into its
    /// own scores. An unreadable glossary degrades to the built-in dictionaries
    /// with a console note: a reporting artifact must never fail the suite that
    /// produced it.
    /// </remarks>
    private DomainVocabulary ProjectVocabulary()
    {
        try
        {
            return GlossaryVocabulary.FromFile(
                GlossarySettings.ResolveFile(
                    _readEnv, Directory.GetCurrentDirectory()));
        }
        catch (Exception e) when (e is ArgumentException or IOException)
        {
            _console.WriteLine(
                "Committed glossary could not be read, scoring with the "
                + $"built-in dictionaries only: {e.Message}");
            return DomainVocabulary.Empty;
        }
    }

    private void HarvestGlossary()
    {
        try
        {
            GlossarySuiteReporter.Write(
                _accumulator.Entries,
                GlossarySettings.ResolveFile(_readEnv, Directory.GetCurrentDirectory()),
                _outputDir,
                _console);
        }
        catch (ArgumentException e)
        {
            _console.WriteLine($"Glossary harvest skipped: {e.Message}");
        }
    }
}
