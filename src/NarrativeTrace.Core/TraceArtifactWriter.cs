// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Text;

namespace NarrativeTrace.Core;

/// <summary>
/// The renderers a <see cref="TraceArtifactWriter"/> needs but that live outside
/// Core (diagram renderers and the JSON exporter) — injected as delegates so
/// Core stays dependency-free, mirroring Java's <c>TraceTestSupport</c> which
/// takes its renderers as parameters.
/// </summary>
/// <param name="Mermaid">Renders the Mermaid sequence diagram.</param>
/// <param name="PlantUml">Renders the PlantUML sequence diagram.</param>
/// <param name="Json">Renders the chapter-tree JSON document.</param>
/// <param name="CanonicalEntries">
/// Renders the canonical entry array (<c>&lt;test&gt;.canonical.json</c>).
/// Only called when <see cref="EntryArtifacts.Canonical"/> is set.
/// </param>
/// <param name="StructuralEntries">
/// Renders the value-free entry array (<c>&lt;test&gt;.structural.json</c>).
/// Only called when <see cref="EntryArtifacts.Structural"/> is set.
/// </param>
public sealed record TraceArtifactRenderers(
    Func<TraceTree, string> Mermaid,
    Func<TraceTree, string> PlantUml,
    Func<TraceTree, TraceMetadata, string> Json,
    Func<TraceTree, string> CanonicalEntries,
    Func<TraceTree, string> StructuralEntries);

/// <summary>
/// Writes per-test trace artifacts into the Java-compatible
/// <c>traces/&lt;Class&gt;/&lt;slug&gt;</c> layout: the primary format file, plus — for
/// Markdown — a sibling <c>.json</c>, a <c>diagrams/&lt;Class&gt;/&lt;slug&gt;.mmd</c>
/// companion and the value-free <c>structural/&lt;Class&gt;/&lt;slug&gt;.nt</c> artifact
/// (ADR-002). Empty traces write nothing.
/// </summary>
public static class TraceArtifactWriter
{
    /// <summary>The file extension for a format, including the leading dot.</summary>
    /// <param name="format">The artifact format.</param>
    /// <returns>
    /// <c>.txt</c>, <c>.mmd</c>, <c>.puml</c>, or <c>.md</c>. Note the fallback:
    /// an unrecognized value yields <c>.md</c> rather than throwing, so an
    /// out-of-range cast silently produces a Markdown extension.
    /// </returns>
    public static string Extension(TraceArtifactFormat format)
    {
        return format switch
        {
            TraceArtifactFormat.Text => ".txt",
            TraceArtifactFormat.Mermaid => ".mmd",
            TraceArtifactFormat.PlantUml => ".puml",
            _ => ".md",
        };
    }

    /// <summary>
    /// Writes one test's trace artifacts into the
    /// <c>traces/&lt;Class&gt;/&lt;slug&gt;</c> layout.
    /// </summary>
    /// <param name="tree">
    /// The captured trace. <b>An empty tree writes nothing and returns
    /// silently</b> — no file is created, so a missing artifact means "nothing
    /// was captured", not "the write failed".
    /// </param>
    /// <param name="testClassName">
    /// The test class. A dotted name is reduced to its final segment for the
    /// directory, so two classes with the same simple name in different
    /// namespaces write into the same directory.
    /// </param>
    /// <param name="testMethodName">The test method; slugified into the file name.</param>
    /// <param name="displayName">The human-readable scenario name recorded inside the artifact.</param>
    /// <param name="failed">Whether the test failed — recorded in the companion metadata.</param>
    /// <param name="outputDir">The root to write under. Created if missing.</param>
    /// <param name="format">
    /// The primary artifact format. <see cref="TraceArtifactFormat.Markdown"/>
    /// additionally emits the sibling <c>.json</c>, <c>.mmd</c> diagram and
    /// value-free <c>.nt</c> structural artifacts; every other format writes the
    /// one file only.
    /// </param>
    /// <param name="renderers">
    /// The out-of-Core renderers (diagrams, JSON). Required even for formats
    /// that appear not to need them, since Markdown pulls in all three.
    /// </param>
    /// <param name="console">Where the "wrote artifact" line is echoed. Pass <see cref="TextWriter.Null"/> to stay quiet.</param>
    /// <param name="entryArtifacts">
    /// The opt-in machine-readable entry arrays. Written beside the trace file
    /// whatever the primary format is — they are consumer artifacts, not a
    /// companion of the Markdown view.
    /// </param>
    /// <exception cref="IOException">The output directory or a file could not be written.</exception>
    /// <exception cref="UnauthorizedAccessException">The process lacks permission to write to <paramref name="outputDir"/>.</exception>
    /// <remarks>
    /// Overwrites an existing artifact without warning — reruns are expected to
    /// replace their previous output, which is what keeps an approval baseline
    /// diffable.
    /// </remarks>
    public static void Write(
        TraceTree tree,
        string testClassName,
        string testMethodName,
        string displayName,
        bool failed,
        string outputDir,
        TraceArtifactFormat format,
        TraceArtifactRenderers renderers,
        TextWriter console,
        EntryArtifacts entryArtifacts = default)
    {
        if (tree.IsEmpty)
        {
            return;
        }

        var resolver = new OutputDirectoryResolver(outputDir);
        var slug = OutputDirectoryResolver.ToFileSlug(testMethodName);
        var file = Path.Combine(
            resolver.TraceDirectory(testClassName), slug + Extension(format));
        WriteFile(
            file,
            RenderForFormat(format, tree, displayName, failed, renderers));
        EchoToConsole(console, displayName, tree, file);

        WriteMarkdownExtras(
            tree, testClassName, displayName, failed, outputDir, slug,
            resolver, renderers, format);
        WriteEntryArtifacts(
            tree, resolver.TraceDirectory(testClassName), slug, renderers,
            entryArtifacts);
    }

    /// <summary>
    /// Writes the opt-in entry arrays beside the trace file. Both are named
    /// from the same slug, so they sit next to the artifact a human opens.
    /// </summary>
    private static void WriteEntryArtifacts(
        TraceTree tree,
        string traceDirectory,
        string slug,
        TraceArtifactRenderers renderers,
        EntryArtifacts entryArtifacts)
    {
        if (entryArtifacts.Canonical)
        {
            WriteFile(
                Path.Combine(traceDirectory, slug + ".canonical.json"),
                renderers.CanonicalEntries(tree));
        }

        if (entryArtifacts.Structural)
        {
            WriteFile(
                Path.Combine(traceDirectory, slug + ".structural.json"),
                renderers.StructuralEntries(tree));
        }
    }

    private static string RenderForFormat(
        TraceArtifactFormat format, TraceTree tree, string displayName,
        bool failed, TraceArtifactRenderers renderers)
    {
        return format switch
        {
            // Frame sees the raw name on purpose: it strips a Test_/Should_
            // prefix itself, which humanizing first would defeat.
            TraceArtifactFormat.Text =>
                ScenarioFramer.Frame(displayName)
                + "\n\n" + IndentedTextRenderer.Render(tree),
            TraceArtifactFormat.Mermaid => renderers.Mermaid(tree),
            TraceArtifactFormat.PlantUml => renderers.PlantUml(tree),
            _ => MarkdownRenderer.RenderDocument(
                tree,
                new TraceMetadata(
                    ScenarioFramer.Humanize(displayName),
                    ScenarioResultExtensions.Of(failed))),
        };
    }

    /// <summary>
    /// The companions only the Markdown view carries; a no-op for every other
    /// format, so the caller reads as one unconditional sequence of writes.
    /// </summary>
    private static void WriteMarkdownExtras(
        TraceTree tree, string testClassName, string displayName, bool failed,
        string outputDir, string slug, OutputDirectoryResolver resolver,
        TraceArtifactRenderers renderers, TraceArtifactFormat format)
    {
        if (format != TraceArtifactFormat.Markdown)
        {
            return;
        }

        var simpleName = SimpleName(testClassName);
        var scenario = ScenarioFramer.Humanize(displayName);
        WriteFile(
            Path.Combine(outputDir, "diagrams", simpleName, slug + ".mmd"),
            renderers.Mermaid(tree));
        var metadata = new TraceMetadata(
            scenario, ScenarioResultExtensions.Of(failed), TestClass: simpleName);
        WriteFile(
            Path.Combine(resolver.TraceDirectory(testClassName), slug + ".json"),
            renderers.Json(tree, metadata));

        // ADR-002: the AI-safe structural artifact — zero values, deterministic,
        // diffable. Lives in its own tree so an agent can be handed the whole
        // directory without ever meeting a runtime value.
        WriteFile(
            Path.Combine(outputDir, "structural", simpleName, slug + ".nt"),
            StructuralTraceRenderer.RenderDocument(tree, scenario));
    }

    private static void EchoToConsole(
        TextWriter console, string displayName, TraceTree tree, string file)
    {
        console.Write(
            "\n" + ScenarioFramer.Frame(displayName)
            + "\n\n" + IndentedTextRenderer.Render(tree));
        console.WriteLine("Trace written: " + file);
    }

    // Feeds directly into Path.Combine for the diagrams/ and structural/
    // companions below, so it needs the same guard OutputDirectoryResolver
    // applies to the primary trace directory — one sanitization rule, not two.
    private static string SimpleName(string testClassName)
    {
        var dot = testClassName.LastIndexOf('.');
        var simpleName = dot >= 0 ? testClassName.Substring(dot + 1) : testClassName;
        return OutputDirectoryResolver.ToDirectorySlug(simpleName);
    }

    // Narration and exception-message text never pass through ControlEscape
    // (that text is prose an author wrote, and the renderers are meant to
    // show it), so an unpaired surrogate can still reach this last line
    // before the filesystem. .NET's default UTF-8 encoder raises
    // EncoderFallbackException on one — File.WriteAllText's implicit
    // encoding refuses rather than substitutes — so a value the application
    // merely returned could fail the run that traced it. This encoding
    // substitutes the replacement character instead: an artifact with one
    // U+FFFD in it is a readable artifact; a failed write is a failed build.
    private static readonly Encoding SubstitutingUtf8 = Encoding.GetEncoding(
        "utf-8",
        new EncoderReplacementFallback("�"),
        new DecoderReplacementFallback("�"));

    private static void WriteFile(string path, string content)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir!);
        }

        File.WriteAllBytes(path, SubstitutingUtf8.GetBytes(content));
    }
}
