// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Text.Json;
using NarrativeTrace.Core;
using NarrativeTrace.Runtime;
using Xunit;

namespace NarrativeTrace.Core.Tests;

/// <summary>
/// Validates <see cref="ChapterExporter"/> output against
/// <c>chapter.schema.json</c> and cross-checks the embedded chapter tree.
/// </summary>
public class ChapterExporterTests
{
    private static readonly DateTimeOffset FixedNow =
        new(2026, 7, 5, 12, 0, 0, TimeSpan.Zero);

    private static readonly ChapterExporter Exporter =
        new(() => FixedNow);

    private static TraceTree Capture(Action<SyncNarrativeContext> body)
    {
        var identity = new ServiceIdentity("OrderSvc", "1.0", "test");
        var ctx = new SyncNarrativeContext(
            new NarrativeTraceConfig(
                TracingLevel.Detail, identity));
        body(ctx);
        return ctx.CaptureTrace();
    }

    private static TraceTree ManualTree(
        long durationTicks, TraceOutcome outcome)
    {
        var sc = new SpanContext(
            new TraceId("0af7651916cd43dd8448eb211c80319c"),
            new SpanId("b7ad6b7169203331"),
            null,
            ServiceName: "OrderSvc")
        {
            StoryId = "story-1",
            ChapterId = "chap-1",
        };
        return new TraceTree([
            new TraceNode(
                new MethodSignature("OrderService", "PlaceOrder", []),
                outcome, [], durationTicks, SpanContext: sc),
        ]);
    }

    [Fact]
    public void Chapter_header_fields_are_exact_for_success()
    {
        var tree = ManualTree(
            5 * TimeSpan.TicksPerMillisecond, new Returned(null));

        var r = JsonDocument.Parse(
            Exporter.ExportChapter(tree, new TraceMetadata("o", ScenarioResult.Success)))
            .RootElement;

        Assert.Equal(
            "2026-07-05T12:00:00.000Z",
            r.GetProperty("timestamp").GetString());
        Assert.Equal("info", r.GetProperty("level").GetString());
        Assert.Equal(
            "Chapter complete: OrderService.PlaceOrder [5ms] success",
            r.GetProperty("message").GetString());
        Assert.Equal(
            "chapter", r.GetProperty("nt.entryType").GetString());
        Assert.Equal(
            "success", r.GetProperty("nt.outcome").GetString());
        Assert.Equal(
            "complete", r.GetProperty("nt.completionStatus").GetString());
        Assert.Equal(
            "1.2", r.GetProperty("nt.schemaVersion").GetString());
        Assert.Equal("OrderSvc", r.GetProperty("service").GetString());
        Assert.Equal(
            "0af7651916cd43dd8448eb211c80319c",
            r.GetProperty("trace_id").GetString());
        Assert.Equal("story-1", r.GetProperty("nt.storyId").GetString());
        Assert.Equal("chap-1", r.GetProperty("nt.chapterId").GetString());
        Assert.Equal(
            5, r.GetProperty("nt.totalDurationMs").GetInt64());
        Assert.Equal(
            new TraceId("0af7651916cd43dd8448eb211c80319c").HumanName,
            r.GetProperty("nt.traceName").GetString());
    }

    [Fact]
    public void Captured_trace_chapter_validates()
    {
        var tree = Capture(ctx =>
        {
            ctx.EnterMethod("OrderService", "PlaceOrder", []);
            ctx.ExitMethodWithReturn("\"OK\"");
        });

        var json = Exporter.ExportChapter(tree, new TraceMetadata("order", ScenarioResult.Success));

        SchemaValidator.AssertValid("chapter.schema.json", json);
    }

    [Fact]
    public void Embedded_chapter_tree_is_valid()
    {
        var tree = Capture(ctx =>
        {
            ctx.EnterMethod("OrderService", "PlaceOrder", []);
            ctx.ExitMethodWithReturn("\"OK\"");
        });

        var json = Exporter.ExportChapter(tree, new TraceMetadata("order", ScenarioResult.Success));
        var chapterTree = JsonDocument.Parse(json)
            .RootElement.GetProperty("nt.chapterTree").GetString();

        SchemaValidator.AssertValid("chapter-tree.schema.json", chapterTree!);
    }

    [Fact]
    public void Failed_trace_has_error_level_and_failure_outcome()
    {
        var tree = Capture(ctx =>
        {
            ctx.EnterMethod("PaymentService", "Charge", []);
            ctx.ExitMethodWithException(new InvalidOperationException("no"));
        });

        var doc = JsonDocument.Parse(
            Exporter.ExportChapter(tree, new TraceMetadata("pay", ScenarioResult.Error)));

        Assert.Equal(
            "error", doc.RootElement.GetProperty("level").GetString());
        Assert.Equal(
            "failure",
            doc.RootElement.GetProperty("nt.outcome").GetString());
    }

    [Fact]
    public void Title_derived_from_root_method()
    {
        var tree = Capture(ctx =>
        {
            ctx.EnterMethod("OrderService", "PlaceOrder", []);
            ctx.ExitMethodWithReturn(null);
        });

        var doc = JsonDocument.Parse(
            Exporter.ExportChapter(tree, new TraceMetadata("o", ScenarioResult.Success)));

        Assert.Equal(
            "OrderService.PlaceOrder",
            doc.RootElement.GetProperty("nt.title").GetString());
    }

    [Fact]
    public void Entry_count_counts_all_nested_nodes()
    {
        var tree = Capture(ctx =>
        {
            ctx.EnterMethod("A", "Run", []);
            ctx.EnterMethod("B", "Do", []);
            ctx.ExitMethodWithReturn(null);
            ctx.EnterMethod("C", "Go", []);
            ctx.ExitMethodWithReturn(null);
            ctx.ExitMethodWithReturn(null);
        });

        var doc = JsonDocument.Parse(
            Exporter.ExportChapter(tree, new TraceMetadata("o", ScenarioResult.Success)));

        Assert.Equal(
            3, doc.RootElement.GetProperty("nt.entryCount").GetInt32());
    }

    [Fact]
    public void Incomplete_root_maps_to_partial_outcome()
    {
        var tree = ManualTree(0, new Incomplete());

        var doc = JsonDocument.Parse(
            Exporter.ExportChapter(tree, new TraceMetadata("o", ScenarioResult.Success)));

        Assert.Equal(
            "partial",
            doc.RootElement.GetProperty("nt.outcome").GetString());
        Assert.Equal(
            "info", doc.RootElement.GetProperty("level").GetString());
    }

    [Fact]
    public void Trace_id_taken_from_a_span_context_anywhere_in_the_tree()
    {
        // Was roots-only ("first root with a span context"): a trace whose only
        // context sat on a child then exported as context-free. Inheritance is
        // depth-first over the whole forest as of 2026-08-30, and it answers for
        // the service too, so a mixed tree reports one service rather than the
        // unknown fallback its context-free first root would give.
        var sc = new SpanContext(
            new TraceId("0af7651916cd43dd8448eb211c80319c"),
            new SpanId("b7ad6b7169203331"),
            null,
            ServiceName: "OrderSvc")
        {
            StoryId = "s",
            ChapterId = "c",
        };
        var nested = new TraceNode(
            new MethodSignature("Third", "Run", []),
            new Returned(null), [], 0, SpanContext: sc);
        var rootWithoutContext = new TraceNode(
            new MethodSignature("First", "Run", []),
            new Returned(null), [nested], 0);
        var tree = new TraceTree([
            rootWithoutContext,
            new TraceNode(
                new MethodSignature("Second", "Run", []),
                new Returned(null), [], 0),
        ]);

        var r = JsonDocument.Parse(
            Exporter.ExportChapter(tree, new TraceMetadata("o", ScenarioResult.Success)))
            .RootElement;

        Assert.Equal(
            "0af7651916cd43dd8448eb211c80319c",
            r.GetProperty("trace_id").GetString());
        Assert.Equal("OrderSvc", r.GetProperty("service").GetString());
        Assert.Equal("s", r.GetProperty("nt.storyId").GetString());
        Assert.Equal("c", r.GetProperty("nt.chapterId").GetString());
    }

    [Fact]
    public void Empty_tree_produces_unknown_title()
    {
        var tree = new TraceTree([]);

        var json = Exporter.ExportChapter(
            tree, new TraceMetadata("o", ScenarioResult.Success));
        var r = JsonDocument.Parse(json).RootElement;

        Assert.Equal("unknown", r.GetProperty("nt.title").GetString());
        // No root call to derive a story from, so story and chapter take the
        // same fallback the title uses; the trace id still comes from the
        // generate rung, so the chapter validates against its own schema.
        Assert.Equal("unknown", r.GetProperty("nt.storyId").GetString());
        Assert.Equal("unknown", r.GetProperty("nt.chapterId").GetString());
        Assert.Matches("^[0-9a-f]{32}$", r.GetProperty("trace_id").GetString());
        Assert.Equal(
            new TraceId(r.GetProperty("trace_id").GetString()!).HumanName,
            r.GetProperty("nt.traceName").GetString());
        SchemaValidator.AssertValid("chapter.schema.json", json);
    }

    [Fact]
    public void An_empty_tree_keeps_no_identity_of_its_own()
    {
        // Nothing ran, so there is nothing to identify: the tree stays
        // unidentified and the chapter mints an id at resolution time. Nothing
        // else in that tree can disagree — an empty tree has no entries.
        var tree = new TraceTree([]);

        Assert.True(tree.TraceId.IsEmpty);
    }

    [Fact]
    public void A_context_free_chapter_carries_a_generated_identity_and_validates()
    {
        // The known gap: chapter.schema.json requires trace_id, nt.storyId and
        // nt.chapterId, and a span-less tree used to omit all three.
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("OrderService", "PlaceOrder", []),
                new Returned(null), [], TimeSpan.TicksPerMillisecond),
        ]);

        var json = Exporter.ExportChapter(
            tree, new TraceMetadata("o", ScenarioResult.Success));
        var r = JsonDocument.Parse(json).RootElement;

        Assert.Equal(tree.TraceId.Value, r.GetProperty("trace_id").GetString());
        Assert.Equal(
            "OrderService.PlaceOrder", r.GetProperty("nt.storyId").GetString());
        Assert.Equal(
            "OrderService.PlaceOrder", r.GetProperty("nt.chapterId").GetString());
        Assert.Equal(
            tree.TraceId.HumanName, r.GetProperty("nt.traceName").GetString());
        SchemaValidator.AssertValid("chapter.schema.json", json);
    }

    [Fact]
    public void Two_context_free_chapters_never_name_the_same_trace()
    {
        var first = Chapter(ContextFreeTree());
        var second = Chapter(ContextFreeTree());

        Assert.NotEqual(
            first.GetProperty("trace_id").GetString(),
            second.GetProperty("trace_id").GetString());
        Assert.Equal(
            first.GetProperty("nt.storyId").GetString(),
            second.GetProperty("nt.storyId").GetString());
        Assert.Equal(
            first.GetProperty("nt.chapterId").GetString(),
            second.GetProperty("nt.chapterId").GetString());
    }

    [Fact]
    public void One_tree_names_one_trace_however_often_it_is_exported()
    {
        var tree = ContextFreeTree();

        Assert.Equal(
            Chapter(tree).GetProperty("trace_id").GetString(),
            Chapter(tree).GetProperty("trace_id").GetString());
    }

    private static TraceTree ContextFreeTree()
    {
        return new TraceTree([
            new TraceNode(
                new MethodSignature("OrderService", "PlaceOrder", []),
                new Returned(null), [], TimeSpan.TicksPerMillisecond),
        ]);
    }

    private static JsonElement Chapter(TraceTree tree)
    {
        return JsonDocument.Parse(
            Exporter.ExportChapter(
                tree, new TraceMetadata("o", ScenarioResult.Success)))
            .RootElement.Clone();
    }

    [Fact]
    public void Chapter_without_a_service_name_carries_the_fallback()
    {
        // chapter.schema.json lists service as required; it used to be omitted entirely
        // when the host pinned no name, producing a chapter that fails its own schema.
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("OrderService", "PlaceOrder", []),
                new Returned(null), [], TimeSpan.TicksPerMillisecond),
        ]);

        var r = JsonDocument.Parse(
            Exporter.ExportChapter(tree, new TraceMetadata("o", ScenarioResult.Success)))
            .RootElement;

        Assert.Equal("unknown_service:dotnet", r.GetProperty("service").GetString());
    }

    // TraceNode.Children is a type, not a guarantee of acyclicity - a hand-built or replayed tree
    // can hold a genuine reference cycle. ExportChapter must terminate rather than recurse the
    // call stack forever — the same unbounded-recursion defect fixed in the java flagship.
    [Fact]
    public void ExportChapter_does_not_hang_on_a_cyclic_tree()
    {
        var aChildren = new List<TraceNode>();
        var bChildren = new List<TraceNode>();
        var a = new TraceNode(
            new MethodSignature("Svc", "A", []), new Returned(null), aChildren.AsReadOnly(), 0);
        var b = new TraceNode(
            new MethodSignature("Svc", "B", []), new Returned(null), bChildren.AsReadOnly(), 0);
        aChildren.Add(b);
        bChildren.Add(a);

        var result = Exporter.ExportChapter(
            new TraceTree([a]), new TraceMetadata("o", ScenarioResult.Success));

        var entryCount = JsonDocument.Parse(result).RootElement.GetProperty("nt.entryCount").GetInt32();
        Assert.True(entryCount > 0);
    }
}
