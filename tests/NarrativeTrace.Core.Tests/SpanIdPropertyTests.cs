// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Text.RegularExpressions;
using FsCheck.Xunit;
using NarrativeTrace.Core;
using Xunit;

namespace NarrativeTrace.Core.Tests;

/// <summary>
/// D8's invariant over arbitrary shapes: span ids are a bijection with tree positions — every
/// position the structural trace prints (a call, a fire-and-forget launch) has exactly one id, the
/// ids are prefix-closed and dense under each parent, nesting depth matches id depth — and every
/// other flavour prints exactly the same ids.
/// </summary>
public sealed partial class SpanIdPropertyTests
{
    private static readonly string[] Classes = ["Repo", "Audit", "Mail", "Pay"];
    private static readonly string[] Methods = ["Load", "Save", "Send"];

    [Property(MaxTest = 300)]
    public void Structural_ids_are_a_bijection_with_tree_positions(int seed)
    {
        var tree = RandomTree(new Random(seed));
        var lines = StructuralTraceRenderer.Render(tree).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var positioned = lines.Where(IsPositionLine).ToList();
        var ids = positioned.Select(l => SpanPath.Of(l)).ToList();

        Assert.All(ids, id => Assert.NotNull(id));
        Assert.Equal(ids.Count, ids.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(CountPositions(tree.Roots), ids.Count);
        AssertPrefixClosedAndDense(ids!);
    }

    [Property(MaxTest = 300)]
    public void Every_flavour_prints_the_structural_ids(int seed)
    {
        var tree = RandomTree(new Random(seed));
        var structural = IdsIn(StructuralTraceRenderer.Render(tree));

        Assert.Equal(structural, IdsIn(IndentedTextRenderer.Render(tree)));
        Assert.Equal(structural, IdsIn(MarkdownRenderer.Render(tree, new MarkdownOptions { IncludeFrontmatter = false })));
        Assert.Equal(structural, IdsIn(ProseRenderer.Render(tree)));
    }

    /// <summary>
    /// Capture order across threads is the scheduler's; it moves no id. (Members with the same
    /// <c>Class.method</c> keep capture order by design, so only groups of distinct signatures are
    /// reversed here.)
    /// </summary>
    [Property(MaxTest = 200)]
    public void Concurrent_capture_order_never_changes_an_id(int seed)
    {
        var tree = RandomTree(new Random(seed));
        var holder = new TraceNode(new MethodSignature("Root", "Holder", []), new Returned(null), tree.Roots, 0);
        var reordered = new TraceTree(ReverseConcurrentMembers(holder).Children);

        Assert.Equal(StructuralTraceRenderer.Render(tree), StructuralTraceRenderer.Render(reordered));
    }

    [Property(MaxTest = 200)]
    public void An_id_free_baseline_of_any_shape_still_compares_unchanged(int seed)
    {
        var current = StructuralTraceRenderer.RenderDocument(RandomTree(new Random(seed)), "s");
        var idFree = string.Join("\r\n", current.TrimEnd('\n').Split('\n').Select(SpanPath.Strip));

        Assert.True(StructuralDelta.Between(idFree, current).Unchanged);
    }

    private static bool IsPositionLine(string line)
    {
        var body = SpanPath.Strip(line).TrimStart();
        return body.StartsWith("- ", StringComparison.Ordinal)
            || body.StartsWith("~ fire-and-forget", StringComparison.Ordinal);
    }

    private static List<string> IdsIn(string text) =>
        Id().Matches(text).Select(m => m.Value).OrderBy(id => id, StringComparer.Ordinal).ToList();

    [GeneratedRegex(@"#\d+(\.\d+)*(?=[\s)]|$)")]
    private static partial Regex Id();

    private static void AssertPrefixClosedAndDense(List<string> ids)
    {
        var set = new HashSet<string>(ids, StringComparer.Ordinal);
        foreach (var group in ids.GroupBy(ParentOf))
        {
            Assert.True(group.Key is null || set.Contains(group.Key), $"parent {group.Key} missing");
            var positions = group.Select(id => int.Parse(id[(id.LastIndexOfAny(['#', '.']) + 1)..], System.Globalization.CultureInfo.InvariantCulture)).Order();
            Assert.Equal(Enumerable.Range(1, group.Count()), positions);
        }
    }

    private static string? ParentOf(string id)
    {
        var dot = id.LastIndexOf('.');
        return dot < 0 ? null : id[..dot];
    }

    /// <summary>One position per call, plus one per fire-and-forget launch; a launcher stub is not a call.</summary>
    private static int CountPositions(IReadOnlyList<TraceNode> siblings)
    {
        var count = 0;
        foreach (var segment in ChildSegment.Partition(siblings))
        {
            if (segment.Kind == ConcurrencyKind.FireAndForget)
            {
                count++;
            }

            foreach (var node in segment.Nodes.Where(n => !IsStub(n)))
            {
                count += 1 + CountPositions(node.Children);
            }
        }

        return count;
    }

    private static bool IsStub(TraceNode node) =>
        node.Signature.MethodName == "fire-and-forget" && node.Children.Count == 0
        && node.Concurrency?.Kind == ConcurrencyKind.FireAndForget;

    private static TraceNode ReverseConcurrentMembers(TraceNode node)
    {
        var children = new List<TraceNode>();
        foreach (var segment in ChildSegment.Partition(node.Children))
        {
            var members = segment.Nodes.Select(ReverseConcurrentMembers).ToList();
            var distinct = members.Select(m => m.Signature.ClassName + "." + m.Signature.MethodName)
                .Distinct(StringComparer.Ordinal).Count() == members.Count;
            if (segment.IsConcurrent && segment.Kind != ConcurrencyKind.FireAndForget && distinct)
            {
                members.Reverse();
            }

            children.AddRange(members);
        }

        return node with { Children = children };
    }

    private static TraceTree RandomTree(Random random)
    {
        var groups = 0;
        return new TraceTree(Siblings(random, 0, ref groups));
    }

    private static List<TraceNode> Siblings(Random random, int depth, ref int groups)
    {
        var siblings = new List<TraceNode>();
        var count = depth > 3 ? 0 : random.Next(depth == 0 ? 1 : 0, 4);
        for (var i = 0; i < count; i++)
        {
            var kind = random.Next(6);
            var info = kind < 3 ? null : NewGroup(kind, ++groups);
            if (info?.Kind == ConcurrencyKind.FireAndForget)
            {
                siblings.Add(new TraceNode(
                    new MethodSignature("Bg", "fire-and-forget", []), new Incomplete(), [], 0, 0, info));
            }

            var members = info is null ? 1 : random.Next(info.Kind == ConcurrencyKind.FireAndForget ? 0 : 1, 4);
            for (var m = 0; m < members; m++)
            {
                siblings.Add(Call(random, depth, info, ref groups));
            }
        }

        return siblings;
    }

    private static ConcurrencyInfo NewGroup(int kind, int group)
    {
        var concurrency = kind switch
        {
            3 => ConcurrencyKind.ForkJoin,
            4 => ConcurrencyKind.Async,
            _ => ConcurrencyKind.FireAndForget,
        };
        return new ConcurrencyInfo("g-" + group, "t", group, "t", true, concurrency);
    }

    private static TraceNode Call(Random random, int depth, ConcurrencyInfo? info, ref int groups)
    {
        var signature = new MethodSignature(
            Classes[random.Next(Classes.Length)], Methods[random.Next(Methods.Length)], []);
        TraceOutcome outcome = random.Next(3) == 0 ? new Returned("1") : new Returned(null);
        return new TraceNode(signature, outcome, Siblings(random, depth + 1, ref groups), 0, 0, info);
    }
}
