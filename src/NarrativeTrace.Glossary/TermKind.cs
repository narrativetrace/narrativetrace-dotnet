// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Glossary;

/// <summary>Grammatical shape of a glossary term.</summary>
/// <remarks>
/// Lets harvesting and translation treat words, phrases, and narration
/// templates differently — template entries key on raw template text and hold
/// per-locale template variants, while word/phrase entries key on normalized
/// identifier language.
/// </remarks>
public enum TermKind
{
    /// <summary>A single word, keyed on its normalized identifier form.</summary>
    Word,

    /// <summary>A multi-word noun phrase naming a thing, e.g. <c>order line</c>.</summary>
    NounPhrase,

    /// <summary>A multi-word verb phrase naming an action, e.g. <c>place order</c>.</summary>
    VerbPhrase,

    /// <summary>
    /// A narration template. Keyed on the raw template text rather than
    /// normalized language, and carries per-locale variants — so template
    /// entries do not interoperate with the word and phrase kinds.
    /// </summary>
    Template,
}
