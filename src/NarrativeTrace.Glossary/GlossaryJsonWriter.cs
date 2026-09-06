// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace NarrativeTrace.Glossary;

/// <summary>Deterministic serializer for <c>glossary.json</c>.</summary>
/// <remarks>
/// <para>
/// The committed glossary must be byte-identical whenever vocabulary is
/// unchanged (anti-churn, ADR-012). Determinism rules: contexts sorted by
/// name, terms in the model's canonical <c>(context, term)</c> order
/// (enforced by the <see cref="Glossary"/> constructor), fixed key order,
/// 2-space indent, trailing newline. The writer never emits volatile
/// statistics — those belong to build-directory reports.
/// </para>
/// <para>
/// The <c>abbreviations</c> section and its <c>schemaVersion: 2</c> stamp are
/// emitted only when the section has entries, so a repository that never used
/// the feature keeps writing the byte-identical schema-1 file it wrote before
/// the section existed. The stamp never goes down: a glossary already at 2
/// stays at 2 even after its last abbreviation is removed, or reading and
/// rewriting it would silently rewrite the file.
/// </para>
/// </remarks>
public static class GlossaryJsonWriter
{
    /// <summary>Serializes a glossary to its canonical JSON text.</summary>
    /// <param name="glossary">Glossary to serialize; must not be null.</param>
    /// <returns>Deterministic JSON document ending in a newline.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="glossary"/> is null.</exception>
    public static string Write(Glossary glossary)
    {
        if (glossary is null)
        {
            throw new ArgumentNullException(nameof(glossary));
        }

        var version = EffectiveVersion(glossary).ToString(CultureInfo.InvariantCulture);
        var output = "{\n"
            + $"  \"schemaVersion\": {version},\n"
            + $"  \"contexts\": {RenderContexts(glossary)},\n"
            + RenderAbbreviations(glossary.Abbreviations)
            + $"  \"terms\": {RenderTerms(glossary)}\n"
            + "}\n";
        // Indexing instead of EndsWith: netstandard2.0 lacks the char overload
        // and the analyzer rejects the string one.
        Debug.Assert(
            output[output.Length - 1] == '\n',
            "canonical file must end with a trailing newline");
        return output;
    }

    /// <summary>
    /// The version the file is stamped with: 2 once abbreviations are
    /// declared, otherwise whatever the glossary already carried.
    /// </summary>
    private static int EffectiveVersion(Glossary glossary)
    {
        return glossary.Abbreviations.Count > 0
            ? Math.Max(glossary.SchemaVersion, 2)
            : glossary.SchemaVersion;
    }

    /// <summary>Renders the whole line, empty when the section has no entries.</summary>
    private static string RenderAbbreviations(IReadOnlyDictionary<string, string> abbreviations)
    {
        if (abbreviations.Count == 0)
        {
            return string.Empty;
        }

        var rendered = abbreviations.Keys
            .OrderBy(token => token, StringComparer.Ordinal)
            .Select(token => $"    {Quoted(token)}: {Quoted(abbreviations[token])}");
        return "  \"abbreviations\": {\n" + string.Join(",\n", rendered) + "\n  },\n";
    }

    private static string RenderContexts(Glossary glossary)
    {
        if (glossary.Contexts.Count == 0)
        {
            return "{}";
        }

        var rendered = glossary.Contexts.Keys
            .OrderBy(name => name, StringComparer.Ordinal)
            .Select(name => RenderContext(glossary.Contexts[name]));
        return "{\n" + string.Join(",\n", rendered) + "\n  }";
    }

    private static string RenderContext(BoundedContext context)
    {
        var body = new StringBuilder();
        body.Append("    ").Append(Quoted(context.Name)).Append(": {\n");
        body.Append("      \"packages\": ").Append(RenderStringArray(context.Packages));
        if (context.Description is not null)
        {
            body.Append(",\n      \"description\": ").Append(Quoted(context.Description));
        }

        body.Append("\n    }");
        return body.ToString();
    }

    private static string RenderTerms(Glossary glossary)
    {
        if (glossary.Terms.Count == 0)
        {
            return "[]";
        }

        var rendered = glossary.Terms.Select(RenderTerm);
        return "[\n" + string.Join(",\n", rendered) + "\n  ]";
    }

    private static string RenderTerm(GlossaryTerm term)
    {
        var fields = new List<string>
        {
            $"\"term\": {Quoted(term.Term)}",
            $"\"context\": {Quoted(term.Context)}",
            $"\"kind\": {Quoted(term.Kind.JsonName())}",
            $"\"status\": {Quoted(term.Status.JsonName())}",
        };
        AddOptionalFields(fields, term);
        fields.Add($"\"firstSeen\": {Quoted(FormatDate(term.FirstSeen))}");
        return "    {\n      " + string.Join(",\n      ", fields) + "\n    }";
    }

    private static void AddOptionalFields(List<string> fields, GlossaryTerm term)
    {
        if (term.Definition is not null)
        {
            fields.Add($"\"definition\": {Quoted(term.Definition)}");
        }

        if (term.Translations.Count > 0)
        {
            fields.Add($"\"translations\": {RenderTranslations(term.Translations)}");
        }

        if (term.Synonyms.Count > 0)
        {
            fields.Add($"\"synonyms\": {RenderSynonyms(term.Synonyms)}");
        }

        if (term.Sources.Count > 0)
        {
            fields.Add($"\"sources\": {RenderStringArray(term.Sources)}");
        }
    }

    private static string RenderTranslations(IReadOnlyDictionary<string, string> translations)
    {
        var rendered = translations.Keys
            .OrderBy(locale => locale, StringComparer.Ordinal)
            .Select(locale => $"        {Quoted(locale)}: {Quoted(translations[locale])}");
        return "{\n" + string.Join(",\n", rendered) + "\n      }";
    }

    private static string RenderSynonyms(IReadOnlyList<SynonymAlias> synonyms)
    {
        var rendered = synonyms.Select(RenderSynonym);
        return "[\n" + string.Join(",\n", rendered) + "\n      ]";
    }

    private static string RenderSynonym(SynonymAlias synonym)
    {
        var body = $"{{ \"alias\": {Quoted(synonym.Alias)}";
        if (synonym.Note is not null)
        {
            body += $", \"note\": {Quoted(synonym.Note)}";
        }

        return $"        {body} }}";
    }

    private static string RenderStringArray(IReadOnlyList<string> values)
    {
        return "[" + string.Join(", ", values.Select(Quoted)) + "]";
    }

    private static string FormatDate(DateTime date)
    {
        return date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    private static string Quoted(string value)
    {
        return "\"" + JsonEscape.Escape(value) + "\"";
    }
}
