// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Text;

namespace NarrativeTrace.Core;

/// <summary>
/// The scenario → file index a suite run leaves beside its artifacts.
/// </summary>
/// <remarks>
/// INTENT: Artifact names are derived, not announced, so a reader who knows a
/// scenario had to guess which file holds it — and once a test method runs
/// more than once, guessing stops working. <c>manifest.json</c> answers the
/// question directly: one row per traced scenario, naming the test that
/// produced it, its invocation number when the method ran more than once,
/// and every artifact it owns as a path relative to the output directory.
/// Rows appear in suite execution order and paths always use <c>/</c>, so the
/// file diffs cleanly and reads the same on every platform. The listing is
/// what is actually on disk: an artifact a format or a flag did not produce
/// is absent from the row rather than listed and missing.
/// </remarks>
public static class ScenarioManifest
{
    /// <summary>The manifest's name inside the output directory.</summary>
    public const string FileName = "manifest.json";

    private const string Schema = "narrativetrace/scenario-manifest/1";

    /// <summary>One probe of the <c>traces</c> tree: the role a file plays, and the suffix that finds it.</summary>
    private sealed record TraceRole(string Role, string Suffix);

    /// <summary>
    /// The <c>traces</c> tree probed in listing order. The four rendering
    /// formats share the <c>trace</c> role — a run writes exactly one of them
    /// — and the first that exists claims it.
    /// </summary>
    private static readonly IReadOnlyList<TraceRole> TraceRoles =
    [
        new TraceRole("trace", ".md"),
        new TraceRole("trace", ".txt"),
        new TraceRole("trace", ".mmd"),
        new TraceRole("trace", ".puml"),
        new TraceRole("json", ".json"),
        new TraceRole("canonicalJson", ".canonical.json"),
        new TraceRole("structuralJson", ".structural.json"),
    ];

    /// <summary>
    /// One traced scenario's row.
    /// </summary>
    /// <param name="Scenario">The humanized scenario name, as the artifacts' own headers spell it.</param>
    /// <param name="Identity">Which test invocation produced it.</param>
    /// <param name="Artifacts">
    /// Role → path relative to the output directory, in listing order. A
    /// list of pairs rather than a dictionary so the rendered bytes never
    /// depend on a hashing implementation detail — this file is meant to be
    /// diffed.
    /// </param>
    public sealed record Entry(
        string Scenario, ArtifactIdentity Identity, IReadOnlyList<KeyValuePair<string, string>> Artifacts);

    /// <summary>
    /// Builds one scenario's row by probing the artifact layout for files that exist.
    /// </summary>
    /// <param name="outputDir">The run's output directory, which every listed path is relative to.</param>
    /// <param name="identity">Which test invocation the row belongs to.</param>
    /// <param name="scenario">The humanized scenario name, as the artifacts' own headers spell it.</param>
    public static Entry EntryFor(string outputDir, ArtifactIdentity identity, string scenario)
    {
        var resolver = new OutputDirectoryResolver(outputDir);
        var artifacts = new List<KeyValuePair<string, string>>();
        var seenRoles = new HashSet<string>(StringComparer.Ordinal);
        foreach (var role in TraceRoles)
        {
            PutIfPresent(
                artifacts, seenRoles, role.Role, outputDir, resolver.TraceArtifact(identity, role.Suffix));
        }

        PutIfPresent(artifacts, seenRoles, "diagram", outputDir, resolver.DiagramFile(identity));
        PutIfPresent(artifacts, seenRoles, "structural", outputDir, resolver.StructuralFile(identity));
        return new Entry(scenario, identity, artifacts);
    }

    /// <summary>Records a file that exists under its role, keeping the first path a role resolves to.</summary>
    private static void PutIfPresent(
        List<KeyValuePair<string, string>> artifacts, HashSet<string> seenRoles,
        string role, string outputDir, string file)
    {
        if (seenRoles.Contains(role) || !File.Exists(file))
        {
            return;
        }

        seenRoles.Add(role);
        artifacts.Add(new KeyValuePair<string, string>(role, RelativePath(outputDir, file)));
    }

    /// <summary>
    /// The path as the manifest states it: relative to the output directory,
    /// <c>/</c>-separated.
    /// </summary>
    /// <remarks>
    /// A string-prefix strip rather than <c>Path.GetRelativePath</c> (absent
    /// from netstandard2.0, which this assembly still targets): every path
    /// probed here is built by <see cref="OutputDirectoryResolver"/> from
    /// this same <paramref name="outputDir"/> via <c>Path.Combine</c>, so it
    /// is always a literal descendant — no <c>..</c> traversal or symlink
    /// resolution is ever needed.
    /// </remarks>
    private static string RelativePath(string outputDir, string file)
    {
        var normalizedBase = outputDir.Replace('\\', '/').TrimEnd('/') + "/";
        var normalizedFile = file.Replace('\\', '/');
        return normalizedFile.StartsWith(normalizedBase, StringComparison.Ordinal)
            ? normalizedFile.Substring(normalizedBase.Length)
            : normalizedFile;
    }

    /// <summary>Writes <c>manifest.json</c>; writes nothing at all when the run traced no scenario.</summary>
    public static void Write(IReadOnlyList<Entry> entries, string outputDir)
    {
        Write(entries, outputDir, run: null);
    }

    /// <summary>
    /// The same write, naming the test-suite run that produced it: a top-level
    /// <c>run</c> object (<c>id</c>, <c>name</c>) beside <c>scenarios</c>.
    /// </summary>
    /// <param name="run"><see langword="null"/> to omit the <c>run</c> object entirely — a caller that has not adopted <see cref="RunIdentity"/>.</param>
    public static void Write(IReadOnlyList<Entry> entries, string outputDir, RunIdentity? run)
    {
        if (entries.Count == 0)
        {
            return;
        }

        TraceFileWriter.Write(Path.Combine(outputDir, FileName), Render(entries, run));
    }

    /// <summary>The manifest document, rendered, with no run identity.</summary>
    public static string Render(IReadOnlyList<Entry> entries)
    {
        return Render(entries, run: null);
    }

    /// <summary>The manifest document, rendered, naming <paramref name="run"/> when it is not <see langword="null"/>.</summary>
    public static string Render(IReadOnlyList<Entry> entries, RunIdentity? run)
    {
        var rows = new List<string>(entries.Count);
        foreach (var entry in entries)
        {
            rows.Add(RenderEntry(entry));
        }

        return "{\n  \"schema\": \"" + Schema + "\",\n" + RenderRun(run) + "  \"scenarios\": [\n"
            + string.Join(",\n", rows) + "\n  ]\n}\n";
    }

    /// <summary>The <c>"run": {...},\n</c> object, or empty text when <paramref name="run"/> is <see langword="null"/>.</summary>
    private static string RenderRun(RunIdentity? run)
    {
        if (run is null)
        {
            return string.Empty;
        }

        return "  \"run\": {\n"
            + "    \"id\": \"" + JsonEscape.Escape(run.Id.Value) + "\",\n"
            + "    \"name\": \"" + JsonEscape.Escape(run.Name) + "\"\n"
            + "  },\n";
    }

    private static string RenderEntry(Entry entry)
    {
        var identity = entry.Identity;
        var sb = new StringBuilder("    {\n");
        sb.Append("      \"scenario\": \"").Append(JsonEscape.Escape(entry.Scenario)).Append("\",\n");
        sb.Append("      \"testClass\": \"")
            .Append(JsonEscape.Escape(identity.TestClassName)).Append("\",\n");
        sb.Append("      \"testMethod\": \"")
            .Append(JsonEscape.Escape(identity.MethodName)).Append("\",\n");
        if (identity.IsInvocation)
        {
            sb.Append("      \"invocation\": ").Append(identity.InvocationIndex).Append(",\n");
        }

        sb.Append("      \"artifacts\": {\n").Append(RenderArtifacts(entry.Artifacts));
        sb.Append("      }\n    }");
        return sb.ToString();
    }

    private static string RenderArtifacts(IReadOnlyList<KeyValuePair<string, string>> artifacts)
    {
        if (artifacts.Count == 0)
        {
            return string.Empty;
        }

        var lines = new List<string>(artifacts.Count);
        foreach (var pair in artifacts)
        {
            lines.Add(
                "        \"" + JsonEscape.Escape(pair.Key) + "\": \"" + JsonEscape.Escape(pair.Value) + "\"");
        }

        return string.Join(",\n", lines) + "\n";
    }
}
