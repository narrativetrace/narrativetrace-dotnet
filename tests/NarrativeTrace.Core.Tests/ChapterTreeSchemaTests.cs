// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using NarrativeTrace.Runtime;
using Xunit;

namespace NarrativeTrace.Core.Tests;

/// <summary>
/// Validates <see cref="JsonExporter"/> output against
/// <c>chapter-tree.schema.json</c>, the cross-language contract for the
/// nested trace tree embedded in a chapter.
/// </summary>
public class ChapterTreeSchemaTests
{
    private const string Schema = "chapter-tree.schema.json";

    [Fact]
    public void Captured_single_method_trace_validates()
    {
        var ctx = new SyncNarrativeContext(new NarrativeTraceConfig());
        ctx.EnterMethod(
            "OrderService", "PlaceOrder",
            [new ParameterCapture("customerId", "C1", false)]);
        ctx.ExitMethodWithReturn("\"OK\"");

        var json = JsonExporter.Export(
            ctx.CaptureTrace(), new TraceMetadata("order", ScenarioResult.Success));

        SchemaValidator.AssertValid(Schema, json);
    }

    [Fact]
    public void Trace_with_thrown_exception_validates()
    {
        var ctx = new SyncNarrativeContext(new NarrativeTraceConfig());
        ctx.EnterMethod("PaymentService", "Charge", []);
        ctx.ExitMethodWithException(
            new InvalidOperationException("card declined"));

        var json = JsonExporter.Export(
            ctx.CaptureTrace(), new TraceMetadata("payment", ScenarioResult.Success));

        SchemaValidator.AssertValid(Schema, json);
    }

    [Fact]
    public void Nested_captured_trace_validates()
    {
        var ctx = new SyncNarrativeContext(new NarrativeTraceConfig());
        ctx.EnterMethod("OrderService", "PlaceOrder", []);
        ctx.EnterMethod("InventoryRepo", "Reserve", []);
        ctx.ExitMethodWithReturn("true");
        ctx.ExitMethodWithReturn("\"OK\"");

        var json = JsonExporter.Export(
            ctx.CaptureTrace(), new TraceMetadata("order", ScenarioResult.Success));

        SchemaValidator.AssertValid(Schema, json);
    }

    [Fact]
    public void Manual_tree_with_concurrency_validates()
    {
        var info = new ConcurrencyInfo(
            "fork-1", "Svc.Run", 4, ".NET TP Worker", true,
            ConcurrencyKind.ForkJoin);
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Svc", "Run", []),
                new Returned(null), [], 0, Concurrency: info),
        ]);

        var json = JsonExporter.Export(tree, new TraceMetadata("s", ScenarioResult.Success));

        SchemaValidator.AssertValid(Schema, json);
    }
}
