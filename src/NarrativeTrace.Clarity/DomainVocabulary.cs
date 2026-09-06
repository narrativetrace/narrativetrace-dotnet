// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Clarity;

/// <summary>
/// The vocabulary one project has declared to be its own, as the clarity
/// scorers see it.
/// </summary>
/// <remarks>
/// <para>
/// Makes the built-in dictionaries extensible per project without a second
/// configuration file. A repository's committed glossary (ADR-012) is the
/// single vocabulary source; <c>NarrativeTrace.Glossary</c> maps it onto this
/// type, and every dictionary consults it beside its own tiers. Clarity
/// therefore teaches in the project's own language instead of scoring its
/// domain words as unknown.
/// </para>
/// <para>
/// Two rules are deliberate and load-bearing. <b>Only single tokens count</b> —
/// the scorers tokenize identifiers, so a multi-word glossary phrase
/// (<c>credit tranche</c>) can never match one token and is dropped at
/// construction rather than silently never matching. <b>The built-in
/// dictionaries keep their authority</b> — this type answers questions, it does
/// not override answers; each dictionary decides where the project vocabulary
/// sits in its own precedence order, and none lets a project promote a generic
/// word to domain vocabulary.
/// </para>
/// <para>
/// Accepted shorthand is a <b>separate declaration</b>, not a side effect of
/// the terms. A project accepts <c>fx</c> by listing it in the glossary's
/// <c>abbreviations</c> section, which also carries the expansion the notes
/// spell it out with; committing a phrase that happens to contain the token
/// teaches it as a domain noun and nothing more.
/// </para>
/// <para>
/// <see cref="Empty"/> is the normal state: a project without a committed
/// glossary scores exactly as it did before this type existed.
/// </para>
/// </remarks>
public sealed class DomainVocabulary
{
    /// <summary>The vocabulary of a project that has declared none.</summary>
    public static readonly DomainVocabulary Empty =
        new(NoTokens(), NoTokens(), NoExpansions());

    private readonly HashSet<string> verbs;
    private readonly HashSet<string> nouns;
    private readonly Dictionary<string, string> abbreviations;

    private DomainVocabulary(
        HashSet<string> verbs,
        HashSet<string> nouns,
        Dictionary<string, string> abbreviations)
    {
        this.verbs = verbs;
        this.nouns = nouns;
        this.abbreviations = abbreviations;
    }

    /// <summary>Verbs the project declares, lowercased single tokens.</summary>
    public IReadOnlyCollection<string> Verbs => this.verbs;

    /// <summary>Nouns the project declares, lowercased single tokens.</summary>
    public IReadOnlyCollection<string> Nouns => this.nouns;

    /// <summary>Shorthand the project accepts, lowercased single tokens.</summary>
    public IReadOnlyCollection<string> Abbreviations => this.abbreviations.Keys;

    /// <summary>Whether the project declared nothing this vocabulary answers for.</summary>
    public bool IsEmpty =>
        this.verbs.Count == 0 && this.nouns.Count == 0 && this.abbreviations.Count == 0;

    /// <summary>
    /// Builds a vocabulary from raw declared terms, keeping only the
    /// single-token ones.
    /// </summary>
    /// <param name="verbs">Verb terms; blank and multi-word entries are dropped.</param>
    /// <param name="nouns">Noun terms; blank and multi-word entries are dropped.</param>
    /// <param name="abbreviations">
    /// Accepted shorthand mapped to its expansion, or null for none; blank and
    /// multi-word tokens are dropped on the same rule as the terms.
    /// </param>
    public static DomainVocabulary Of(
        IEnumerable<string> verbs,
        IEnumerable<string> nouns,
        IReadOnlyDictionary<string, string>? abbreviations = null)
    {
        if (verbs is null)
        {
            throw new ArgumentNullException(nameof(verbs));
        }

        if (nouns is null)
        {
            throw new ArgumentNullException(nameof(nouns));
        }

        return new DomainVocabulary(
            SingleTokens(verbs), SingleTokens(nouns), Expansions(abbreviations));
    }

    /// <summary>Whether the project declared this token as one of its verbs.</summary>
    public bool IsDomainVerb(string token)
    {
        return this.verbs.Contains(Lower(token));
    }

    /// <summary>Whether the project declared this token as one of its nouns.</summary>
    public bool IsDomainNoun(string token)
    {
        return this.nouns.Contains(Lower(token));
    }

    /// <summary>
    /// Whether the project listed this token as accepted shorthand.
    /// </summary>
    /// <remarks>
    /// Only the glossary's <c>abbreviations</c> section answers yes. Accepting
    /// shorthand is a decision someone made and reviewed, so the abbreviation
    /// dictionary stops asking for it to be spelled out; a token that merely
    /// appears inside a committed term is not one.
    /// </remarks>
    public bool IsAcceptedAbbreviation(string token)
    {
        return this.abbreviations.ContainsKey(Lower(token));
    }

    /// <summary>
    /// The declared expansion of an accepted abbreviation, or null when the
    /// project did not accept this token.
    /// </summary>
    /// <remarks>
    /// What lets a note teach <c>fx → foreign exchange</c> in the project's own
    /// words instead of falling silent.
    /// </remarks>
    public string? ExpansionOf(string token)
    {
        return this.abbreviations.TryGetValue(Lower(token), out var expansion)
            ? expansion
            : null;
    }

    private static HashSet<string> NoTokens()
    {
        return new HashSet<string>(StringComparer.Ordinal);
    }

    private static Dictionary<string, string> NoExpansions()
    {
        return new Dictionary<string, string>(StringComparer.Ordinal);
    }

    /// <summary>
    /// Keeps the single-token entries, on the same rule as the terms: a
    /// multi-word key could never equal an identifier token.
    /// </summary>
    private static Dictionary<string, string> Expansions(
        IReadOnlyDictionary<string, string>? abbreviations)
    {
        var kept = NoExpansions();
        foreach (var entry in abbreviations ?? NoExpansions())
        {
            var token = entry.Key.Trim().ToLowerInvariant();
            if (token.Length > 0 && token.IndexOf(' ') == -1)
            {
                kept[token] = entry.Value;
            }
        }

        return kept;
    }

    private static string Lower(string token)
    {
        if (token is null)
        {
            throw new ArgumentNullException(nameof(token));
        }

        return token.ToLowerInvariant();
    }

    private static HashSet<string> SingleTokens(IEnumerable<string> terms)
    {
        var kept = new HashSet<string>(StringComparer.Ordinal);
        foreach (var term in terms)
        {
            var normalized = term.Trim().ToLowerInvariant();
            if (normalized.Length > 0 && normalized.IndexOf(' ') == -1)
            {
                kept.Add(normalized);
            }
        }

        return kept;
    }
}
