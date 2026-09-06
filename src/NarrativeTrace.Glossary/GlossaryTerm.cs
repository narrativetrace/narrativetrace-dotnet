// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Glossary;

/// <summary>
/// One entry of the domain glossary: a canonical term within a bounded
/// context.
/// </summary>
/// <remarks>
/// The unit of ubiquitous language. Term identity is <c>(context, term)</c>;
/// the same term may exist independently in two contexts with different
/// definitions and translations. Human-owned fields
/// (<see cref="Definition"/>, <see cref="Translations"/>,
/// <see cref="Synonyms"/>, curated status) are never overwritten by
/// harvesting.
/// </remarks>
public sealed record GlossaryTerm
{
    /// <param name="term">Canonical term in normalized form (lowercase, space-separated).</param>
    /// <param name="context">Bounded-context name this term belongs to.</param>
    /// <param name="kind">Grammatical shape of the term.</param>
    /// <param name="status">Curation lifecycle state.</param>
    /// <param name="definition">Human-written meaning, or null if not yet curated.</param>
    /// <param name="translations">Locale tag → translated term (human- or Pro-owned).</param>
    /// <param name="synonyms">Deprecated aliases of this term.</param>
    /// <param name="sources">First observed code sites, as <c>Class.Member</c> strings.</param>
    /// <param name="firstSeen">Date the term first entered the glossary; set once, never updated.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="term"/> or <paramref name="context"/> is blank, or
    /// <paramref name="firstSeen"/> carries a time-of-day component.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="kind"/> or <paramref name="status"/> is not a defined enum value.
    /// </exception>
    /// <exception cref="ArgumentNullException">Any collection argument is null.</exception>
    public GlossaryTerm(
        string term,
        string context,
        TermKind kind,
        TermStatus status,
        string? definition,
        IReadOnlyDictionary<string, string> translations,
        IReadOnlyList<SynonymAlias> synonyms,
        IReadOnlyList<string> sources,
        DateTime firstSeen)
    {
        GuardScalars(term, context, kind, status, firstSeen);
        GuardCollections(translations, synonyms, sources);
        Term = term;
        Context = context;
        Kind = kind;
        Status = status;
        Definition = definition;
        Translations = translations.ToDictionary(
            pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        Synonyms = synonyms.ToArray();
        Sources = sources.ToArray();
        FirstSeen = firstSeen;
    }

    /// <summary>Canonical term in normalized form (lowercase, space-separated).</summary>
    public string Term { get; }

    /// <summary>Bounded-context name this term belongs to.</summary>
    public string Context { get; }

    /// <summary>Grammatical shape of the term.</summary>
    public TermKind Kind { get; }

    /// <summary>Curation lifecycle state.</summary>
    public TermStatus Status { get; }

    /// <summary>Human-written meaning, or null if not yet curated. Human-owned.</summary>
    public string? Definition { get; }

    /// <summary>Locale tag → translated term. Human- or Pro-owned.</summary>
    public IReadOnlyDictionary<string, string> Translations { get; }

    /// <summary>Deprecated aliases of this term. Human-owned.</summary>
    public IReadOnlyList<SynonymAlias> Synonyms { get; }

    /// <summary>First observed code sites, as <c>Class.Member</c> strings.</summary>
    public IReadOnlyList<string> Sources { get; }

    /// <summary>Date the term first entered the glossary; date-only, set once, never updated.</summary>
    public DateTime FirstSeen { get; }

    private static void GuardCollections(
        IReadOnlyDictionary<string, string> translations,
        IReadOnlyList<SynonymAlias> synonyms,
        IReadOnlyList<string> sources)
    {
        if (translations is null)
        {
            throw new ArgumentNullException(nameof(translations));
        }

        if (synonyms is null)
        {
            throw new ArgumentNullException(nameof(synonyms));
        }

        if (sources is null)
        {
            throw new ArgumentNullException(nameof(sources));
        }
    }

    private static void GuardScalars(
        string term, string context, TermKind kind, TermStatus status, DateTime firstSeen)
    {
        if (string.IsNullOrWhiteSpace(term))
        {
            throw new ArgumentException("term must not be blank", nameof(term));
        }

        if (string.IsNullOrWhiteSpace(context))
        {
            throw new ArgumentException("context must not be blank", nameof(context));
        }

        // JsonName rejects undefined enum values with ArgumentOutOfRangeException.
        kind.JsonName();
        status.JsonName();
        if (firstSeen.TimeOfDay != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "firstSeen must be a date without a time component", nameof(firstSeen));
        }
    }
}
