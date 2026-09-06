// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Clarity;
using NarrativeTrace.Core;
using NarrativeTrace.Runtime;
using NarrativeTrace.Diagrams;

namespace NarrativeTrace.TestingXunit;

/// <summary>
/// Writes one test's trace to disk in any combination of the supported formats.
/// </summary>
/// <remarks>
/// The flat, explicit alternative to <see cref="NarrativeFixture"/>'s
/// environment-driven artifact writing: it always writes when called, takes the
/// formats as an argument, and uses a flat directory rather than the
/// <c>traces/&lt;Class&gt;/</c> layout.
/// </remarks>
public static class TraceOutputWriter
{
    /// <summary>Renders a trace into one file per requested format.</summary>
    /// <param name="tree">The finished trace. Unlike the artifact writer, an empty tree still produces files.</param>
    /// <param name="testName">
    /// Base file name and the scenario recorded inside JSON output. Characters
    /// illegal in a file name are replaced with underscores, so two test names
    /// differing only in punctuation can collide on one file.
    /// </param>
    /// <param name="outputDir">Directory to write into; created if missing.</param>
    /// <param name="formats">
    /// The formats to emit, one file each. Passing none writes nothing — the
    /// directory is still created.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">A format value is not a defined <see cref="TraceFormat"/>.</exception>
    /// <exception cref="IOException">The directory or a file could not be written.</exception>
    /// <remarks>Overwrites existing files without warning.</remarks>
    public static void Write(
        TraceTree tree,
        string testName,
        string outputDir = "narrativetrace-output",
        params TraceFormat[] formats)
    {
        var safeName = SanitizeFileName(testName);
        Directory.CreateDirectory(outputDir);

        for (var i = 0; i < formats.Length; i++)
        {
            WriteFormat(
                tree, safeName, outputDir, formats[i]);
        }
    }

    private static void WriteFormat(
        TraceTree tree, string safeName,
        string outputDir, TraceFormat format)
    {
        var (content, ext) = Render(
            tree, safeName, format);
        var path = Path.Combine(
            outputDir, safeName + ext);
        File.WriteAllText(path, content);
    }

    private static (string Content, string Ext) Render(
        TraceTree tree, string name,
        TraceFormat format)
    {
        return format switch
        {
            TraceFormat.Markdown =>
                (MarkdownRenderer.Render(tree), ".md"),
            TraceFormat.Mermaid =>
                (MermaidSequenceRenderer.Render(tree),
                    ".mmd"),
            TraceFormat.PlantUml =>
                (PlantUmlSequenceRenderer.Render(tree),
                    ".puml"),
            TraceFormat.Json =>
                (RenderJson(tree, name), ".json"),
            TraceFormat.ClarityJson =>
                (RenderClarity(tree, name),
                    ".clarity.json"),
            _ => throw new ArgumentOutOfRangeException(
                nameof(format)),
        };
    }

    private static string RenderJson(
        TraceTree tree, string name)
    {
        // This writer is handed a tree and a name, never a test outcome, so the
        // result is derived from the trace. Callers that know the framework's
        // verdict should use NarrativeFixture.WriteArtifacts instead.
        var metadata = new TraceMetadata(
            name,
            ScenarioResultExtensions.Of(
                TraceNode.HasAnyError(tree.Roots)));
        return JsonExporter.Export(tree, metadata);
    }

    private static string RenderClarity(
        TraceTree tree, string name)
    {
        var result = ClarityAnalyzer.Analyze(tree);
        return ClarityJsonExporter.Export(result, name);
    }

    private static readonly char[] ExtraInvalid =
        ['/', '\\', ':', '*', '?', '"', '<', '>', '|'];

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = name.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (Array.IndexOf(invalid, chars[i]) >= 0
                || Array.IndexOf(
                    ExtraInvalid, chars[i]) >= 0)
            {
                chars[i] = '_';
            }
        }

        return new string(chars);
    }
}
