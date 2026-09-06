// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Core;

/// <summary>
/// A run of sibling nodes that belong together — either one sequential call or
/// all the branches of one concurrent group.
/// </summary>
/// <remarks>
/// The rendering view over a <see cref="TraceNode"/>'s flat
/// <see cref="TraceNode.Children"/> list. A renderer that walked those children
/// directly would print each branch of a fork as if it happened in sequence;
/// segmenting first lets it emit one "in parallel" block instead. Obtain
/// segments from <see cref="Partition"/> — the constructor is private, so these
/// are always derived from a real child list rather than assembled by hand.
/// </remarks>
/// <example>
/// <code>
/// foreach (var segment in ChildSegment.Partition(node.Children))
/// {
///     if (segment.IsConcurrent)
///         RenderParallelBlock(segment.Nodes, segment.Kind!.Value);
///     else
///         RenderSequential(segment.Nodes[0]);
/// }
/// </code>
/// </example>
public sealed class ChildSegment
{
    /// <summary>
    /// The concurrency group these nodes share, or <see langword="null"/> for a
    /// sequential segment. Equivalent to testing <see cref="IsConcurrent"/>.
    /// </summary>
    public string? GroupId { get; }

    /// <summary>
    /// The nodes in this segment, in their original child order. Never empty: a
    /// sequential segment holds exactly one node, a concurrent one holds every
    /// branch of the group.
    /// </summary>
    public IReadOnlyList<TraceNode> Nodes { get; }

    /// <summary>
    /// How the group ran, or <see langword="null"/> for a sequential segment.
    /// Non-null exactly when <see cref="IsConcurrent"/> is <see langword="true"/>,
    /// so it is safe to dereference after that check.
    /// </summary>
    public ConcurrencyKind? Kind { get; }

    private ChildSegment(
        string? groupId,
        IReadOnlyList<TraceNode> nodes,
        ConcurrencyKind? kind)
    {
        GroupId = groupId;
        Nodes = nodes;
        Kind = kind;
    }

    /// <summary>
    /// Whether this segment is a concurrent group rather than a single
    /// sequential call.
    /// </summary>
    /// <remarks>
    /// A fork with only one branch is still concurrent — this reports how the
    /// work was launched, not how many nodes came back, so do not infer it from
    /// <see cref="Nodes"/> having more than one element.
    /// </remarks>
    public bool IsConcurrent => GroupId is not null;

    /// <summary>
    /// Groups a node's children into sequential calls and concurrent runs,
    /// preserving order.
    /// </summary>
    /// <param name="children">
    /// A node's children, as captured. Typically <see cref="TraceNode.Children"/>;
    /// an empty list yields an empty result.
    /// </param>
    /// <returns>
    /// The segments in child order. Concatenating every segment's
    /// <see cref="Nodes"/> reproduces <paramref name="children"/> exactly.
    /// </returns>
    /// <remarks>
    /// Groups are collected from <b>contiguous</b> runs of children sharing a
    /// <see cref="ConcurrencyInfo.GroupId"/>. If a sequential call is captured
    /// between two branches of the same fork, that group is split into two
    /// segments carrying the same <see cref="GroupId"/> rather than merged into
    /// one — so treat <see cref="GroupId"/> as a label, not as a unique key over
    /// the returned list.
    /// </remarks>
    public static IReadOnlyList<ChildSegment>
        Partition(IReadOnlyList<TraceNode> children)
    {
        var segments = new List<ChildSegment>();
        var i = 0;
        while (i < children.Count)
        {
            var group = children[i].Concurrency;
            if (group is null)
            {
                segments.Add(Sequential(children[i]));
                i++;
            }
            else
            {
                i = CollectGroup(
                    children, i, group.GroupId,
                    group.Kind, segments);
            }
        }

        return segments;
    }

    private static int CollectGroup(
        IReadOnlyList<TraceNode> children,
        int start, string groupId,
        ConcurrencyKind kind,
        List<ChildSegment> segments)
    {
        var nodes = new List<TraceNode>();
        var j = start;
        while (j < children.Count
            && children[j].Concurrency?.GroupId
                == groupId)
        {
            nodes.Add(children[j]);
            j++;
        }

        segments.Add(new ChildSegment(
            groupId, nodes, kind));
        return j;
    }

    private static ChildSegment Sequential(
        TraceNode node)
    {
        return new ChildSegment(
            null, new[] { node }, null);
    }
}
