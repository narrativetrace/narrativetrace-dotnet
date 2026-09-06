// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Clarity;

namespace NarrativeTrace.Glossary;

/// <summary>
/// Reads a repository's committed glossary as the vocabulary clarity scores
/// with.
/// </summary>
/// <remarks>
/// <para>
/// One file, one review workflow. The glossary a team already curates
/// (ADR-012) is the only place a project declares domain vocabulary — there is
/// no second clarity dictionary file to keep in sync. This class is the whole
/// bridge: <see cref="Glossary"/> in, <see cref="DomainVocabulary"/> out, plus
/// the committed-file lookup the test-time and scan-time entry points share.
/// </para>
/// <para>
/// Three rules make the mapping trustworthy. <b>Only the committed file
/// counts</b> — nothing harvested during the run itself is consulted; the
/// commit is the human approval, and a self-expanding vocabulary would make
/// scores non-deterministic and self-certifying. <b>Deprecated synonyms are
/// not vocabulary</b> — an alias exists to be flagged, so promoting it would
/// silence the very issue the glossary declares it for. <b><see
/// cref="TermStatus.Stale"/> terms are not vocabulary</b> — marking a term
/// stale is an explicit human statement that the word left the domain.
/// </para>
/// <para>
/// Bounded contexts are flattened: clarity scores identifiers, which carry no
/// namespace, so every context's vocabulary applies everywhere.
/// <see cref="TermKind.Template"/> entries are skipped — their text is raw
/// narration, not a word.
/// </para>
/// <para>
/// The schema-2 <c>abbreviations</c> section crosses as itself: it is the only
/// source of accepted shorthand, and it carries the expansion the notes spell
/// out with. Terms never accept shorthand, however short they are.
/// </para>
/// </remarks>
public static class GlossaryVocabulary
{
    /// <summary>Name of the committed glossary file, as the harvest writes it.</summary>
    public const string GlossaryFile = "glossary.json";

    /// <summary>
    /// Maps a glossary onto the vocabulary the clarity scorers consult.
    /// </summary>
    /// <param name="glossary">The committed glossary; must not be null.</param>
    /// <returns>The project's declared vocabulary.</returns>
    /// <remarks>
    /// A verb phrase contributes its leading verb as a domain verb and the rest
    /// as domain nouns (<c>settle trade</c> → verb <c>settle</c>, noun
    /// <c>trade</c>); words and noun phrases contribute every token as a domain
    /// noun. Multi-word terms therefore still teach, one token at a time, which
    /// is the granularity identifiers are scored at. The declared abbreviations
    /// cross unchanged — a term is never accepted shorthand.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="glossary"/> is null.</exception>
    public static DomainVocabulary Of(Glossary glossary)
    {
        if (glossary is null)
        {
            throw new ArgumentNullException(nameof(glossary));
        }

        var verbs = new List<string>();
        var nouns = new List<string>();
        foreach (var term in glossary.Terms)
        {
            Collect(term, verbs, nouns);
        }

        return DomainVocabulary.Of(verbs, nouns, glossary.Abbreviations);
    }

    /// <summary>
    /// Reads the committed glossary at the given path.
    /// </summary>
    /// <param name="glossaryFilePath">
    /// Path to the committed <c>glossary.json</c> — what
    /// <see cref="GlossarySettings.ResolveFile"/> returns — or null when the
    /// upward search found none and the feature is therefore off.
    /// </param>
    /// <returns>
    /// The project's declared vocabulary, empty when no glossary is committed.
    /// </returns>
    /// <remarks>
    /// A null path, a blank path, and a path to a file that does not exist all
    /// mean the same thing — the project has declared no vocabulary — and yield
    /// <see cref="DomainVocabulary.Empty"/>. A glossary that exists but cannot
    /// be parsed is a different matter and throws, mirroring
    /// <see cref="GlossaryJsonReader"/>'s fail-loudly contract: a committed
    /// file with a typo is a defect, not an absence.
    /// </remarks>
    public static DomainVocabulary FromFile(string? glossaryFilePath)
    {
        if (string.IsNullOrWhiteSpace(glossaryFilePath)
            || !File.Exists(glossaryFilePath))
        {
            return DomainVocabulary.Empty;
        }

        return Of(GlossaryJsonReader.Read(File.ReadAllText(glossaryFilePath!)));
    }

    /// <summary>
    /// Reads the committed glossary from the directory holding it.
    /// </summary>
    /// <param name="glossaryDirectory">
    /// Directory holding the committed <c>glossary.json</c>, or null.
    /// </param>
    /// <returns>
    /// The project's declared vocabulary, empty when no glossary is committed.
    /// </returns>
    public static DomainVocabulary From(string? glossaryDirectory)
    {
        return string.IsNullOrWhiteSpace(glossaryDirectory)
            ? DomainVocabulary.Empty
            : FromFile(Path.Combine(glossaryDirectory!, GlossaryFile));
    }

    private static void Collect(
        GlossaryTerm term, List<string> verbs, List<string> nouns)
    {
        if (term.Status == TermStatus.Stale || term.Kind == TermKind.Template)
        {
            return;
        }

        var tokens = term.Term.Split(' ');
        if (term.Kind == TermKind.VerbPhrase)
        {
            verbs.Add(tokens[0]);
            for (var i = 1; i < tokens.Length; i++)
            {
                nouns.Add(tokens[i]);
            }

            return;
        }

        nouns.AddRange(tokens);
    }
}
