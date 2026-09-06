// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.AspNetCore;
using NarrativeTrace.Core;
using NarrativeTrace.Runtime;
using Xunit;

namespace NarrativeTrace.AspNetCore.Tests;

public class LoggerExporterScopeTests
{
    private static TraceTree TracedTree()
    {
        var context = new SyncNarrativeContext(new NarrativeTraceConfig());
        context.EnterMethod("OrderService", "PlaceOrder", []);
        context.ExitMethodWithReturn(null);
        return context.CaptureTrace();
    }

    [Fact]
    public void Export_scopes_canonical_schema_fields_from_the_root_span()
    {
        var logger = new ScopeCapturingLogger();
        var exporter = new LoggerTraceExporter(new SingleLoggerFactory(logger));
        var tree = TracedTree();
        var root = tree.Roots[0].SpanContext!;

        exporter.Export(tree, new RequestContext(200, 5));

        var scope = logger.LastScopeAsMap();
        Assert.Equal("chapter", scope["nt.entryType"]);
        Assert.Equal("1.2", scope["nt.schemaVersion"]);
        Assert.Equal(root.StoryId, scope["nt.storyId"]);
        Assert.Equal(root.ChapterId, scope["nt.chapterId"]);
        Assert.Equal(root.TraceId.Value, scope["trace_id"]);
        Assert.Equal(root.TraceId.HumanName, scope["nt.traceName"]);
    }
}
