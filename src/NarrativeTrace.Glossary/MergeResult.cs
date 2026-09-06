// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Glossary;

/// <summary>
/// Outcome of one additive merge of a harvest into an existing glossary.
/// </summary>
/// <remarks>
/// The single value from which all run output derives — the merged glossary
/// is written back to disk, <see cref="NewTerms"/> feeds the console summary,
/// and <see cref="SuppressedAliasUses"/> feeds vocabulary-violation
/// reporting.
/// </remarks>
public sealed record MergeResult
{
    /// <param name="glossary">Merged glossary; a superset of the input, never mutated entries.</param>
    /// <param name="newTerms">Terms added by this merge, in canonical order.</param>
    /// <param name="suppressedAliasUses">
    /// Observations whose phrase matched a deprecated alias in its context —
    /// kept out of the glossary, reported as violations.
    /// </param>
    /// <exception cref="ArgumentNullException">Any argument is null.</exception>
    public MergeResult(
        Glossary glossary,
        IReadOnlyList<GlossaryTerm> newTerms,
        IReadOnlyList<HarvestCandidate> suppressedAliasUses)
    {
        Glossary = glossary ?? throw new ArgumentNullException(nameof(glossary));
        NewTerms = (newTerms ?? throw new ArgumentNullException(nameof(newTerms)))
            .ToArray();
        SuppressedAliasUses =
            (suppressedAliasUses
                ?? throw new ArgumentNullException(nameof(suppressedAliasUses)))
            .ToArray();
    }

    /// <summary>Merged glossary; a superset of the input, never mutated entries.</summary>
    public Glossary Glossary { get; }

    /// <summary>Terms added by this merge, in canonical order.</summary>
    public IReadOnlyList<GlossaryTerm> NewTerms { get; }

    /// <summary>Suppressed alias observations, reported as vocabulary violations.</summary>
    public IReadOnlyList<HarvestCandidate> SuppressedAliasUses { get; }
}
