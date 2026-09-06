// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;

namespace NarrativeTrace.Glossary;

/// <summary>
/// The suite-level glossary hook: harvests the run's traces into the
/// committed glossary and emits every vocabulary surface.
/// </summary>
/// <remarks>
/// Runs beside the clarity report at suite completion (ADR-012): reads
/// <c>glossary.json</c>, harvests the accumulated trace trees, merges
/// additively, rewrites <c>glossary.json</c> + regenerates
/// <c>glossary.md</c> only when their bytes change (anti-churn), writes the
/// volatile <c>glossary-usage.json</c> into the run output directory, and
/// prints the vocabulary summary line. No glossary file or an empty suite is
/// a silent no-op — the feature is opt-in by file presence. A malformed
/// glossary throws <see cref="ArgumentException"/>: the committed file is
/// hand-curated and a typo must fail loudly, not be skipped.
/// </remarks>
public static class GlossarySuiteReporter
{
    /// <summary>Harvests with the system clock and the run's own namespace index.</summary>
    /// <remarks>
    /// Trace nodes carry simple class names only, while bounded contexts are
    /// declared in terms of namespaces, so the missing half comes from
    /// <see cref="ClassPackageIndex.FromLoadedAssemblies"/> — the run's own
    /// loaded types. Without it every harvested candidate would land in
    /// <c>_unassigned</c> and contexts would be inert on the path that ships.
    /// The index is built only when there is a suite to harvest: indexing
    /// every loaded assembly for a run that harvests nothing is pure cost.
    /// </remarks>
    /// <param name="entries">Accumulated (scenario, tree) pairs of the run; must not be null.</param>
    /// <param name="glossaryFile">Path to <c>glossary.json</c>, or null when the feature is off.</param>
    /// <param name="outputDir">Run artifact directory for <c>glossary-usage.json</c>; must not be blank.</param>
    /// <param name="console">Sink for the vocabulary summary line; must not be null.</param>
    public static void Write(
        IReadOnlyList<KeyValuePair<string, TraceTree>> entries,
        string? glossaryFile,
        string outputDir,
        TextWriter console)
    {
        var harvesting = entries is { Count: > 0 }
            && glossaryFile is not null
            && File.Exists(glossaryFile);
        Func<string, string?> namespaceOf = harvesting
            ? ClassPackageIndex.FromLoadedAssemblies().NamespaceOf
            : _ => null;
        Write(entries, glossaryFile, outputDir, console, () => DateTime.UtcNow, namespaceOf);
    }

    /// <summary>Harvests with injectable clock and namespace lookup (test seam).</summary>
    /// <param name="entries">Accumulated (scenario, tree) pairs of the run; must not be null.</param>
    /// <param name="glossaryFile">Path to <c>glossary.json</c>, or null when the feature is off.</param>
    /// <param name="outputDir">Run artifact directory for <c>glossary-usage.json</c>; must not be blank.</param>
    /// <param name="console">Sink for the vocabulary summary line; must not be null.</param>
    /// <param name="clock">Source of <c>firstSeen</c> dates; must not be null.</param>
    /// <param name="namespaceOf">Simple class name → namespace, or null when unknown; must not be null.</param>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="outputDir"/> is blank, or the glossary file is malformed.</exception>
    public static void Write(
        IReadOnlyList<KeyValuePair<string, TraceTree>> entries,
        string? glossaryFile,
        string outputDir,
        TextWriter console,
        Func<DateTime> clock,
        Func<string, string?> namespaceOf)
    {
        Guard(entries, outputDir, console, clock, namespaceOf);
        if (entries.Count == 0 || glossaryFile is null || !File.Exists(glossaryFile))
        {
            return;
        }

        var existing = GlossaryJsonReader.Read(File.ReadAllText(glossaryFile));
        var harvest = new GlossaryHarvester(new ContextResolver(existing), namespaceOf)
            .Harvest(entries.Select(entry => entry.Value).ToList());
        var merge = new GlossaryMerger(clock).Merge(existing, harvest);
        var violations = VocabularyViolations.Collect(
            merge.Glossary, merge.SuppressedAliasUses);
        WriteArtifacts(glossaryFile, outputDir, merge, harvest, violations);
        console.WriteLine(
            VocabularySummaryFormatter.FormatSummary(merge.NewTerms.Count, violations));
    }

    private static void WriteArtifacts(
        string glossaryFile,
        string outputDir,
        MergeResult merge,
        HarvestResult harvest,
        IReadOnlyList<VocabularyViolation> violations)
    {
        WriteIfChanged(glossaryFile, GlossaryJsonWriter.Write(merge.Glossary));
        WriteIfChanged(
            Path.ChangeExtension(glossaryFile, ".md"),
            GlossaryMarkdownRenderer.Render(merge.Glossary));
        GlossaryUsageReport.Write(
            Path.Combine(outputDir, GlossaryUsageReport.FileName),
            harvest, merge.NewTerms, violations);
    }

    /// <summary>Anti-churn: byte-identical content never touches the file.</summary>
    private static void WriteIfChanged(string file, string content)
    {
        if (File.Exists(file) && File.ReadAllText(file) == content)
        {
            return;
        }

        File.WriteAllText(file, content);
    }

    private static void Guard(
        IReadOnlyList<KeyValuePair<string, TraceTree>> entries,
        string outputDir,
        TextWriter console,
        Func<DateTime> clock,
        Func<string, string?> namespaceOf)
    {
        if (entries is null)
        {
            throw new ArgumentNullException(nameof(entries));
        }

        if (string.IsNullOrWhiteSpace(outputDir))
        {
            throw new ArgumentException("outputDir must not be blank", nameof(outputDir));
        }

        if (console is null)
        {
            throw new ArgumentNullException(nameof(console));
        }

        if (clock is null)
        {
            throw new ArgumentNullException(nameof(clock));
        }

        if (namespaceOf is null)
        {
            throw new ArgumentNullException(nameof(namespaceOf));
        }
    }
}
