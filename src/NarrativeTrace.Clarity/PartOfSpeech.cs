// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Clarity;

/// <summary>
/// The grammatical role a name token appears to play.
/// </summary>
/// <remarks>
/// Determined by morphology — suffixes and shape — with no dictionary lookup and
/// no surrounding context, so it is a heuristic. English-oriented: tokens from
/// other languages, and domain jargon, commonly land in <see cref="Unknown"/>,
/// which the scorers treat as "no signal" rather than as a penalty.
/// </remarks>
public enum PartOfSpeech
{
    /// <summary>A thing — what a class, parameter or property name should be.</summary>
    Noun,

    /// <summary>An action — what a method name should lead with.</summary>
    Verb,

    /// <summary>A qualifier, typically modifying a noun elsewhere in the name.</summary>
    Adjective,

    /// <summary>Not classifiable. Carries no signal; it is not itself a mark against the name.</summary>
    Unknown,
}
