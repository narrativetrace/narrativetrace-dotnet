// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using System.Text.Json;

namespace NarrativeTrace.Runtime;

/// <summary>
/// Serializes a finished trace to the canonical JSON document that machine
/// consumers read.
/// </summary>
/// <remarks>
/// The one output format with a schema contract rather than a prose layout —
/// the cross-runtime interchange format, so its shape is shared with the Java
/// edition and changing it is a breaking change. Stateless and thread-safe.
/// </remarks>
public static class JsonExporter
{
    /// <summary>Exports a trace as an indented canonical JSON document.</summary>
    /// <param name="tree">The finished trace. An empty tree still produces a well-formed document, not an empty string.</param>
    /// <param name="metadata">Provenance recorded alongside the trace — scenario name, test identity, timestamp.</param>
    /// <returns>The JSON document as a UTF-8 string, indented for readability and diffing.</returns>
    /// <remarks>
    /// Builds the whole document in memory, so cost scales with trace size.
    /// Unlike <see cref="StructuralTraceRenderer"/> this <b>does</b> include
    /// captured values, exception messages and timings — treat the output as
    /// carrying the same sensitivity as the data that was traced.
    /// </remarks>
    public static string Export(
        TraceTree tree, TraceMetadata metadata)
    {
        // TraceNode.Children is a type, not a guarantee of acyclicity - bound
        // once, here, so every recursive walk below (WriteTrace, WriteEvents)
        // can never overflow the stack or loop forever on a hand-built or
        // replayed cycle. Cheap on ordinary input: TreeWalk.Bound returns
        // Roots unchanged once it confirms there is nothing to bound.
        tree = tree with { Roots = TreeWalk.Bound(tree.Roots) };

        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(
            stream, new JsonWriterOptions { Indented = true });

        writer.WriteStartObject();
        WriteVersion(writer);
        WriteScenario(writer, tree, metadata);
        WriteTrace(writer, tree);
        WriteEvents(writer, tree);
        writer.WriteEndObject();
        writer.Flush();

        return System.Text.Encoding.UTF8.GetString(
            stream.ToArray());
    }

    private static void WriteVersion(Utf8JsonWriter writer)
    {
        writer.WriteString("version", "1.0");
    }

    private static void WriteScenario(
        Utf8JsonWriter writer, TraceTree tree,
        TraceMetadata metadata)
    {
        writer.WriteStartObject("scenario");
        writer.WriteString("name", metadata.Scenario);
        writer.WriteString(
            "result", metadata.Result.WireName());
        WriteTotalDuration(writer, tree);
        WriteOptionalMetadata(writer, metadata);
        writer.WriteEndObject();
    }

    /// <summary>
    /// Writes <c>scenario.durationMs</c> from the <b>first root only</b>,
    /// matching the Java runtime and the other whole-trace duration sites in
    /// this runtime (the Markdown document header and <c>ChapterExporter</c>).
    /// </summary>
    /// <remarks>
    /// Deliberately not a sum over roots. Multiple roots come from grafted
    /// fire-and-forget work, which typically ran <i>concurrently</i>, so summing
    /// double-counts wall-clock time — two branches of 100 ms would report
    /// 200 ms for a scenario that took 100. First-root is not wall-clock either
    /// (it ignores later roots); the correct form is
    /// <c>max(start + duration) − min(start)</c>, but changing that is a
    /// cross-runtime schema-behaviour change and must be agreed across the
    /// runtimes first.
    /// </remarks>
    private static void WriteTotalDuration(
        Utf8JsonWriter writer, TraceTree tree)
    {
        var total = tree.Roots.Count > 0
            ? tree.Roots[0].DurationTicks
            : 0L;

        writer.WriteNumber(
            "durationMs",
            total / TimeSpan.TicksPerMillisecond);
    }

    private static void WriteOptionalMetadata(
        Utf8JsonWriter writer, TraceMetadata metadata)
    {
        if (metadata.TestClass is not null)
        {
            writer.WriteString("testClass", metadata.TestClass);
        }

        if (metadata.TestMethod is not null)
        {
            writer.WriteString(
                "testMethod", metadata.TestMethod);
        }

        if (metadata.Framework is not null)
        {
            writer.WriteString("framework", metadata.Framework);
        }

        if (metadata.Timestamp is not null)
        {
            writer.WriteString("timestamp", metadata.Timestamp);
        }
    }

    // Identity comes from the shared TraceIdentity, resolved depth-first over
    // the whole forest, so this document names the same trace as the chapter
    // that embeds it and the canonical entries flattened from the same tree.
    // The block is written always: chapter-tree.schema.json marks it optional,
    // but that is a schema floor, not the contract between the three emitters
    // — a span-less tree still gets a real, unique identity from TraceTree's
    // generate rung, and this document must not omit it while ChapterExporter
    // and TraceTreeCanonicalMapper report it.
    private static void WriteTrace(
        Utf8JsonWriter writer, TraceTree tree)
    {
        var identity = TraceIdentity.Of(tree);

        writer.WriteStartObject("trace");
        writer.WriteString("traceId", identity.TraceId.Value);
        writer.WriteString("traceName", identity.TraceName);
        WriteInheritedContext(writer, identity.Inherited);
        writer.WriteEndObject();
    }

    // The request-scoped fields, written only when the tree actually carried a
    // span context to inherit them from. A generated identity knows which
    // trace this is and nothing about who called it, and inventing a service
    // name or a client IP would be worse than omitting them.
    private static void WriteInheritedContext(
        Utf8JsonWriter writer, SpanContext? inherited)
    {
        if (inherited is null)
        {
            return;
        }

        WriteOptional(writer, "serviceName", inherited.ServiceName);
        WriteOptional(writer, "serviceVersion", inherited.ServiceVersion);
        WriteOptional(writer, "environment", inherited.Environment);
        WriteOptional(writer, "httpMethod", inherited.HttpMethod);
        WriteOptional(writer, "httpRoute", inherited.HttpRoute?.Value);
        WriteOptional(writer, "clientIp", inherited.ClientIp?.Value);
        WriteOptional(writer, "enduserId", inherited.EnduserId?.Value);
        WriteOptional(writer, "sessionId", inherited.SessionId?.Value);
        WriteOptional(writer, "tenantId", inherited.TenantId?.Value);
    }

    private static void WriteOptional(
        Utf8JsonWriter writer, string name, string? value)
    {
        if (value is not null)
        {
            writer.WriteString(name, value);
        }
    }

    private static void WriteEvents(
        Utf8JsonWriter writer, TraceTree tree)
    {
        writer.WriteStartArray("events");
        var nextId = 1;
        WriteNodeEvents(
            writer, tree.Roots, null, 0, ref nextId);
        writer.WriteEndArray();
    }

    private static void WriteNodeEvents(
        Utf8JsonWriter writer,
        IReadOnlyList<TraceNode> nodes,
        string? parentSpanId, int depth, ref int nextId)
    {
        for (var i = 0; i < nodes.Count; i++)
        {
            WriteNodePair(
                writer, nodes[i], parentSpanId,
                depth, ref nextId);
        }
    }

    private static void WriteNodePair(
        Utf8JsonWriter writer, TraceNode node,
        string? parentSpanId, int depth, ref int nextId)
    {
        var spanId = ResolveSpanId(node, ref nextId);
        WriteEnterEvent(writer, node, spanId, parentSpanId, depth);
        WriteNodeEvents(
            writer, node.Children, spanId, depth + 1,
            ref nextId);
        WriteExitEvent(writer, node, spanId, parentSpanId, depth);
    }

    private static string ResolveSpanId(TraceNode node, ref int nextId)
    {
        return node.SpanContext?.SpanId.Value
            ?? (nextId++).ToString(
                System.Globalization.CultureInfo.InvariantCulture);
    }

    private static void WriteEnterEvent(
        Utf8JsonWriter writer, TraceNode node,
        string spanId, string? parentSpanId, int depth)
    {
        writer.WriteStartObject();
        writer.WriteString("spanId", spanId);
        writer.WriteString("type", "enter");
        writer.WriteString(
            "className", node.Signature.ClassName);
        writer.WriteString(
            "methodName", node.Signature.MethodName);
        WriteParameters(writer, node.Signature.Parameters);
        WriteParentSpanId(writer, parentSpanId);
        writer.WriteNumber("depth", depth);
        writer.WriteEndObject();
    }

    private static void WriteExitEvent(
        Utf8JsonWriter writer, TraceNode node,
        string spanId, string? parentSpanId, int depth)
    {
        writer.WriteStartObject();
        writer.WriteString("spanId", spanId);
        writer.WriteString("type", "exit");
        writer.WriteString(
            "className", node.Signature.ClassName);
        writer.WriteString(
            "methodName", node.Signature.MethodName);
        WriteOutcome(writer, node);
        WriteDuration(writer, node);
        WriteParentSpanId(writer, parentSpanId);
        writer.WriteNumber("depth", depth);
        WriteConcurrency(writer, node);
        writer.WriteEndObject();
    }

    private static void WriteConcurrency(
        Utf8JsonWriter writer, TraceNode node)
    {
        if (node.Concurrency is null)
        {
            return;
        }

        var c = node.Concurrency;
        writer.WriteStartObject("concurrency");
        writer.WriteString("groupId", c.GroupId);
        writer.WriteString("kind", KebabCase(c.Kind));
        writer.WriteString("taskLabel", c.TaskLabel);
        writer.WriteNumber("threadId", c.ThreadId);
        WriteThreadName(writer, c.ThreadName);
        writer.WriteBoolean(
            "isThreadPoolThread", c.IsThreadPoolThread);
        writer.WriteEndObject();
    }

    private static string KebabCase(ConcurrencyKind kind)
    {
        var name = kind.ToString();
        var sb = new System.Text.StringBuilder(name.Length + 4);
        for (var i = 0; i < name.Length; i++)
        {
            var ch = name[i];
            if (char.IsUpper(ch) && i > 0)
            {
                sb.Append('-');
            }

            sb.Append(char.ToLowerInvariant(ch));
        }

        return sb.ToString();
    }

    private static void WriteThreadName(
        Utf8JsonWriter writer, string? threadName)
    {
        if (threadName is not null)
        {
            writer.WriteString("threadName", threadName);
        }
        else
        {
            writer.WriteNull("threadName");
        }
    }

    private static void WriteParameters(
        Utf8JsonWriter writer,
        IReadOnlyList<ParameterCapture> parameters)
    {
        writer.WriteStartArray("parameters");
        for (var i = 0; i < parameters.Count; i++)
        {
            writer.WriteStartObject();
            writer.WriteString("name", parameters[i].Name);
            writer.WriteString(
                "value", parameters[i].DisplayValue());
            writer.WriteBoolean(
                "redacted", parameters[i].Redacted);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static void WriteOutcome(
        Utf8JsonWriter writer, TraceNode node)
    {
        switch (node.Outcome)
        {
            case Returned { RenderedValue: { } value }:
                writer.WriteString("outcome", "returned");
                writer.WriteString("returnValue", value);
                break;
            case Returned:
                writer.WriteString("outcome", "returned");
                break;
            case Threw { Error: { } ex }:
                writer.WriteString("outcome", "threw");
                writer.WriteString(
                    "errorType", ex.GetType().Name);
                writer.WriteString(
                    "errorMessage", ExceptionMessage.Text(ex));
                break;
            case Incomplete:
                writer.WriteString("outcome", "incomplete");
                break;
        }
    }

    private static void WriteDuration(
        Utf8JsonWriter writer, TraceNode node)
    {
        writer.WriteNumber(
            "durationMs",
            node.DurationTicks / TimeSpan.TicksPerMillisecond);
    }

    private static void WriteParentSpanId(
        Utf8JsonWriter writer, string? parentSpanId)
    {
        if (parentSpanId is not null)
        {
            writer.WriteString("parentSpanId", parentSpanId);
        }
    }
}
