// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace NarrativeTrace.SecurityTests.Corpus;

/// <summary>
/// Reader for the shared hostile corpus in <c>HostileCorpus/</c>.
/// </summary>
/// <remarks>
/// <para>
/// INTENT: One loader, so every property reads the same fixtures the same way. The corpus is the
/// cross-port artifact — the ports copy the JSON verbatim and reimplement only this reader and
/// <see cref="HostileGraphs"/> — so nothing platform-specific may leak into the files.
/// </para>
/// <para>
/// @llmNote The fixtures are ASCII: every hostile character is a <c>\uXXXX</c> escape that the
/// JSON parser turns back into the real code point here. That is deliberate — a corpus carrying
/// raw C0 bytes is unreadable in a diff and gets silently normalised by editors, which is exactly
/// how a case stops testing what it was written for.
/// </para>
/// <para>
/// @edgeCase A case may declare <c>repeat</c> instead of <c>value</c>, which is how a 1 MiB input
/// lives in a 6 kB fixture. <c>prefix</c> and <c>suffix</c> bracket the repetition.
/// </para>
/// </remarks>
public static class HostileCorpus
{
    private const string Directory = "HostileCorpus";

    private static readonly ConcurrentDictionary<string, JsonDocument> Parsed = new();
    private static readonly ConcurrentDictionary<string, object> Cases = new();

    /// <summary>Hostile scalar values, for the renderer and every output format.</summary>
    public static IReadOnlyList<CorpusCase> Strings() =>
        (IReadOnlyList<CorpusCase>)Cases.GetOrAdd("strings", _ => ReadCases(Read("strings.json").RootElement.GetProperty("cases")));

    /// <summary>Prompt-injection payloads, for the AI-consumer containment oracle.</summary>
    public static IReadOnlyList<CorpusCase> Injections() =>
        (IReadOnlyList<CorpusCase>)Cases.GetOrAdd("injections", _ => ReadCases(Read("injection.json").RootElement.GetProperty("cases")));

    /// <summary>W3C <c>traceparent</c> header values, each saying whether the parser must accept it.</summary>
    public static IReadOnlyList<HeaderCase> Traceparents() =>
        (IReadOnlyList<HeaderCase>)Cases.GetOrAdd("traceparent", _ => ReadHeaders(Read("headers.json").RootElement.GetProperty("traceparent")));

    /// <summary>W3C <c>tracestate</c> header values.</summary>
    public static IReadOnlyList<HeaderCase> Tracestates() =>
        (IReadOnlyList<HeaderCase>)Cases.GetOrAdd("tracestate", _ => ReadHeaders(Read("headers.json").RootElement.GetProperty("tracestate")));

    /// <summary><c>[Narrated]</c>/<c>[OnError]</c> template strings.</summary>
    public static IReadOnlyList<TemplateCase> Templates() =>
        (IReadOnlyList<TemplateCase>)Cases.GetOrAdd("templates", _ => ReadTemplates());

    /// <summary>Declarative object-graph shapes; <see cref="HostileGraphs"/> turns one into a live graph.</summary>
    public static IReadOnlyList<GraphCase> Graphs() =>
        (IReadOnlyList<GraphCase>)Cases.GetOrAdd("graphs", _ => ReadGraphs());

    /// <summary>
    /// Declarative <c>TraceNode</c> call-tree shapes (depth, cycles); <see cref="HostileTreeShapes"/>
    /// turns one into a live <c>TraceTree</c>.
    /// </summary>
    public static IReadOnlyList<TreeShapeCase> TreeShapes() =>
        (IReadOnlyList<TreeShapeCase>)Cases.GetOrAdd("tree-shapes", _ => ReadTreeShapes());

    /// <summary>Test class/method names an artifact writer must survive.</summary>
    public static IReadOnlyList<CorpusCase> Names() =>
        (IReadOnlyList<CorpusCase>)Cases.GetOrAdd("names", _ => ReadCases(Read("names.json").RootElement.GetProperty("cases")));

    /// <summary>Sensitive-field name vocabulary and national-id value shapes, for both redaction axes.</summary>
    public static IReadOnlyList<RedactionCase> Redactions() =>
        (IReadOnlyList<RedactionCase>)Cases.GetOrAdd("redaction", _ => ReadRedactions());

    private static List<CorpusCase> ReadCases(JsonElement array)
    {
        var result = new List<CorpusCase>();
        foreach (var node in array.EnumerateArray())
            result.Add(new CorpusCase(Text(node, "id")!, Text(node, "description")!, Materialize(node)));
        return result;
    }

    private static List<HeaderCase> ReadHeaders(JsonElement array)
    {
        var result = new List<HeaderCase>();
        foreach (var node in array.EnumerateArray())
        {
            var accepted = node.TryGetProperty("accepted", out var acceptedNode) && acceptedNode.GetBoolean();
            result.Add(new HeaderCase(Text(node, "id")!, Text(node, "description")!, Materialize(node), accepted));
        }
        return result;
    }

    private static List<TemplateCase> ReadTemplates()
    {
        var result = new List<TemplateCase>();
        foreach (var node in Read("templates.json").RootElement.GetProperty("cases").EnumerateArray())
        {
            result.Add(new TemplateCase(
                Text(node, "id")!,
                Text(node, "description")!,
                Materialize(node, "template"),
                Text(node, "values"),
                Text(node, "expect")));
        }
        return result;
    }

    private static List<GraphCase> ReadGraphs()
    {
        var result = new List<GraphCase>();
        foreach (var node in Read("graphs.json").RootElement.GetProperty("cases").EnumerateArray())
            result.Add(ReadGraphCase(node));
        return result;
    }

    private static GraphCase ReadGraphCase(JsonElement node)
    {
        var layers = new List<string>();
        if (node.TryGetProperty("layers", out var layersNode))
            foreach (var layer in layersNode.EnumerateArray())
                layers.Add(DecodeJsonString(layer.GetRawText()));

        var n = node.TryGetProperty("n", out var nNode) ? nNode.GetInt32() : 0;
        return new GraphCase(
            Text(node, "id")!,
            Text(node, "description")!,
            Text(node, "kind"),
            layers,
            Text(node, "layer"),
            Text(node, "container"),
            Text(node, "member"),
            Text(node, "state"),
            Text(node, "payload"),
            n);
    }

    private static List<TreeShapeCase> ReadTreeShapes()
    {
        var result = new List<TreeShapeCase>();
        foreach (var node in Read("tree-shapes.json").RootElement.GetProperty("cases").EnumerateArray())
        {
            result.Add(new TreeShapeCase(
                Text(node, "id")!,
                Text(node, "description")!,
                Text(node, "kind")!,
                node.GetProperty("n").GetInt32()));
        }

        return result;
    }

    private static List<RedactionCase> ReadRedactions()
    {
        var result = new List<RedactionCase>();
        foreach (var node in Read("redaction.json").RootElement.GetProperty("cases").EnumerateArray())
        {
            result.Add(new RedactionCase(
                Text(node, "id")!,
                Text(node, "description")!,
                Text(node, "name"),
                Text(node, "value"),
                Text(node, "canary"),
                Text(node, "expect")!));
        }
        return result;
    }

    /// <summary><c>prefix</c> + <c>unit</c> x <c>count</c> + <c>suffix</c>, or the literal <c>value</c>.</summary>
    private static string Materialize(JsonElement node, string literalField = "value")
    {
        if (!node.TryGetProperty("repeat", out var repeat))
            return Text(node, literalField) ?? "";

        var unit = DecodeJsonString(repeat.GetProperty("unit").GetRawText());
        var count = repeat.GetProperty("count").GetInt32();
        var prefix = Text(node, "prefix") ?? "";
        var suffix = Text(node, "suffix") ?? "";
        return prefix + string.Concat(Enumerable.Repeat(unit, count)) + suffix;
    }

    private static string? Text(JsonElement node, string field) =>
        node.TryGetProperty(field, out var value) && value.ValueKind != JsonValueKind.Null
            ? DecodeJsonString(value.GetRawText())
            : null;

    /// <summary>
    /// Decodes a JSON string literal's <b>raw</b> token text (quotes and escapes intact, as
    /// <see cref="JsonElement.GetRawText"/> returns it) into a .NET string, without
    /// <see cref="JsonElement.GetString"/>'s well-formed-UTF-16 requirement.
    /// </summary>
    /// <remarks>
    /// <see cref="JsonElement.GetString"/> throws <see cref="InvalidOperationException"/>
    /// ("Cannot read incomplete UTF-16 JSON text as string with missing low surrogate") for exactly
    /// the corpus cases this reader exists to carry — a lone <c>\ud800</c>/<c>\udc00</c> escape is
    /// not control-escaped and not a control character, so it reaches the JSON reader unchanged, and
    /// <see cref="System.Text.Json"/> refuses to materialize it as a string at all. Jackson (the
    /// Java port's reader) makes the opposite, corpus-intended choice: a <c>\uXXXX</c> escape becomes
    /// exactly that UTF-16 code unit, paired or not — which is the whole point of the
    /// <c>unpaired-high-surrogate</c>/<c>unpaired-low-surrogate</c>/<c>reversed-surrogate-pair</c>
    /// cases. This decoder implements the JSON string grammar (RFC 8259 §7) by hand, the one piece
    /// .NET's own parser will not do here, so the corpus loads exactly as written rather than losing
    /// the cases it exists to carry.
    /// </remarks>
    private static string DecodeJsonString(string rawJsonToken)
    {
        var inner = rawJsonToken.AsSpan(1, rawJsonToken.Length - 2);
        var sb = new StringBuilder(inner.Length);
        var i = 0;
        while (i < inner.Length)
        {
            if (inner[i] != '\\')
            {
                sb.Append(inner[i]);
                i++;
                continue;
            }

            i = AppendEscape(sb, inner, i + 1);
        }

        return sb.ToString();
    }

    /// <summary>Appends the escape sequence starting at <paramref name="index"/> and returns the next index to read.</summary>
    private static int AppendEscape(StringBuilder sb, ReadOnlySpan<char> inner, int index)
    {
        switch (inner[index])
        {
            case '"': sb.Append('"'); return index + 1;
            case '\\': sb.Append('\\'); return index + 1;
            case '/': sb.Append('/'); return index + 1;
            case 'b': sb.Append('\b'); return index + 1;
            case 'f': sb.Append('\f'); return index + 1;
            case 'n': sb.Append('\n'); return index + 1;
            case 'r': sb.Append('\r'); return index + 1;
            case 't': sb.Append('\t'); return index + 1;
            case 'u':
                var code = ushort.Parse(
                    inner.Slice(index + 1, 4), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
                sb.Append((char)code);
                return index + 5;
            default:
                sb.Append(inner[index]);
                return index + 1;
        }
    }

    private static JsonDocument Read(string fileName) => Parsed.GetOrAdd(fileName, Parse);

    private static JsonDocument Parse(string fileName)
    {
        var resource = $"{Directory}/{fileName}";
        var path = Path.Combine(AppContext.BaseDirectory, resource);
        if (!File.Exists(path))
            throw new InvalidOperationException($"hostile corpus not found: {path}");
        using var stream = File.OpenRead(path);
        return JsonDocument.Parse(stream);
    }
}
