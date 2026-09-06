// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Clarity;

/// <summary>
/// Composes the human-readable "why" behind an element's clarity score.
/// </summary>
/// <remarks>
/// Clarity is meant to teach, not just judge — a bare score is a verdict with
/// no appeal. This turns the same dictionary knowledge the scorers already use
/// into one plain-language note per element, emitted at every score, so a good
/// name shows why it is good and a weak one shows what to change. Shorthand the
/// project accepted is spelled out from its own glossary rather than passed
/// over in silence: <c>noun 'fx' (foreign exchange)</c>.
/// </remarks>
public static class ElementNoteComposer
{
    /// <summary>Explains a method name's verb and object quality.</summary>
    /// <param name="methodName">Method name to explain; must not be blank.</param>
    /// <returns>A note naming the leading verb and the trailing noun.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="methodName"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="methodName"/> yields no tokens — it is blank, or made up
    /// entirely of the delimiters the tokenizer strips.
    /// </exception>
    /// <param name="vocabulary">
    /// The project's committed glossary vocabulary, so the note speaks the
    /// project's language; null uses the built-in dictionaries alone.
    /// </param>
    public static string MethodNote(
        string methodName, DomainVocabulary? vocabulary = null)
    {
        var tokens = Tokenize(methodName);
        var verb = tokens[0];
        var noun = tokens[tokens.Count - 1];
        return VerbPhrase(verb, vocabulary) + " + " + NounPhrase(noun, vocabulary);
    }

    /// <summary>
    /// Tokenizes, rejecting anything that leaves nothing to explain. Blankness
    /// is checked before tokenizing: whitespace is not a delimiter the
    /// tokenizer strips, so "   " would otherwise survive as a token and be
    /// reported back as a verb.
    /// </summary>
    private static IReadOnlyList<string> Tokenize(string methodName)
    {
        if (methodName is null)
        {
            throw new ArgumentNullException(nameof(methodName));
        }

        if (string.IsNullOrWhiteSpace(methodName))
        {
            throw new ArgumentException(
                "Method name is blank.", nameof(methodName));
        }

        var tokens = IdentifierTokenizer.Tokenize(methodName);
        if (tokens.Count == 0)
        {
            throw new ArgumentException(
                "Method name has no tokens to explain.", nameof(methodName));
        }

        return tokens;
    }

    private static string VerbPhrase(string verb, DomainVocabulary? vocabulary)
    {
        var phrase = VerbDictionary.Classify(verb, vocabulary) == VerbCategory.Generic
            ? $"Generic verb '{verb}'"
            : $"Verb '{verb}'";
        return phrase + SpelledOut(verb, vocabulary);
    }

    private static string NounPhrase(string noun, DomainVocabulary? vocabulary)
    {
        var phrase = GenericTokenDetector.Classify(noun, vocabulary) == TokenTier.Vague
            ? $"vague noun '{noun}'"
            : $"noun '{noun}'";
        return phrase + SpelledOut(noun, vocabulary);
    }

    /// <summary>
    /// The parenthesized expansion of accepted shorthand, empty for every other
    /// token. Teaching the reader the project's own word beats staying silent
    /// about a token the project decided not to spell out in code.
    /// </summary>
    private static string SpelledOut(string token, DomainVocabulary? vocabulary)
    {
        return vocabulary?.ExpansionOf(token) is { } expansion
            ? $" ({expansion})"
            : string.Empty;
    }
}
