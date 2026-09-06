// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Globalization;

namespace NarrativeTrace.Glossary;

/// <summary>Strict reader for <c>glossary.json</c>.</summary>
/// <remarks>
/// The committed glossary is hand-curated; a typo (unknown key, bad enum
/// label, malformed date) must fail loudly at load, never be silently
/// dropped. Validation happens before any work — every structural rule of
/// <see cref="Glossary"/> is enforced by the record constructors this reader
/// calls.
/// </remarks>
public static class GlossaryJsonReader
{
    private static readonly HashSet<string> RootKeys =
        ["schemaVersion", "contexts", "abbreviations", "terms"];

    private static readonly HashSet<string> ContextKeys =
        ["packages", "description"];

    private static readonly HashSet<string> TermKeys =
        ["term", "context", "kind", "status", "definition",
         "translations", "synonyms", "sources", "firstSeen"];

    private static readonly HashSet<string> SynonymKeys = ["alias", "note"];

    /// <summary>Parses glossary JSON text into a validated <see cref="Glossary"/>.</summary>
    /// <param name="json">Complete JSON document; must not be null.</param>
    /// <exception cref="ArgumentNullException"><paramref name="json"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// Malformed JSON, unknown keys, missing required keys, bad enum labels,
    /// or malformed dates.
    /// </exception>
    public static Glossary Read(string json)
    {
        var root = AsObject(JsonParser.Parse(json), "document root");
        RejectUnknownKeys(root, RootKeys, "glossary");
        var schemaVersion = (int)AsLong(Required(root, "schemaVersion"), "schemaVersion");
        var contexts = ReadContexts(AsObject(Required(root, "contexts"), "contexts"));
        var terms = ReadTerms(AsArray(Required(root, "terms"), "terms"));
        return new Glossary(schemaVersion, contexts, terms, ReadAbbreviations(root));
    }

    /// <summary>
    /// Reads the optional schema-2 <c>abbreviations</c> section. Accepted at
    /// any version: the section is additive and harmless, and refusing it on a
    /// 1-stamped file would buy nothing but a migration cliff.
    /// </summary>
    private static Dictionary<string, string> ReadAbbreviations(
        IReadOnlyDictionary<string, object> root)
    {
        var abbreviations = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!root.ContainsKey("abbreviations"))
        {
            return abbreviations;
        }

        foreach (var entry in AsObject(root["abbreviations"], "abbreviations"))
        {
            abbreviations[entry.Key] =
                AsString(entry.Value, $"abbreviation '{entry.Key}'");
        }

        return abbreviations;
    }

    private static Dictionary<string, BoundedContext> ReadContexts(
        IReadOnlyDictionary<string, object> raw)
    {
        var contexts = new Dictionary<string, BoundedContext>(StringComparer.Ordinal);
        foreach (var entry in raw)
        {
            var body = AsObject(entry.Value, $"context '{entry.Key}'");
            RejectUnknownKeys(body, ContextKeys, $"context '{entry.Key}'");
            var packages = ReadStringList(Required(body, "packages"), "packages");
            contexts[entry.Key] = new BoundedContext(
                entry.Key, packages, OptionalString(body, "description"));
        }

        return contexts;
    }

    private static List<GlossaryTerm> ReadTerms(IReadOnlyList<object> raw)
    {
        var terms = new List<GlossaryTerm>();
        foreach (var element in raw)
        {
            terms.Add(ReadTerm(AsObject(element, "term entry")));
        }

        return terms;
    }

    private static GlossaryTerm ReadTerm(IReadOnlyDictionary<string, object> body)
    {
        RejectUnknownKeys(body, TermKeys, "term");
        return new GlossaryTerm(
            AsString(Required(body, "term"), "term"),
            AsString(Required(body, "context"), "context"),
            ReadKind(AsString(Required(body, "kind"), "kind")),
            ReadStatus(AsString(Required(body, "status"), "status")),
            OptionalString(body, "definition"),
            ReadTranslations(body),
            ReadSynonyms(body),
            body.ContainsKey("sources")
                ? ReadStringList(body["sources"], "sources")
                : [],
            ReadDate(AsString(Required(body, "firstSeen"), "firstSeen")));
    }

    private static Dictionary<string, string> ReadTranslations(
        IReadOnlyDictionary<string, object> body)
    {
        var translations = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!body.ContainsKey("translations"))
        {
            return translations;
        }

        foreach (var entry in AsObject(body["translations"], "translations"))
        {
            translations[entry.Key] = AsString(entry.Value, $"translation '{entry.Key}'");
        }

        return translations;
    }

    private static List<SynonymAlias> ReadSynonyms(IReadOnlyDictionary<string, object> body)
    {
        var synonyms = new List<SynonymAlias>();
        if (!body.ContainsKey("synonyms"))
        {
            return synonyms;
        }

        foreach (var element in AsArray(body["synonyms"], "synonyms"))
        {
            var synonym = AsObject(element, "synonym entry");
            RejectUnknownKeys(synonym, SynonymKeys, "synonym");
            synonyms.Add(new SynonymAlias(
                AsString(Required(synonym, "alias"), "alias"),
                OptionalString(synonym, "note")));
        }

        return synonyms;
    }

    private static TermKind ReadKind(string label)
    {
        foreach (TermKind kind in Enum.GetValues(typeof(TermKind)))
        {
            if (kind.JsonName() == label)
            {
                return kind;
            }
        }

        throw new ArgumentException($"unknown term kind '{label}'");
    }

    private static TermStatus ReadStatus(string label)
    {
        foreach (TermStatus status in Enum.GetValues(typeof(TermStatus)))
        {
            if (status.JsonName() == label)
            {
                return status;
            }
        }

        throw new ArgumentException($"unknown term status '{label}'");
    }

    private static DateTime ReadDate(string text)
    {
        if (!DateTime.TryParseExact(
                text, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var date))
        {
            throw new ArgumentException($"invalid firstSeen date '{text}'");
        }

        return date;
    }

    private static List<string> ReadStringList(object value, string what)
    {
        var strings = new List<string>();
        foreach (var element in AsArray(value, what))
        {
            strings.Add(AsString(element, $"{what} element"));
        }

        return strings;
    }

    private static void RejectUnknownKeys(
        IReadOnlyDictionary<string, object> body, HashSet<string> known, string what)
    {
        var unknown = body.Keys.FirstOrDefault(key => !known.Contains(key));
        if (unknown is not null)
        {
            throw new ArgumentException($"unknown key '{unknown}' in {what}");
        }
    }

    private static object Required(IReadOnlyDictionary<string, object> body, string key)
    {
        if (!body.TryGetValue(key, out var value) || ReferenceEquals(value, JsonParser.Null))
        {
            throw new ArgumentException($"missing required key '{key}'");
        }

        return value;
    }

    private static string? OptionalString(
        IReadOnlyDictionary<string, object> body, string key)
    {
        if (!body.TryGetValue(key, out var value) || ReferenceEquals(value, JsonParser.Null))
        {
            return null;
        }

        return AsString(value, key);
    }

    private static IReadOnlyDictionary<string, object> AsObject(object value, string what)
    {
        if (value is not IReadOnlyDictionary<string, object> obj)
        {
            throw new ArgumentException($"{what} must be a JSON object");
        }

        return obj;
    }

    private static IReadOnlyList<object> AsArray(object value, string what)
    {
        if (value is not IReadOnlyList<object> array)
        {
            throw new ArgumentException($"{what} must be a JSON array");
        }

        return array;
    }

    private static string AsString(object value, string what)
    {
        if (value is not string s)
        {
            throw new ArgumentException($"{what} must be a JSON string");
        }

        return s;
    }

    private static long AsLong(object value, string what)
    {
        if (value is not long n)
        {
            throw new ArgumentException($"{what} must be a JSON integer");
        }

        return n;
    }
}
