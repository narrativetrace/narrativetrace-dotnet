// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
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
    public static ScenarioDelta? Write(
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
        return Write(
            tree, ArtifactIdentity.OfMethod(testClassName, testMethodName), displayName, failed,
            outputDir, format, renderers, console, entryArtifacts);
    }

    /// <summary>
    /// The same write, keyed by the full <see cref="ArtifactIdentity"/> — the
    /// overload an integration whose test method may run more than once
    /// (parameterized, repeated) must use, so each invocation gets its own
    /// files instead of overwriting the previous one's.
    /// </summary>
    /// <param name="tree">The captured trace; an empty tree writes nothing and returns <see langword="null"/>.</param>
    /// <param name="identity">Which test invocation this write belongs to.</param>
    /// <param name="displayName">The human-readable scenario name recorded inside the artifact.</param>
    /// <param name="failed">Whether the run is green — see the remarks below.</param>
    /// <param name="outputDir">The root to write under. Created if missing.</param>
    /// <param name="format">The primary artifact format.</param>
    /// <param name="renderers">The out-of-Core renderers (diagrams, JSON).</param>
    /// <param name="console">Where the "wrote artifact" line is echoed.</param>
    /// <param name="entryArtifacts">The opt-in machine-readable entry arrays.</param>
    /// <returns>
    /// The scenario's structural delta against its last green <c>.nt</c>
    /// artifact, when the Markdown path produced one; <see langword="null"/>
    /// for every other format and for an empty trace.
    /// </returns>
    /// <remarks>
    /// <paramref name="failed"/> is the run's whole verdict, not just its
    /// assertions: a test that passed but whose structure an approval gate
    /// rejected must be reported as failed here too, or the rejected
    /// structure would advance the last-green baseline (see
    /// <see cref="WriteStructuralArtifact"/>).
    /// </remarks>
    public static ScenarioDelta? Write(
        TraceTree tree,
        ArtifactIdentity identity,
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
            return null;
        }

        var resolver = new OutputDirectoryResolver(outputDir);
        var slug = identity.FileSlug();
        var file = Path.Combine(
            resolver.TraceDirectory(identity.TestClassName), slug + Extension(format));
        WriteFile(
            file,
            RenderForFormat(format, tree, displayName, failed, renderers));
        EchoToConsole(console, displayName, tree, file);

        var delta = WriteMarkdownExtras(
            tree, identity, displayName, failed, resolver, renderers, format);
        WriteEntryArtifacts(
            tree, resolver.TraceDirectory(identity.TestClassName), slug, renderers,
            entryArtifacts);
        return delta;
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
    private static ScenarioDelta? WriteMarkdownExtras(
        TraceTree tree, ArtifactIdentity identity, string displayName, bool failed,
        OutputDirectoryResolver resolver, TraceArtifactRenderers renderers,
        TraceArtifactFormat format)
    {
        if (format != TraceArtifactFormat.Markdown)
        {
            return null;
        }

        var simpleName = SimpleName(identity.TestClassName);
        var scenario = ScenarioFramer.Humanize(displayName);
        WriteFile(resolver.DiagramFile(identity), renderers.Mermaid(tree));
        var metadata = new TraceMetadata(
            scenario, ScenarioResultExtensions.Of(failed), TestClass: simpleName);
        WriteFile(
            resolver.TraceArtifact(identity, ".json"), renderers.Json(tree, metadata));

        // Not `scenario`: the structural artifact is the value-free one, and a
        // display name may have had an argument interpolated into it (see
        // ArtifactIdentity.StructuralScenario).
        return WriteStructuralArtifact(
            tree, failed, resolver.StructuralFile(identity), identity.StructuralScenario(displayName));
    }

    /// <summary>
    /// Writes the ADR-002 structural artifact (<c>.nt</c>) and classifies the
    /// scenario against it.
    /// </summary>
    /// <remarks>
    /// The file on disk is the LAST GREEN baseline: a green run advances it,
    /// a non-green run compares against it but never overwrites it — so the
    /// delta always reads "what changed since the last time this scenario
    /// passed".
    /// </remarks>
    private static ScenarioDelta WriteStructuralArtifact(
        TraceTree tree, bool failed, string ntFile, string scenario)
    {
        var current = StructuralTraceRenderer.RenderDocument(tree, scenario);
        var baseline = File.Exists(ntFile) ? File.ReadAllText(ntFile) : null;
        var delta = ScenarioDelta.Of(scenario, baseline, current);
        if (!failed)
        {
            WriteFile(ntFile, current);
        }

        return delta;
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

    // Encoding policy (substitute rather than throw on an unpaired surrogate)
    // now lives in the shared TraceFileWriter, used by every writer that
    // touches disk — this is a thin alias kept so the many WriteFile(...)
    // call sites above did not all need renaming.
    private static void WriteFile(string path, string content)
    {
        TraceFileWriter.Write(path, content);
    }
}
