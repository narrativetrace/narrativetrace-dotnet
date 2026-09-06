// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Glossary;

/// <summary>
/// The whole domain glossary of one repository: bounded contexts plus
/// canonical terms.
/// </summary>
/// <remarks>
/// <para>
/// The in-memory form of <c>glossary.json</c> — simultaneously the
/// translation dictionary, the reviewable domain documentation, and the
/// vocabulary norm clarity diagnostics enforce (ADR-012). Structural
/// invariants are enforced at construction, so an inconsistent glossary can
/// never exist: term identity <c>(context, term)</c> is unique, every term's
/// context is declared, and no deprecated alias equals a canonical term
/// within the same context. Terms are canonicalized to <c>(context, term)</c>
/// order at construction.
/// </para>
/// <para>
/// <see cref="Abbreviations"/> is the schema-2 section: the shorthand the
/// project has decided to accept, each mapped to its spelled-out form. It is
/// declared, never harvested — accepting <c>fx</c> must be a decision someone
/// made, not a side effect of some committed phrase happening to contain the
/// token.
/// </para>
/// </remarks>
public sealed record Glossary
{
    /// <param name="schemaVersion">Glossary file schema version, 1 or 2; at least 1.</param>
    /// <param name="contexts">Bounded contexts keyed by context name.</param>
    /// <param name="terms">Canonical terms; identity is <c>(context, term)</c>.</param>
    /// <param name="abbreviations">
    /// Accepted shorthand mapped to its expansion (<c>fx</c> →
    /// <c>foreign exchange</c>), or null for none. Keys are single tokens,
    /// lowercased at construction because identifier tokens are matched
    /// case-insensitively; expansions must not be blank.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="schemaVersion"/> is below 1, an abbreviation key is
    /// blank, multi-token or duplicated once lowercased, an expansion is
    /// blank, or the glossary is structurally inconsistent (duplicate term
    /// key, undeclared context, alias colliding with a canonical term in its
    /// context).
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="contexts"/> or <paramref name="terms"/> is null.
    /// </exception>
    public Glossary(
        int schemaVersion,
        IReadOnlyDictionary<string, BoundedContext> contexts,
        IReadOnlyList<GlossaryTerm> terms,
        IReadOnlyDictionary<string, string>? abbreviations = null)
    {
        Guard(schemaVersion, contexts, terms);
        SchemaVersion = schemaVersion;
        Abbreviations = NormalizedAbbreviations(abbreviations);
        Contexts = contexts.ToDictionary(
            pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        Terms = terms
            .OrderBy(term => term.Context, StringComparer.Ordinal)
            .ThenBy(term => term.Term, StringComparer.Ordinal)
            .ToArray();
        var violation = FindViolation(Contexts, Terms);
        if (violation is not null)
        {
            throw new ArgumentException(violation);
        }
    }

    /// <summary>Glossary file schema version, 1 or 2.</summary>
    public int SchemaVersion { get; }

    /// <summary>
    /// Accepted shorthand keyed by lowercased token, valued by its expansion;
    /// empty when the project has declared none.
    /// </summary>
    public IReadOnlyDictionary<string, string> Abbreviations { get; }

    /// <summary>Bounded contexts keyed by context name.</summary>
    public IReadOnlyDictionary<string, BoundedContext> Contexts { get; }

    /// <summary>Canonical terms in <c>(context, term)</c> order.</summary>
    public IReadOnlyList<GlossaryTerm> Terms { get; }

    /// <summary>Returns whether this glossary is structurally consistent.</summary>
    /// <remarks>
    /// Constructor guards make this always true for live instances; the
    /// method re-checks the same rules for test-time invariant verification.
    /// </remarks>
    internal bool Invariant()
    {
        return SchemaVersion >= 1
            && FindViolation(Contexts, Terms) is null
            && Abbreviations.All(entry => IsSingleToken(entry.Key)
                && string.Equals(
                    entry.Key, entry.Key.ToLowerInvariant(), StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(entry.Value));
    }

    /// <summary>
    /// Lowercases the keys and rejects what could never match: a blank or
    /// multi-token key can never equal an identifier token, so accepting it
    /// would leave the author believing a declaration that does nothing.
    /// </summary>
    private static Dictionary<string, string> NormalizedAbbreviations(
        IReadOnlyDictionary<string, string>? abbreviations)
    {
        var normalized = new Dictionary<string, string>(StringComparer.Ordinal);
        if (abbreviations is null)
        {
            return normalized;
        }

        foreach (var entry in abbreviations)
        {
            GuardAbbreviation(entry.Key, entry.Value, normalized);
            normalized[entry.Key.ToLowerInvariant()] = entry.Value;
        }

        return normalized;
    }

    private static void GuardAbbreviation(
        string token, string expansion, Dictionary<string, string> soFar)
    {
        if (!IsSingleToken(token))
        {
            throw new ArgumentException(
                $"abbreviation '{token}' must be a single token", nameof(token));
        }

        if (string.IsNullOrWhiteSpace(expansion))
        {
            throw new ArgumentException(
                $"abbreviation '{token}' has a blank expansion", nameof(expansion));
        }

        if (soFar.ContainsKey(token.ToLowerInvariant()))
        {
            throw new ArgumentException($"duplicate abbreviation '{token}'", nameof(token));
        }
    }

    /// <summary>
    /// Whether the text is one whitespace-free token — the only shape an
    /// identifier token can ever equal.
    /// </summary>
    private static bool IsSingleToken(string token)
    {
        return !string.IsNullOrEmpty(token) && !token.Any(char.IsWhiteSpace);
    }

    private static void Guard(
        int schemaVersion,
        IReadOnlyDictionary<string, BoundedContext> contexts,
        IReadOnlyList<GlossaryTerm> terms)
    {
        if (schemaVersion < 1)
        {
            throw new ArgumentException(
                $"schemaVersion must be at least 1: {schemaVersion}",
                nameof(schemaVersion));
        }

        if (contexts is null)
        {
            throw new ArgumentNullException(nameof(contexts));
        }

        if (terms is null)
        {
            throw new ArgumentNullException(nameof(terms));
        }
    }

    /// <summary>Returns a description of the first structural violation, or null when consistent.</summary>
    private static string? FindViolation(
        IReadOnlyDictionary<string, BoundedContext> contexts,
        IReadOnlyList<GlossaryTerm> terms)
    {
        return FindDuplicateTermKey(terms)
            ?? FindUndeclaredContext(contexts, terms)
            ?? FindAliasCollision(terms);
    }

    private static string? FindDuplicateTermKey(IReadOnlyList<GlossaryTerm> terms)
    {
        var duplicate = terms.GroupBy(TermKey.Of).FirstOrDefault(keyed => keyed.Count() > 1);
        return duplicate is null ? null : $"duplicate term key: {duplicate.Key}";
    }

    private static string? FindUndeclaredContext(
        IReadOnlyDictionary<string, BoundedContext> contexts,
        IReadOnlyList<GlossaryTerm> terms)
    {
        var undeclared = terms.FirstOrDefault(term => !contexts.ContainsKey(term.Context));
        return undeclared is null
            ? null
            : $"term '{undeclared.Term}' references undeclared context '{undeclared.Context}'";
    }

    private static string? FindAliasCollision(IReadOnlyList<GlossaryTerm> terms)
    {
        var termKeys = new HashSet<TermKey>(terms.Select(TermKey.Of));
        return terms
            .SelectMany(term => term.Synonyms, (term, synonym) => (term, synonym))
            .Where(pair => termKeys.Contains(
                new TermKey(pair.term.Context, pair.synonym.Alias)))
            .Select(pair => $"alias '{pair.synonym.Alias}' equals a canonical term "
                + $"in context '{pair.term.Context}'")
            .FirstOrDefault();
    }
}
