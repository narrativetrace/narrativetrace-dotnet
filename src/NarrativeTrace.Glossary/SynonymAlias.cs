// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Glossary;

/// <summary>A deprecated alias of a canonical glossary term.</summary>
/// <remarks>
/// Records "this phrasing exists in the wild; always use the canonical term
/// instead". Harvesting suppresses aliases (never re-adds them as terms) and
/// flags their use in code as a vocabulary violation.
/// </remarks>
public sealed record SynonymAlias
{
    /// <param name="alias">The deprecated phrasing, in normalized form (lowercase, space-separated).</param>
    /// <param name="note">Optional human context for why the alias exists.</param>
    /// <exception cref="ArgumentException"><paramref name="alias"/> is null, empty, or whitespace.</exception>
    public SynonymAlias(string alias, string? note = null)
    {
        if (string.IsNullOrWhiteSpace(alias))
        {
            throw new ArgumentException("alias must not be blank", nameof(alias));
        }

        Alias = alias;
        Note = note;
    }

    /// <summary>The deprecated phrasing, in normalized form (lowercase, space-separated).</summary>
    public string Alias { get; }

    /// <summary>Optional human context for why the alias exists, or null.</summary>
    public string? Note { get; }
}
