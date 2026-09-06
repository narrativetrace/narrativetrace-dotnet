// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Text.Json;
using NarrativeTrace.Core;
using NarrativeTrace.Runtime;
using Xunit;

namespace NarrativeTrace.Core.Tests;

/// <summary>
/// Validates <see cref="CanonicalEntrySerializer"/> output against
/// <c>entry.schema.json</c>, the per-event interoperability contract.
/// </summary>
public class CanonicalEntrySerializerTests
{
    private const string Schema = "entry.schema.json";

    private static CanonicalEntry EnterEntry() => new(
        Timestamp: "2026-07-05T12:00:00.000Z",
        Level: "trace",
        Message: "→ OrderService.PlaceOrder()",
        Service: "OrderSvc",
        Environment: null,
        TraceId: "0af7651916cd43dd8448eb211c80319c",
        SpanId: "b7ad6b7169203331",
        ParentSpanId: null,
        CodeNamespace: "OrderService",
        CodeFunction: "PlaceOrder",
        NtEventType: "method_enter");

    [Fact]
    public void Minimal_enter_entry_validates()
    {
        var json = CanonicalEntrySerializer.ToJson(EnterEntry());

        SchemaValidator.AssertValid(Schema, json);
    }

    [Fact]
    public void Exit_entry_with_return_value_validates()
    {
        var entry = EnterEntry() with
        {
            Level = "trace",
            NtEventType = "method_exit",
            NtOutcome = "success",
            DurationMs = 42,
            NtReturnValue = "\"OK\"",
        };

        var json = CanonicalEntrySerializer.ToJson(entry);

        SchemaValidator.AssertValid(Schema, json);
    }

    [Fact]
    public void Exit_entry_with_exception_validates()
    {
        var entry = EnterEntry() with
        {
            Level = "error",
            NtEventType = "method_exit",
            NtOutcome = "failure",
            DurationMs = 7,
            ExceptionType = "InvalidOperationException",
            ExceptionMessage = "card declined",
        };

        var json = CanonicalEntrySerializer.ToJson(entry);

        SchemaValidator.AssertValid(Schema, json);
    }

    [Fact]
    public void Enter_entry_with_parameters_validates()
    {
        var entry = EnterEntry() with
        {
            NtParameters =
            [
                new ParameterCapture("customerId", "C1", false),
                new ParameterCapture("card", "[REDACTED]", true),
            ],
        };

        var json = CanonicalEntrySerializer.ToJson(entry);

        SchemaValidator.AssertValid(Schema, json);
    }

    [Fact]
    public void Serializes_every_field_under_canonical_key()
    {
        var entry = EnterEntry() with
        {
            Environment = "prod",
            ParentSpanId = "00f067aa0ba902b7",
            NtTraceName = "bold elk soars",
            NtStoryId = "story-1",
            NtChapterId = "chap-1",
            NtReturnValue = "\"OK\"",
        };

        var r = JsonDocument.Parse(
            CanonicalEntrySerializer.ToJson(entry)).RootElement;

        Assert.Equal(
            "2026-07-05T12:00:00.000Z",
            r.GetProperty("timestamp").GetString());
        Assert.Equal("trace", r.GetProperty("level").GetString());
        Assert.Equal(
            "→ OrderService.PlaceOrder()",
            r.GetProperty("message").GetString());
        Assert.Equal("OrderSvc", r.GetProperty("service").GetString());
        Assert.Equal("prod", r.GetProperty("environment").GetString());
        Assert.Equal(
            "0af7651916cd43dd8448eb211c80319c",
            r.GetProperty("trace_id").GetString());
        Assert.Equal(
            "b7ad6b7169203331", r.GetProperty("span_id").GetString());
        Assert.Equal(
            "00f067aa0ba902b7",
            r.GetProperty("parent_span_id").GetString());
        Assert.Equal(
            "OrderService", r.GetProperty("code.namespace").GetString());
        Assert.Equal(
            "PlaceOrder", r.GetProperty("code.function").GetString());
        Assert.Equal("entry", r.GetProperty("nt.entryType").GetString());
        Assert.Equal(
            "method_enter", r.GetProperty("nt.eventType").GetString());
        Assert.Equal("1.2", r.GetProperty("nt.schemaVersion").GetString());
        Assert.Equal(
            "bold elk soars", r.GetProperty("nt.traceName").GetString());
        Assert.Equal("story-1", r.GetProperty("nt.storyId").GetString());
        Assert.Equal("chap-1", r.GetProperty("nt.chapterId").GetString());
        Assert.Equal(
            "\"OK\"", r.GetProperty("nt.returnValue").GetString());
    }

    [Fact]
    public void Serializes_the_1_1_and_1_2_identity_fields()
    {
        var entry = EnterEntry() with
        {
            NtNarrationTemplate = "Opening for {customerId}",
            NtPackage = "Acme.Billing",
            NtReturnType = "System.String",
            NtExceptionPackage = "System",
            NtInstanceId = "1f4a2b",
            CodeFilepath = "/src/OrderService.cs",
            CodeLineno = 42,
            ThreadName = "worker-1",
            ThreadId = 17,
            NtThreadVirtual = false,
            HostName = "build-agent",
            ProcessPid = 4242,
            ProcessRuntimeVersion = ".NET 10.0.0",
        };

        var r = JsonDocument.Parse(
            CanonicalEntrySerializer.ToJson(entry)).RootElement;

        Assert.Equal(
            "Opening for {customerId}",
            r.GetProperty("nt.narrationTemplate").GetString());
        Assert.Equal("Acme.Billing", r.GetProperty("nt.package").GetString());
        Assert.Equal("System.String", r.GetProperty("nt.returnType").GetString());
        Assert.Equal("System", r.GetProperty("nt.exceptionPackage").GetString());
        Assert.Equal("1f4a2b", r.GetProperty("nt.instanceId").GetString());
        Assert.Equal("/src/OrderService.cs", r.GetProperty("code.filepath").GetString());
        Assert.Equal(42, r.GetProperty("code.lineno").GetInt32());
        Assert.Equal("worker-1", r.GetProperty("thread.name").GetString());
        Assert.Equal(17, r.GetProperty("thread.id").GetInt64());
        Assert.False(r.GetProperty("nt.threadVirtual").GetBoolean());
        Assert.Equal("build-agent", r.GetProperty("host.name").GetString());
        Assert.Equal(4242, r.GetProperty("process.pid").GetInt32());
        Assert.Equal(".NET 10.0.0", r.GetProperty("process.runtime.version").GetString());
        SchemaValidator.AssertValid(Schema, CanonicalEntrySerializer.ToJson(entry));
    }

    [Fact]
    public void An_entry_supplying_no_identity_fields_omits_every_one_of_them()
    {
        var r = JsonDocument.Parse(
            CanonicalEntrySerializer.ToJson(EnterEntry())).RootElement;

        foreach (var key in new[]
        {
            "nt.narrationTemplate", "nt.package", "nt.returnType",
            "nt.exceptionPackage", "nt.instanceId", "code.filepath",
            "code.lineno", "thread.name", "thread.id", "nt.threadVirtual",
            "host.name", "process.pid", "process.runtime.version",
        })
        {
            Assert.False(r.TryGetProperty(key, out _), key);
        }
    }

    [Fact]
    public void A_parameter_carries_its_declared_type_when_one_was_captured()
    {
        var entry = EnterEntry() with
        {
            NtParameters =
            [
                new ParameterCapture("customerId", "\"C1\"", false, null, "System.String"),
                new ParameterCapture("quantity", "2", false),
            ],
        };

        var parameters = JsonDocument.Parse(CanonicalEntrySerializer.ToJson(entry))
            .RootElement.GetProperty("nt.parameters");

        Assert.Equal("System.String", parameters[0].GetProperty("type").GetString());
        Assert.False(parameters[1].TryGetProperty("type", out _));
        SchemaValidator.AssertValid(Schema, CanonicalEntrySerializer.ToJson(entry));
    }

    [Fact]
    public void Serializes_exit_numeric_and_exception_fields()
    {
        var entry = EnterEntry() with
        {
            NtEventType = "method_exit",
            NtOutcome = "failure",
            DurationMs = 42,
            NtBranchIndex = 3,
            ExceptionType = "InvalidOperationException",
            ExceptionMessage = "declined",
        };

        var r = JsonDocument.Parse(
            CanonicalEntrySerializer.ToJson(entry)).RootElement;

        Assert.Equal("failure", r.GetProperty("nt.outcome").GetString());
        Assert.Equal(42, r.GetProperty("durationMs").GetInt64());
        Assert.Equal(3, r.GetProperty("nt.branchIndex").GetInt32());
        Assert.Equal(
            "InvalidOperationException",
            r.GetProperty("exception.type").GetString());
        Assert.Equal(
            "declined", r.GetProperty("exception.message").GetString());
    }

    [Fact]
    public void Serializes_parameters_with_name_value_redacted()
    {
        var entry = EnterEntry() with
        {
            NtParameters =
            [
                new ParameterCapture("card", "[REDACTED]", true),
            ],
        };

        var p = JsonDocument.Parse(
                CanonicalEntrySerializer.ToJson(entry))
            .RootElement.GetProperty("nt.parameters")[0];

        Assert.Equal("card", p.GetProperty("name").GetString());
        Assert.Equal("[REDACTED]", p.GetProperty("value").GetString());
        Assert.True(p.GetProperty("redacted").GetBoolean());
    }

    [Fact]
    public void Null_service_fails_loudly_instead_of_serializing_as_a_blank()
    {
        // service is schema-required; an empty string passes {"type": "string"} while
        // carrying no information. The mapper supplies unknown_service:dotnet, so a null
        // reaching the serializer is an upstream defect and must not be papered over.
        var entry = EnterEntry() with { Service = null };

        Assert.Throws<InvalidOperationException>(
            () => CanonicalEntrySerializer.ToJson(entry));
    }

    [Fact]
    public void Null_parameter_value_serializes_as_empty_string()
    {
        var entry = EnterEntry() with
        {
            NtParameters = [new ParameterCapture("x", null!, false)],
        };

        var p = JsonDocument.Parse(
                CanonicalEntrySerializer.ToJson(entry))
            .RootElement.GetProperty("nt.parameters")[0];

        Assert.Equal("", p.GetProperty("value").GetString());
    }

    [Fact]
    public void Fork_id_serializes_under_nt_fork_id_key()
    {
        var entry = EnterEntry() with { NtForkId = "fork-1" };

        var r = JsonDocument.Parse(
            CanonicalEntrySerializer.ToJson(entry)).RootElement;

        Assert.Equal("fork-1", r.GetProperty("nt.forkId").GetString());
    }

    [Fact]
    public void Null_optional_fields_are_omitted()
    {
        var json = CanonicalEntrySerializer.ToJson(EnterEntry());

        Assert.DoesNotContain("parent_span_id", json);
        Assert.DoesNotContain("nt.returnValue", json);
        Assert.DoesNotContain("nt.parameters", json);
        Assert.DoesNotContain("environment", json);
    }
}
