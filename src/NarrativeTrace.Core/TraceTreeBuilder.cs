// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Core;

/// <summary>
/// Folds a flat capture-event stream into the <see cref="TraceTree"/> renderers
/// and exporters consume.
/// </summary>
/// <remarks>
/// The seam between capture and presentation. Because the tree is rebuilt from
/// span identity and parent linkage rather than from event order, it tolerates
/// spans closing out of order, spans that never closed, and subtrees grafted in
/// from other threads. Pure and stateless — the same events and level always
/// produce the same tree.
/// </remarks>
public static class TraceTreeBuilder
{
    /// <summary>Builds the finished trace from captured events.</summary>
    /// <param name="events">
    /// The capture stream in emission order. Incomplete streams are expected,
    /// not exceptional: an enter with no matching exit becomes an
    /// <see cref="Incomplete"/> node, and a graft whose parent span is absent is
    /// promoted to a root rather than dropped.
    /// </param>
    /// <param name="level">
    /// The level to filter the result to. Applied <b>after</b> the tree is
    /// assembled, so parent nodes are retained where needed to keep a surviving
    /// child reachable.
    /// </param>
    /// <param name="traceId">
    /// The capturing context's trace id, passed straight through to
    /// <see cref="TraceTree.TraceId"/>. Never generated here: an idle context
    /// has no id to hand over and must not acquire one by being asked for its
    /// trace, so the default leaves the tree to resolve its own.
    /// </param>
    /// <returns>
    /// The assembled, filtered tree — a forest, since a stream can yield several
    /// roots. Empty rather than <see langword="null"/> when nothing survives.
    /// </returns>
    /// <remarks>
    /// Durations and timestamps are normalized here from
    /// <see cref="System.Diagnostics.Stopwatch"/> units to <see cref="TimeSpan"/>
    /// ticks — this is the one place that conversion happens, which is why
    /// <see cref="TraceNode"/> exposes TimeSpan ticks while
    /// <see cref="TraceEvent"/> exposes raw Stopwatch ones.
    /// </remarks>
    public static TraceTree Build(
        IReadOnlyList<TraceEvent> events,
        TracingLevel level,
        TraceId traceId = default)
    {
        var enters = IndexEnters(events);
        var exits = IndexExits(events);
        var grafts = IndexGrafts(events);
        var rootGrafts = CollectRootGrafts(events);
        var roots = CollectRoots(enters, exits, grafts, rootGrafts);
        var filtered = ApplyLevelFilter(roots, level);
        return new TraceTree(filtered.AsReadOnly(), traceId);
    }

    private static Dictionary<SpanId, EnterEvent> IndexEnters(
        IReadOnlyList<TraceEvent> events)
    {
        var map = new Dictionary<SpanId, EnterEvent>();
        for (var i = 0; i < events.Count; i++)
        {
            if (events[i] is EnterEvent e)
            {
                map[e.SpanContext.SpanId] = e;
            }
        }

        return map;
    }

    private static Dictionary<SpanId, ExitEvent> IndexExits(
        IReadOnlyList<TraceEvent> events)
    {
        var map = new Dictionary<SpanId, ExitEvent>();
        for (var i = 0; i < events.Count; i++)
        {
            if (events[i] is ExitEvent e)
            {
                map[e.SpanContext.SpanId] = e;
            }
        }

        return map;
    }

    private static Dictionary<SpanId, List<TraceNode>> IndexGrafts(
        IReadOnlyList<TraceEvent> events)
    {
        var map = new Dictionary<SpanId, List<TraceNode>>();
        for (var i = 0; i < events.Count; i++)
        {
            if (events[i] is GraftEvent { ParentSpanId: { } parent } g)
            {
                if (!map.TryGetValue(parent, out var list))
                {
                    list = [];
                    map[parent] = list;
                }

                list.Add(g.Node);
            }
        }

        return map;
    }

    private static List<TraceNode> CollectRootGrafts(
        IReadOnlyList<TraceEvent> events)
    {
        var roots = new List<TraceNode>();
        for (var i = 0; i < events.Count; i++)
        {
            if (events[i] is GraftEvent { ParentSpanId: null } g)
            {
                roots.Add(g.Node);
            }
        }

        return roots;
    }

    private static List<TraceNode> CollectRoots(
        Dictionary<SpanId, EnterEvent> enters,
        Dictionary<SpanId, ExitEvent> exits,
        Dictionary<SpanId, List<TraceNode>> grafts,
        List<TraceNode> rootGrafts)
    {
        // S3267: LINQ allocates an iterator + closure per tree build; BuildTree
        // is benchmark-gated at 0 bytes of added allocation.
#pragma warning disable S3267
        var roots = new List<TraceNode>();
        foreach (var enter in enters.Values)
        {
            // A span whose parent is not in this stream is a root here: the
            // caller lives in another capture (a worker reading its own trace
            // under a propagated snapshot), and dropping the node would lose
            // work that demonstrably happened.
            if (enter.SpanContext.ParentSpanId is not { } parent
                || !enters.ContainsKey(parent))
            {
                roots.Add(BuildNode(
                    enter, enters, exits, grafts));
            }
        }
#pragma warning restore S3267

        roots.AddRange(rootGrafts);
        AddOrphanGraftedRoots(roots, grafts, enters);
        return roots;
    }

    private static void AddOrphanGraftedRoots(
        List<TraceNode> roots,
        Dictionary<SpanId, List<TraceNode>> grafts,
        Dictionary<SpanId, EnterEvent> enters)
    {
        // S3267: see BuildRoots — same allocation-gated tree-building path.
#pragma warning disable S3267
        foreach (var kvp in grafts)
        {
            if (!enters.ContainsKey(kvp.Key))
            {
                roots.AddRange(kvp.Value);
            }
        }
#pragma warning restore S3267
    }

    private static TraceNode BuildNode(
        EnterEvent enter,
        Dictionary<SpanId, EnterEvent> enters,
        Dictionary<SpanId, ExitEvent> exits,
        Dictionary<SpanId, List<TraceNode>> grafts)
    {
        var children = CollectChildren(
            enter.SpanContext.SpanId, enters, exits, grafts);
        var (outcome, duration) =
            ResolveOutcome(enter, exits);
        return new TraceNode(
            SignatureFor(enter, exits), outcome,
            children.AsReadOnly(),
            StopwatchTicks.ToTimeSpanTicks(duration),
            StopwatchTicks.ToTimeSpanTicks(enter.TimestampTicks),
            enter.Concurrency,
            enter.SpanContext);
    }

    // An exit-time error context (resolved against the thrown type)
    // supersedes any enter-time [OnError] baking on the signature.
    private static MethodSignature SignatureFor(
        EnterEvent enter,
        Dictionary<SpanId, ExitEvent> exits)
    {
        return exits.TryGetValue(
                enter.SpanContext.SpanId, out var exit)
            && exit.ErrorContext is { } context
            ? enter.Signature with { ErrorContext = context }
            : enter.Signature;
    }

    private static List<TraceNode> CollectChildren(
        SpanId parentSpanId,
        Dictionary<SpanId, EnterEvent> enters,
        Dictionary<SpanId, ExitEvent> exits,
        Dictionary<SpanId, List<TraceNode>> grafts)
    {
        var children = new List<TraceNode>();
        foreach (var child in enters.Values)
        {
            if (child.SpanContext.ParentSpanId == parentSpanId)
            {
                children.Add(BuildNode(
                    child, enters, exits, grafts));
            }
        }

        if (grafts.TryGetValue(parentSpanId, out var g))
        {
            children.AddRange(g);
        }

        return children;
    }

    private static List<TraceNode> ApplyLevelFilter(
        List<TraceNode> nodes, TracingLevel level)
    {
        return level switch
        {
            TracingLevel.Errors =>
                FilterErrorsOnly(nodes),
            TracingLevel.Summary =>
                FilterSummary(nodes),
            _ => nodes,
        };
    }

    // Mirrors Java retainErrorPathsNode: an error node is returned with its
    // full subtree intact (preserving what succeeded before it failed); a
    // non-error node is kept only when some descendant survives pruning.
    private static List<TraceNode> FilterErrorsOnly(
        List<TraceNode> nodes)
    {
        var result = new List<TraceNode>();
        foreach (var node in nodes)
        {
            if (RetainErrorPathsNode(node) is { } pruned)
            {
                result.Add(pruned);
            }
        }

        return result;
    }

    private static TraceNode? RetainErrorPathsNode(TraceNode node)
    {
        if (node.Outcome is Threw or Incomplete)
        {
            return node;
        }

        var errorChildren =
            FilterErrorsOnly(node.Children.ToList());
        return errorChildren.Count == 0
            ? null
            : node with { Children = errorChildren.AsReadOnly() };
    }

    private static List<TraceNode> FilterSummary(
        List<TraceNode> nodes)
    {
        return nodes
            .Select(n =>
            {
                if (n.Children.Count == 0)
                {
                    return n;
                }

                var leaves = new List<TraceNode>();
                CollectLeaves(n.Children, leaves);
                return n with
                {
                    Children = leaves.AsReadOnly(),
                };
            })
            .ToList();
    }

    private static void CollectLeaves(
        IReadOnlyList<TraceNode> nodes,
        List<TraceNode> leaves)
    {
        foreach (var node in nodes)
        {
            // Mirror Java pruneSummaryCollect: keep leaves and intermediate
            // error frames (with their subtree) rather than only leaves.
            if (node.Children.Count == 0
                || node.Outcome is Threw or Incomplete)
            {
                leaves.Add(node);
            }
            else
            {
                CollectLeaves(node.Children, leaves);
            }
        }
    }

    private static (TraceOutcome, long) ResolveOutcome(
        EnterEvent enter,
        Dictionary<SpanId, ExitEvent> exits)
    {
        if (exits.TryGetValue(enter.SpanContext.SpanId, out var exit))
        {
            return (exit.Outcome,
                exit.TimestampTicks - enter.TimestampTicks);
        }

        return (new Incomplete(), 0);
    }
}
