// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using NarrativeTrace.Runtime;
using Xunit;

namespace NarrativeTrace.Core.Tests;

public class TraceEventTests
{
    [Fact]
    public void TraceEvent_hierarchy_compiles_with_three_variants()
    {
        var sig = new MethodSignature("Svc", "Run", []);
        var traceId = new TraceId("4bf92f3577b34da6a3ce929d0e0e4736");
        var spanId = SpanIdGenerator.GenerateSpanId();
        var context = new SpanContext(traceId, spanId, null);

        var enter = new EnterEvent(context, 1000L, sig);
        var exit = new ExitEvent(context, 2000L, new Returned("42"));
        var graft = new GraftEvent(
            null, 3000L,
            new TraceNode(sig, new Returned(null), [], 0));

        Assert.Equal(spanId, enter.SpanContext.SpanId);
        Assert.Null(enter.SpanContext.ParentSpanId);
        Assert.Equal(1000L, enter.TimestampTicks);
        Assert.Same(sig, enter.Signature);

        Assert.Equal(spanId, exit.SpanContext.SpanId);
        Assert.Equal(2000L, exit.TimestampTicks);
        Assert.IsType<Returned>(exit.Outcome);

        Assert.Null(graft.ParentSpanId);
        Assert.Equal(3000L, graft.TimestampTicks);
        Assert.NotNull(graft.Node);
    }

    [Fact]
    public void Markdown_renders_Incomplete_outcome()
    {
        var node = new TraceNode(
            new MethodSignature("Svc", "Run", []),
            new Incomplete(), [], 0);
        var tree = new TraceTree([node]);

        var result = MarkdownRenderer.Render(tree);

        Assert.Contains("incomplete", result,
            StringComparison.OrdinalIgnoreCase);
    }


    [Fact]
    public void Json_renders_Incomplete_outcome()
    {
        var node = new TraceNode(
            new MethodSignature("Svc", "Run", []),
            new Incomplete(), [], 0);
        var tree = new TraceTree([node]);

        var result = JsonExporter.Export(
            tree, new TraceMetadata("test", ScenarioResult.Success));

        Assert.Contains("incomplete", result,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Prose_renders_Incomplete_outcome()
    {
        var node = new TraceNode(
            new MethodSignature("Svc", "Run", []),
            new Incomplete(), [], 0);
        var tree = new TraceTree([node]);

        var result = ProseRenderer.Render(tree);

        Assert.Contains("incomplete", result,
            StringComparison.OrdinalIgnoreCase);
    }

}
