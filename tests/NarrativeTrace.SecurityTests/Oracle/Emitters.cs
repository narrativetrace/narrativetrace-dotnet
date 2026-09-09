// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using NarrativeTrace.Diagrams;
using NarrativeTrace.Runtime;

namespace NarrativeTrace.SecurityTests.Oracle;

/// <summary>
/// Every output NarrativeTrace can produce from one trace, in one map.
/// </summary>
/// <remarks>
/// <para>
/// INTENT: An oracle that names the formats it checks goes stale the moment a format is added. The
/// suite asserts over <em>this</em> map, so a new emitter added here is immediately covered by
/// redaction, well-formedness, injection containment and boundedness at once.
/// </para>
/// <para>
/// @llmNote Both halves matter. The in-memory renderers are what a library consumer calls; the
/// written artifacts are what the product puts on disk. Everything here goes through the shipped
/// path (<see cref="TraceArtifactWriter"/>), the same one <c>NarrativeFixture</c> and
/// <c>NarrativeTestBase</c> use.
/// </para>
/// <para>
/// @edgeCase This runtime has no standalone <c>FrontmatterBuilder</c> or Mermaid
/// <c>renderWithAliases</c> — confirmed absent by direct audit of
/// <c>NarrativeTrace.Core</c>/<c>NarrativeTrace.Diagrams</c>. Frontmatter is checked from the
/// embedded block inside <c>renderer:markdown-document</c> instead of a second standalone emitter.
/// </para>
/// </remarks>
public static class Emitters
{
    /// <summary>The scenario name every artifact is written under, unless a test supplies its own.</summary>
    public const string Scenario = "a hostile scenario";

    private const string TestClass = "com.example.HostileTest";
    private const string TestMethod = "rendersHostileInput";

    /// <summary>A one-node tree whose parameter and return value carry the given rendered text.</summary>
    public static TraceTree TreeOf(string renderedArgument, string renderedReturn)
    {
        var node = new TraceNode(
            new MethodSignature(
                "CardRepository",
                "FindByNumber",
                [new ParameterCapture("probe", renderedArgument, Redacted: false)]),
            new Returned(renderedReturn),
            Children: [],
            DurationTicks: 42_000_000L);
        return new TraceTree([node]);
    }

    /// <summary>
    /// A one-node tree whose class name, method name and parameter name — not its value — carry the
    /// given text at once, so a fuzz route driving only VALUES (as <see cref="TreeOf"/> and
    /// <see cref="TreeNarrating"/> do) cannot find an escaping gap that is specific to trace
    /// METADATA (found by mirroring an adversarial audit: a renderer that escapes a rendered value
    /// and, on a nearby line, interpolates the class/method/parameter name raw).
    /// </summary>
    public static TraceTree TreeWithHostileMetadata(string metadata)
    {
        var node = new TraceNode(
            new MethodSignature(
                metadata,
                metadata,
                [new ParameterCapture(metadata, "\"ok\"", Redacted: false)]),
            new Returned("\"ok\""),
            Children: [],
            DurationTicks: 42_000_000L);
        return new TraceTree([node]);
    }

    /// <summary>A one-node tree whose narration and error context carry the given prose.</summary>
    public static TraceTree TreeNarrating(string narration, string errorContext)
    {
        var node = new TraceNode(
            new MethodSignature(
                "PaymentService", "Charge", Parameters: [], narration, errorContext, NarrationTemplate: "charging {card}"),
            new Returned("true"),
            Children: [],
            DurationTicks: 42_000_000L);
        return new TraceTree([node]);
    }

    /// <summary>A one-node tree that threw, so the exception-message paths are exercised.</summary>
    public static TraceTree TreeThrowing(Exception thrown)
    {
        var node = new TraceNode(
            new MethodSignature("PaymentService", "Charge", Parameters: []),
            new Threw(thrown),
            Children: [],
            DurationTicks: 42_000_000L);
        return new TraceTree([node]);
    }

    /// <summary>
    /// Every output the product can produce from <paramref name="tree"/>, keyed by emitter.
    /// </summary>
    /// <returns>Renderer outputs keyed <c>renderer:&lt;name&gt;</c> and written artifacts keyed <c>artifact:&lt;file name&gt;</c>.</returns>
    public static Dictionary<string, string> EveryOutput(TraceTree tree)
    {
        var outputs = Renderers(tree);
        var dir = TemporaryDirectory();
        try
        {
            foreach (var (key, value) in WrittenArtifacts(tree, dir))
                outputs[key] = value;
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }

        return outputs;
    }

    /// <summary>The in-memory renderers, which a library consumer calls directly.</summary>
    public static Dictionary<string, string> Renderers(TraceTree tree) => Renderers(tree, Scenario);

    /// <summary>
    /// The same, under a caller-supplied scenario.
    /// </summary>
    /// <remarks>
    /// INTENT: The scenario is a route of its own — caller-supplied text that reaches the YAML
    /// frontmatter, the Markdown body header, the structural header and the JSON scenario name,
    /// each with its own escaping. The body-header defect this fixed (a raw, unescaped append) was
    /// only reachable through this parameter, which every caller here used to pin to
    /// <see cref="Scenario"/>.
    /// </remarks>
    public static Dictionary<string, string> Renderers(TraceTree tree, string scenario)
    {
        var metadata = new TraceMetadata(scenario, ScenarioResult.Success);
        return new Dictionary<string, string>
        {
            ["renderer:prose"] = ProseRenderer.Render(tree),
            ["renderer:indented"] = IndentedTextRenderer.Render(tree),
            ["renderer:markdown"] = MarkdownRenderer.Render(tree),
            ["renderer:markdown-document"] = MarkdownRenderer.RenderDocument(tree, metadata),
            ["renderer:structural"] = StructuralTraceRenderer.RenderDocument(tree, scenario),
            ["renderer:json"] = JsonExporter.Export(tree, metadata),
            ["renderer:mermaid"] = MermaidSequenceRenderer.Render(tree),
            ["renderer:plantuml"] = PlantUmlSequenceRenderer.Render(tree),
            ["renderer:canonical-entries"] = CanonicalEntryArrayExporter.Canonical(tree),
            ["renderer:structural-entries"] = CanonicalEntryArrayExporter.Structural(tree),
        };
    }

    /// <summary>Everything the shipped writer puts on disk, plus the console summary it prints.</summary>
    public static Dictionary<string, string> WrittenArtifacts(TraceTree tree, string dir) =>
        WrittenArtifacts(tree, dir, TestClass, TestMethod);

    /// <summary>
    /// The same, with the test class/method name overridable — the artifact-naming corpus drives a
    /// hostile name through here, not through a value.
    /// </summary>
    public static Dictionary<string, string> WrittenArtifacts(
        TraceTree tree, string dir, string className, string methodName)
    {
        using var console = new StringWriter();
        Write(tree, dir, className, methodName, console);
        var outputs = new Dictionary<string, string> { ["artifact:console"] = console.ToString() };
        foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
            outputs[$"artifact:{Path.GetFileName(file)}"] = File.ReadAllText(file);
        return outputs;
    }

    private static void Write(
        TraceTree tree, string dir, string className, string methodName, TextWriter console)
    {
        var renderers = new TraceArtifactRenderers(
            MermaidSequenceRenderer.Render,
            PlantUmlSequenceRenderer.Render,
            JsonExporter.Export,
            CanonicalEntryArrayExporter.Canonical,
            CanonicalEntryArrayExporter.Structural);
        TraceArtifactWriter.Write(
            tree, className, methodName, Scenario, failed: false, dir,
            TraceArtifactFormat.Markdown, renderers, console,
            new EntryArtifacts(Canonical: true, Structural: true));
    }

    private static string TemporaryDirectory()
    {
        var dir = Path.Combine(Path.GetTempPath(), "narrativetrace-security-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(dir);
        return dir;
    }
}
