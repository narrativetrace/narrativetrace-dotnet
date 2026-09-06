// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Glossary;

/// <summary>
/// Lookup from <c>(context, normalized alias)</c> to the canonical term that
/// deprecates it.
/// </summary>
/// <remarks>
/// Harvesting and violation reporting both ask the same question — "is this
/// normalized phrase a deprecated alias here?" — so the answer is precomputed
/// once per glossary. Matching is exact on the whole normalized phrase and
/// scoped per context: an alias is only recognized in contexts where its
/// canonical term is defined.
/// </remarks>
public sealed class AliasIndex
{
    private readonly Dictionary<TermKey, GlossaryTerm> canonicalByAlias;

    private AliasIndex(Dictionary<TermKey, GlossaryTerm> canonicalByAlias)
    {
        this.canonicalByAlias = canonicalByAlias;
    }

    /// <summary>Builds the index from a glossary's synonym declarations.</summary>
    /// <param name="glossary">Glossary to index; must not be null.</param>
    /// <returns>Index over every <c>(term context, synonym alias)</c> pair.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="glossary"/> is null.</exception>
    public static AliasIndex Of(Glossary glossary)
    {
        if (glossary is null)
        {
            throw new ArgumentNullException(nameof(glossary));
        }

        var byAlias = new Dictionary<TermKey, GlossaryTerm>();
        foreach (var term in glossary.Terms)
        {
            foreach (var synonym in term.Synonyms)
            {
                byAlias[new TermKey(term.Context, synonym.Alias)] = term;
            }
        }

        return new AliasIndex(byAlias);
    }

    /// <summary>Returns whether the key names a deprecated alias in its context.</summary>
    /// <param name="key">Context plus normalized phrase; must not be null.</param>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is null.</exception>
    public bool IsAlias(TermKey key)
    {
        return CanonicalFor(key) is not null;
    }

    /// <summary>Returns the canonical term that deprecates the given alias.</summary>
    /// <param name="key">Context plus normalized phrase; must not be null.</param>
    /// <returns>The canonical term, or null when the phrase is not an alias in that context.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is null.</exception>
    public GlossaryTerm? CanonicalFor(TermKey key)
    {
        if (key is null)
        {
            throw new ArgumentNullException(nameof(key));
        }

        return canonicalByAlias.TryGetValue(key, out var canonical) ? canonical : null;
    }
}
