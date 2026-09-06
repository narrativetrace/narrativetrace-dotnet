// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace NarrativeTrace.Runtime;

/// <summary>
/// Exports a trace tree as a flat chapter JSON object matching
/// <c>chapter.schema.json</c> — one service's complete contribution to a
/// trace. The <c>nt.chapterTree</c> field embeds the full nested trace tree
/// (produced by <see cref="JsonExporter"/>) as a serialized string, sent
/// only to the NarrativeTrace backend.
/// </summary>
/// <remarks>
/// Entry count is computed recursively across all nested children. The
/// outcome is derived from the root node: Returned → success, Threw →
/// failure, Incomplete → partial. The completion timestamp source is
/// injectable for deterministic testing.
/// </remarks>
public sealed class ChapterExporter
{
    private readonly Func<DateTimeOffset> _now;

    /// <summary>Creates an exporter that timestamps chapters with the current UTC time.</summary>
    public ChapterExporter()
        : this(() => DateTimeOffset.UtcNow)
    {
    }

    /// <summary>Creates an exporter with an injected clock.</summary>
    /// <param name="now">
    /// Supplies the export timestamp. The seam that makes exported output
    /// deterministic in tests — pass a fixed instant so snapshots stay stable.
    /// </param>
    public ChapterExporter(Func<DateTimeOffset> now)
    {
        _now = now;
    }

    /// <summary>Exports the tree as a chapter JSON string.</summary>
    /// <remarks>
    /// Identity — <c>trace_id</c>, <c>nt.storyId</c>, <c>nt.chapterId</c>,
    /// <c>nt.traceName</c> — is resolved once through
    /// <see cref="TraceIdentity.Of"/>, the same resolution
    /// <see cref="TraceTreeCanonicalMapper"/> reads, so a chapter and the
    /// entries of its own trace can never name two different traces.
    /// </remarks>
    public string ExportChapter(TraceTree tree, TraceMetadata metadata)
    {
        // TraceNode.Children is a type, not a guarantee of acyclicity - bound
        // once, here, so CountEntries below (and JsonExporter's own walk)
        // can never overflow the stack or loop forever on a hand-built or
        // replayed cycle. Cheap on ordinary input: TreeWalk.Bound returns
        // Roots unchanged once it confirms there is nothing to bound.
        tree = tree with { Roots = TreeWalk.Bound(tree.Roots) };
        var root = tree.Roots.Count > 0 ? tree.Roots[0] : null;
        var identity = TraceIdentity.Of(tree);
        var outcome = DeriveOutcome(root);
        var title = DeriveTitle(root);
        var chapterTree = JsonExporter.Export(tree, metadata);
        return BuildJson(identity, root, outcome, title, chapterTree, tree);
    }

    private string BuildJson(
        TraceIdentity identity, TraceNode? root, string outcome,
        string title, string chapterTree, TraceTree tree)
    {
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(
            stream, new JsonWriterOptions { Indented = true });

        writer.WriteStartObject();
        WriteHeader(writer, identity, root, outcome, title);
        WriteSchemaFields(writer, identity, outcome, title);
        WriteMetrics(writer, root, tree);
        writer.WriteString("nt.chapterTree", chapterTree);
        writer.WriteEndObject();
        writer.Flush();

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    // service, trace_id, nt.storyId and nt.chapterId are all required by
    // chapter.schema.json, so they are written, never omitted: identity is
    // resolved eagerly (adopt → inherit → generate), so a span-less tree has a
    // value to write instead of yielding a chapter that fails its own schema.
    private void WriteHeader(
        Utf8JsonWriter writer, TraceIdentity identity, TraceNode? root,
        string outcome, string title)
    {
        var durationMs = DurationMs(root);
        writer.WriteString("timestamp", FormatIso(_now()));
        writer.WriteString(
            "level", outcome == "failure" ? "error" : "info");
        writer.WriteString(
            "message",
            $"Chapter complete: {title} [{durationMs}ms] {outcome}");
        writer.WriteString(
            "service",
            CanonicalEntryMapper.ServiceNameOrUnknown(identity.Inherited?.ServiceName));
        writer.WriteString("trace_id", identity.TraceId.Value);
    }

    private static void WriteSchemaFields(
        Utf8JsonWriter writer, TraceIdentity identity,
        string outcome, string title)
    {
        writer.WriteString("nt.entryType", "chapter");
        writer.WriteString("nt.storyId", identity.StoryId);
        writer.WriteString("nt.chapterId", identity.ChapterId);
        writer.WriteString("nt.title", title);
        writer.WriteString("nt.outcome", outcome);
        writer.WriteString("nt.completionStatus", "complete");
        // Derived from the resolved id rather than carried beside it, so the
        // name and the trace_id above always describe the same trace.
        writer.WriteString("nt.traceName", identity.TraceName);
        writer.WriteString("nt.schemaVersion", CanonicalSchema.Version);
    }

    private static void WriteMetrics(
        Utf8JsonWriter writer, TraceNode? root, TraceTree tree)
    {
        if (root is not null)
        {
            writer.WriteNumber("nt.totalDurationMs", DurationMs(root));
        }

        writer.WriteNumber("nt.entryCount", CountEntries(tree.Roots));
    }

    private static long DurationMs(TraceNode? node)
    {
        return node is null
            ? 0
            : node.DurationTicks / TimeSpan.TicksPerMillisecond;
    }

    private static string DeriveOutcome(TraceNode? root)
    {
        return root?.Outcome switch
        {
            Returned => "success",
            Threw => "failure",
            Incomplete => "partial",
            _ => "success",
        };
    }

    private static string DeriveTitle(TraceNode? root)
    {
        return root is null
            ? "unknown"
            : $"{root.Signature.ClassName}.{root.Signature.MethodName}";
    }

    private static int CountEntries(IReadOnlyList<TraceNode> nodes)
    {
        var count = 0;
        for (var i = 0; i < nodes.Count; i++)
        {
            count += 1 + CountEntries(nodes[i].Children);
        }

        return count;
    }

    private static string FormatIso(DateTimeOffset time)
    {
        return time.UtcDateTime.ToString(
            "yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
    }
}
