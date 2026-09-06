// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using Xunit;

namespace NarrativeTrace.Core.Tests;

public class ChildSegmentTests
{
    [Fact]
    public void PartitionChildren_groups_concurrent_siblings()
    {
        var info = new ConcurrencyInfo(
            "fork-1", "A.Do", 1, null, true,
            ConcurrencyKind.ForkJoin);
        var children = new[]
        {
            MakeNode("A", "Do", info),
            MakeNode("B", "Do", info),
        };

        var segments =
            ChildSegment.Partition(children);

        Assert.Single(segments);
        Assert.Equal("fork-1", segments[0].GroupId);
        Assert.Equal(2, segments[0].Nodes.Count);
    }

    [Fact]
    public void PartitionChildren_keeps_sequential_nodes_as_is()
    {
        var children = new[]
        {
            MakeNode("A", "Do"),
            MakeNode("B", "Do"),
        };

        var segments =
            ChildSegment.Partition(children);

        Assert.Equal(2, segments.Count);
        Assert.All(segments,
            s => Assert.False(s.IsConcurrent));
    }

    [Fact]
    public void PartitionChildren_handles_mixed_sequential_and_concurrent()
    {
        var info = new ConcurrencyInfo(
            "fork-1", "B.Do", 1, null, true,
            ConcurrencyKind.ForkJoin);
        var children = new[]
        {
            MakeNode("A", "Do"),
            MakeNode("B", "Do", info),
            MakeNode("C", "Do", info),
            MakeNode("D", "Do"),
        };

        var segments =
            ChildSegment.Partition(children);

        Assert.Equal(3, segments.Count);
        Assert.False(segments[0].IsConcurrent);
        Assert.True(segments[1].IsConcurrent);
        Assert.Equal(2, segments[1].Nodes.Count);
        Assert.False(segments[2].IsConcurrent);
    }

    [Fact]
    public void IsFireAndForget_flag_on_ChildSegment()
    {
        var info = new ConcurrencyInfo(
            "fanf-1", "Bg.Do", 1, null, true,
            ConcurrencyKind.FireAndForget);
        var children = new[]
        {
            MakeNode("Bg", "Do", info),
        };

        var segments =
            ChildSegment.Partition(children);

        Assert.Single(segments);
        Assert.Equal(
            ConcurrencyKind.FireAndForget,
            segments[0].Kind);
    }

    [Fact]
    public void All_renderers_handle_empty_fork_group()
    {
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("P", "Run", []),
                new Returned(null), [], 0),
        ]);

        var md = MarkdownRenderer.Render(tree);
        var text = IndentedTextRenderer.Render(tree);
        var prose = ProseRenderer.Render(tree);

        Assert.DoesNotContain("\u2442", md);
        Assert.DoesNotContain("\u2442", text);
        Assert.DoesNotContain("Concurrently", prose);
    }

    private static TraceNode MakeNode(
        string cls, string method,
        ConcurrencyInfo? info = null)
    {
        return new TraceNode(
            new MethodSignature(cls, method, []),
            new Returned(null), [], 100,
            Concurrency: info);
    }
}
