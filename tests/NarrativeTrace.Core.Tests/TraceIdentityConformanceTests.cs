// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using NarrativeTrace.Runtime;

using System.Text.Json;

using Xunit;

namespace NarrativeTrace.Core.Tests;

/// <summary>
/// The identity gate, driven by the real writers: chapters exported by
/// <see cref="ChapterExporter"/> and entry arrays written to disk by
/// <see cref="TraceArtifactWriter"/> are read back as bytes and validated
/// against the schemas they claim to satisfy.
/// </summary>
/// <remarks>
/// Both halves of the tree matter and each used to be tested only in the half
/// that could not fail: a captured trace always carried identity, and the
/// span-less tree — the plain unit-test capture and the shape the conformance
/// fixtures are built from — was schema-validated as entries but never as a
/// chapter, which is exactly where the required identity fields were omitted.
/// </remarks>
public sealed class TraceIdentityConformanceTests : IDisposable
{
    private static readonly ChapterExporter Exporter =
        new(() => new DateTimeOffset(2026, 8, 30, 12, 0, 0, TimeSpan.Zero));

    private static readonly TraceArtifactRenderers Renderers = new(
        DiagramStub,
        DiagramStub,
        (tree, metadata) => JsonExporter.Export(tree, metadata),
        CanonicalEntryArrayExporter.Canonical,
        CanonicalEntryArrayExporter.Structural);

    private readonly string _dir = Path.Combine(
        Path.GetTempPath(), "nt-identity-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void A_captured_traces_chapter_validates()
    {
        var json = Chapter(Captured());

        SchemaValidator.AssertValid("chapter.schema.json", json);
    }

    [Fact]
    public void A_span_less_trees_chapter_validates()
    {
        var json = Chapter(SpanLessTree());

        SchemaValidator.AssertValid("chapter.schema.json", json);
    }

    [Fact]
    public void An_empty_trees_chapter_validates()
    {
        var json = Chapter(new TraceTree([]));

        SchemaValidator.AssertValid("chapter.schema.json", json);
    }

    [Fact]
    public void Every_written_entry_of_a_span_less_capture_validates()
    {
        foreach (var entry in Written(SpanLessTree()))
        {
            SchemaValidator.AssertValid("entry.schema.json", entry.GetRawText());
        }
    }

    [Fact]
    public void Every_written_entry_of_a_real_capture_validates()
    {
        foreach (var entry in Written(Captured()))
        {
            SchemaValidator.AssertValid("entry.schema.json", entry.GetRawText());
        }
    }

    [Fact]
    public void The_chapter_and_its_own_canonical_entries_name_the_same_trace()
    {
        // Identity is resolved by the tree, not by an exporter, so two
        // exporters of one tree cannot name two different traces. This would
        // fail the moment generation moved back into either of them.
        var tree = SpanLessTree();

        var traceId = JsonDocument.Parse(Chapter(tree))
            .RootElement.GetProperty("trace_id").GetString();

        Assert.NotNull(traceId);
        Assert.NotEmpty(Written(tree));
        Assert.All(
            Written(tree),
            entry => Assert.Equal(traceId, entry.GetProperty("trace_id").GetString()));
    }

    [Fact]
    public void A_copy_that_gained_its_nodes_still_names_one_trace_everywhere()
    {
        // A record copy does not re-run the constructor, so a tree grown from
        // an empty one by `with` was the one shape that reached the exporters
        // unidentified — and each of them would then mint its own id.
        var grown = new TraceTree([]) with { Roots = SpanLessTree().Roots };

        var chapter = Chapter(grown);
        var traceId = JsonDocument.Parse(chapter)
            .RootElement.GetProperty("trace_id").GetString();

        SchemaValidator.AssertValid("chapter.schema.json", chapter);
        Assert.Equal(grown.TraceId.Value, traceId);
        Assert.All(
            Written(grown),
            entry => Assert.Equal(traceId, entry.GetProperty("trace_id").GetString()));
    }

    [Fact]
    public void A_captured_chapter_and_its_entries_name_the_captured_trace()
    {
        var tree = Captured();

        var traceId = JsonDocument.Parse(Chapter(tree))
            .RootElement.GetProperty("trace_id").GetString();

        Assert.Equal(tree.Roots[0].SpanContext!.TraceId.Value, traceId);
        Assert.All(
            Written(tree),
            entry => Assert.Equal(traceId, entry.GetProperty("trace_id").GetString()));
    }

    [Fact]
    public void The_chapter_and_its_embedded_chapter_tree_name_the_same_trace()
    {
        var chapter = JsonDocument.Parse(Chapter(Captured())).RootElement;
        var embedded = EmbeddedTree(chapter);

        Assert.Equal(
            chapter.GetProperty("trace_id").GetString(),
            embedded.GetProperty("trace").GetProperty("traceId").GetString());
    }

    [Fact]
    public void The_chapter_and_its_embedded_chapter_tree_agree_for_a_span_less_tree()
    {
        // The third emitter (item 26c): it used to omit the whole trace block
        // when no *root* carried a span context — legal against
        // chapter-tree.schema.json, and still a chapter naming a trace while
        // the tree embedded inside it named none.
        var chapter = JsonDocument.Parse(Chapter(SpanLessTree())).RootElement;
        var embedded = EmbeddedTree(chapter);

        Assert.Equal(
            chapter.GetProperty("trace_id").GetString(),
            embedded.GetProperty("trace").GetProperty("traceId").GetString());
        Assert.Equal(
            chapter.GetProperty("nt.traceName").GetString(),
            embedded.GetProperty("trace").GetProperty("traceName").GetString());
    }

    [Fact]
    public void The_embedded_tree_finds_the_trace_on_a_deeper_node_when_no_root_carries_one()
    {
        // Roots-only scanning was the other half of the divergence: a mixed
        // tree whose context sits on a child resolved to "no trace at all"
        // here while the other two emitters already inherited depth-first.
        var chapter = JsonDocument.Parse(
            Chapter(TreeWithContextOnAChildOnly())).RootElement;
        var embeddedTrace = EmbeddedTree(chapter).GetProperty("trace");

        Assert.Equal(
            chapter.GetProperty("trace_id").GetString(),
            embeddedTrace.GetProperty("traceId").GetString());
        Assert.Equal(
            "order-service", embeddedTrace.GetProperty("serviceName").GetString());
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    private static JsonElement EmbeddedTree(JsonElement chapter)
    {
        return JsonDocument.Parse(
            chapter.GetProperty("nt.chapterTree").GetString()!).RootElement;
    }

    /// <summary>A mixed tree: the root lost its context, a descendant kept one.</summary>
    private static TraceTree TreeWithContextOnAChildOnly()
    {
        var sc = new SpanContext(
            new TraceId("0af7651916cd43dd8448eb211c80319c"),
            new SpanId("b7ad6b7169203331"),
            null,
            ServiceName: "order-service");
        var child = new TraceNode(
            new MethodSignature("PaymentService", "Charge", []),
            new Returned("\"TXN-1\""),
            [],
            2 * TimeSpan.TicksPerMillisecond,
            SpanContext: sc);
        return new TraceTree([
            new TraceNode(
                new MethodSignature("OrderService", "PlaceOrder", []),
                new Returned("\"order-1\""),
                [child],
                4 * TimeSpan.TicksPerMillisecond),
        ]);
    }

    private static string DiagramStub(TraceTree tree) => "DIAGRAM";

    private static string Chapter(TraceTree tree)
    {
        return Exporter.ExportChapter(
            tree, new TraceMetadata("order", ScenarioResult.Success));
    }

    private static TraceTree Captured()
    {
        var context = new SyncNarrativeContext(
            new NarrativeTraceConfig(
                TracingLevel.Detail, new ServiceIdentity("OrderSvc", "1.0", "test")));
        context.EnterMethod("OrderService", "PlaceOrder", []);
        context.EnterMethod("PaymentService", "Charge", []);
        context.ExitMethodWithReturn("\"OK\"");
        context.ExitMethodWithReturn("\"ORD-1\"");
        return context.CaptureTrace();
    }

    private static TraceTree SpanLessTree()
    {
        var charge = new TraceNode(
            new MethodSignature("PaymentService", "Charge", []),
            new Returned("\"OK\""),
            [],
            0);
        return new TraceTree([
            new TraceNode(
                new MethodSignature(
                    "OrderService",
                    "PlaceOrder",
                    [new ParameterCapture("customerId", "\"C1\"", false)]),
                new Returned("\"ORD-1\""),
                [charge],
                TimeSpan.TicksPerMillisecond * 3),
        ]);
    }

    /// <summary>
    /// Writes the tree's canonical entry array through the real writer and
    /// reads the bytes back as detached elements — cloned, because a
    /// <see cref="JsonElement"/> dies with the document it was parsed from.
    /// </summary>
    private IReadOnlyList<JsonElement> Written(TraceTree tree)
    {
        TraceArtifactWriter.Write(
            tree, "Foo.OrderTests", "PlacesOrder", "places order",
            failed: false, _dir, TraceArtifactFormat.Markdown, Renderers,
            TextWriter.Null, new EntryArtifacts(Canonical: true, Structural: false));
        using var document = JsonDocument.Parse(
            File.ReadAllText(
                Path.Combine(_dir, "traces", "OrderTests", "places_order.canonical.json")));
        return document.RootElement.EnumerateArray()
            .Select(element => element.Clone())
            .ToList();
    }
}
