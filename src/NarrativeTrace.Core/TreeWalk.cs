// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Core;

/// <summary>
/// The one bounded, cycle-safe way to walk a <see cref="TraceNode"/> forest.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="TraceNode.Children"/> is <c>IReadOnlyList&lt;TraceNode&gt;</c> — a type, not a
/// guarantee of acyclicity — and a tree also arrives from replay, deserialization or a hand-built
/// fixture. Ordinary recursive C# traversal over such a tree is not just slow on a pathological
/// input, it is <b>uncatchable</b>: a <see cref="StackOverflowException"/> ends the process before
/// any renderer's own guard can run, and a cyclic tree walked with an unguarded loop grows its
/// work queue forever instead of failing fast. This type exists so no caller has to choose between
/// those two failure modes.
/// </para>
/// <para>
/// Every traversal here runs on an explicit, heap-allocated stack rather than the CLR call stack,
/// so depth is bounded by <see cref="MaxDepth"/> regardless of how small the calling thread's own
/// stack is — confirmed empirically, not assumed: a constrained 256&#160;KB thread stack overflows
/// on as few as 10,000 <em>real</em> recursive frames of a moderately-sized method, which is
/// exactly the depth a naive recursive walker would need to survive.
/// </para>
/// <para>
/// Cycle detection is <b>on the current path</b> (the node's own ancestor chain), not "seen
/// anywhere in the whole walk": a diamond — the same node instance reachable from two different
/// parents — is shared, not cyclic, and must still be walked through both paths. Only a node that
/// is its own ancestor is a cycle.
/// </para>
/// </remarks>
public static class TreeWalk
{
    /// <summary>
    /// The deepest a walk descends before treating a node as a stopping point rather than
    /// continuing into its children.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A root sits at depth 1. Chosen to comfortably exceed any real call chain a traced
    /// application could produce while still keeping a pathological or hostile tree's total work
    /// finite and fast.
    /// </para>
    /// <para>
    /// This is <see cref="Visit"/>'s own bound, which is genuinely safe at any value — it never
    /// recurses the CLR call stack. The number sits lower than the Java flagship's matching
    /// <c>TreeWalk.MAX_DEPTH</c> (10,000) because downstream consumers of a bounded tree impose
    /// their own real stack costs, which vary with host stack headroom and instrumentation;
    /// 2,000 held clean across repeated instrumented runs and is still an order of magnitude
    /// past any plausible real recursive business method.
    /// </para>
    /// </remarks>
    public const int MaxDepth = 2_000;

    /// <summary>The method name <see cref="Bound"/> stamps on a node that closed a cycle.</summary>
    public const string CycleMarker = "… (cycle)";

    /// <summary>The method name <see cref="Bound"/> stamps on a node past <see cref="MaxDepth"/>.</summary>
    public const string DepthLimitMarker = "… (depth limit)";

    /// <summary>Why a walk did not descend into a node's children.</summary>
    public enum StopReason
    {
        /// <summary>The walk descended normally; there was nothing to stop for.</summary>
        None,

        /// <summary>The node is its own ancestor — descending would never terminate.</summary>
        Cycle,

        /// <summary>The node sits past <see cref="MaxDepth"/>.</summary>
        DepthLimit,
    }

    /// <summary>
    /// Called once per node in pre-order (a node before its children, in child order), on an
    /// explicit stack rather than the call stack.
    /// </summary>
    /// <param name="node">The node being visited.</param>
    /// <param name="depth">This node's depth; a root is depth 1.</param>
    /// <param name="reason">
    /// Why the walk will not descend into <paramref name="node"/>'s children, or
    /// <see cref="StopReason.None"/> when it will. The node itself is always visited exactly once
    /// regardless of <paramref name="reason"/> — only descent is affected.
    /// </param>
    /// <returns><see langword="true"/> to continue the walk; <see langword="false"/> to stop it entirely.</returns>
    public delegate bool NodeVisitor(TraceNode node, int depth, StopReason reason);

    /// <summary>Walks every root, pre-order, bounded and cycle-safe.</summary>
    /// <param name="roots">The forest to walk. Never <see langword="null"/>; an empty forest visits nothing.</param>
    /// <param name="visitor">Called once per node reached; return <see langword="false"/> to stop early.</param>
    /// <exception cref="ArgumentNullException"><paramref name="roots"/> or <paramref name="visitor"/> is null.</exception>
    /// <remarks>
    /// The primitive every other member of this type is built from. Runs on an explicit stack, so
    /// neither a cyclic nor an absurdly deep forest can overflow the calling thread's stack.
    /// </remarks>
    public static void Visit(IReadOnlyList<TraceNode> roots, NodeVisitor visitor)
    {
        if (roots is null)
        {
            throw new ArgumentNullException(nameof(roots));
        }

        if (visitor is null)
        {
            throw new ArgumentNullException(nameof(visitor));
        }

        var onPath = new HashSet<TraceNode>(ReferenceComparer.Instance);
        var stack = new List<VisitFrame> { new(roots, 0) };

        while (stack.Count > 0)
        {
            if (!StepVisit(stack, onPath, visitor))
            {
                return;
            }
        }
    }

    /// <summary>Whether any node in the forest matches <paramref name="predicate"/>.</summary>
    /// <param name="roots">The forest to search. Never <see langword="null"/>.</param>
    /// <param name="predicate">Tested against every node the walk reaches.</param>
    /// <returns><see langword="true"/> as soon as a match is found; <see langword="false"/> if the walk exhausts without one.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="roots"/> or <paramref name="predicate"/> is null.</exception>
    public static bool Any(IReadOnlyList<TraceNode> roots, Func<TraceNode, bool> predicate)
    {
        if (predicate is null)
        {
            throw new ArgumentNullException(nameof(predicate));
        }

        var found = false;
        Visit(roots, (node, _, _) =>
        {
            if (!predicate(node))
            {
                return true;
            }

            found = true;
            return false;
        });
        return found;
    }

    /// <summary>The first node, in pre-order, matching <paramref name="predicate"/>.</summary>
    /// <param name="roots">The forest to search. Never <see langword="null"/>.</param>
    /// <param name="predicate">Tested against every node the walk reaches.</param>
    /// <returns>The first matching node, or <see langword="null"/> when none matches within the bound.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="roots"/> or <paramref name="predicate"/> is null.</exception>
    public static TraceNode? FindFirst(IReadOnlyList<TraceNode> roots, Func<TraceNode, bool> predicate)
    {
        if (predicate is null)
        {
            throw new ArgumentNullException(nameof(predicate));
        }

        TraceNode? result = null;
        Visit(roots, (node, _, _) =>
        {
            if (!predicate(node))
            {
                return true;
            }

            result = node;
            return false;
        });
        return result;
    }

    /// <summary>
    /// A copy of the forest, guaranteed acyclic and no deeper than <see cref="MaxDepth"/>, so any
    /// existing recursive renderer can walk the <em>result</em> with its own real call-stack
    /// recursion and never overflow.
    /// </summary>
    /// <param name="roots">The forest to bound. Never <see langword="null"/>.</param>
    /// <returns>
    /// An equivalent forest with every cyclic edge and every node past <see cref="MaxDepth"/>
    /// replaced by a synthetic leaf whose method name is <see cref="CycleMarker"/> or
    /// <see cref="DepthLimitMarker"/> — visible in whichever field a given renderer favors, since
    /// it is stamped onto the class name, method name and narration alike.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="roots"/> is null.</exception>
    /// <remarks>
    /// Call this once, at a renderer's or exporter's entry point, before its own traversal —
    /// not per node. Cheap to call unconditionally: an already-safe forest — the overwhelming
    /// majority of real traces — costs one non-allocating-per-node validation pass and returns
    /// the <b>original</b> list, not a copy; only a forest that actually needs truncating pays for
    /// the full rebuild. A renderer's benchmark should not move on ordinary input.
    /// </remarks>
    public static IReadOnlyList<TraceNode> Bound(IReadOnlyList<TraceNode> roots)
    {
        if (roots is null)
        {
            throw new ArgumentNullException(nameof(roots));
        }

        return IsAlreadyBounded(roots) ? roots : Rebuild(roots);
    }

    // The fast path: walks the forest exactly like Visit, but builds nothing — just answers
    // "would every node's reason be None?" so the common, already-safe case skips Rebuild's
    // per-node allocation entirely.
    private static bool IsAlreadyBounded(IReadOnlyList<TraceNode> roots)
    {
        var safe = true;
        Visit(roots, (_, _, reason) =>
        {
            if (reason == StopReason.None)
            {
                return true;
            }

            safe = false;
            return false;
        });
        return safe;
    }

    private static IReadOnlyList<TraceNode> Rebuild(IReadOnlyList<TraceNode> roots)
    {
        var onPath = new HashSet<TraceNode>(ReferenceComparer.Instance);
        var stack = new List<BoundFrame> { new(null, roots, 0) };

        IReadOnlyList<TraceNode>? result = null;
        while (result is null)
        {
            result = StepBound(stack, onPath);
        }

        return result;
    }

    // One step of Bound's explicit-stack post-order rebuild: either marks/pushes the top frame's
    // next child, or — once a frame is exhausted — folds it into its parent's Built list. Returns
    // the finished forest once the virtual root frame itself is folded, null otherwise.
    private static IReadOnlyList<TraceNode>? StepBound(List<BoundFrame> stack, HashSet<TraceNode> onPath)
    {
        var frame = stack[^1];
        if (frame.Index < frame.Children.Count)
        {
            var node = frame.Children[frame.Index];
            frame.Index++;
            PushOrMark(node, frame, stack, onPath);
            return null;
        }

        stack.RemoveAt(stack.Count - 1);
        if (frame.Owner is not { } owner)
        {
            return frame.Built;
        }

        onPath.Remove(owner);
        stack[^1].Built.Add(owner with { Children = frame.Built });
        return null;
    }

    // One step of Visit's explicit-stack pre-order walk: advances the top frame by one child (or
    // pops a fully-visited frame), calling visitor at most once. Returns false when the visitor
    // asked to stop the whole walk.
    private static bool StepVisit(List<VisitFrame> stack, HashSet<TraceNode> onPath, NodeVisitor visitor)
    {
        var frame = stack[^1];
        return frame.Index >= frame.Children.Count
            ? PopVisitFrame(stack, onPath)
            : VisitNextChild(frame, stack, onPath, visitor);
    }

    private static bool PopVisitFrame(List<VisitFrame> stack, HashSet<TraceNode> onPath)
    {
        var frame = stack[^1];
        stack.RemoveAt(stack.Count - 1);
        if (frame.Owner is { } owner)
        {
            onPath.Remove(owner);
        }

        return true;
    }

    private static bool VisitNextChild(
        VisitFrame frame, List<VisitFrame> stack, HashSet<TraceNode> onPath, NodeVisitor visitor)
    {
        var node = frame.Children[frame.Index];
        frame.Index++;

        var reason = ReasonFor(node, frame.Depth, onPath);
        if (!visitor(node, frame.Depth + 1, reason))
        {
            return false;
        }

        // A leaf has nothing to descend into - skip the frame entirely rather
        // than pushing one only to pop it, unvisited, on the very next step.
        // The overwhelming majority of nodes in a real call tree are leaves,
        // so this is the difference between one allocation per node and one
        // per actual ancestor depth.
        if (reason == StopReason.None && node.Children.Count > 0)
        {
            onPath.Add(node);
            stack.Add(new VisitFrame(node.Children, frame.Depth + 1) { Owner = node });
        }

        return true;
    }

    private static StopReason ReasonFor(TraceNode node, int parentDepth, HashSet<TraceNode> onPath)
    {
        if (onPath.Contains(node))
        {
            return StopReason.Cycle;
        }

        return parentDepth + 1 > MaxDepth ? StopReason.DepthLimit : StopReason.None;
    }

    private static void PushOrMark(
        TraceNode node, BoundFrame frame, List<BoundFrame> stack, HashSet<TraceNode> onPath)
    {
        if (onPath.Contains(node))
        {
            frame.Built.Add(MarkerLeaf(CycleMarker));
            return;
        }

        if (frame.Depth + 1 > MaxDepth)
        {
            frame.Built.Add(MarkerLeaf(DepthLimitMarker));
            return;
        }

        onPath.Add(node);
        stack.Add(new BoundFrame(node, node.Children, frame.Depth + 1));
    }

    private static TraceNode MarkerLeaf(string marker)
    {
        return new TraceNode(
            new MethodSignature(marker, marker, Array.Empty<ParameterCapture>(), marker),
            new Returned(marker),
            Array.Empty<TraceNode>(),
            DurationTicks: 0);
    }

    private sealed class VisitFrame(IReadOnlyList<TraceNode> children, int depth)
    {
        public TraceNode? Owner { get; init; }

        public IReadOnlyList<TraceNode> Children { get; } = children;

        public int Depth { get; } = depth;

        public int Index { get; set; }
    }

    private sealed class BoundFrame(TraceNode? owner, IReadOnlyList<TraceNode> children, int depth)
    {
        public TraceNode? Owner { get; } = owner;

        public IReadOnlyList<TraceNode> Children { get; } = children;

        public int Depth { get; } = depth;

        public int Index { get; set; }

        public List<TraceNode> Built { get; } = [];
    }

    private sealed class ReferenceComparer : IEqualityComparer<TraceNode>
    {
        public static readonly ReferenceComparer Instance = new();

        public bool Equals(TraceNode? x, TraceNode? y) => ReferenceEquals(x, y);

        public int GetHashCode(TraceNode obj) =>
            System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
    }
}
