// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Glossary;

/// <summary>One phrase's translation outcome.</summary>
/// <param name="Text">
/// The translated phrase, or the original phrase when no translation applies.
/// </param>
/// <param name="Complete">
/// True only when every part of the phrase was covered by the glossary.
/// </param>
public sealed record PhraseTranslation(string Text, bool Complete);

/// <summary>
/// Translates normalized glossary phrases into a target locale.
/// </summary>
/// <remarks>
/// The lookup engine of trace translation. The chain for a phrase within a
/// bounded context is: exact phrase entry → per-token word entries →
/// untranslated. Untranslated phrases render as-is and report
/// <see cref="PhraseTranslation.Complete"/> false, so callers can collect
/// them into the "glossary gaps" footer that drives glossary completion.
/// </remarks>
public sealed class GlossaryTranslator
{
    /// <summary>
    /// Context name → normalized term → glossary entry; built once, terms are
    /// immutable.
    /// </summary>
    private readonly Dictionary<string, Dictionary<string, GlossaryTerm>> termsByContext;

    /// <param name="glossary">Glossary supplying per-context terms; must not be null.</param>
    /// <exception cref="ArgumentNullException"><paramref name="glossary"/> is null.</exception>
    public GlossaryTranslator(Glossary glossary)
    {
        if (glossary is null)
        {
            throw new ArgumentNullException(nameof(glossary));
        }

        termsByContext = new Dictionary<string, Dictionary<string, GlossaryTerm>>(
            StringComparer.Ordinal);
        foreach (var term in glossary.Terms)
        {
            if (!termsByContext.TryGetValue(term.Context, out var terms))
            {
                terms = new Dictionary<string, GlossaryTerm>(StringComparer.Ordinal);
                termsByContext[term.Context] = terms;
            }

            terms[term.Term] = term;
        }
    }

    /// <summary>Translates a normalized phrase within a context into a locale.</summary>
    /// <param name="phrase">
    /// Normalized phrase (lowercase, space-separated), as produced by term
    /// harvesting; must not be blank.
    /// </param>
    /// <param name="context">Bounded-context name the phrase was observed in; must not be blank.</param>
    /// <param name="locale">Target locale tag (e.g. <c>es</c>); must not be blank.</param>
    /// <returns>The translated phrase and whether the glossary covered all of it.</returns>
    /// <exception cref="ArgumentException">Any argument is null or blank.</exception>
    public PhraseTranslation Translate(string phrase, string context, string locale)
    {
        RequireNonBlank(phrase, nameof(phrase));
        RequireNonBlank(context, nameof(context));
        RequireNonBlank(locale, nameof(locale));
        var exact = Lookup(phrase, context, locale);
        return exact is not null
            ? new PhraseTranslation(exact, true)
            : TranslateTokenByToken(phrase, context, locale);
    }

    /// <summary>
    /// Looks up the locale variant of a template entry, keyed on the raw
    /// template text.
    /// </summary>
    /// <remarks>
    /// Exact lookup only — templates never fall back to per-token
    /// translation, since their text carries placeholders and prose rather
    /// than a normalized phrase.
    /// </remarks>
    /// <param name="rawTemplate">Raw template text with placeholders intact; must not be blank.</param>
    /// <param name="context">Bounded-context name the template was observed in; must not be blank.</param>
    /// <param name="locale">Target locale tag; must not be blank.</param>
    /// <returns>The locale variant, or null when the glossary carries none.</returns>
    /// <exception cref="ArgumentException">Any argument is null or blank.</exception>
    public string? TemplateVariant(string rawTemplate, string context, string locale)
    {
        RequireNonBlank(rawTemplate, nameof(rawTemplate));
        RequireNonBlank(context, nameof(context));
        RequireNonBlank(locale, nameof(locale));
        return Lookup(rawTemplate, context, locale);
    }

    private PhraseTranslation TranslateTokenByToken(
        string phrase, string context, string locale)
    {
        var tokens = phrase.Split(' ');
        var translated = new string[tokens.Length];
        var complete = true;
        for (var i = 0; i < tokens.Length; i++)
        {
            var word = Lookup(tokens[i], context, locale);
            if (word is null)
            {
                complete = false;
                translated[i] = tokens[i];
            }
            else
            {
                translated[i] = word;
            }
        }

        return new PhraseTranslation(string.Join(" ", translated), complete);
    }

    private string? Lookup(string phrase, string context, string locale)
    {
        return termsByContext.TryGetValue(context, out var terms)
            && terms.TryGetValue(phrase, out var entry)
            && entry.Translations.TryGetValue(locale, out var translation)
            ? translation
            : null;
    }

    private static void RequireNonBlank(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{name} must not be blank", name);
        }
    }
}
