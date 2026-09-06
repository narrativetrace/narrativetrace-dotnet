// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Globalization;

namespace NarrativeTrace.Glossary;

/// <summary>Renders the per-run <c>glossary-usage.json</c> report.</summary>
/// <remarks>
/// The build-directory home for everything volatile — new terms of this run,
/// vocabulary violations, and per-term usage counts. These never enter the
/// committed glossary (anti-churn, ADR-012). Output is deterministic: usage
/// aggregates by <c>(context, phrase)</c> in the harvester's sorted order.
/// </remarks>
public static class GlossaryUsageReport
{
    /// <summary>Conventional file name of the report inside the run output directory.</summary>
    public const string FileName = "glossary-usage.json";

    /// <summary>Renders the report document.</summary>
    /// <param name="harvest">Observations of the run; must not be null.</param>
    /// <param name="newTerms">Terms the merge added; must not be null.</param>
    /// <param name="violations">Aggregated vocabulary violations; must not be null.</param>
    /// <returns>Deterministic JSON text ending in a newline.</returns>
    /// <exception cref="ArgumentNullException">Any argument is null.</exception>
    public static string Render(
        HarvestResult harvest,
        IReadOnlyList<GlossaryTerm> newTerms,
        IReadOnlyList<VocabularyViolation> violations)
    {
        if (harvest is null)
        {
            throw new ArgumentNullException(nameof(harvest));
        }

        if (newTerms is null)
        {
            throw new ArgumentNullException(nameof(newTerms));
        }

        if (violations is null)
        {
            throw new ArgumentNullException(nameof(violations));
        }

        return "{\n  \"newTerms\": " + Array(newTerms.Select(NewTermEntry).ToList())
            + ",\n  \"violations\": " + Array(violations.Select(ViolationEntry).ToList())
            + ",\n  \"usage\": " + Array(UsageEntries(harvest))
            + "\n}\n";
    }

    /// <summary>Renders the report and writes it to the given file, creating parent directories.</summary>
    /// <param name="file">Target file, typically <c>&lt;output dir&gt;/glossary-usage.json</c>.</param>
    /// <param name="harvest">Observations of the run; must not be null.</param>
    /// <param name="newTerms">Terms the merge added; must not be null.</param>
    /// <param name="violations">Aggregated vocabulary violations; must not be null.</param>
    /// <exception cref="ArgumentException"><paramref name="file"/> is blank.</exception>
    /// <exception cref="ArgumentNullException">Any other argument is null.</exception>
    public static void Write(
        string file,
        HarvestResult harvest,
        IReadOnlyList<GlossaryTerm> newTerms,
        IReadOnlyList<VocabularyViolation> violations)
    {
        if (string.IsNullOrWhiteSpace(file))
        {
            throw new ArgumentException("file must not be blank", nameof(file));
        }

        var content = Render(harvest, newTerms, violations);
        var parent = Path.GetDirectoryName(file);
        if (!string.IsNullOrEmpty(parent))
        {
            Directory.CreateDirectory(parent);
        }

        File.WriteAllText(file, content);
    }

    private static string NewTermEntry(GlossaryTerm term)
    {
        return $"{{ \"term\": {Quoted(term.Term)}, \"context\": {Quoted(term.Context)} }}";
    }

    private static string ViolationEntry(VocabularyViolation violation)
    {
        var suggested = violation.SuggestedIdentifier is null
            ? ""
            : $", \"suggestedIdentifier\": {Quoted(violation.SuggestedIdentifier)}";
        return $"{{ \"context\": {Quoted(violation.Context)}"
            + $", \"alias\": {Quoted(violation.Alias)}"
            + $", \"canonicalTerm\": {Quoted(violation.CanonicalTerm)}"
            + $", \"site\": {Quoted(violation.Site)}"
            + $", \"identifier\": {Quoted(violation.Identifier)}"
            + suggested
            + $", \"occurrences\": {Count(violation.Occurrences)} }}";
    }

    /// <summary>Aggregates candidate occurrences by (context, phrase); insertion order is already sorted.</summary>
    private static List<string> UsageEntries(HarvestResult harvest)
    {
        var totals = new List<KeyValuePair<string, int>>();
        var indexByKey = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var candidate in harvest.Candidates)
        {
            var key = $"{Quoted(candidate.Context)}, \"phrase\": {Quoted(candidate.Phrase)}";
            if (indexByKey.TryGetValue(key, out var index))
            {
                totals[index] = new KeyValuePair<string, int>(
                    key, totals[index].Value + candidate.Occurrences);
            }
            else
            {
                indexByKey[key] = totals.Count;
                totals.Add(new KeyValuePair<string, int>(key, candidate.Occurrences));
            }
        }

        return totals
            .Select(e => $"{{ \"context\": {e.Key}, \"occurrences\": {Count(e.Value)} }}")
            .ToList();
    }

    private static string Array(List<string> entries)
    {
        if (entries.Count == 0)
        {
            return "[]";
        }

        return "[\n    " + string.Join(",\n    ", entries) + "\n  ]";
    }

    private static string Count(int value)
    {
        return value.ToString(CultureInfo.InvariantCulture);
    }

    private static string Quoted(string value)
    {
        return "\"" + JsonEscape.Escape(value) + "\"";
    }
}
