// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Json.Schema;
using NarrativeTrace.Core;
using Xunit;
using YamlDotNet.RepresentationModel;

namespace NarrativeTrace.SecurityTests.Oracle;

/// <summary>
/// Well-formedness oracles: each format read back by a real parser for that format.
/// </summary>
/// <remarks>
/// <para>
/// INTENT: "The output looks fine" is not an oracle. A JSON document is well-formed when
/// <see cref="JsonDocument"/> parses it and the canonical schema accepts it; a Mermaid diagram is
/// well-formed when every line is a statement the grammar allows; frontmatter is well-formed when a
/// real YAML parser reads it as a mapping. Reading back with the consumer's own parser is the only
/// check that catches an escaper that is merely plausible.
/// </para>
/// <para>
/// @llmNote The canonical schema is read from where it lives —
/// <c>tests/NarrativeTrace.Core.Tests/Schemas/</c> — rather than copied here, mirroring Java's
/// <c>Formats.readSchema()</c>. A copy would drift.
/// </para>
/// </remarks>
public static partial class Formats
{
    /// <summary>The canonical chapter-tree schema, relative to the repository root.</summary>
    public const string ChapterTreeSchema = "tests/NarrativeTrace.Core.Tests/Schemas/chapter-tree.schema.json";

    /// <summary>
    /// Statements the Mermaid sequence-diagram renderer emits. Anything else on a line means a
    /// value broke out of the message text it was interpolated into.
    /// </summary>
    [GeneratedRegex(@"^(participant .*|.*->>.*: .*|.*-->>.*: .*|.*-x.*: .*|Note over .*: .*)$")]
    private static partial Regex MermaidStatement();

    /// <summary>
    /// Statements the PlantUML sequence-diagram renderer emits. Anything else on a line means a
    /// value or a class/method/parameter name broke out of the text it was interpolated into.
    /// </summary>
    [GeneratedRegex(
        @"^(@startuml|@enduml|participant .*|activate .*|deactivate .*|" +
        @".* -> .* : .*|.* --> .* : .*|.* -\[#red\]-> .* : .*|hnote over .* : .*)$")]
    private static partial Regex PlantUmlStatement();

    /// <summary>Every output whose key ends in <c>.json</c>, or is <c>renderer:json</c>, parses.</summary>
    public static void EveryJsonArtifactParses(IReadOnlyDictionary<string, string> outputs)
    {
        foreach (var (emitter, output) in outputs)
            if (emitter.EndsWith(".json", StringComparison.Ordinal) || emitter == "renderer:json")
                ParseJson(emitter, output);
    }

    /// <summary>
    /// A search-safe text dump of a <see cref="RenderedValue"/> tree, containment field included.
    /// </summary>
    /// <remarks>
    /// <para>
    /// INTENT: Java's <c>Map</c>/<c>List</c> override <c>toString()</c> to print their contents
    /// recursively, so <c>String.valueOf(structuredValue)</c> is a real containment probe there. The
    /// .NET analogues (<see cref="Dictionary{TKey,TValue}"/>, <see cref="List{T}"/>) do not — the
    /// default <c>record</c>-generated <c>ToString()</c> on <see cref="RenderedValue.ObjectVal"/> and
    /// <see cref="RenderedValue.ListVal"/> would print only the collection's type name, leaking
    /// nothing and finding nothing. That would silently defeat the exact case the redaction oracle
    /// exists to catch: a sentinel nested inside a wrapped list, map or record. This walks the tree
    /// by hand instead of relying on <c>ToString()</c>.
    /// </para>
    /// </remarks>
    public static string Describe(RenderedValue value) => value switch
    {
        RenderedValue.StringVal s => s.Value,
        RenderedValue.LongVal l => l.Value.ToString(CultureInfo.InvariantCulture),
        RenderedValue.DoubleVal d => d.Value.ToString(CultureInfo.InvariantCulture),
        RenderedValue.BooleanVal b => b.Value ? "true" : "false",
        RenderedValue.InstantVal i => i.EpochMillis.ToString(CultureInfo.InvariantCulture),
        RenderedValue.NullVal => "null",
        RenderedValue.ObjectVal o => DescribeObject(o),
        RenderedValue.ListVal l => $"[{string.Join(", ", l.Elements.Select(Describe))}]",
        _ => value.ToString() ?? "",
    };

    private static string DescribeObject(RenderedValue.ObjectVal o) =>
        $"{o.TypeName}{{{string.Join(", ", o.Fields.Select(f => $"{f.Key}={Describe(f.Value)}"))}}}";

    /// <summary>Parses <paramref name="json"/>, failing the test with the emitter's name when it does not parse.</summary>
    public static JsonDocument ParseJson(string emitter, string json)
    {
        try
        {
            return JsonDocument.Parse(json);
        }
        catch (JsonException e)
        {
            throw new Xunit.Sdk.XunitException($"{emitter} produced JSON no parser accepts: {e.Message}");
        }
    }

    /// <summary>Validates a chapter-tree document against the canonical schema.</summary>
    public static void ValidatesAgainstChapterTreeSchema(string emitter, string json)
    {
        var schema = JsonSchema.FromFile(Path.Combine(RepositoryPath.Root(), ChapterTreeSchema));
        var results = schema.Evaluate(
            System.Text.Json.Nodes.JsonNode.Parse(json), new EvaluationOptions { OutputFormat = Json.Schema.OutputFormat.List });

        Assert.True(results.IsValid, $"{emitter} violates the canonical chapter-tree schema: {Describe(results)}");
    }

    private static string Describe(EvaluationResults results) =>
        string.Join("\n", results.Details.Where(d => d.HasErrors)
            .SelectMany(d => d.Errors!.Select(e => $"  {d.InstanceLocation}: {e.Key} => {e.Value}")));

    /// <summary>
    /// A Mermaid sequence diagram is well formed when it opens with its keyword and every other
    /// line is one statement the grammar allows — no raw line break inside a label, no injected
    /// directive.
    /// </summary>
    public static void IsWellFormedMermaid(string emitter, string diagram)
    {
        Assert.True(diagram.StartsWith("sequenceDiagram", StringComparison.Ordinal), $"{emitter} must open the diagram");
        foreach (var line in StatementsOf(diagram))
        {
            Assert.True(MermaidStatement().IsMatch(line), $"{emitter} emitted a line the Mermaid grammar does not allow: {line}");
            Assert.False(line.Any(char.IsControl), $"{emitter} left a control character inside a diagram line");
        }
    }

    /// <summary>The statement lines of a diagram: no blanks, no <c>%%</c> comments, indentation stripped.</summary>
    public static List<string> StatementsOf(string diagram) =>
        diagram.Split('\n').Skip(1).Select(l => l.Trim())
            .Where(l => l.Length > 0 && !l.StartsWith("%%", StringComparison.Ordinal)).ToList();

    /// <summary>
    /// A PlantUML diagram is well formed when it is bracketed by its own markers <em>and</em> every
    /// line in between is one statement the grammar allows — bracketing alone says nothing about
    /// what is between the markers.
    /// </summary>
    public static void IsWellFormedPlantUml(string emitter, string diagram)
    {
        var trimmed = diagram.TrimEnd();
        Assert.True(
            trimmed.StartsWith("@startuml", StringComparison.Ordinal) && trimmed.EndsWith("@enduml", StringComparison.Ordinal),
            $"{emitter} must bracket the diagram");
        foreach (var line in PlantUmlStatementsOf(trimmed))
        {
            Assert.True(PlantUmlStatement().IsMatch(line), $"{emitter} emitted a line the PlantUML grammar does not allow: {line}");
            Assert.False(line.Any(char.IsControl), $"{emitter} left a control character inside a diagram line");
        }
    }

    /// <summary>The statement lines of a PlantUML diagram: no blanks, indentation stripped.</summary>
    public static List<string> PlantUmlStatementsOf(string diagram) =>
        diagram.Split('\n').Select(l => l.Trim())
            .Where(l => l.Length > 0).ToList();

    /// <summary>Reads the YAML frontmatter block of a Markdown document.</summary>
    /// <returns>The parsed mapping's keys; the test asserts on those.</returns>
    public static IReadOnlySet<string> FrontmatterKeysOf(string emitter, string markdown)
    {
        var block = FrontmatterBlock(emitter, markdown);
        var yaml = new YamlStream();
        try
        {
            yaml.Load(new StringReader(block));
        }
        catch (Exception e) when (e is YamlDotNet.Core.YamlException)
        {
            throw new Xunit.Sdk.XunitException($"{emitter} produced frontmatter no YAML parser accepts: {e.Message}");
        }

        Assert.True(yaml.Documents.Count > 0, $"{emitter} frontmatter must parse as a document");
        var mapping = Assert.IsType<YamlMappingNode>(yaml.Documents[0].RootNode);
        return mapping.Children.Keys.Select(k => ((YamlScalarNode)k).Value!).ToHashSet();
    }

    /// <summary>The text between the opening and closing <c>---</c> fences.</summary>
    private static string FrontmatterBlock(string emitter, string markdown)
    {
        Assert.True(markdown.StartsWith("---\n", StringComparison.Ordinal), $"{emitter} must open with a frontmatter fence");
        var end = markdown.IndexOf("\n---", "---\n".Length, StringComparison.Ordinal);
        Assert.True(end >= 0, $"{emitter} left the frontmatter block unterminated");
        return markdown[("---\n".Length)..end];
    }

    /// <summary>
    /// The document's shape with every scalar's <em>content</em> erased: field names, array
    /// lengths and node kinds only.
    /// </summary>
    /// <remarks>
    /// INTENT: This is the AI-consumer oracle's comparison. A captured value that stayed one value
    /// produces the same shape whatever it contained; a value that broke out of its string produces
    /// a different one — an extra field, an extra element, a different node kind. Comparing shapes
    /// says "exactly one value" without the test having to guess where in the document that value sits.
    /// </remarks>
    public static string JsonShape(JsonDocument document)
    {
        var sb = new System.Text.StringBuilder();
        AppendShape(document.RootElement, sb);
        return sb.ToString();
    }

    private static void AppendShape(JsonElement element, System.Text.StringBuilder sb)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                sb.Append('{');
                foreach (var property in element.EnumerateObject())
                {
                    sb.Append(property.Name).Append(':');
                    AppendShape(property.Value, sb);
                    sb.Append(',');
                }

                sb.Append('}');
                break;
            case JsonValueKind.Array:
                sb.Append('[');
                foreach (var item in element.EnumerateArray())
                    AppendShape(item, sb);
                sb.Append(']');
                break;
            default:
                sb.Append(element.ValueKind);
                break;
        }
    }

    /// <summary>
    /// Every string node under <paramref name="element"/> whose field is <paramref name="fieldName"/>, in document order.
    /// </summary>
    /// <remarks>
    /// Used to read a captured value back out of the document a parser produced, which is the
    /// strongest form of "it came back as exactly one value": the bytes match, and they are one node.
    /// </remarks>
    public static List<string> StringsNamed(JsonElement element, string fieldName)
    {
        var found = new List<string>();
        CollectStrings(element, fieldName, found);
        return found;
    }

    private static void CollectStrings(JsonElement element, string fieldName, List<string> found)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    if (property.Name == fieldName && property.Value.ValueKind == JsonValueKind.String)
                        found.Add(property.Value.GetString()!);
                    CollectStrings(property.Value, fieldName, found);
                }

                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                    CollectStrings(item, fieldName, found);
                break;
        }
    }

    /// <summary>How many fenced-code delimiters a Markdown document carries.</summary>
    public static long FenceCount(string markdown) =>
        markdown.Split('\n').Count(l => l.Trim().StartsWith("```", StringComparison.Ordinal));

    /// <summary>How many frontmatter fences a Markdown document carries: exactly two, or it is broken.</summary>
    public static long FrontmatterFenceCount(string markdown) =>
        markdown.Split('\n').Count(l => l.Trim() == "---");
}
