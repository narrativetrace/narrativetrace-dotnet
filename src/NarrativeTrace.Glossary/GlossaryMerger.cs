// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Diagnostics;

namespace NarrativeTrace.Glossary;

/// <summary>
/// Additive merge of a <see cref="HarvestResult"/> into an existing
/// <see cref="Glossary"/>.
/// </summary>
/// <remarks>
/// The merge rules of ADR-012, enforced structurally: existing entries are
/// never removed or mutated (human-authored fields are sacrosanct), unseen
/// <c>(context, phrase)</c> pairs join as <c>harvested</c> terms, phrases
/// matching a deprecated alias are suppressed and reported, and re-merging
/// the same harvest is a no-op (idempotence). <c>firstSeen</c> comes from the
/// injected clock, set once at entry creation. Accepted abbreviations are
/// human-owned like <c>definition</c> and <c>translations</c>: the merge
/// carries the section through untouched and harvesting never adds to it.
/// </remarks>
public sealed class GlossaryMerger
{
    /// <summary>New terms record at most this many observed source sites.</summary>
    private const int SourceLimit = 3;

    private const string UnassignedDescription =
        "Harvested terms not yet mapped to a context";

    private readonly Func<DateTime> clock;

    /// <param name="clock">
    /// Source of <c>firstSeen</c> dates for newly created terms (the date
    /// component is used); must not be null.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="clock"/> is null.</exception>
    public GlossaryMerger(Func<DateTime> clock)
    {
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    /// <summary>Merges harvested observations into the glossary, additively.</summary>
    /// <param name="existing">Glossary to merge into; must not be null.</param>
    /// <param name="harvest">Observations of one run; must not be null.</param>
    /// <returns>Merged glossary plus the new terms and suppressed alias uses of this merge.</returns>
    /// <exception cref="ArgumentNullException">Any argument is null.</exception>
    public MergeResult Merge(Glossary existing, HarvestResult harvest)
    {
        if (existing is null)
        {
            throw new ArgumentNullException(nameof(existing));
        }

        if (harvest is null)
        {
            throw new ArgumentNullException(nameof(harvest));
        }

        var suppressed = new List<HarvestCandidate>();
        var newTerms = BuildNewTerms(ClassifyUnseen(existing, harvest, suppressed));
        var merged = MergedGlossary(existing, newTerms);
        AssertAdditive(existing, merged);
        return new MergeResult(merged, newTerms, suppressed);
    }

    /// <summary>Sorts candidates into suppressed alias uses and accumulators for genuinely new terms.</summary>
    private static List<NewTermAccumulator> ClassifyUnseen(
        Glossary existing, HarvestResult harvest, List<HarvestCandidate> suppressed)
    {
        var aliasIndex = AliasIndex.Of(existing);
        var existingKeys = new HashSet<TermKey>(existing.Terms.Select(TermKey.Of));
        var byKey = new Dictionary<TermKey, NewTermAccumulator>();
        var inFirstSeenOrder = new List<NewTermAccumulator>();
        foreach (var candidate in harvest.Candidates)
        {
            var key = new TermKey(candidate.Context, candidate.Phrase);
            if (aliasIndex.IsAlias(key))
            {
                suppressed.Add(candidate);
            }
            else if (!existingKeys.Contains(key))
            {
                Accumulate(byKey, inFirstSeenOrder, key, candidate);
            }
        }

        return inFirstSeenOrder;
    }

    private static void Accumulate(
        Dictionary<TermKey, NewTermAccumulator> byKey,
        List<NewTermAccumulator> inFirstSeenOrder,
        TermKey key,
        HarvestCandidate candidate)
    {
        if (!byKey.TryGetValue(key, out var accumulator))
        {
            accumulator = new NewTermAccumulator(key, candidate.Kind);
            byKey[key] = accumulator;
            inFirstSeenOrder.Add(accumulator);
        }

        accumulator.AddSite(candidate.Site);
    }

    private List<GlossaryTerm> BuildNewTerms(List<NewTermAccumulator> accumulators)
    {
        var firstSeen = clock().Date;
        return accumulators.Select(a => a.ToTerm(firstSeen)).ToList();
    }

    private static Glossary MergedGlossary(
        Glossary existing, IReadOnlyList<GlossaryTerm> newTerms)
    {
        var contexts = existing.Contexts.ToDictionary(
            pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        // S3267: a LINQ filter here would be evaluated lazily against the very
        // dictionary the loop body mutates — correct, but far from obvious.
#pragma warning disable S3267
        foreach (var term in newTerms)
        {
            if (!contexts.ContainsKey(term.Context))
            {
                contexts[term.Context] = DeclaredContext(term.Context);
            }
        }
#pragma warning restore S3267

        var terms = existing.Terms.Concat(newTerms).ToList();
        return new Glossary(
            existing.SchemaVersion, contexts, terms, existing.Abbreviations);
    }

    private static BoundedContext DeclaredContext(string name)
    {
        var description = name == ContextResolver.Unassigned ? UnassignedDescription : null;
        return new BoundedContext(name, [], description);
    }

    /// <summary>Postcondition: merge never removes or replaces an existing entry.</summary>
    private static void AssertAdditive(Glossary existing, Glossary merged)
    {
        Debug.Assert(
            merged.Terms.Count >= existing.Terms.Count,
            "merge must never shrink the glossary");
        Debug.Assert(
            existing.Terms.All(term => merged.Terms.Contains(term)),
            "merge must keep every existing entry unchanged");
        Debug.Assert(
            existing.Abbreviations.Count == merged.Abbreviations.Count,
            "merge must never touch the declared abbreviations");
    }

    /// <summary>Collects the kind of the first observation and up to <see cref="SourceLimit"/> distinct sites.</summary>
    private sealed class NewTermAccumulator
    {
        private readonly TermKey key;
        private readonly TermKind kind;
        private readonly List<string> sites = [];

        internal NewTermAccumulator(TermKey key, TermKind kind)
        {
            this.key = key;
            this.kind = kind;
        }

        internal void AddSite(string site)
        {
            if (sites.Count < SourceLimit && !sites.Contains(site))
            {
                sites.Add(site);
            }
        }

        internal GlossaryTerm ToTerm(DateTime firstSeen)
        {
            return new GlossaryTerm(
                key.Normalized, key.Context, kind, TermStatus.Harvested,
                null, new Dictionary<string, string>(), [], sites, firstSeen);
        }
    }
}
