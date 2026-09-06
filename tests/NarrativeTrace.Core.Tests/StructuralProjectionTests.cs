// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using FsCheck;
using FsCheck.Xunit;

using NarrativeTrace.Core;
using NarrativeTrace.Runtime;

using Xunit;

namespace NarrativeTrace.Core.Tests;

/// <summary>
/// The value-free projection (ADR-002 Level 1): what an AI consumer is allowed
/// to see, and — more importantly — what it can never see.
/// </summary>
public class StructuralProjectionTests
{
    /// <summary>Marks content that must never reach a structural artifact.</summary>
    private const string Sentinel = "NTLEAKSENTINEL";

    [Fact]
    public void An_enter_entry_keeps_its_shape_and_loses_its_argument_values()
    {
        var projected = StructuralProjection.Project(Enter());

        Assert.Equal("→ OrderService.PlaceOrder(customerId, card)", projected.Message);
        Assert.Equal(
            [StructuralProjection.Elided, StructuralProjection.Elided],
            projected.NtParameters!.Select(p => p.RenderedValue));
        Assert.Equal(
            ["customerId", "card"], projected.NtParameters!.Select(p => p.Name));
        Assert.True(projected.NtParameters![1].Redacted);
    }

    [Fact]
    public void A_returning_exit_loses_the_returned_value()
    {
        var projected = StructuralProjection.Project(Exit() with
        {
            NtReturnValue = "\"ORD-42 for alice@example.com\"",
        });

        Assert.Null(projected.NtReturnValue);
        Assert.Equal("← OrderService.PlaceOrder returned", projected.Message);
    }

    [Fact]
    public void A_thrown_exit_keeps_the_type_and_loses_the_message()
    {
        var projected = StructuralProjection.Project(Exit() with
        {
            NtOutcome = "failure",
            ExceptionType = "InvalidOperationException",
            ExceptionMessage = "card 4111-1111-1111-1111 declined",
        });

        Assert.Equal("InvalidOperationException", projected.ExceptionType);
        Assert.Null(projected.ExceptionMessage);
        Assert.Equal("!! InvalidOperationException", projected.Message);
    }

    [Fact]
    public void An_incomplete_exit_says_so()
    {
        var projected = StructuralProjection.Project(Exit() with
        {
            NtOutcome = "incomplete",
        });

        Assert.Equal("← OrderService.PlaceOrder incomplete", projected.Message);
    }

    [Fact]
    public void Identity_and_timing_flow_through_untouched()
    {
        var entry = Enter();

        var projected = StructuralProjection.Project(entry);

        Assert.Equal(entry.TraceId, projected.TraceId);
        Assert.Equal(entry.SpanId, projected.SpanId);
        Assert.Equal(entry.ParentSpanId, projected.ParentSpanId);
        Assert.Equal(entry.Timestamp, projected.Timestamp);
        Assert.Equal(entry.CodeNamespace, projected.CodeNamespace);
        Assert.Equal(entry.CodeFunction, projected.CodeFunction);
        Assert.Equal(entry.NtSchemaVersion, projected.NtSchemaVersion);
    }

    [Fact]
    public void A_lifecycle_entry_keeps_its_own_message()
    {
        var fork = Enter() with { NtEventType = "fork", Message = "fork [g-1]" };

        Assert.Equal("fork [g-1]", StructuralProjection.Project(fork).Message);
    }

    [Fact]
    public void An_entry_without_parameters_stays_without_parameters()
    {
        var projected = StructuralProjection.Project(
            Enter() with { NtParameters = null });

        Assert.Null(projected.NtParameters);
        Assert.Equal("→ OrderService.PlaceOrder()", projected.Message);
    }

    [Fact]
    public void A_null_entry_is_rejected()
    {
        Assert.Throws<ArgumentNullException>(
            () => StructuralProjection.Project(null!));
    }

    [Fact]
    public void The_projection_still_validates_against_the_entry_schema()
    {
        foreach (var entry in new[] { Enter(), Exit() })
        {
            SchemaValidator.AssertValid(
                "entry.schema.json",
                CanonicalEntrySerializer.ToJson(StructuralProjection.Project(entry)));
        }
    }

    /// <summary>
    /// The safety property, stated over hostile content rather than over one
    /// example: whatever a value field holds, the serialized structural entry
    /// must not contain it. A new value-shaped schema field that forgets to
    /// elide fails here rather than in production.
    /// </summary>
    /// <remarks>
    /// The payload carries a sentinel prefix, and the assertion hunts the
    /// sentinel: a bare random string would false-positive on any single
    /// character that legitimately appears in a name or a timestamp, and would
    /// false-negative on a value that leaked in JSON-escaped form. The sentinel
    /// is escape-free and cannot occur in the structural output by accident.
    /// </remarks>
    [Property]
    public bool No_runtime_value_survives_into_the_serialized_projection(
        NonEmptyString payload)
    {
        var hostile = Sentinel + payload.Get;
        var entry = Enter() with
        {
            NtParameters = [new ParameterCapture("customerId", hostile, false)],
            NtReturnValue = hostile,
            ExceptionType = "InvalidOperationException",
            ExceptionMessage = hostile,
            Message = hostile,
        };

        var json = CanonicalEntrySerializer.ToJson(StructuralProjection.Project(entry));

        return !json.Contains(Sentinel, StringComparison.Ordinal);
    }

    private static CanonicalEntry Enter() =>
        new(
            Timestamp: "2026-07-05T00:00:00.000Z",
            Level: "trace",
            Message: "→ OrderService.PlaceOrder(customerId: \"C1\", card: [REDACTED])",
            Service: "OrderSvc",
            Environment: "test",
            TraceId: "0af7651916cd43dd8448eb211c80319c",
            SpanId: "b7ad6b7169203331",
            ParentSpanId: null,
            CodeNamespace: "OrderService",
            CodeFunction: "PlaceOrder",
            NtEventType: "method_enter",
            NtParameters:
            [
                new ParameterCapture("customerId", "\"C1\"", false),
                new ParameterCapture("card", "[REDACTED]", true),
            ]);

    private static CanonicalEntry Exit() =>
        Enter() with
        {
            NtEventType = "method_exit",
            NtOutcome = "success",
            NtParameters = null,
            Message = "← OrderService.PlaceOrder",
            DurationMs = 7,
        };
}
