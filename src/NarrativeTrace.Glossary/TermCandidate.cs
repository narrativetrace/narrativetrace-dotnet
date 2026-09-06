// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Glossary;

/// <summary>One harvestable phrase produced by normalization.</summary>
public sealed record TermCandidate
{
    /// <param name="phrase">Normalized phrase text.</param>
    /// <param name="kind">Grammatical shape derived from structure (leading verb, token count).</param>
    /// <exception cref="ArgumentException"><paramref name="phrase"/> is blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> is not a defined value.</exception>
    public TermCandidate(string phrase, TermKind kind)
    {
        if (string.IsNullOrWhiteSpace(phrase))
        {
            throw new ArgumentException("phrase must not be blank", nameof(phrase));
        }

        kind.JsonName();
        Phrase = phrase;
        Kind = kind;
    }

    /// <summary>Normalized phrase text.</summary>
    public string Phrase { get; }

    /// <summary>Grammatical shape derived from structure.</summary>
    public TermKind Kind { get; }
}
