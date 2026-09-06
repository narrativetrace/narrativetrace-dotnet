// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using System.Text;
using System.Text.Json;

namespace NarrativeTrace.Runtime;

/// <summary>
/// Serializes a <see cref="CanonicalEntry"/> to JSON using the canonical
/// dotted field names from <c>entry.schema.json</c> (<c>code.namespace</c>,
/// <c>nt.eventType</c>, <c>exception.type</c>, …). Null optional fields are
/// omitted from the output.
/// </summary>
public static class CanonicalEntrySerializer
{
    /// <summary>Serializes one canonical entry to a single-line JSON object.</summary>
    /// <param name="entry">The entry to serialize.</param>
    /// <returns>
    /// A compact JSON object. Absent optional fields are omitted rather than
    /// emitted as <c>null</c>, so consumers must treat a missing key and a null
    /// value as the same thing.
    /// </returns>
    public static string ToJson(CanonicalEntry entry)
    {
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(
            stream, new JsonWriterOptions { Indented = true });

        writer.WriteStartObject();
        WriteUniversal(writer, entry);
        WriteOtel(writer, entry);
        WriteResource(writer, entry);
        WriteNarrative(writer, entry);
        writer.WriteEndObject();
        writer.Flush();

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteUniversal(
        Utf8JsonWriter writer, CanonicalEntry e)
    {
        writer.WriteString("timestamp", e.Timestamp);
        writer.WriteString("level", e.Level);
        writer.WriteString("message", e.Message);
        // A required field must never be silently blanked: an empty string passes a
        // {"type": "string"} schema check while carrying no information. A null here is
        // an upstream mapper bug, so fail loudly rather than paper over it.
        writer.WriteString("service", e.Service ?? throw new InvalidOperationException(
            "Required canonical field is null: service"));
        WriteOptional(writer, "environment", e.Environment);
    }

    private static void WriteOtel(Utf8JsonWriter writer, CanonicalEntry e)
    {
        WriteOptional(writer, "trace_id", e.TraceId);
        WriteOptional(writer, "span_id", e.SpanId);
        WriteOptional(writer, "parent_span_id", e.ParentSpanId);
        WriteOptional(writer, "code.namespace", e.CodeNamespace);
        WriteOptional(writer, "code.function", e.CodeFunction);
        WriteOptional(writer, "exception.type", e.ExceptionType);
        WriteOptional(writer, "exception.message", e.ExceptionMessage);
        WriteOptional(writer, "code.filepath", e.CodeFilepath);
        WriteOptionalInt(writer, "code.lineno", e.CodeLineno);
        WriteOptionalLong(writer, "durationMs", e.DurationMs);
    }

    /// <summary>
    /// The 1.2 environment identity: thread and process resource fields, all
    /// optional and all omitted unless the capture supplied them.
    /// </summary>
    private static void WriteResource(Utf8JsonWriter writer, CanonicalEntry e)
    {
        WriteOptional(writer, "thread.name", e.ThreadName);
        WriteOptionalLong(writer, "thread.id", e.ThreadId);
        WriteOptional(writer, "host.name", e.HostName);
        WriteOptionalInt(writer, "process.pid", e.ProcessPid);
        WriteOptional(writer, "process.runtime.version", e.ProcessRuntimeVersion);
    }

    private static void WriteNarrative(
        Utf8JsonWriter writer, CanonicalEntry e)
    {
        writer.WriteString("nt.entryType", e.NtEntryType);
        writer.WriteString("nt.eventType", e.NtEventType);
        writer.WriteString("nt.schemaVersion", e.NtSchemaVersion);
        WriteOptional(writer, "nt.traceName", e.NtTraceName);
        WriteOptional(writer, "nt.storyId", e.NtStoryId);
        WriteOptional(writer, "nt.chapterId", e.NtChapterId);
        WriteOptional(writer, "nt.outcome", e.NtOutcome);
        WriteOptional(writer, "nt.forkId", e.NtForkId);
        WriteOptionalInt(writer, "nt.branchIndex", e.NtBranchIndex);
        WriteOptional(writer, "nt.causalId", e.NtCausalId);
        WriteOptional(writer, "nt.returnValue", e.NtReturnValue);
        WriteIdentity(writer, e);
        WriteParameters(writer, e.NtParameters);
    }

    /// <summary>
    /// The 1.1 narration template and the 1.2 declared-identity fields — what
    /// makes an entry name the exact method it came from, overloads included.
    /// </summary>
    private static void WriteIdentity(Utf8JsonWriter writer, CanonicalEntry e)
    {
        WriteOptional(writer, "nt.narrationTemplate", e.NtNarrationTemplate);
        WriteOptional(writer, "nt.package", e.NtPackage);
        WriteOptional(writer, "nt.returnType", e.NtReturnType);
        WriteOptional(writer, "nt.exceptionPackage", e.NtExceptionPackage);
        WriteOptional(writer, "nt.instanceId", e.NtInstanceId);
        if (e.NtThreadVirtual is { } virtualThread)
        {
            writer.WriteBoolean("nt.threadVirtual", virtualThread);
        }
    }

    private static void WriteParameters(
        Utf8JsonWriter writer, IReadOnlyList<ParameterCapture>? parameters)
    {
        if (parameters is null)
        {
            return;
        }

        writer.WriteStartArray("nt.parameters");
        for (var i = 0; i < parameters.Count; i++)
        {
            writer.WriteStartObject();
            writer.WriteString("name", parameters[i].Name);
            writer.WriteString("value", parameters[i].RenderedValue ?? "");
            writer.WriteBoolean("redacted", parameters[i].Redacted);
            WriteOptional(writer, "type", parameters[i].DeclaredType);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static void WriteOptional(
        Utf8JsonWriter writer, string name, string? value)
    {
        if (value is not null)
        {
            writer.WriteString(name, value);
        }
    }

    private static void WriteOptionalLong(
        Utf8JsonWriter writer, string name, long? value)
    {
        if (value is not null)
        {
            writer.WriteNumber(name, value.Value);
        }
    }

    private static void WriteOptionalInt(
        Utf8JsonWriter writer, string name, int? value)
    {
        if (value is not null)
        {
            writer.WriteNumber(name, value.Value);
        }
    }
}
