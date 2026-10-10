// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Text;

namespace NarrativeTrace.Tooling.Frameworks;

/// <summary>One start tag of an MSBuild manifest: its element name and its attributes, as written.</summary>
/// <param name="Name">The element name (<c>PackageReference</c>, <c>Project</c>, …).</param>
/// <param name="Attributes">Its attributes by name, case-insensitively — MSBuild's own rule.</param>
public sealed record ManifestElement(string Name, IReadOnlyDictionary<string, string> Attributes)
{
    /// <summary>Whether the element is named <paramref name="name"/>, case-insensitively.</summary>
    public bool Is(string name) => string.Equals(Name, name, StringComparison.OrdinalIgnoreCase);

    /// <summary>The <c>;</c>-separated items of an attribute, trimmed; empty when the attribute is absent.</summary>
    public IEnumerable<string> Items(string attribute) =>
        Attributes.TryGetValue(attribute, out var value)
            ? value.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            : [];
}

/// <summary>
/// Reads a manifest's text as the start tags it declares — a scan, not a pattern, and linear in the
/// text whatever its shape.
/// </summary>
/// <remarks>
/// <para>INTENT: a regex that walks "anything up to <c>&gt;</c>, then the attribute" is quadratic on a
/// manifest of unclosed tags and stops at a <c>&gt;</c> inside a quoted <c>Condition</c>. This scanner
/// reads attribute values by their quotes, never looks back, and treats comments and CDATA sections as
/// the text they are (<see cref="ManifestPatterns.WithoutCommentsAndCData"/>). A tag the text never
/// closes is not an element.</para>
/// <para><b>@llmNote</b> Text only: <c>Include="$(Var)"</c> is the literal <c>$(Var)</c>, never the value
/// the property holds — evaluating MSBuild is a build, and the doctor never runs one.</para>
/// </remarks>
public static class ManifestElements
{
    /// <summary>Every complete start tag in <paramref name="manifest"/>, in document order.</summary>
    public static IReadOnlyList<ManifestElement> Read(string manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        var text = ManifestPatterns.WithoutCommentsAndCData(manifest);
        var elements = new List<ManifestElement>();
        var at = text.IndexOf('<', StringComparison.Ordinal);
        while (at >= 0)
        {
            var next = ReadElement(text, at + 1, elements);
            at = next >= text.Length ? -1 : text.IndexOf('<', next);
        }

        return elements;
    }

    /// <summary>Reads one start tag whose name begins at <paramref name="at"/>; returns where scanning resumes.</summary>
    private static int ReadElement(string text, int at, List<ManifestElement> elements)
    {
        var name = Name(text, at);
        if (name.Length == 0)
        {
            return at;
        }

        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var (resume, closed) = ReadAttributes(text, at + name.Length, attributes);
        if (closed)
        {
            elements.Add(new ManifestElement(name, attributes));
        }

        return resume;
    }

    /// <summary>
    /// Reads attributes up to the tag's <c>&gt;</c> or <c>/&gt;</c>; returns where scanning resumes and
    /// whether the tag closed. Anything else — a <c>&lt;</c>, the end of the text, a token that is not
    /// an attribute — abandons the tag.
    /// </summary>
    private static (int Resume, bool Closed) ReadAttributes(string text, int from, Dictionary<string, string> attributes)
    {
        var i = from;
        while (true)
        {
            i = SkipSpace(text, i);
            if (i >= text.Length || text[i] == '<')
            {
                return (i, false);
            }

            if (text[i] == '>' || (text[i] == '/' && i + 1 < text.Length && text[i + 1] == '>'))
            {
                return (i + 1, true);
            }

            var next = ReadAttribute(text, i, attributes);
            if (next == i)
            {
                return (i + 1, false);
            }

            i = next;
        }
    }

    /// <summary>Reads <c>name = "value"</c> at <paramref name="at"/>; returns <paramref name="at"/> unchanged when there is none.</summary>
    private static int ReadAttribute(string text, int at, Dictionary<string, string> attributes)
    {
        var name = Name(text, at);
        var i = SkipSpace(text, at + name.Length);
        if (name.Length == 0 || i >= text.Length || text[i] != '=')
        {
            return at;
        }

        i = SkipSpace(text, i + 1);
        if (i >= text.Length || (text[i] != '"' && text[i] != '\''))
        {
            return at;
        }

        var close = text.IndexOf(text[i], i + 1);
        if (close < 0)
        {
            return text.Length;
        }

        attributes[name] = text[(i + 1)..close];
        return close + 1;
    }

    private static string Name(string text, int at)
    {
        var end = at;
        while (end < text.Length && (char.IsAsciiLetterOrDigit(text[end]) || text[end] is '_' or '.' or ':' or '-'))
        {
            end++;
        }

        return end > at && char.IsAsciiLetter(text[at]) ? text[at..end] : string.Empty;
    }

    private static int SkipSpace(string text, int at)
    {
        var i = at;
        while (i < text.Length && char.IsWhiteSpace(text[i]))
        {
            i++;
        }

        return i;
    }
}
