// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Globalization;
using NarrativeTrace.Glossary;

namespace NarrativeTrace.Cli;

/// <summary>
/// The <c>glossary</c> verb: validates the committed <c>glossary.json</c>,
/// rewrites it in canonical form, and regenerates the <c>glossary.md</c> view.
/// </summary>
/// <remarks>
/// The curation round-trip tool (ADR-012): after hand-editing the JSON, this
/// verb fails loudly on any schema or structural error, normalizes formatting
/// and term order so diffs stay minimal, and refreshes the Markdown review
/// surface. Byte-identical files are not rewritten (anti-churn).
/// </remarks>
public static class GlossaryCommand
{
    /// <summary>Runs the glossary round-trip; returns the process exit code.</summary>
    /// <param name="glossaryPath">Path to <c>glossary.json</c>.</param>
    /// <param name="output">Sink for the summary line.</param>
    /// <param name="error">Sink for validation errors.</param>
    /// <returns>0 on success, 2 when the file is missing or invalid.</returns>
    public static int Run(string glossaryPath, TextWriter output, TextWriter error)
    {
        if (!File.Exists(glossaryPath))
        {
            error.WriteLine($"error: no glossary at {glossaryPath}");
            return 2;
        }

        Glossary.Glossary glossary;
        try
        {
            glossary = GlossaryJsonReader.Read(File.ReadAllText(glossaryPath));
        }
        catch (ArgumentException ex)
        {
            error.WriteLine($"error: invalid glossary: {ex.Message}");
            return 2;
        }

        WriteArtifacts(glossaryPath, glossary);
        output.WriteLine(Summary(glossaryPath, glossary));
        return 0;
    }

    private static void WriteArtifacts(string glossaryPath, Glossary.Glossary glossary)
    {
        WriteIfChanged(glossaryPath, GlossaryJsonWriter.Write(glossary));
        WriteIfChanged(
            Path.ChangeExtension(glossaryPath, ".md"),
            GlossaryMarkdownRenderer.Render(glossary));
    }

    private static void WriteIfChanged(string file, string content)
    {
        if (File.Exists(file) && File.ReadAllText(file) == content)
        {
            return;
        }

        File.WriteAllText(file, content);
    }

    private static string Summary(string glossaryPath, Glossary.Glossary glossary)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"Glossary valid: {glossary.Contexts.Count} contexts, "
            + $"{glossary.Terms.Count} terms; canonical form and "
            + $"{Path.GetFileName(Path.ChangeExtension(glossaryPath, ".md"))} up to date.");
    }
}
