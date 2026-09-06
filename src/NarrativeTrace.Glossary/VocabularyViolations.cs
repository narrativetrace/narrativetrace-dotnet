// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Glossary;

/// <summary>
/// Builds <see cref="VocabularyViolation"/>s from the alias uses a merge
/// suppressed.
/// </summary>
/// <remarks>
/// One aggregation point between the merger's raw suppressed observations and
/// every violation surface. Observations of the same
/// <c>(context, alias, site, identifier)</c> are summed regardless of kind;
/// the canonical term comes from the glossary's <see cref="AliasIndex"/>; the
/// rename suggestion is mechanical and may be absent.
/// </remarks>
public static class VocabularyViolations
{
    /// <summary>Aggregates suppressed alias uses into deterministic violation records.</summary>
    /// <param name="glossary">Glossary whose synonym declarations caused the suppression; must not be null.</param>
    /// <param name="suppressed">
    /// Suppressed observations from <see cref="MergeResult.SuppressedAliasUses"/>;
    /// must not be null.
    /// </param>
    /// <returns>Violations sorted by <c>(context, alias, site, identifier)</c>.</returns>
    /// <exception cref="ArgumentNullException">Any argument is null.</exception>
    /// <exception cref="ArgumentException">A suppressed candidate is not an alias in its context.</exception>
    public static IReadOnlyList<VocabularyViolation> Collect(
        Glossary glossary, IReadOnlyList<HarvestCandidate> suppressed)
    {
        if (glossary is null)
        {
            throw new ArgumentNullException(nameof(glossary));
        }

        if (suppressed is null)
        {
            throw new ArgumentNullException(nameof(suppressed));
        }

        return Sorted(Aggregate(glossary, suppressed));
    }

    private static Dictionary<string, VocabularyViolation> Aggregate(
        Glossary glossary, IReadOnlyList<HarvestCandidate> suppressed)
    {
        var aliasIndex = AliasIndex.Of(glossary);
        var aggregated = new Dictionary<string, VocabularyViolation>(StringComparer.Ordinal);
        foreach (var candidate in suppressed)
        {
            var violation = ToViolation(candidate, aliasIndex);
            var key = GroupKey(violation);
            aggregated[key] = aggregated.TryGetValue(key, out var existing)
                ? Sum(existing, violation)
                : violation;
        }

        return aggregated;
    }

    private static List<VocabularyViolation> Sorted(
        Dictionary<string, VocabularyViolation> aggregated)
    {
        return aggregated.Values
            .OrderBy(v => v.Context, StringComparer.Ordinal)
            .ThenBy(v => v.Alias, StringComparer.Ordinal)
            .ThenBy(v => v.Site, StringComparer.Ordinal)
            .ThenBy(v => v.Identifier, StringComparer.Ordinal)
            .ToList();
    }

    private static VocabularyViolation ToViolation(
        HarvestCandidate candidate, AliasIndex aliasIndex)
    {
        var key = new TermKey(candidate.Context, candidate.Phrase);
        var canonical = aliasIndex.CanonicalFor(key)
            ?? throw new ArgumentException(
                $"suppressed candidate is not an alias: {candidate}");
        var suggestion = RenameSuggester.Suggest(
            candidate.Identifier, candidate.Phrase, canonical.Term);
        return new VocabularyViolation(
            candidate.Context, candidate.Phrase, canonical.Term,
            candidate.Site, candidate.Identifier, suggestion, candidate.Occurrences);
    }

    private static string GroupKey(VocabularyViolation violation)
    {
        return $"{violation.Context}|{violation.Alias}|{violation.Site}|{violation.Identifier}";
    }

    private static VocabularyViolation Sum(
        VocabularyViolation left, VocabularyViolation right)
    {
        return new VocabularyViolation(
            left.Context, left.Alias, left.CanonicalTerm, left.Site,
            left.Identifier, left.SuggestedIdentifier,
            left.Occurrences + right.Occurrences);
    }
}
