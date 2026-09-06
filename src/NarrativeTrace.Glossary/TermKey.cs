// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Glossary;

/// <summary>
/// Identity of a glossary term: bounded context plus normalized term text.
/// </summary>
/// <remarks>
/// All term and alias lookups key on this pair — the same normalized text is
/// a distinct concept in each context.
/// </remarks>
/// <param name="Context">Bounded-context name.</param>
/// <param name="Normalized">Term text in normalized form (lowercase, space-separated).</param>
public sealed record TermKey(string Context, string Normalized)
{
    /// <summary>Returns the identity key of a glossary term.</summary>
    public static TermKey Of(GlossaryTerm term)
    {
        if (term is null)
        {
            throw new ArgumentNullException(nameof(term));
        }

        return new TermKey(term.Context, term.Term);
    }
}
